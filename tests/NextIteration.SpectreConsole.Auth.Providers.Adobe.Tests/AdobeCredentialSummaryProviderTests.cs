using System.Text.Json;
using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.Adobe.Tests;

public sealed class AdobeCredentialSummaryProviderTests
{
    [Fact]
    public void ProviderName_IsAdobe()
    {
        var provider = new AdobeCredentialSummaryProvider();

        Assert.Equal("Adobe", provider.ProviderName);
    }

    [Fact]
    public void GetDisplayFields_ValidJson_ReturnsApiKeyPlainAndClientSecretMasked()
    {
        var credential = new AdobeCredential
        {
            ImsUrl = new Uri("https://ims-na1.adobelogin.com/"),
            ApiKey = "abc-api-key",
            ClientSecret = "abcdefghij-very-long-secret-xyz9",
            BaseUrl = new Uri("https://partners.adobe.io/"),
            Environment = "Production",
        };
        var json = JsonSerializer.Serialize(credential, AdobeCredential.JsonOptions);
        var provider = new AdobeCredentialSummaryProvider();

        var fields = provider.GetDisplayFields(json);

        Assert.Equal(4, fields.Count);
        Assert.Equal(new KeyValuePair<string, string>("API Key", "abc-api-key"), fields[0]);
        Assert.Equal(new KeyValuePair<string, string>("IMS URL", "https://ims-na1.adobelogin.com/"), fields[1]);
        Assert.Equal(new KeyValuePair<string, string>("Base URL", "https://partners.adobe.io/"), fields[2]);
        Assert.Equal("Client Secret", fields[3].Key);
        Assert.Equal("abcd...xyz9", fields[3].Value);
    }

    [Fact]
    public void GetDisplayFields_ShortSecret_MasksWithFourStars()
    {
        var credential = new AdobeCredential
        {
            ImsUrl = new Uri("https://ims-na1.adobelogin.com/"),
            ApiKey = "abc-api-key",
            ClientSecret = "abc",
            BaseUrl = new Uri("https://partners.adobe.io/"),
            Environment = "Production",
        };
        var json = JsonSerializer.Serialize(credential, AdobeCredential.JsonOptions);
        var provider = new AdobeCredentialSummaryProvider();

        var fields = provider.GetDisplayFields(json);

        var secret = fields.Single(f => f.Key == "Client Secret");
        Assert.Equal("****", secret.Value);
    }

    [Fact]
    public void GetDisplayFields_MalformedJson_ReturnsUnreadableMarker()
    {
        var provider = new AdobeCredentialSummaryProvider();

        var fields = provider.GetDisplayFields("{ not json");

        Assert.Single(fields);
        Assert.Equal("Status", fields[0].Key);
        Assert.Equal("<unreadable credential>", fields[0].Value);
    }

    [Fact]
    public void GetDisplayFields_JsonNullLiteral_ReturnsUnreadableMarker()
    {
        var provider = new AdobeCredentialSummaryProvider();

        var fields = provider.GetDisplayFields("null");

        Assert.Single(fields);
        Assert.Equal("Status", fields[0].Key);
        Assert.Equal("<unreadable credential>", fields[0].Value);
    }
}
