using NextIteration.SpectreConsole.Auth.Commands;
using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.Adobe
{
    /// <summary>
    /// Projects an <see cref="AdobeCredential"/> into the label/value pairs
    /// shown by <c>accounts list</c> — API key (plain; it's a public
    /// identifier), IMS URL, base URL, and a masked client-secret
    /// fingerprint.
    /// </summary>
    public sealed class AdobeCredentialSummaryProvider : ICredentialSummaryProvider
    {
        /// <inheritdoc />
        public string ProviderName => AdobeCredential.ProviderName;

        /// <inheritdoc />
        public IReadOnlyList<KeyValuePair<string, string>> GetDisplayFields(string decryptedCredentialJson)
        {
            // Defensive: if deserialization fails (corrupt keystore, schema
            // drift), surface a visible marker instead of throwing into the
            // Spectre render loop and taking down the list command.
            AdobeCredential? credential;
            try
            {
                credential = JsonSerializer.Deserialize<AdobeCredential>(decryptedCredentialJson, AdobeCredential.JsonOptions);
            }
            catch (JsonException)
            {
                return [new("Status", "<unreadable credential>")];
            }

            if (credential is null)
            {
                return [new("Status", "<unreadable credential>")];
            }

            return
            [
                // API Key is the OAuth2 client_id — public, safe to show.
                new("API Key", credential.ApiKey),
                new("IMS URL", credential.ImsUrl.ToString()),
                new("Base URL", credential.BaseUrl.ToString()),
                new("Client Secret", Mask(credential.ClientSecret)),
            ];
        }

        // Secrets are ~30+ chars in practice; short inputs get a fixed
        // four-star mask so the display never leaks length information.
        private static string Mask(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= 10 ? "****" : value[..4] + "..." + value[^4..];
        }
    }
}
