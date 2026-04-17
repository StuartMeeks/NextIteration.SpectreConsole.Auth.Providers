using System.Text.Json;
using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.Airtable.Tests;

public sealed class AirtableCredentialTests
{
    [Fact]
    public void ProviderName_IsAirtable()
    {
        Assert.Equal("Airtable", AirtableCredential.ProviderName);
    }

    [Fact]
    public void SupportedEnvironments_ContainsAllEnumValues()
    {
        var envs = AirtableCredential.SupportedEnvironments;

        Assert.Contains("Production", envs);
        Assert.Contains("Staging", envs);
        Assert.Contains("Test", envs);
        Assert.Equal(3, envs.Count);
    }

    [Fact]
    public void Serialize_ProducesExpectedJsonShape()
    {
        var credential = NewCredential();

        var json = JsonSerializer.Serialize(credential, AirtableCredential.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("pat-abc-123", root.GetProperty("accessToken").GetString());
        Assert.Equal("Production", root.GetProperty("environment").GetString());
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = NewCredential();

        var json = JsonSerializer.Serialize(original, AirtableCredential.JsonOptions);
        var roundTripped = JsonSerializer.Deserialize<AirtableCredential>(json, AirtableCredential.JsonOptions);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.AccessToken, roundTripped!.AccessToken);
        Assert.Equal(original.Environment, roundTripped.Environment);
    }

    [Fact]
    public void Deserialize_WithMissingRequiredField_Throws()
    {
        // accessToken intentionally omitted — required members should cause
        // System.Text.Json to throw a JsonException at deserialization time.
        const string payload = """
            { "environment": "Production" }
            """;

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<AirtableCredential>(payload, AirtableCredential.JsonOptions));
    }

    private static AirtableCredential NewCredential() => new()
    {
        AccessToken = "pat-abc-123",
        Environment = "Production",
    };
}
