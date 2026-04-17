using NextIteration.SpectreConsole.Auth.Commands;
using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne
{
    /// <summary>
    /// Projects a <see cref="SoftwareOneCredential"/> into the label/value
    /// pairs shown by <c>accounts list</c> — Actor, Base URL, and a masked
    /// API token.
    /// </summary>
    public sealed class SoftwareOneCredentialSummaryProvider : ICredentialSummaryProvider
    {
        /// <inheritdoc />
        public string ProviderName => SoftwareOneCredential.ProviderName;

        /// <inheritdoc />
        public IReadOnlyList<KeyValuePair<string, string>> GetDisplayFields(string decryptedCredentialJson)
        {
            // Defensive: if deserialization fails (corrupt keystore, schema
            // drift), surface a visible marker instead of throwing into the
            // Spectre render loop and taking down the list command.
            SoftwareOneCredential? credential;
            try
            {
                credential = JsonSerializer.Deserialize<SoftwareOneCredential>(decryptedCredentialJson, SoftwareOneCredential.JsonOptions);
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
                new("Actor", credential.Actor),
                new("Base URL", credential.BaseUrl.ToString()),
                new("Token", Mask(credential.ApiToken)),
            ];
        }

        // Tokens are ~32+ chars in practice; short inputs get a fixed
        // four-star mask so the display never leaks length information.
        private static string Mask(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= 10 ? "****" : value[..4] + "..." + value[^4..];
        }
    }
}
