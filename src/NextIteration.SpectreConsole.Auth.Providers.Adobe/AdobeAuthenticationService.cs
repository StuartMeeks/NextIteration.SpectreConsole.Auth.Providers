using NextIteration.SpectreConsole.Auth.Persistence;
using NextIteration.SpectreConsole.Auth.Services;
using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.Adobe
{
    /// <summary>
    /// Authenticates against Adobe IMS using the OAuth2 client-credentials
    /// flow. Reads the selected <see cref="AdobeCredential"/> from the
    /// credential store and exchanges it for an <see cref="AdobeToken"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The hardcoded scope set <c>openid,AdobeID,read_organizations</c> is
    /// enough to list organisations the service account belongs to but is
    /// typically <b>not</b> sufficient for real VIP Marketplace operations
    /// (transactions, SKU catalogue, projected-product context). Consumers
    /// that need richer scopes currently need to fork this service; a
    /// configurable scope set is a planned follow-up.
    /// </para>
    /// </remarks>
    public sealed class AdobeAuthenticationService : IAuthenticationService<AdobeCredential, AdobeToken>
    {
        // Named HttpClient identity. Consumers wishing to pre-configure the
        // client (proxy, retry handler, etc.) can call services.AddHttpClient(HttpClientName).
        public const string HttpClientName = "Adobe Authenticator";

        // Path segment relative to the credential's ImsUrl.
        private const string TokenEndpointPath = "ims/token/v3";

        private const string DefaultScopes = "openid,AdobeID,read_organizations";

        private readonly ICredentialManager _credentialManager;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>DI constructor.</summary>
        public AdobeAuthenticationService(
            ICredentialManager credentialManager,
            IHttpClientFactory httpClientFactory)
        {
            ArgumentNullException.ThrowIfNull(credentialManager);
            ArgumentNullException.ThrowIfNull(httpClientFactory);

            _credentialManager = credentialManager;
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc />
        public async Task<AdobeToken> AuthenticateAsync()
        {
            var credentialJson = await _credentialManager
                .GetSelectedCredentialAsync(AdobeCredential.ProviderName)
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(credentialJson))
            {
                throw new InvalidOperationException($"No {AdobeCredential.ProviderName} credential selected.");
            }

            var credential = JsonSerializer.Deserialize<AdobeCredential>(credentialJson, AdobeCredential.JsonOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize {AdobeCredential.ProviderName} credential.");

            return await AuthenticateAsync(credential).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<AdobeToken> AuthenticateAsync(AdobeCredential credential)
        {
            ArgumentNullException.ThrowIfNull(credential);
            ValidateCredential(credential);

            var client = _httpClientFactory.CreateClient(HttpClientName);

            // Use an absolute URI for the request rather than mutating
            // client.BaseAddress — a named HttpClient may be shared across
            // callers via IHttpClientFactory's handler pool, and mutating
            // state on it is a concurrency footgun.
            var tokenEndpoint = new Uri(credential.ImsUrl, TokenEndpointPath);

            // FormUrlEncodedContent is IDisposable — dispose it to release
            // the underlying byte buffer promptly.
            using var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = credential.ApiKey,
                ["client_secret"] = credential.ClientSecret,
                ["scope"] = DefaultScopes,
            });

            using var response = await client.PostAsync(tokenEndpoint, requestContent).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // Include the IMS response body in the error so callers can
                // see e.g. {"error":"invalid_client","error_description":"..."}
                // rather than just a bare status code.
                throw new HttpRequestException(
                    $"Adobe IMS token request failed: {(int)response.StatusCode} {response.StatusCode}. Body: {responseBody}");
            }

            var dto = JsonSerializer.Deserialize<AdobeTokenDto>(responseBody)
                ?? throw new InvalidOperationException("Adobe IMS returned a 200 response with a body that did not deserialize to AdobeTokenDto.");

            return new AdobeToken
            {
                AccessToken = dto.AccessToken,
                TokenType = NormalizeTokenType(dto.TokenType),
                ExpiresIn = dto.ExpiresIn,
                BaseUrl = credential.BaseUrl,
            };
        }

        /// <inheritdoc />
        public Task<bool> ValidateTokenAsync(AdobeToken token)
        {
            ArgumentNullException.ThrowIfNull(token);
            return Task.FromResult(!token.IsExpired);
        }

        // =========================================================
        // Helpers
        // =========================================================

        /// <summary>
        /// Guards against stored credentials whose <see cref="AdobeCredential.ApiKey"/>,
        /// <see cref="AdobeCredential.ClientSecret"/>, or
        /// <see cref="AdobeCredential.Environment"/> are present-but-empty
        /// (e.g. a hand-edited keystore file). The collector already rejects
        /// empty input interactively; this is belt-and-braces so the auth
        /// call fails fast with a clear message rather than sending empty
        /// strings to IMS and receiving an opaque 400.
        /// </summary>
        private static void ValidateCredential(AdobeCredential credential)
        {
            if (string.IsNullOrWhiteSpace(credential.ApiKey))
            {
                throw new ArgumentException(
                    $"{nameof(AdobeCredential.ApiKey)} is required and must not be whitespace.",
                    nameof(credential));
            }
            if (string.IsNullOrWhiteSpace(credential.ClientSecret))
            {
                throw new ArgumentException(
                    $"{nameof(AdobeCredential.ClientSecret)} is required and must not be whitespace.",
                    nameof(credential));
            }
            if (string.IsNullOrWhiteSpace(credential.Environment))
            {
                throw new ArgumentException(
                    $"{nameof(AdobeCredential.Environment)} is required and must not be whitespace.",
                    nameof(credential));
            }
        }

        /// <summary>
        /// IMS returns <c>"bearer"</c> (lowercase) in the <c>token_type</c>
        /// field. RFC 6750 says the scheme is case-insensitive, but some
        /// downstream HTTP servers only accept the <c>"Bearer"</c> spelling.
        /// Normalize to TitleCase so
        /// <see cref="AdobeToken.GetAuthorizationHeader"/> produces a scheme
        /// that works everywhere.
        /// </summary>
        private static string NormalizeTokenType(string tokenType)
        {
            if (string.IsNullOrEmpty(tokenType)) return tokenType;
            return char.ToUpperInvariant(tokenType[0]) + tokenType[1..].ToLowerInvariant();
        }
    }
}
