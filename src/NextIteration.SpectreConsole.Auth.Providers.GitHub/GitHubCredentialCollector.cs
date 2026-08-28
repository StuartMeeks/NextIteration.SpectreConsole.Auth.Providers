using Spectre.Console;

using NextIteration.SpectreConsole.Auth.Commands;

using System.Globalization;
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

        // Bounds on the server-supplied device-flow poll interval.
        internal const int MinPollIntervalSeconds = 1;
        internal const int MaxPollIntervalSeconds = 60;
        internal const int SlowDownBackoffSeconds = 5;

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
        public async Task<(string credentialData, string environment)> CollectAsync(CancellationToken cancellationToken = default)
        {
            var host = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter GitHub host:")
                    .DefaultValue(DefaultHost)
                    .Validate(ValidateHost), cancellationToken).ConfigureAwait(false);

            var clientId = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter OAuth App client id:")
                    .Validate(value => string.IsNullOrWhiteSpace(value)
                        ? ValidationResult.Error("Client id cannot be empty")
                        : ValidationResult.Success()), cancellationToken).ConfigureAwait(false);

            var scopes = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter scopes (space-separated):")
                    .DefaultValue(DefaultScopes)
                    .AllowEmpty(), cancellationToken).ConfigureAwait(false);

            var webBaseUrl = DeriveWebBaseUrl(host);
            var apiBaseUrl = DeriveApiBaseUrl(host);
            var environment = DeriveEnvironment(host);

            // 1. Ask GitHub for a device + user code.
            var deviceCode = await RequestDeviceCodeAsync(webBaseUrl, clientId, scopes, cancellationToken).ConfigureAwait(false);

            // 2. Tell the user where to go and what to type.
            AnsiConsole.Write(new Panel(
                new Markup(
                    $"Open [link]{Markup.Escape(deviceCode.VerificationUri)}[/] and enter the code:\n\n" +
                    $"[bold yellow]{Markup.Escape(deviceCode.UserCode)}[/]"))
                .Header("GitHub device authorization")
                .BorderColor(Color.Grey));

            // 3. Poll until the user authorises (or the code expires).
            var tokenDto = await PollForTokenAsync(webBaseUrl, clientId, deviceCode, cancellationToken).ConfigureAwait(false);

            // 4. Enrich with the authenticated user's identity.
            var accessToken = tokenDto.AccessToken!;
            var user = await LookupUserAsync(apiBaseUrl, accessToken, cancellationToken).ConfigureAwait(false);

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
        internal async Task<GitHubDeviceCodeDto> RequestDeviceCodeAsync(Uri webBaseUrl, string clientId, string scopes, CancellationToken cancellationToken = default)
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

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"GitHub device-code request failed: {(int)response.StatusCode} {response.StatusCode}. Body: {TruncateErrorBody(body)}");
            }

            var deviceCode = DeserializeOrThrow<GitHubDeviceCodeDto>(
                body,
                JsonOptions,
                "GitHub device-code request returned a success status with a body that did not deserialize.");

            ValidateDeviceCode(deviceCode);

            return deviceCode;
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
            // Clamp at both ends. The floor stops a 0 or negative interval
            // hot-looping the token endpoint; the ceiling stops a misconfigured
            // or hostile server stalling the prompt — or, past ~4.29e9 seconds,
            // making Task.Delay throw ArgumentOutOfRangeException from inside
            // the first await, where the deadline check below cannot intervene.
            // An interval above a minute is meaningless against a device code
            // that expires in fifteen.
            var interval = TimeSpan.FromSeconds(
                Math.Clamp(deviceCode.Interval, MinPollIntervalSeconds, MaxPollIntervalSeconds));
            var deadline = _now() + TimeSpan.FromSeconds(deviceCode.ExpiresIn);

            while (true)
            {
                await _delay(interval, cancellationToken).ConfigureAwait(false);

                var dto = await PostTokenRequestAsync(webBaseUrl, new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["device_code"] = deviceCode.DeviceCode,
                    ["grant_type"] = DeviceCodeGrantType,
                }, cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(dto.AccessToken))
                {
                    return dto;
                }

                switch (dto.Error)
                {
                    case "authorization_pending":
                        break;
                    case "slow_down":
                        // Back off within the same ceiling, so repeated
                        // slow_down responses cannot walk the interval past the
                        // cap either.
                        interval = TimeSpan.FromSeconds(Math.Min(
                            interval.TotalSeconds + SlowDownBackoffSeconds,
                            MaxPollIntervalSeconds));
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
        internal async Task<GitHubUserDto> LookupUserAsync(Uri apiBaseUrl, string accessToken, CancellationToken cancellationToken = default)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(apiBaseUrl, "user"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd(UserAgent);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"GitHub user lookup failed: {(int)response.StatusCode} {response.StatusCode}. Body: {SanitiseErrorBody(body, accessToken)}");
            }

            var user = DeserializeOrThrow<GitHubUserDto>(
                body,
                JsonOptions,
                "GitHub user lookup returned a success status with a body that did not deserialize.");

            if (string.IsNullOrWhiteSpace(user.Login))
            {
                throw new InvalidOperationException(
                    "GitHub user lookup returned a 200 response with no usable login value.");
            }

            return user;
        }

        private async Task<GitHubAccessTokenDto> PostTokenRequestAsync(Uri webBaseUrl, Dictionary<string, string> form, CancellationToken cancellationToken = default)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(webBaseUrl, "login/oauth/access_token"))
            {
                Content = new FormUrlEncodedContent(form),
            };
            ApplyJsonHeaders(request);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            // GitHub returns 200 even for the pending/slow_down states (the
            // error lives in the JSON body), so a non-success status here is a
            // genuine transport/credential failure rather than a poll state.
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"GitHub token request failed: {(int)response.StatusCode} {response.StatusCode}. Body: {TruncateErrorBody(body)}");
            }

            return DeserializeOrThrow<GitHubAccessTokenDto>(
                body,
                JsonOptions,
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

        // Characters that would let a pasted value smuggle userinfo, a path, a
        // query, a fragment or a scheme past the https://{host}/ interpolation.
        private static readonly char[] HostRejectedChars = ['/', '\\', '@', '?', '#', ':'];

        // The same set minus ':', for the unbracketed IPv6 literal case.
        private static readonly char[] HostRejectedCharsExceptColon = ['/', '\\', '@', '?', '#'];

        private const string BareHostError
            = "Must be a bare host such as github.com, ghe.example.com or ghe.example.com:8443";

        internal static ValidationResult ValidateHost(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ValidationResult.Error("Host cannot be empty");
            }

            var normalized = NormalizeHost(value);

            // The host is interpolated into https://{host}/, so anything Uri
            // would read as userinfo, a port, a path, a query or a fragment has
            // to be rejected here — Uri.TryCreate happily parses all of them,
            // and a userinfo prefix ("github.com@evil.example.com") sends the
            // access token and every later refresh POST to the trailing host
            // while still reading as github.com in `accounts list`.
            if (!TrySplitHostAndPort(normalized, out var host, out var port))
            {
                return ValidationResult.Error(BareHostError);
            }

            if (host.Length == 0 || Uri.CheckHostName(host) == UriHostNameType.Unknown)
            {
                return ValidationResult.Error(BareHostError);
            }

            if (port.Length != 0
                && !(ushort.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var portNumber)
                    && portNumber != 0))
            {
                return ValidationResult.Error(BareHostError);
            }

            // Belt-and-braces: whatever Uri makes of the value must be exactly
            // the host we just validated and nothing more.
            if (!Uri.TryCreate($"https://{normalized}/", UriKind.Absolute, out var probe)
                || probe.UserInfo.Length != 0
                || probe.AbsolutePath != "/"
                || probe.Query.Length != 0
                || probe.Fragment.Length != 0)
            {
                return ValidationResult.Error(BareHostError);
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Splits a bare <c>host</c> or <c>host:port</c> — including a bracketed
        /// IPv6 literal — into its two parts. Returns <see langword="false"/>
        /// when the value carries anything else (userinfo, a path, a scheme).
        /// </summary>
        private static bool TrySplitHostAndPort(string value, out string host, out string port)
        {
            host = value;
            port = string.Empty;

            if (value.StartsWith('['))
            {
                var close = value.IndexOf(']', StringComparison.Ordinal);
                if (close < 0)
                {
                    return false;
                }

                host = value[..(close + 1)];
                var rest = value[(close + 1)..];
                if (rest.Length == 0)
                {
                    return true;
                }

                if (rest[0] != ':')
                {
                    return false;
                }

                // A colon with nothing after it is malformed, not "no port".
                port = rest[1..];
                return port.Length != 0 && HasNoRejectedChars(port);
            }

            var colon = value.IndexOf(':', StringComparison.Ordinal);
            if (colon >= 0)
            {
                // A second colon with no brackets means an unbracketed IPv6
                // literal, which carries no port and which the Uri round-trip
                // below rejects anyway (the bracketed form is the one to type).
                if (value.IndexOf(':', colon + 1) >= 0)
                {
                    return HasNoRejectedCharsExceptColon(value);
                }

                host = value[..colon];
                port = value[(colon + 1)..];

                // A colon with nothing after it is malformed, not "no port".
                if (port.Length == 0)
                {
                    return false;
                }
            }

            return HasNoRejectedChars(host) && HasNoRejectedChars(port);
        }

        private static bool HasNoRejectedChars(string value)
            => value.IndexOfAny(HostRejectedChars) < 0 && !value.Any(char.IsWhiteSpace);

        private static bool HasNoRejectedCharsExceptColon(string value)
            => value.IndexOfAny(HostRejectedCharsExceptColon) < 0
                && !value.Any(char.IsWhiteSpace);

        /// <summary>
        /// Rejects a device-code response whose fields are absent in substance
        /// rather than in shape.
        /// </summary>
        /// <remarks>
        /// <c>required</c> is satisfied by a property being <em>present</em> in
        /// the JSON, not by its value being non-null, and
        /// <c>RespectNullableAnnotations</c> is not enabled — so a body whose
        /// <c>device_code</c> is JSON null deserializes cleanly and is only
        /// noticed when the poll loop posts an empty device code and GitHub
        /// answers with an error that names nothing the user can act on.
        /// </remarks>
        private static void ValidateDeviceCode(GitHubDeviceCodeDto deviceCode)
        {
            if (string.IsNullOrWhiteSpace(deviceCode.DeviceCode))
            {
                throw new InvalidOperationException(
                    "GitHub device-code request returned a 200 response with no usable device_code value.");
            }

            if (string.IsNullOrWhiteSpace(deviceCode.UserCode))
            {
                throw new InvalidOperationException(
                    "GitHub device-code request returned a 200 response with no usable user_code value.");
            }

            if (string.IsNullOrWhiteSpace(deviceCode.VerificationUri))
            {
                throw new InvalidOperationException(
                    "GitHub device-code request returned a 200 response with no usable verification_uri value.");
            }

            if (deviceCode.ExpiresIn <= 0)
            {
                throw new InvalidOperationException(
                    $"GitHub device-code request returned a 200 response with a non-positive expires_in ({deviceCode.ExpiresIn}).");
            }
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
        /// <summary>
        /// Deserializes <paramref name="json"/>, turning both a literal
        /// <c>null</c> body and a <see cref="JsonException"/> into the same
        /// <see cref="InvalidOperationException"/>.
        /// </summary>
        /// <remarks>
        /// The bare <c>?? throw</c> this replaces only fired for a body that is
        /// the literal <c>null</c>. Anything merely malformed — an HTML captive
        /// portal interstitial, a truncated response, a payload missing a
        /// required member — escaped as a raw <see cref="JsonException"/> whose
        /// message names a System.Text.Json path and nothing the user can act
        /// on. The original exception is preserved as the inner exception.
        /// </remarks>
        private static TValue DeserializeOrThrow<TValue>(
            string json, JsonSerializerOptions? options, string failureMessage)
        {
            TValue? value;

            try
            {
                value = JsonSerializer.Deserialize<TValue>(json, options);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(failureMessage, ex);
            }

            return value ?? throw new InvalidOperationException(failureMessage);
        }

    }
}
