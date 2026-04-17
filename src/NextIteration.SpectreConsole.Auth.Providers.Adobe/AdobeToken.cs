using NextIteration.SpectreConsole.Auth.Tokens;

namespace NextIteration.SpectreConsole.Auth.Providers.Adobe
{
    /// <summary>
    /// Short-lived bearer token issued by Adobe IMS in response to the
    /// OAuth2 client-credentials flow.
    /// </summary>
    public sealed class AdobeToken : IToken
    {
        /// <summary>
        /// Safety margin applied to <see cref="IsExpired"/> so a token
        /// that's about to expire surfaces as expired <i>before</i> the
        /// exact issue-time + expires-in boundary. Avoids the race where
        /// a caller checks <c>IsExpired</c>, gets <see langword="false"/>,
        /// then trips a 401 a few seconds later because the token aged
        /// out between check and use.
        /// </summary>
        public static readonly TimeSpan ExpiryClockSkew = TimeSpan.FromSeconds(30);

        /// <summary>The opaque access token.</summary>
        public required string AccessToken { get; init; }

        /// <summary>Token type as reported by IMS (typically <c>bearer</c>).</summary>
        public required string TokenType { get; init; }

        /// <summary>Lifetime of the token in seconds, as reported at issue time.</summary>
        public required int ExpiresIn { get; init; }

        /// <summary>Base URL of the API the token was issued for.</summary>
        public required Uri BaseUrl { get; init; }

        /// <summary>Timestamp (UTC) at which the token was created.</summary>
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

        /// <inheritdoc />
        public bool IsExpired
            => DateTime.UtcNow >= CreatedAt.AddSeconds(ExpiresIn) - ExpiryClockSkew;

        /// <inheritdoc />
        public string GetAuthorizationHeader() => $"{TokenType} {AccessToken}";
    }
}
