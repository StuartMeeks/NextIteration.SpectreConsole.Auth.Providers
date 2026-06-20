using NextIteration.SpectreConsole.Auth.Tokens;

namespace NextIteration.SpectreConsole.Auth.Providers.GitHub
{
    /// <summary>
    /// GitHub user access token (bearer). Produced by
    /// <see cref="GitHubAuthenticationService"/> either as a pass-through of the
    /// stored token or, for an OAuth App that issues expiring tokens, as a
    /// freshly refreshed token.
    /// </summary>
    /// <remarks>
    /// The token is never serialized to disk — the credential is. When
    /// <see cref="ExpiresAt"/> is <see langword="null"/> the token does not
    /// expire on its own (classic OAuth App); a revoked token surfaces as a 401
    /// on first use, which consumers should handle regardless.
    /// </remarks>
    public sealed class GitHubToken : IToken
    {
        /// <summary>
        /// Safety margin applied to <see cref="IsExpired"/> so a token that is
        /// about to expire surfaces as expired <i>before</i> the exact expiry
        /// boundary, avoiding a check-then-401 race.
        /// </summary>
        public static readonly TimeSpan ExpiryClockSkew = TimeSpan.FromSeconds(30);

        /// <summary>The user access token.</summary>
        public required string AccessToken { get; init; }

        /// <summary>REST API base URL the token was issued for.</summary>
        public required Uri BaseUrl { get; init; }

        /// <summary>
        /// Absolute expiry of the token, or <see langword="null"/> for a
        /// non-expiring (classic OAuth App) token.
        /// </summary>
        public DateTimeOffset? ExpiresAt { get; init; }

        /// <summary>Token scheme used in the <c>Authorization</c> header.</summary>
        public const string TokenType = "Bearer";

        /// <inheritdoc />
        public bool IsExpired
            => ExpiresAt is { } expiry && DateTimeOffset.UtcNow >= expiry - ExpiryClockSkew;

        /// <inheritdoc />
        public string GetAuthorizationHeader() => $"Bearer {AccessToken}";
    }
}
