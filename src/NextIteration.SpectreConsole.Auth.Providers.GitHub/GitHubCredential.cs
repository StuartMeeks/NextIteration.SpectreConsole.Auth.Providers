using NextIteration.SpectreConsole.Auth.Credentials;

using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.GitHub
{
    /// <summary>
    /// GitHub credential captured via the OAuth <b>device flow</b> — the same
    /// flow <c>gh auth login</c> uses by default. Carries the user access token
    /// obtained after the user authorised the device in their browser, plus the
    /// OAuth App <see cref="ClientId"/> and host URLs needed to refresh the
    /// token later, and (for display) the authenticated user's login/name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="RefreshToken"/> and <see cref="AccessTokenExpiresAt"/>
    /// fields are only populated when the OAuth App has opted in to
    /// <i>expiring user tokens</i>. For a classic (non-expiring) OAuth App they
    /// are <see langword="null"/> and the access token is used as-is forever (a
    /// revoked token surfaces as a 401 on first use).
    /// </para>
    /// <para>
    /// <see cref="Login"/> and <see cref="Name"/> are populated by
    /// <see cref="GitHubCredentialCollector"/> at add-time via a
    /// <c>GET {ApiBaseUrl}user</c> call once the token is obtained — they
    /// confirm "authenticated as X" and drive the <c>accounts list</c> display.
    /// </para>
    /// </remarks>
    public sealed class GitHubCredential : ICredential
    {
        private const string GitHubProviderName = "GitHub";

        /// <summary>
        /// Options matching the on-disk keystore format for this credential:
        /// camelCase property names, indented for human readability. Exposed so
        /// consumers (and tests) that round-trip the credential stay consistent
        /// with the collector's serialization.
        /// </summary>
        public static JsonSerializerOptions JsonOptions { get; } = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        /// <inheritdoc cref="ICredential.ProviderName" />
        public static string ProviderName => GitHubProviderName;

        /// <summary>OAuth App client id the device flow was run against.</summary>
        public required string ClientId { get; init; }

        /// <summary>The user access token issued by the device flow.</summary>
        public required string AccessToken { get; init; }

        /// <summary>
        /// Refresh token, when the OAuth App issues expiring tokens; otherwise
        /// <see langword="null"/>.
        /// </summary>
        public string? RefreshToken { get; init; }

        /// <summary>
        /// Absolute expiry of <see cref="AccessToken"/>, when the OAuth App
        /// issues expiring tokens; otherwise <see langword="null"/> (the token
        /// does not expire on its own).
        /// </summary>
        public DateTimeOffset? AccessTokenExpiresAt { get; init; }

        /// <summary>Space-delimited scopes the token was granted.</summary>
        public required string Scopes { get; init; }

        /// <summary>
        /// Web host base URL used for the device-flow and refresh endpoints
        /// (e.g. <c>https://github.com/</c>, or <c>https://ghe.example.com/</c>
        /// for GitHub Enterprise Server).
        /// </summary>
        public required Uri WebBaseUrl { get; init; }

        /// <summary>
        /// REST API base URL the token targets (e.g. <c>https://api.github.com/</c>,
        /// or <c>https://ghe.example.com/api/v3/</c> for GitHub Enterprise Server).
        /// </summary>
        public required Uri ApiBaseUrl { get; init; }

        /// <summary>Login (handle) of the authenticated user.</summary>
        public required string Login { get; init; }

        /// <summary>Display name of the authenticated user, when set on their profile.</summary>
        public string? Name { get; init; }

        /// <inheritdoc />
        public required string Environment { get; init; }

        /// <inheritdoc cref="ICredential.SupportedEnvironments" />
        public static List<string> SupportedEnvironments => GetSupportedEnvironments();

        private static List<string> GetSupportedEnvironments() => [.. Enum.GetNames<Environments>()];

        /// <summary>
        /// Environments the GitHub provider distinguishes. Not prompted —
        /// derived from the host the credential was created against.
        /// </summary>
        public enum Environments
        {
            /// <summary>Public github.com.</summary>
            GitHubCom,

            /// <summary>A GitHub Enterprise Server instance.</summary>
            Enterprise
        }
    }
}
