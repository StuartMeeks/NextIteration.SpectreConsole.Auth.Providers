using Spectre.Console;
using NextIteration.SpectreConsole.Auth.Commands;
using System.Net.Http.Headers;
using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.GitHub
{
    /// <summary>
    /// Interactive collector that runs the GitHub OAuth <b>device flow</b> — the
    /// same flow <c>gh auth login</c> uses by default. Prompts for the GitHub
    /// host, the OAuth App client id, and the requested scopes; requests a
    /// device + user code; shows the user the code and verification URL; polls
    /// until the user authorises (or the code expires); then enriches the
    /// resulting credential with the authenticated user's identity via
    /// <c>GET {ApiBaseUrl}user</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The OAuth App must have <b>device flow enabled</b> in its settings, and
    /// the user supplies that app's (public) client id — nothing secret is
    /// prompted or stored beyond the resulting token.
    /// </para>
    /// <para>
    /// Consumers must register <c>IHttpClientFactory</c> in DI
    /// (<c>services.AddHttpClient()</c>). Registered automatically by
    /// <see cref="ServiceCollectionExtensions.AddGitHubAuthProvider"/>.
    /// </para>
    /// </remarks>
    public sealed class GitHubCredentialCollector : ICredentialCollector
    {
        /// <summary>
        /// Named HttpClient identity used by the collector. Consumers wishing to
        /// pre-configure the client (proxy, retry handler, user-agent) can call
        /// <c>services.AddHttpClient(GitHubCredentialCollector.HttpClientName, …)</c>.
        /// </summary>
        public const string HttpClientName = "GitHub Credential Validator";

        internal const string DefaultHost = "github.com";
        internal const string DefaultScopes = "repo read:org";
        internal const string DeviceCodeGrantType = "urn:ietf:params:oauth:grant-type:device_code";
        internal const string UserAgent = "NextIteration.SpectreConsole.Auth.Providers.GitHub";

        // Hard cap on the response-body slice surfaced in exceptions — big
        // enough to keep useful error payloads, small enough to bound logs.
        internal const int ErrorBodyMaxChars = 512;

        private static readonly JsonSerializerOptions JsonOptions
            = new() { PropertyNameCaseInsensitive = true };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly Func<DateTimeOffset> _now;

        /// <summary>DI constructor.</summary>
        public GitHubCredentialCollector(IHttpClientFactory httpClientFactory)
            : this(httpClientFactory, static (ts, ct) => Task.Delay(ts, ct), static () => DateTimeOffset.UtcNow)
        {
        }

        /// <summary>
        /// Test seam: lets the polling loop run against an injected delay and
        /// clock so the device-flow tests don't sleep on the real interval.
        /// </summary>
        internal GitHubCredentialCollector(
            IHttpClientFactory httpClientFactory,
            Func<TimeSpan, CancellationToken, Task> delay,
            Func<DateTimeOffset> now)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(delay);
            ArgumentNullException.ThrowIfNull(now);

            _httpClientFactory = httpClientFactory;
            _delay = delay;
            _now = now;
        }

        /// <inheritdoc />
        public string ProviderName => GitHubCredential.ProviderName;

        /// <inheritdoc />
        public async Task<(string credentialData, string environment)> CollectAsync()
        {
            var host = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter GitHub host:")
                    .DefaultValue(DefaultHost)
                    .Validate(ValidateHost)).ConfigureAwait(false);

            var clientId = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter OAuth App client id:")
                    .Validate(value => string.IsNullOrWhiteSpace(value)
                        ? ValidationResult.Error("Client id cannot be empty")
                        : ValidationResult.Success())).ConfigureAwait(false);

            var scopes = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter scopes (space-separated):")
                    .DefaultValue(DefaultScopes)
                    .AllowEmpty()).ConfigureAwait(false);

            var webBaseUrl = DeriveWebBaseUrl(host);
            var apiBaseUrl = DeriveApiBaseUrl(host);
            var environment = DeriveEnvironment(host);

            // 1. Ask GitHub for a device + user code.
            var deviceCode = await RequestDeviceCodeAsync(webBaseUrl, clientId, scopes).ConfigureAwait(false);

            // 2. Tell the user where to go and what to type.
            AnsiConsole.Write(new Panel(
                new Markup(
                    $"Open [link]{Markup.Escape(deviceCode.VerificationUri)}[/] and enter the code:\n\n" +
                    $"[bold yellow]{Markup.Escape(deviceCode.UserCode)}[/]"))
                .Header("GitHub device authorization")
                .BorderColor(Color.Grey));

            // 3. Poll until the user authorises (or the code expires).
            var tokenDto = await PollForTokenAsync(webBaseUrl, clientId, deviceCode, CancellationToken.None).ConfigureAwait(false);

            // 4. Enrich with the authenticated user's identity.
            var accessToken = tokenDto.AccessToken!;
            var user = await LookupUserAsync(apiBaseUrl, accessToken).ConfigureAwait(false);

            DateTimeOffset? expiresAt = tokenDto.ExpiresIn is { } seconds
                ? _now() + TimeSpan.FromSeconds(seconds)
                : null;

            var credential = new GitHubCredential
            {
                ClientId = clientId,
                AccessToken = accessToken,
                RefreshToken = tokenDto.RefreshToken,
                AccessTokenExpiresAt = expiresAt,
                Scopes = string.IsNullOrWhiteSpace(tokenDto.Scope) ? scopes : tokenDto.Scope!,
                WebBaseUrl = webBaseUrl,
                ApiBaseUrl = apiBaseUrl,
                Login = user.Login,
                Name = user.Name,
                Environment = environment,
            };

            AnsiConsole.MarkupLine($"[green]Authenticated as[/] [bold]{Markup.Escape(user.Login)}[/].");

            return (JsonSerializer.Serialize(credential, GitHubCredential.JsonOptions), credential.Environment);
        }

        /// <summary>
        /// Requests a device + user verification code from
        /// <c>POST {web}/login/device/code</c>. Throws on any non-success status
        /// or malformed body.
        /// </summary>
        internal async Task<GitHubDeviceCodeDto> RequestDeviceCodeAsync(Uri webBaseUrl, string clientId, string scopes)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(webBaseUrl, "login/device/code"))
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["scope"] = scopes ?? string.Empty,
                }),
            };
            ApplyJsonHeaders(request);

            using var response = await client.SendAsync(request).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"GitHub device-code request failed: {(int)response.StatusCode} {response.StatusCode}. Body: {TruncateErrorBody(body)}");
            }

            return JsonSerializer.Deserialize<GitHubDeviceCodeDto>(body, JsonOptions)
                ?? throw new InvalidOperationException(
                    "GitHub device-code request returned a success status with a body that did not deserialize.");
        }

        /// <summary>
        /// Polls <c>POST {web}/login/oauth/access_token</c> until the user
        /// authorises the device, honouring the server-supplied
        /// <c>interval</c>, the <c>slow_down</c> back-off, and the
        /// <c>authorization_pending</c> state. Throws on terminal errors
        /// (<c>access_denied</c>, <c>expired_token</c>, unexpected) or when the
        /// device code's lifetime elapses.
        /// </summary>
        internal async Task<GitHubAccessTokenDto> PollForTokenAsync(
            Uri webBaseUrl, string clientId, GitHubDeviceCodeDto deviceCode, CancellationToken cancellationToken)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(deviceCode.Interval, 1));
            var deadline = _now() + TimeSpan.FromSeconds(deviceCode.ExpiresIn);

            while (true)
            {
                await _delay(interval, cancellationToken).ConfigureAwait(false);

                var dto = await PostTokenRequestAsync(webBaseUrl, new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["device_code"] = deviceCode.DeviceCode,
                    ["grant_type"] = DeviceCodeGrantType,
                }).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(dto.AccessToken))
                {
                    return dto;
                }

                switch (dto.Error)
                {
                    case "authorization_pending":
                        break;
                    case "slow_down":
                        interval += TimeSpan.FromSeconds(5);
                        break;
                    case "access_denied":
                        throw new InvalidOperationException(
                            "GitHub device authorization was denied by the user.");
                    case "expired_token":
                        throw new InvalidOperationException(
                            "The GitHub device code expired before authorization completed. Run `accounts add` again.");
                    case null or "":
                        throw new InvalidOperationException(
                            "GitHub token endpoint returned neither an access token nor an error.");
                    default:
                        throw new InvalidOperationException(
                            $"GitHub device authorization failed: {dto.Error}{FormatErrorDescription(dto.ErrorDescription)}");
                }

                if (_now() >= deadline)
                {
                    throw new InvalidOperationException(
                        "Timed out waiting for GitHub device authorization. Run `accounts add` again.");
                }
            }
        }

        /// <summary>
        /// Resolves the authenticated user via <c>GET {api}user</c>. Throws on
        /// any non-success status or malformed body.
        /// </summary>
        internal async Task<GitHubUserDto> LookupUserAsync(Uri apiBaseUrl, string accessToken)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(apiBaseUrl, "user"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd(UserAgent);

            using var response = await client.SendAsync(request).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"GitHub user lookup failed: {(int)response.StatusCode} {response.StatusCode}. Body: {SanitiseErrorBody(body, accessToken)}");
            }

            return JsonSerializer.Deserialize<GitHubUserDto>(body, JsonOptions)
                ?? throw new InvalidOperationException(
                    "GitHub user lookup returned a success status with a body that did not deserialize.");
        }

        private async Task<GitHubAccessTokenDto> PostTokenRequestAsync(Uri webBaseUrl, Dictionary<string, string> form)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(webBaseUrl, "login/oauth/access_token"))
            {
                Content = new FormUrlEncodedContent(form),
            };
            ApplyJsonHeaders(request);

            using var response = await client.SendAsync(request).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            // GitHub returns 200 even for the pending/slow_down states (the
            // error lives in the JSON body), so a non-success status here is a
            // genuine transport/credential failure rather than a poll state.
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"GitHub token request failed: {(int)response.StatusCode} {response.StatusCode}. Body: {TruncateErrorBody(body)}");
            }

            return JsonSerializer.Deserialize<GitHubAccessTokenDto>(body, JsonOptions)
                ?? throw new InvalidOperationException(
                    "GitHub token request returned a success status with a body that did not deserialize.");
        }

        private static void ApplyJsonHeaders(HttpRequestMessage request)
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd(UserAgent);
        }

        /// <summary>
        /// Maps the prompted host to the web base URL used for the device-flow
        /// and token endpoints.
        /// </summary>
        internal static Uri DeriveWebBaseUrl(string host)
            => new($"https://{NormalizeHost(host)}/", UriKind.Absolute);

        /// <summary>
        /// Maps the prompted host to the REST API base URL. github.com uses the
        /// dedicated <c>api.github.com</c> host; GitHub Enterprise Server uses
        /// <c>/api/v3/</c> under the instance host.
        /// </summary>
        internal static Uri DeriveApiBaseUrl(string host)
        {
            var normalized = NormalizeHost(host);
            return string.Equals(normalized, DefaultHost, StringComparison.OrdinalIgnoreCase)
                ? new Uri("https://api.github.com/", UriKind.Absolute)
                : new Uri($"https://{normalized}/api/v3/", UriKind.Absolute);
        }

        /// <summary>Derives the environment label from the host.</summary>
        internal static string DeriveEnvironment(string host)
            => string.Equals(NormalizeHost(host), DefaultHost, StringComparison.OrdinalIgnoreCase)
                ? GitHubCredential.Environments.GitHubCom.ToString()
                : GitHubCredential.Environments.Enterprise.ToString();

        private static string NormalizeHost(string host)
            => host.Trim().TrimEnd('/');

        internal static ValidationResult ValidateHost(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ValidationResult.Error("Host cannot be empty");
            }

            // The host is turned into https://{host}/ — reject anything that
            // isn't a clean host[:port] (e.g. a pasted scheme or path).
            return Uri.TryCreate($"https://{NormalizeHost(value)}/", UriKind.Absolute, out _)
                ? ValidationResult.Success()
                : ValidationResult.Error("Must be a bare host such as github.com or ghe.example.com");
        }

        private static string FormatErrorDescription(string? description)
            => string.IsNullOrWhiteSpace(description) ? string.Empty : $" — {description}";

        internal static string TruncateErrorBody(string body)
        {
            if (string.IsNullOrEmpty(body) || body.Length <= ErrorBodyMaxChars)
            {
                return body;
            }

            return string.Concat(body.AsSpan(0, ErrorBodyMaxChars), "… [truncated]");
        }

        /// <summary>
        /// Redacts the access token from an error body (a misbehaving proxy can
        /// echo request material) and truncates to <see cref="ErrorBodyMaxChars"/>.
        /// </summary>
        internal static string SanitiseErrorBody(string body, string accessToken)
        {
            if (string.IsNullOrEmpty(body))
            {
                return body;
            }

            var redacted = string.IsNullOrEmpty(accessToken)
                ? body
                : body.Replace(accessToken, "<redacted>", StringComparison.Ordinal);

            return TruncateErrorBody(redacted);
        }
    }
}
