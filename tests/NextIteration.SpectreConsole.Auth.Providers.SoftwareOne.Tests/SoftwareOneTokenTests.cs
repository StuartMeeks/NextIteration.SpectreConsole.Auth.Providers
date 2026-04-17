using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne.Tests;

public sealed class SoftwareOneTokenTests
{
    [Fact]
    public void IsExpired_IsAlwaysFalse()
    {
        // SoftwareOne tokens don't expire on their own — revocation surfaces
        // as a 401 at request time, not through IsExpired.
        var token = NewToken("any-token");

        Assert.False(token.IsExpired);
    }

    [Fact]
    public void TokenType_IsBearer()
    {
        Assert.Equal("Bearer", SoftwareOneToken.TokenType);
    }

    [Fact]
    public void GetAuthorizationHeader_IsBearerSpaceToken()
    {
        var token = NewToken("abc-123");

        Assert.Equal("Bearer abc-123", token.GetAuthorizationHeader());
    }

    [Fact]
    public void AllFields_RoundTripThroughInitializer()
    {
        var token = new SoftwareOneToken
        {
            ApiToken = "t1",
            Actor = "Vendor",
            Environment = "Staging",
            BaseUrl = new Uri("https://staging.softwareone.com/"),
        };

        Assert.Equal("t1", token.ApiToken);
        Assert.Equal("Vendor", token.Actor);
        Assert.Equal("Staging", token.Environment);
        Assert.Equal(new Uri("https://staging.softwareone.com/"), token.BaseUrl);
    }

    private static SoftwareOneToken NewToken(string apiToken) => new()
    {
        ApiToken = apiToken,
        Actor = "Operations",
        Environment = "Production",
        BaseUrl = new Uri("https://api.softwareone.com/"),
    };
}
