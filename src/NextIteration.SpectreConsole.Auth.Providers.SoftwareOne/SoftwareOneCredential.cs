using NextIteration.SpectreConsole.Auth.Credentials;
using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne
{
    /// <summary>
    /// SoftwareOne Marketplace API-token credential. Carries the long-lived
    /// portal-issued API token plus the base URL, environment, the actor
    /// role the token is scoped to, and (as of 0.3.0) the SoftwareOne-side
    /// metadata for the token: token id/name and the owning account's
    /// id/name/type.
    /// </summary>
    /// <remarks>
    /// The metadata fields (<see cref="TokenId"/>, <see cref="TokenName"/>,
    /// <see cref="AccountId"/>, <see cref="AccountName"/>, <see cref="AccountType"/>)
    /// are populated by <see cref="SoftwareOneCredentialCollector"/> at
    /// add-time via a live lookup against the SoftwareOne Marketplace API.
    /// If the lookup finds exactly one matching token the credential is
    /// stored; otherwise the collector fails and the credential is not
    /// saved. The metadata is therefore always present on a stored 0.3.0+
    /// credential and is safe to treat as required.
    /// </remarks>
    public sealed class SoftwareOneCredential : ICredential
    {
        private const string SoftwareOneProviderName = "SoftwareOne";

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
        public static string ProviderName => SoftwareOneProviderName;

        /// <summary>The SoftwareOne API token.</summary>
        public required string ApiToken { get; init; }

        /// <summary>Base URL of the SoftwareOne API this credential targets.</summary>
        public required Uri BaseUrl { get; init; }

        /// <inheritdoc />
        public required string Environment { get; init; }

        /// <summary>
        /// Actor (role) this token authenticates as. One of the values
        /// returned by <see cref="SupportedActors"/>.
        /// </summary>
        public required string Actor { get; init; }

        /// <summary>SoftwareOne-side identifier for the API token (as returned by the Marketplace API).</summary>
        public required string TokenId { get; init; }

        /// <summary>Human-readable name of the API token as configured in the Marketplace portal.</summary>
        public required string TokenName { get; init; }

        /// <summary>SoftwareOne-side identifier for the account that owns the token.</summary>
        public required string AccountId { get; init; }

        /// <summary>Human-readable name of the account that owns the token.</summary>
        public required string AccountName { get; init; }

        /// <summary>Account type as reported by the Marketplace API (e.g. <c>Vendor</c>, <c>Client</c>, <c>Operations</c>).</summary>
        public required string AccountType { get; init; }

        /// <inheritdoc cref="ICredential.SupportedEnvironments" />
        public static List<string> SupportedEnvironments => GetSupportedEnvironments();

        /// <summary>Actor roles the SoftwareOne provider supports.</summary>
        public static List<string> SupportedActors => GetSupportedActors();

        private static List<string> GetSupportedEnvironments() => [.. Enum.GetNames<Environments>()];

        private static List<string> GetSupportedActors() => [.. Enum.GetNames<Actors>()];

        /// <summary>Environments the SoftwareOne provider supports.</summary>
        public enum Environments
        {
            /// <summary>Production SoftwareOne environment.</summary>
            Production,

            /// <summary>Staging SoftwareOne environment.</summary>
            Staging,

            /// <summary>Test SoftwareOne environment.</summary>
            Test
        }

        /// <summary>Actor roles the SoftwareOne provider supports.</summary>
        public enum Actors
        {
            /// <summary>Operations actor — typically used for back-office / admin flows.</summary>
            Operations,

            /// <summary>Vendor actor — typically used for catalog / listing flows.</summary>
            Vendor
        }
    }
}
