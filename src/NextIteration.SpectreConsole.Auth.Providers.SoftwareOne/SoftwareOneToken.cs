using NextIteration.SpectreConsole.Auth.Tokens;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne
{
    /// <summary>
    /// SoftwareOne bearer token. SoftwareOne issues long-lived API tokens
    /// from the Marketplace portal — there is no exchange or refresh flow,
    /// so this is effectively a pass-through wrapper around the stored
    /// <see cref="SoftwareOneCredential.ApiToken"/> plus the metadata
    /// consumers need to route a request (base URL, environment, actor).
    /// </summary>
    /// <remarks>
    /// The token is never serialized to disk — the credential is. All
    /// properties are therefore left un-annotated with JSON attributes.
    /// <see cref="IsExpired"/> is hard-coded to <see langword="false"/>:
    /// SoftwareOne tokens don't expire on their own, but they can be
    /// revoked in the portal. A revoked token surfaces as a 401 on the
    /// first API call; consumers should handle that path regardless.
    /// </remarks>
    public sealed class SoftwareOneToken : IToken
    {
        /// <summary>The SoftwareOne API token.</summary>
        public required string ApiToken { get; init; }

        /// <summary>
        /// Actor role the token is scoped to (e.g. Operations, Vendor).
        /// Consumers use this to select which endpoints to call.
        /// </summary>
        public required string Actor { get; init; }

        /// <summary>
        /// Environment the token is scoped to (e.g. Production, Staging,
        /// Test). Consumers use this to route requests.
        /// </summary>
        public required string Environment { get; init; }

        /// <summary>Base URL of the SoftwareOne API the token was issued for.</summary>
        public required Uri BaseUrl { get; init; }

        /// <summary>Token scheme used in the <c>Authorization</c> header.</summary>
        public const string TokenType = "Bearer";

        /// <inheritdoc />
        public bool IsExpired => false;

        /// <inheritdoc />
        public string GetAuthorizationHeader() => $"Bearer {ApiToken}";
    }
}
