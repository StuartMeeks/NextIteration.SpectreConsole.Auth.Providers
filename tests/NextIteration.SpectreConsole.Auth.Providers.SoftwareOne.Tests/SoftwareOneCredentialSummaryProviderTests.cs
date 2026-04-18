using System.Text.Json;
using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne.Tests;

public sealed class SoftwareOneCredentialSummaryProviderTests
{
    [Fact]
    public void ProviderName_IsSoftwareOne()
    {
        var provider = new SoftwareOneCredentialSummaryProvider();

        Assert.Equal("SoftwareOne", provider.ProviderName);
    }

    [Fact]
    public void GetDisplayFields_ValidJson_ReturnsAccountActorBaseUrlAndMaskedToken()
    {
        var credential = new SoftwareOneCredential
        {
            ApiToken = "abcdefghij-very-long-token-xyz9",
            BaseUrl = new Uri("https://api.softwareone.com/"),
            Environment = "Production",
            Actor = "Operations",
            TokenId = "TOK-001",
            TokenName = "prod-deploy",
            AccountId = "ACC-777",
            AccountName = "Contoso GmbH",
            AccountType = "Reseller",
        };
        var json = JsonSerializer.Serialize(credential, SoftwareOneCredential.JsonOptions);
        var provider = new SoftwareOneCredentialSummaryProvider();

        var fields = provider.GetDisplayFields(json);

        Assert.Equal(4, fields.Count);
        Assert.Equal(new KeyValuePair<string, string>("Account", "Contoso GmbH (Reseller)"), fields[0]);
        Assert.Equal(new KeyValuePair<string, string>("Actor", "Operations"), fields[1]);
        Assert.Equal(new KeyValuePair<string, string>("Base URL", "https://api.softwareone.com/"), fields[2]);
        Assert.Equal("Token", fields[3].Key);
        // Long token: first four + ellipsis + last four + token name suffix.
        Assert.Equal("abcd...xyz9 — prod-deploy", fields[3].Value);
    }

    [Fact]
    public void GetDisplayFields_ShortToken_MasksWithFourStars()
    {
        // Any token <= 10 chars gets a fixed four-star mask so length isn't leaked.
        var credential = new SoftwareOneCredential
        {
            ApiToken = "abc",
            BaseUrl = new Uri("https://api.softwareone.com/"),
            Environment = "Production",
            Actor = "Operations",
            TokenId = "TOK-001",
            TokenName = "prod-deploy",
            AccountId = "ACC-777",
            AccountName = "Contoso GmbH",
            AccountType = "Reseller",
        };
        var json = JsonSerializer.Serialize(credential, SoftwareOneCredential.JsonOptions);
        var provider = new SoftwareOneCredentialSummaryProvider();

        var fields = provider.GetDisplayFields(json);

        var tokenField = fields.Single(f => f.Key == "Token");
        Assert.Equal("**** — prod-deploy", tokenField.Value);
    }

    [Fact]
    public void GetDisplayFields_MalformedJson_ReturnsUnreadableMarker()
    {
        var provider = new SoftwareOneCredentialSummaryProvider();

        var fields = provider.GetDisplayFields("{ not json");

        Assert.Single(fields);
        Assert.Equal("Status", fields[0].Key);
        Assert.Equal("<unreadable credential>", fields[0].Value);
    }

    [Fact]
    public void GetDisplayFields_JsonNullLiteral_ReturnsUnreadableMarker()
    {
        // JsonSerializer.Deserialize<T>("null") returns null for reference
        // types — the provider's null-guard catches it.
        var provider = new SoftwareOneCredentialSummaryProvider();

        var fields = provider.GetDisplayFields("null");

        Assert.Single(fields);
        Assert.Equal("Status", fields[0].Key);
        Assert.Equal("<unreadable credential>", fields[0].Value);
    }

    [Fact]
    public void GetDisplayFields_Pre030CredentialWithoutMetadata_ReturnsUnreadableMarker()
    {
        // A 0.2.0-era credential lacks the required metadata fields;
        // deserialization throws JsonException, which the provider catches
        // and surfaces as the unreadable marker.
        var pre030 = """
            {
              "apiToken": "abc-123",
              "baseUrl": "https://api.softwareone.com/",
              "environment": "Production",
              "actor": "Operations"
            }
            """;
        var provider = new SoftwareOneCredentialSummaryProvider();

        var fields = provider.GetDisplayFields(pre030);

        Assert.Single(fields);
        Assert.Equal("Status", fields[0].Key);
        Assert.Equal("<unreadable credential>", fields[0].Value);
    }
}
