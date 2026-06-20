using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.GitHub.Tests;

public sealed class GitHubTokenTests
{
    [Fact]
    public void GetAuthorizationHeader_IsBearerToken()
    {
        var token = new GitHubToken
        {
            AccessToken = "gho_abc",
            BaseUrl = new Uri("https://api.github.com/"),
        };

        Assert.Equal("Bearer gho_abc", token.GetAuthorizationHeader());
    }

    [Fact]
    public void IsExpired_IsFalse_WhenNoExpirySet()
    {
        var token = new GitHubToken
        {
            AccessToken = "gho_abc",
            BaseUrl = new Uri("https://api.github.com/"),
            ExpiresAt = null,
        };

        Assert.False(token.IsExpired);
    }

    [Fact]
    public void IsExpired_IsTrue_WhenPastExpiryMinusSkew()
    {
        var token = new GitHubToken
        {
            AccessToken = "gho_abc",
            BaseUrl = new Uri("https://api.github.com/"),
            // Already past, well beyond the clock-skew window.
            ExpiresAt = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1),
        };

        Assert.True(token.IsExpired);
    }

    [Fact]
    public void IsExpired_IsTrue_WithinClockSkewWindow()
    {
        var token = new GitHubToken
        {
            AccessToken = "gho_abc",
            BaseUrl = new Uri("https://api.github.com/"),
            // Expires in 10s, but the 30s skew window trips it early.
            ExpiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10),
        };

        Assert.True(token.IsExpired);
    }

    [Fact]
    public void IsExpired_IsFalse_WhenComfortablyInFuture()
    {
        var token = new GitHubToken
        {
            AccessToken = "gho_abc",
            BaseUrl = new Uri("https://api.github.com/"),
            ExpiresAt = DateTimeOffset.UtcNow + TimeSpan.FromHours(1),
        };

        Assert.False(token.IsExpired);
    }
}
