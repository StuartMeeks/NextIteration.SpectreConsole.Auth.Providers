using System.Text.Json;
using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.Adobe.Tests;

public sealed class AdobeCredentialTests
{
    [Fact]
    public void ProviderName_IsAdobe()
    {
        Assert.Equal("Adobe", AdobeCredential.ProviderName);
    }

    [Fact]
    public void SupportedEnvironments_ContainsAllEnumValues()
    {
        var envs = AdobeCredential.SupportedEnvironments;

        Assert.Contains("Production", envs);
        Assert.Contains("Sandbox", envs);
        Assert.Equal(2, envs.Count);
    }

    [Fact]
    public void Serialize_ProducesExpectedJsonShape()
    {
        var credential = NewCredential();

        var json = JsonSerializer.Serialize(credential, AdobeCredential.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("https://ims-na1.adobelogin.com/", root.GetProperty("imsUrl").GetString());
        Assert.Equal("abc-api-key", root.GetProperty("apiKey").GetString());
        Assert.Equal("super-secret", root.GetProperty("clientSecret").GetString());
        Assert.Equal("https://partners.adobe.io/", root.GetProperty("baseUrl").GetString());
        Assert.Equal("Production", root.GetProperty("environment").GetString());
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = NewCredential();

        var json = JsonSerializer.Serialize(original, AdobeCredential.JsonOptions);
        var roundTripped = JsonSerializer.Deserialize<AdobeCredential>(json, AdobeCredential.JsonOptions);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.ImsUrl, roundTripped!.ImsUrl);
        Assert.Equal(original.ApiKey, roundTripped.ApiKey);
        Assert.Equal(original.ClientSecret, roundTripped.ClientSecret);
        Assert.Equal(original.BaseUrl, roundTripped.BaseUrl);
        Assert.Equal(original.Environment, roundTripped.Environment);
    }

    [Fact]
    public void Deserialize_WithMissingRequiredField_Throws()
    {
        // imsUrl intentionally omitted — required members should cause
        // System.Text.Json to throw a JsonException at deserialization time.
        const string payload = """
            { "apiKey": "x", "clientSecret": "s", "baseUrl": "https://partners.adobe.io/", "environment": "Production" }
            """;

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<AdobeCredential>(payload, AdobeCredential.JsonOptions));
    }

    private static AdobeCredential NewCredential() => new()
    {
        ImsUrl = new Uri("https://ims-na1.adobelogin.com/"),
        ApiKey = "abc-api-key",
        ClientSecret = "super-secret",
        BaseUrl = new Uri("https://partners.adobe.io/"),
        Environment = "Production",
    };
}
