using System.Text.Json;
using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne.Tests;

public sealed class SoftwareOneCredentialTests
{
    [Fact]
    public void ProviderName_IsSoftwareOne()
    {
        Assert.Equal("SoftwareOne", SoftwareOneCredential.ProviderName);
    }

    [Fact]
    public void SupportedEnvironments_ContainsAllEnumValues()
    {
        var envs = SoftwareOneCredential.SupportedEnvironments;

        Assert.Contains("Production", envs);
        Assert.Contains("Staging", envs);
        Assert.Contains("Test", envs);
        Assert.Equal(3, envs.Count);
    }

    [Fact]
    public void SupportedActors_ContainsAllEnumValues()
    {
        var actors = SoftwareOneCredential.SupportedActors;

        Assert.Contains("Operations", actors);
        Assert.Contains("Vendor", actors);
        Assert.Equal(2, actors.Count);
    }

    [Fact]
    public void Serialize_ProducesExpectedJsonShape()
    {
        var credential = NewCredential();

        var json = JsonSerializer.Serialize(credential, SoftwareOneCredential.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("abc-123", root.GetProperty("apiToken").GetString());
        Assert.Equal("https://api.softwareone.com/", root.GetProperty("baseUrl").GetString());
        Assert.Equal("Production", root.GetProperty("environment").GetString());
        Assert.Equal("Operations", root.GetProperty("actor").GetString());
        Assert.Equal("TOK-001", root.GetProperty("tokenId").GetString());
        Assert.Equal("prod-deploy", root.GetProperty("tokenName").GetString());
        Assert.Equal("ACC-777", root.GetProperty("accountId").GetString());
        Assert.Equal("Contoso GmbH", root.GetProperty("accountName").GetString());
        Assert.Equal("Reseller", root.GetProperty("accountType").GetString());
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = NewCredential();

        var json = JsonSerializer.Serialize(original, SoftwareOneCredential.JsonOptions);
        var roundTripped = JsonSerializer.Deserialize<SoftwareOneCredential>(json, SoftwareOneCredential.JsonOptions);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.ApiToken, roundTripped!.ApiToken);
        Assert.Equal(original.BaseUrl, roundTripped.BaseUrl);
        Assert.Equal(original.Environment, roundTripped.Environment);
        Assert.Equal(original.Actor, roundTripped.Actor);
        Assert.Equal(original.TokenId, roundTripped.TokenId);
        Assert.Equal(original.TokenName, roundTripped.TokenName);
        Assert.Equal(original.AccountId, roundTripped.AccountId);
        Assert.Equal(original.AccountName, roundTripped.AccountName);
        Assert.Equal(original.AccountType, roundTripped.AccountType);
    }

    [Fact]
    public void Deserialize_WithMissingRequiredField_Throws()
    {
        // apiToken intentionally omitted — required members should cause
        // System.Text.Json to throw a JsonException at deserialization time.
        const string payload = """
            {
              "baseUrl": "https://api.softwareone.com/",
              "environment": "Production",
              "actor": "Operations",
              "tokenId": "TOK-001",
              "tokenName": "prod-deploy",
              "accountId": "ACC-777",
              "accountName": "Contoso GmbH",
              "accountType": "Reseller"
            }
            """;

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<SoftwareOneCredential>(payload, SoftwareOneCredential.JsonOptions));
    }

    [Fact]
    public void Deserialize_WithPreviousSchemaMissingMetadata_Throws()
    {
        // A 0.2.0-era credential (pre-enrichment) is no longer deserialisable
        // under 0.3.0 — the metadata fields are required. Migration requires
        // deleting the old credential and re-adding via `accounts add`.
        const string payload = """
            {
              "apiToken": "abc-123",
              "baseUrl": "https://api.softwareone.com/",
              "environment": "Production",
              "actor": "Operations"
            }
            """;

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<SoftwareOneCredential>(payload, SoftwareOneCredential.JsonOptions));
    }

    private static SoftwareOneCredential NewCredential() => new()
    {
        ApiToken = "abc-123",
        BaseUrl = new Uri("https://api.softwareone.com/"),
        Environment = "Production",
        Actor = "Operations",
        TokenId = "TOK-001",
        TokenName = "prod-deploy",
        AccountId = "ACC-777",
        AccountName = "Contoso GmbH",
        AccountType = "Reseller",
    };
}
