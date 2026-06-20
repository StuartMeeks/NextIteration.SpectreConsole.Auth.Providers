using NextIteration.SpectreConsole.Auth.Commands;
using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.GitHub
{
    /// <summary>
    /// Projects a <see cref="GitHubCredential"/> into the label/value pairs
    /// shown by <c>accounts list</c>: the authenticated identity, the granted
    /// scopes, the API host, and the token as a masked fingerprint.
    /// </summary>
    public sealed class GitHubCredentialSummaryProvider : ICredentialSummaryProvider
    {
        /// <inheritdoc />
        public string ProviderName => GitHubCredential.ProviderName;

        /// <inheritdoc />
        public IReadOnlyList<KeyValuePair<string, string>> GetDisplayFields(string decryptedCredentialJson)
        {
            // Defensive: if deserialization fails (corrupt keystore, schema
            // drift), surface a visible marker instead of throwing into the
            // Spectre render loop and taking down the list command.
            GitHubCredential? credential;
            try
            {
                credential = JsonSerializer.Deserialize<GitHubCredential>(decryptedCredentialJson, GitHubCredential.JsonOptions);
            }
            catch (JsonException)
            {
                return [new("Status", "<unreadable credential>")];
            }

            if (credential is null)
            {
                return [new("Status", "<unreadable credential>")];
            }

            var account = string.IsNullOrWhiteSpace(credential.Name)
                ? credential.Login
                : $"{credential.Login} ({credential.Name})";

            return
            [
                new("Account", account),
                new("Scopes", string.IsNullOrWhiteSpace(credential.Scopes) ? "(none)" : credential.Scopes),
                new("Host", credential.ApiBaseUrl.ToString()),
                new("Token", Mask(credential.AccessToken)),
            ];
        }

        // Tokens are long in practice; short inputs get a fixed four-star mask
        // so the display never leaks length information.
        private static string Mask(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= 10 ? "****" : value[..4] + "..." + value[^4..];
        }
    }
}
