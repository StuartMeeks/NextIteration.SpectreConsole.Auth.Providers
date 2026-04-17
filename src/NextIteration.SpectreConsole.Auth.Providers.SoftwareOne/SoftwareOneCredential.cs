using NextIteration.SpectreConsole.Auth.Credentials;
using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne
{
    /// <summary>
    /// SoftwareOne Marketplace API-token credential. Carries the long-lived
    /// portal-issued API token plus the base URL, environment, and the
    /// actor role the token is scoped to.
    /// </summary>
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
