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
        /// <remarks>
        /// Documented and defaulted as UTC, but this is a public
        /// <see langword="init"/> property, so a consumer can hand it a
        /// <see cref="DateTimeKind.Local"/> value — <c>DateTime.Now</c> is the
        /// obvious mistake. <see cref="IsExpired"/> normalises rather than
        /// comparing a Kind-agnostic value against <see cref="DateTime.UtcNow"/>.
        /// <see cref="DateTimeKind.Unspecified"/> is taken at its documented
        /// word and treated as UTC, which is what a value round-tripped through
        /// JSON without an offset will be.
        /// </remarks>
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// <see cref="CreatedAt"/> as an unambiguous UTC instant.
        /// </summary>
        private DateTime CreatedAtUtc
            => CreatedAt.Kind switch
            {
                DateTimeKind.Utc => CreatedAt,
                DateTimeKind.Local => CreatedAt.ToUniversalTime(),
                _ => DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc),
            };

        /// <inheritdoc />
        public bool IsExpired
            => DateTime.UtcNow >= CreatedAtUtc.AddSeconds(ExpiresIn) - ExpiryClockSkew;

        /// <inheritdoc />
        public string GetAuthorizationHeader() => $"{TokenType} {AccessToken}";
    }
}
