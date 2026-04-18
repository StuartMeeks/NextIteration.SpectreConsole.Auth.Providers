using System.Net;
using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne.Tests;

/// <summary>
/// Tests for <see cref="SoftwareOneCredentialCollector.LookupTokenAsync"/>,
/// the Marketplace API validation call that's run at the end of
/// <c>CollectAsync</c>. The interactive prompt flow itself is not unit
/// tested (would need a Spectre test console harness); this exercise the
/// HTTP path directly using a stub <see cref="IHttpClientFactory"/>.
/// </summary>
public sealed class SoftwareOneCredentialCollectorLookupTests
{
    private static readonly Uri BaseUrl = new("https://api.softwareone.com/");

    [Fact]
    public async Task LookupTokenAsync_SingleMatch_ReturnsTokenDto()
    {
        var http = StubHttpClientFactory.ReturningJson("""
            {
              "data": [
                {
                  "id": "TOK-001",
                  "name": "prod-deploy",
                  "account": { "id": "ACC-777", "name": "Contoso GmbH", "type": "Reseller" }
                }
              ]
            }
            """);
        var collector = new SoftwareOneCredentialCollector(http);

        var token = await collector.LookupTokenAsync(BaseUrl, "abc-123");

        Assert.Equal("TOK-001", token.Id);
        Assert.Equal("prod-deploy", token.Name);
        Assert.Equal("ACC-777", token.Account.Id);
        Assert.Equal("Contoso GmbH", token.Account.Name);
        Assert.Equal("Reseller", token.Account.Type);
    }

    [Fact]
    public async Task LookupTokenAsync_SingleMatch_SendsBearerAuthAndCorrectPath()
    {
        var http = StubHttpClientFactory.ReturningJson("""
            { "data": [ { "id": "T", "name": "n", "account": { "id": "A", "name": "a", "type": "t" } } ] }
            """);
        var collector = new SoftwareOneCredentialCollector(http);

        _ = await collector.LookupTokenAsync(BaseUrl, "my-token-value");

        Assert.NotNull(http.LastRequest);
        Assert.Equal(HttpMethod.Get, http.LastRequest!.Method);
        Assert.Equal("Bearer", http.LastRequest.Headers.Authorization?.Scheme);
        Assert.Equal("my-token-value", http.LastRequest.Headers.Authorization?.Parameter);
        Assert.Contains("/v1/accounts/api-tokens?eq(token,'my-token-value')&limit=2",
            http.LastRequest.RequestUri?.ToString() ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task LookupTokenAsync_TokenValueWithSpecialChars_IsUrlEncoded()
    {
        var http = StubHttpClientFactory.ReturningJson("""
            { "data": [ { "id": "T", "name": "n", "account": { "id": "A", "name": "a", "type": "t" } } ] }
            """);
        var collector = new SoftwareOneCredentialCollector(http);

        _ = await collector.LookupTokenAsync(BaseUrl, "token with spaces & slash/");

        // URL-encoded: space -> %20, & -> %26, / -> %2F.
        // Use AbsoluteUri (canonical form) rather than ToString() which
        // returns a display-friendly form that decodes %20 back to space.
        var uri = http.LastRequest!.RequestUri!.AbsoluteUri;
        Assert.Contains("token%20with%20spaces%20%26%20slash%2F", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LookupTokenAsync_ZeroMatches_Throws()
    {
        var http = StubHttpClientFactory.ReturningJson("""{ "data": [] }""");
        var collector = new SoftwareOneCredentialCollector(http);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => collector.LookupTokenAsync(BaseUrl, "abc-123"));

        Assert.Contains("zero matches", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LookupTokenAsync_MultipleMatches_Throws()
    {
        var http = StubHttpClientFactory.ReturningJson("""
            {
              "data": [
                { "id": "T1", "name": "n1", "account": { "id": "A1", "name": "a1", "type": "t1" } },
                { "id": "T2", "name": "n2", "account": { "id": "A2", "name": "a2", "type": "t2" } }
              ]
            }
            """);
        var collector = new SoftwareOneCredentialCollector(http);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => collector.LookupTokenAsync(BaseUrl, "abc-123"));

        Assert.Contains("2 matches", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LookupTokenAsync_HttpError_Throws_WithBodyInMessage()
    {
        var http = StubHttpClientFactory.ReturningJson(
            """{ "error": "unauthorized" }""",
            HttpStatusCode.Unauthorized);
        var collector = new SoftwareOneCredentialCollector(http);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => collector.LookupTokenAsync(BaseUrl, "abc-123"));

        Assert.Contains("Unauthorized", ex.Message, StringComparison.Ordinal);
        Assert.Contains("unauthorized", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LookupTokenAsync_MalformedJson_Throws()
    {
        var http = StubHttpClientFactory.ReturningJson("{ not json");
        var collector = new SoftwareOneCredentialCollector(http);

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => collector.LookupTokenAsync(BaseUrl, "abc-123"));
    }

    [Fact]
    public void Constructor_NullHttpClientFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SoftwareOneCredentialCollector(null!));
    }
}
