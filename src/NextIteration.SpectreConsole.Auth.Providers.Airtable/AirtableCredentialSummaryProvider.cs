using NextIteration.SpectreConsole.Auth.Commands;
using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.Airtable
{
    /// <summary>
    /// Projects an <see cref="AirtableCredential"/> into the label/value
    /// pairs shown by <c>accounts list</c> — a single masked token column.
    /// </summary>
    public sealed class AirtableCredentialSummaryProvider : ICredentialSummaryProvider
    {
        /// <inheritdoc />
        public string ProviderName => AirtableCredential.ProviderName;

        /// <inheritdoc />
        public IReadOnlyList<KeyValuePair<string, string>> GetDisplayFields(string decryptedCredentialJson)
        {
            // Defensive: if deserialization fails (corrupt keystore, schema
            // drift), surface a visible marker instead of throwing into the
            // Spectre render loop and taking down the list command.
            AirtableCredential? credential;
            try
            {
                credential = JsonSerializer.Deserialize<AirtableCredential>(decryptedCredentialJson, AirtableCredential.JsonOptions);
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
                new("Token", Mask(credential.AccessToken)),
            ];
        }

        // Tokens are ~80+ chars in practice; short inputs get a fixed
        // four-star mask so the display never leaks length information.
        private static string Mask(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= 10 ? "****" : value[..4] + "..." + value[^4..];
        }
    }
}
