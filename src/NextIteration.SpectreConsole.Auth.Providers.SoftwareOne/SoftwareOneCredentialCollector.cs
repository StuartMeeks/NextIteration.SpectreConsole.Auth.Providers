using Spectre.Console;

using NextIteration.SpectreConsole.Auth.Commands;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne
{
    /// <summary>
    /// Interactive collector for SoftwareOne credentials. Prompts for the
    /// API token (hidden), base URL, environment, and actor, then validates
    /// the token against the SoftwareOne Marketplace API to confirm it
    /// resolves to exactly one token record and to enrich the credential
    /// with the token's SoftwareOne-side metadata (token id/name +
    /// account id/name/type).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The validation step performs an authenticated
    /// <c>GET {BaseUrl}/v1/accounts/api-tokens?eq(token,'…')&amp;limit=2</c>
    /// call. If the lookup fails (network error, non-success status, zero
    /// matches, or multiple matches) the collector throws and the
    /// credential is <b>not</b> stored — failing fast keeps the keystore
    /// free of half-valid credentials that would 401 on first use anyway.
    /// </para>
    /// <para>
    /// Consumers must register <c>IHttpClientFactory</c> in DI
    /// (<c>services.AddHttpClient()</c>) — the collector's constructor
    /// depends on it. Registered automatically by
    /// <see cref="ServiceCollectionExtensions.AddSoftwareOneAuthProvider"/>.
    /// </para>
    /// </remarks>
    public sealed class SoftwareOneCredentialCollector : ICredentialCollector
    {
        /// <summary>
        /// Named HttpClient identity used by the collector for the
        /// validation lookup. Consumers wishing to pre-configure the
        /// client (proxy, retry handler, user-agent) can call
        /// <c>services.AddHttpClient(SoftwareOneCredentialCollector.HttpClientName, …)</c>.
        /// </summary>
        public const string HttpClientName = "SoftwareOne Credential Validator";

        private const string DefaultBaseUrl = "https://api.softwareone.com/";

        // Cached composite format avoids re-parsing the format string on
        // every lookup (CA1863).
        private static readonly CompositeFormat TokenLookupPathFormat
            = CompositeFormat.Parse("v1/accounts/api-tokens?eq(token,'{0}')&limit=2");

        // Cached options for the lookup response — shared across calls
        // (CA1869).
        private static readonly JsonSerializerOptions TokenLookupJsonOptions
            = new() { PropertyNameCaseInsensitive = true };

        // Hard cap on the response-body slice we surface in exceptions.
        // Big enough to keep useful "invalid_token"-style payloads, small
        // enough that an upstream proxy echoing the full request can't bloat
        // logs or sneak a credential past our redaction.
        internal const int ErrorBodyMaxChars = 512;

        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>DI constructor.</summary>
        public SoftwareOneCredentialCollector(IHttpClientFactory httpClientFactory)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc />
        public string ProviderName => SoftwareOneCredential.ProviderName;

        /// <inheritdoc />
        public async Task<(string credentialData, string environment)> CollectAsync()
        {
            var apiToken = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter API Token:")
                    .Secret()
                    .Validate(value => string.IsNullOrWhiteSpace(value)
                        ? ValidationResult.Error("API token cannot be empty")
                        : ValidationResult.Success())).ConfigureAwait(false);

            var baseUrlInput = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter Base URL:")
                    .DefaultValue(DefaultBaseUrl)
                    .Validate(ValidateSecureBaseUrl)).ConfigureAwait(false);

            var environment = await AnsiConsole.PromptAsync(
                new SelectionPrompt<string>()
                    .Title("Select environment:")
                    .AddChoices(SoftwareOneCredential.SupportedEnvironments)).ConfigureAwait(false);

            var actor = await AnsiConsole.PromptAsync(
                new SelectionPrompt<string>()
                    .Title("Select actor:")
                    .AddChoices(SoftwareOneCredential.SupportedActors)).ConfigureAwait(false);

            var baseUrl = new Uri(baseUrlInput, UriKind.Absolute);

            // Validate the token against the SoftwareOne Marketplace API.
            // Failures throw; the caller never reaches the serialise step
            // and the credential is never stored.
            var tokenDto = await LookupTokenAsync(baseUrl, apiToken).ConfigureAwait(false);

            var credential = new SoftwareOneCredential
            {
                ApiToken = apiToken,
                BaseUrl = baseUrl,
                Environment = environment,
                Actor = actor,
                TokenId = tokenDto.Id,
                TokenName = tokenDto.Name,
                AccountId = tokenDto.Account.Id,
                AccountName = tokenDto.Account.Name,
                AccountType = tokenDto.Account.Type,
            };

            return (JsonSerializer.Serialize(credential, SoftwareOneCredential.JsonOptions), credential.Environment);
        }

        /// <summary>
        /// Calls the Marketplace API to resolve the supplied token to its
        /// record. Returns the single matching <see cref="SoftwareOneTokenDto"/>
        /// on success; throws <see cref="InvalidOperationException"/> on
        /// any failure mode (HTTP error, zero matches, multiple matches,
        /// malformed response).
        /// </summary>
        internal async Task<SoftwareOneTokenDto> LookupTokenAsync(Uri baseUrl, string apiToken)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            var requestUri = new Uri(baseUrl, string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                TokenLookupPathFormat,
                Uri.EscapeDataString(apiToken)));

            // ^ string.Format signature that takes CompositeFormat keeps
            //   CA1863 satisfied — unlike the plain string-format overload.

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await client.SendAsync(request).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // Sanitise before surfacing: a misbehaving upstream proxy can
                // echo the request URL — which carries the token in the
                // eq(token,'…') query — back into an error page. Redact the
                // literal token and cap the slice we keep so neither logs
                // nor exception aggregators receive credential material.
                var safeBody = SanitiseErrorBody(responseBody, apiToken);
                throw new InvalidOperationException(
                    $"SoftwareOne token lookup failed: {(int)response.StatusCode} {response.StatusCode}. Body: {safeBody}");
            }

            var result = JsonSerializer.Deserialize<SoftwareOneTokenSearchResult>(responseBody, TokenLookupJsonOptions)
                ?? throw new InvalidOperationException(
                    "SoftwareOne token lookup returned a success status with a body that did not deserialize to a search result.");

            if (result.Data.Count == 0)
            {
                throw new InvalidOperationException(
                    "SoftwareOne token lookup returned zero matches — the token was not found in the Marketplace. Check the token value and try again.");
            }

            if (result.Data.Count > 1)
            {
                throw new InvalidOperationException(
                    $"SoftwareOne token lookup returned {result.Data.Count} matches for what should be a unique token value. The Marketplace API response is ambiguous; aborting rather than guessing.");
            }

            return result.Data[0];
        }

        /// <summary>
        /// Accept the URL only if it's an absolute https URI, or an
        /// http loopback (so devs can point the collector at a local
        /// mock or proxy without compromising the token over the wire
        /// in real deployments).
        /// </summary>
        internal static ValidationResult ValidateSecureBaseUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed))
            {
                return ValidationResult.Error("Must be a valid absolute http(s) URL");
            }

            if (parsed.Scheme == Uri.UriSchemeHttps)
            {
                return ValidationResult.Success();
            }

            if (parsed.Scheme == Uri.UriSchemeHttp && parsed.IsLoopback)
            {
                return ValidationResult.Success();
            }

            return ValidationResult.Error(
                "Must use https. http is only accepted for loopback addresses (the SoftwareOne API token is sent in the URL query and must not traverse the network in cleartext).");
        }

        /// <summary>
        /// Strip the literal token value from the response body and
        /// truncate to <see cref="ErrorBodyMaxChars"/>. The token is
        /// what we're trying to prevent from leaking into logs; the
        /// truncation bounds size for everything else (memory pressure,
        /// noise in aggregators).
        /// </summary>
        internal static string SanitiseErrorBody(string body, string apiToken)
        {
            if (string.IsNullOrEmpty(body))
            {
                return body;
            }

            var redacted = string.IsNullOrEmpty(apiToken)
                ? body
                : body.Replace(apiToken, "<redacted>", StringComparison.Ordinal);

            if (redacted.Length <= ErrorBodyMaxChars)
            {
                return redacted;
            }

            return string.Concat(redacted.AsSpan(0, ErrorBodyMaxChars), "… [truncated]");
        }
    }
}
