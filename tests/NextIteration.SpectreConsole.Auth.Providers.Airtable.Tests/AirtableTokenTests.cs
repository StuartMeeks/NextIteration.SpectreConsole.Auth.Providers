using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.Airtable.Tests;

public sealed class AirtableTokenTests
{
    [Fact]
    public void IsExpired_IsAlwaysFalse()
    {
        // Airtable PATs don't expire on their own — revocation surfaces
        // as a 401 at request time, not through IsExpired.
        var token = NewToken("any-token");

        Assert.False(token.IsExpired);
    }

    [Fact]
    public void TokenType_IsBearer()
    {
        Assert.Equal("Bearer", AirtableToken.TokenType);
    }

    [Fact]
    public void GetAuthorizationHeader_IsBearerSpaceToken()
    {
        var token = NewToken("pat-abc-123");

        Assert.Equal("Bearer pat-abc-123", token.GetAuthorizationHeader());
    }

    [Fact]
    public void AllFields_RoundTripThroughInitializer()
    {
        var token = new AirtableToken
        {
            AccessToken = "t1",
            BaseUrl = new Uri("https://api.airtable.com/"),
        };

        Assert.Equal("t1", token.AccessToken);
        Assert.Equal(new Uri("https://api.airtable.com/"), token.BaseUrl);
    }

    private static AirtableToken NewToken(string accessToken) => new()
    {
        AccessToken = accessToken,
        BaseUrl = new Uri("https://api.airtable.com/"),
    };
}
