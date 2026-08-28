using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.Adobe.Tests
{
    public sealed class AdobeTokenTests
    {
        [Fact]
        public void IsExpired_IsFalse_WhenJustIssuedWithLongLifetime()
        {
            var token = NewToken(expiresIn: 3600, createdAt: DateTime.UtcNow);

            Assert.False(token.IsExpired);
        }

        [Fact]
        public void IsExpired_IsFalse_WhenCreatedAtIsLocalKind()
        {
            // CreatedAt is a public init property, so DateTime.Now is an easy
            // mistake. Before normalisation this compared a local wall-clock
            // value against DateTime.UtcNow: east of UTC a fresh token looked
            // valid for hours after it had actually expired (every call 401s
            // with no re-authenticate), and west of UTC it looked expired on
            // arrival.
            var token = NewToken(expiresIn: 3600, createdAt: DateTime.Now);

            Assert.False(token.IsExpired);
        }

        [Fact]
        public void IsExpired_IsTrue_WhenLocalKindTokenHasActuallyExpired()
        {
            var token = NewToken(expiresIn: 60, createdAt: DateTime.Now.AddMinutes(-10));

            Assert.True(token.IsExpired);
        }

        [Fact]
        public void IsExpired_TreatsUnspecifiedKindAsUtc_AsDocumented()
        {
            // A value round-tripped through JSON without an offset comes back
            // Unspecified. CreatedAt is documented as UTC, so it is taken at its
            // word rather than letting ToUniversalTime() assume local time —
            // which would otherwise shift the expiry by the machine's offset.
            var unspecified = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            Assert.False(NewToken(expiresIn: 3600, createdAt: unspecified).IsExpired);
            Assert.True(NewToken(expiresIn: 0, createdAt: unspecified).IsExpired);
        }

        [Fact]
        public void IsExpired_AgreesAcrossAllThreeKinds_ForTheSameInstant()
        {
            var utcNow = DateTime.UtcNow;
            var asUtc = NewToken(expiresIn: 3600, createdAt: utcNow);
            var asLocal = NewToken(expiresIn: 3600, createdAt: utcNow.ToLocalTime());

            Assert.Equal(asUtc.IsExpired, asLocal.IsExpired);
        }

        [Fact]
        public void IsExpired_IsTrue_WhenLifetimeIsZero()
        {
            // ExpiresIn=0 means already expired (the 30s clock-skew buffer
            // makes the boundary even more aggressive — always true).
            var token = NewToken(expiresIn: 0, createdAt: DateTime.UtcNow);

            Assert.True(token.IsExpired);
        }

        [Fact]
        public void IsExpired_IsTrue_WhenWithinClockSkewOfExpiry()
        {
            // Issued 1 minute ago with a 60s lifetime → would normally have
            // 0s remaining, but the 30s clock-skew buffer fires it as expired
            // 30s before the actual expiry.
            var token = NewToken(expiresIn: 60, createdAt: DateTime.UtcNow.AddSeconds(-45));

            Assert.True(token.IsExpired);
        }

        [Fact]
        public void IsExpired_IsFalse_WhenOutsideClockSkewOfExpiry()
        {
            // 90s lifetime, issued now → should have ~60s before clock skew fires.
            var token = NewToken(expiresIn: 90, createdAt: DateTime.UtcNow);

            Assert.False(token.IsExpired);
        }

        [Fact]
        public void GetAuthorizationHeader_UsesTokenTypeAndAccessToken()
        {
            var token = NewToken(expiresIn: 3600, createdAt: DateTime.UtcNow);

            Assert.Equal("bearer xyz", token.GetAuthorizationHeader());
        }

        [Fact]
        public void ExpiryClockSkew_IsThirtySeconds() => Assert.Equal(TimeSpan.FromSeconds(30), AdobeToken.ExpiryClockSkew);

        private static AdobeToken NewToken(int expiresIn, DateTime createdAt) => new()
        {
            AccessToken = "xyz",
            TokenType = "bearer",
            ExpiresIn = expiresIn,
            BaseUrl = new Uri("https://partners.adobe.io/"),
            CreatedAt = createdAt,
        };
    }
}
