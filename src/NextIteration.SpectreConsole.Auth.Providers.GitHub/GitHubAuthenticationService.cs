using NextIteration.SpectreConsole.Auth.Persistence;
using NextIteration.SpectreConsole.Auth.Services;

using System.Net.Http.Headers;
using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.GitHub
{
    /// <summary>
    /// Projects the selected <see cref="GitHubCredential"/> into a
    /// <see cref="GitHubToken"/>. For a classic (non-expiring) OAuth App this is
    /// a straight pass-through of the stored token. For an OAuth App that issues
    /// expiring tokens, an expired access token is refreshed via
    /// <c>POST {WebBaseUrl}login/oauth/access_token</c> (<c>grant_type=refresh_token</c>)
    /// before the token is returned.
    /// </summary>
    /// <remarks>
    /// The refreshed access token is <b>not</b> written back to the credential
    /// store in this version — each authenticate call that needs a refresh
    /// performs one. Consumers that authenticate frequently against an expiring
    /// app should cache the returned <see cref="GitHubToken"/> for its lifetime.
    /// Consumers must register <c>IHttpClientFactory</c> (<c>services.AddHttpClient()</c>).
    /// </remarks>
    public sealed class GitHubAuthenticationService : IAuthenticationService<GitHubCredential, GitHubToken>
    {
        /// <summary>
        /// Named HttpClient identity used for the refresh call. Shares the
        /// collector's name so a single <c>AddHttpClient</c> configuration
        /// covers both.
        /// </summary>
        public const string HttpClientName = GitHubCredentialCollector.HttpClientName;

        private const string RefreshGrantType = "refresh_token";

        private static readonly JsonSerializerOptions JsonOptions
            = new() { PropertyNameCaseInsensitive = true };

        private readonly ICredentialManager _credentialManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly Func<DateTimeOffset> _now;

        /// <summary>DI constructor.</summary>
        public GitHubAuthenticationService(
            ICredentialManager credentialManager,
            IHttpClientFactory httpClientFactory)
            : this(credentialManager, httpClientFactory, static () => DateTimeOffset.UtcNow)
        {
        }

        /// <summary>Test seam: lets the refresh path compute expiry against an injected clock.</summary>
        internal GitHubAuthenticationService(
            ICredentialManager credentialManager,
            IHttpClientFactory httpClientFactory,
            Func<DateTimeOffset> now)
        {
            ArgumentNullException.ThrowIfNull(credentialManager);
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(now);

            _credentialManager = credentialManager;
            _httpClientFactory = httpClientFactory;
            _now = now;
        }

        /// <inheritdoc />
        public async Task<GitHubToken> AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            var credentialJson = await _credentialManager
                .GetSelectedCredentialAsync(GitHubCredential.ProviderName, cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(credentialJson))
            {
                throw new InvalidOperationException($"No {GitHubCredential.ProviderName} credential selected.");
            }

            var credential = DeserializeOrThrow<GitHubCredential>(
                credentialJson,
                GitHubCredential.JsonOptions,
                $"Failed to deserialize the stored {GitHubCredential.ProviderName} credential. It may have been written by an incompatible version — delete it and re-run `accounts add`.");

            return await AuthenticateAsync(credential, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<GitHubToken> AuthenticateAsync(GitHubCredential credential, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(credential);
            ValidateCredential(credential);

            var isExpired = credential.AccessTokenExpiresAt is { } expiry
                && _now() >= expiry - GitHubToken.ExpiryClockSkew;

            // Refresh only when the token is expired AND we have a refresh token
            // to do it with; otherwise pass the stored token straight through.
            if (isExpired && !string.IsNullOrEmpty(credential.RefreshToken))
            {
                return await RefreshAsync(credential, cancellationToken).ConfigureAwait(false);
            }

            return new GitHubToken
            {
                AccessToken = credential.AccessToken,
                BaseUrl = credential.ApiBaseUrl,
                ExpiresAt = credential.AccessTokenExpiresAt,
            };
        }

        /// <inheritdoc />
        public Task<bool> ValidateTokenAsync(GitHubToken token, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(token);
            return Task.FromResult(!token.IsExpired);
        }

        private async Task<GitHubToken> RefreshAsync(GitHubCredential credential, CancellationToken cancellationToken = default)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            using var request = new HttpRequestMessage(
                HttpMethod.Post, new Uri(EnsureTrailingSlash(credential.WebBaseUrl), "login/oauth/access_token"))
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = credential.ClientId,
                    ["refresh_token"] = credential.RefreshToken!,
                    ["grant_type"] = RefreshGrantType,
                }),
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd(GitHubCredentialCollector.UserAgent);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"GitHub token refresh failed: {(int)response.StatusCode} {response.StatusCode}. Body: {GitHubCredentialCollector.SanitiseErrorBody(body, credential.RefreshToken!)}");
            }

            var dto = DeserializeOrThrow<GitHubAccessTokenDto>(
                body,
                JsonOptions,
                "GitHub token refresh returned a success status with a body that did not deserialize.");

            if (!string.IsNullOrEmpty(dto.Error) || string.IsNullOrEmpty(dto.AccessToken))
            {
                throw new InvalidOperationException(
                    $"GitHub token refresh was rejected: {dto.Error ?? "no access token returned"}. The refresh token may be expired — run `accounts add` again.");
            }

            DateTimeOffset? expiresAt = dto.ExpiresIn is { } seconds
                ? _now() + TimeSpan.FromSeconds(seconds)
                : null;

            return new GitHubToken
            {
                AccessToken = dto.AccessToken!,
                BaseUrl = credential.ApiBaseUrl,
                ExpiresAt = expiresAt,
            };
        }

        /// <summary>
        /// Guards against stored credentials whose required fields are
        /// present-but-empty (e.g. a hand-edited keystore) and against a
        /// downgraded API URL. Belt-and-braces: surfaces a clear message rather
        /// than shipping an empty bearer token over a cleartext channel.
        /// </summary>
        private static void ValidateCredential(GitHubCredential credential)
        {
            RequireNonWhitespace(credential.AccessToken, nameof(GitHubCredential.AccessToken));
            RequireNonWhitespace(credential.Environment, nameof(GitHubCredential.Environment));
            RequireSecureUrl(credential.ApiBaseUrl, nameof(GitHubCredential.ApiBaseUrl));
            RequireSecureUrl(credential.WebBaseUrl, nameof(GitHubCredential.WebBaseUrl));

            static void RequireNonWhitespace(string value, string fieldName)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException(
                        $"{fieldName} is required and must not be whitespace.",
                        fieldName);
                }
            }

            static void RequireSecureUrl(Uri url, string fieldName)
            {
                if (url.Scheme == Uri.UriSchemeHttps)
                {
                    return;
                }

                if (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback)
                {
                    return;
                }

                throw new ArgumentException(
                    $"{fieldName} must use https (http is only accepted for loopback addresses).",
                    fieldName);
            }
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

        /// <summary>
        /// Returns <paramref name="baseUrl"/> guaranteed to end in <c>/</c>.
        /// </summary>
        /// <remarks>
        /// <see cref="Uri"/>'s relative-resolution rules replace the base's last
        /// path segment unless the base ends in a slash, so
        /// <c>https://gw.corp/adobe-ims</c> combined with <c>ims/token/v3</c>
        /// silently yields <c>https://gw.corp/ims/token/v3</c> — the gateway
        /// prefix is dropped and the request fails somewhere the user cannot
        /// see, with an error that points at their credentials rather than at
        /// the mangled URL. Normalising at the point of combination also fixes
        /// credentials already stored without the trailing slash.
        /// </remarks>
        internal static Uri EnsureTrailingSlash(Uri baseUrl)
        {
            if (baseUrl.AbsolutePath.EndsWith('/'))
            {
                return baseUrl;
            }

            return new UriBuilder(baseUrl) { Path = baseUrl.AbsolutePath + "/" }.Uri;
        }

    }
}
