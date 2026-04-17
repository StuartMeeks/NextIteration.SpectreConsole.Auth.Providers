using NextIteration.SpectreConsole.Auth.Credentials;
using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.Airtable
{
    /// <summary>
    /// Airtable personal-access-token credential. Airtable's API accepts
    /// the token as a bearer on each request, so there is no separate
    /// token-exchange step.
    /// </summary>
    /// <remarks>
    /// Personal Access Tokens (PATs) are created in the user's Airtable
    /// account settings. Scopes (e.g. <c>data.records:read</c>) and base
    /// restrictions are configured at token-creation time and cannot be
    /// changed at runtime.
    /// </remarks>
    public sealed class AirtableCredential : ICredential
    {
        private const string AirtableProviderName = "Airtable";

        /// <summary>
        /// Options matching the on-disk keystore format for this credential:
        /// camelCase property names, indented for human readability.
        /// Exposed so consumers (and tests) that round-trip the credential
        /// can stay consistent with the collector's serialization.
        /// </summary>
        public static JsonSerializerOptions JsonOptions { get; } = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        /// <inheritdoc cref="ICredential.ProviderName" />
        public static string ProviderName => AirtableProviderName;

        /// <summary>The Airtable personal access token.</summary>
        public required string AccessToken { get; init; }

        /// <inheritdoc />
        public required string Environment { get; init; }

        /// <inheritdoc cref="ICredential.SupportedEnvironments" />
        public static List<string> SupportedEnvironments => GetSupportedEnvironments();

        private static List<string> GetSupportedEnvironments() => [.. Enum.GetNames<Environments>()];

        /// <summary>Environments the Airtable provider supports.</summary>
        public enum Environments
        {
            /// <summary>Production Airtable base.</summary>
            Production,

            /// <summary>Staging Airtable base.</summary>
            Staging,

            /// <summary>Test Airtable base.</summary>
            Test
        }
    }
}
