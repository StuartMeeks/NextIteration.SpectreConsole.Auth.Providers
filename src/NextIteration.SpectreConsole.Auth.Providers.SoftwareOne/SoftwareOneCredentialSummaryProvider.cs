using NextIteration.SpectreConsole.Auth.Commands;
using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne
{
    /// <summary>
    /// Projects a <see cref="SoftwareOneCredential"/> into the label/value
    /// pairs shown by <c>accounts list</c>. As of 0.3.0 the credential
    /// carries validated SoftwareOne-side metadata (token id/name, account
    /// id/name/type), so the display is richer: the account is the primary
    /// identity, with the actor/environment/base URL as context and the
    /// token rendered as a masked fingerprint plus its friendly name.
    /// </summary>
    public sealed class SoftwareOneCredentialSummaryProvider : ICredentialSummaryProvider
    {
        /// <inheritdoc />
        public string ProviderName => SoftwareOneCredential.ProviderName;

        /// <inheritdoc />
        public IReadOnlyList<KeyValuePair<string, string>> GetDisplayFields(string decryptedCredentialJson)
        {
            // Defensive: if deserialization fails (corrupt keystore, schema
            // drift from a pre-0.3.0 credential), surface a visible marker
            // instead of throwing into the Spectre render loop and taking
            // down the list command.
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
                new("Account", $"{credential.AccountName} ({credential.AccountType})"),
                new("Actor", credential.Actor),
                new("Base URL", credential.BaseUrl.ToString()),
                new("Token", $"{Mask(credential.ApiToken)} — {credential.TokenName}"),
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
