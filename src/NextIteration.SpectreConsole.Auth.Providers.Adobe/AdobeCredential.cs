using NextIteration.SpectreConsole.Auth.Credentials;
using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.Adobe
{
    /// <summary>
    /// Adobe VIP Marketplace credential holding the OAuth2
    /// client-credentials flow inputs (IMS URL, API key / client ID, client
    /// secret) plus the base URL for the target Adobe API.
    /// </summary>
    public sealed class AdobeCredential : ICredential
    {
        private const string AdobeProviderName = "Adobe";

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
        public static string ProviderName => AdobeProviderName;

        /// <summary>Adobe IMS endpoint (e.g. <c>https://ims-na1.adobelogin.com/</c>).</summary>
        public required Uri ImsUrl { get; init; }

        /// <summary>Adobe API key (also used as the OAuth2 <c>client_id</c>).</summary>
        public required string ApiKey { get; init; }

        /// <summary>OAuth2 client secret paired with <see cref="ApiKey"/>.</summary>
        public required string ClientSecret { get; init; }

        /// <summary>Base URL of the Adobe API this credential authenticates against.</summary>
        public required Uri BaseUrl { get; init; }

        /// <inheritdoc />
        public required string Environment { get; init; }

        /// <inheritdoc cref="ICredential.SupportedEnvironments" />
        public static List<string> SupportedEnvironments => GetSupportedEnvironments();

        private static List<string> GetSupportedEnvironments() => [.. Enum.GetNames<Environments>()];

        /// <summary>Environments the Adobe provider supports.</summary>
        public enum Environments
        {
            /// <summary>Production Adobe environment.</summary>
            Production,

            /// <summary>Sandbox / non-production Adobe environment.</summary>
            Sandbox
        }
    }
}
