using System.Net;
using System.Text.Json;
using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.Adobe.Tests;

public sealed class AdobeAuthenticationServiceTests
{
    [Fact]
    public void Constructor_NullCredentialManager_Throws()
    {
        var http = StubHttpClientFactory.ReturningJson("{}");
        Assert.Throws<ArgumentNullException>(
            () => new AdobeAuthenticationService(null!, http));
    }

    [Fact]
    public void Constructor_NullHttpClientFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new AdobeAuthenticationService(new FakeCredentialManager(), null!));
    }

    [Fact]
    public async Task AuthenticateAsync_WithCredential_PostsToTokenEndpoint()
    {
        var http = StubHttpClientFactory.ReturningJson("""
            { "access_token": "at", "token_type": "bearer", "expires_in": 86400 }
            """);
        var service = new AdobeAuthenticationService(new FakeCredentialManager(), http);

        _ = await service.AuthenticateAsync(NewCredential());

        Assert.NotNull(http.LastRequest);
        Assert.Equal(HttpMethod.Post, http.LastRequest!.Method);
        Assert.Equal(
            new Uri("https://ims-na1.adobelogin.com/ims/token/v3"),
            http.LastRequest.RequestUri);
    }

    [Fact]
    public async Task AuthenticateAsync_WithCredential_SendsClientCredentialsForm()
    {
        var http = StubHttpClientFactory.ReturningJson("""
            { "access_token": "at", "token_type": "bearer", "expires_in": 86400 }
            """);
        var service = new AdobeAuthenticationService(new FakeCredentialManager(), http);

        _ = await service.AuthenticateAsync(NewCredential());

        Assert.NotNull(http.LastRequestBody);
        Assert.Contains("grant_type=client_credentials", http.LastRequestBody!, StringComparison.Ordinal);
        Assert.Contains("client_id=abc-api-key", http.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("client_secret=super-secret", http.LastRequestBody, StringComparison.Ordinal);
        // "openid,AdobeID,read_organizations" url-encoded — commas become %2C.
        Assert.Contains("scope=openid%2CAdobeID%2Cread_organizations", http.LastRequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticateAsync_WithCredential_ProjectsImsResponseIntoToken()
    {
        var http = StubHttpClientFactory.ReturningJson("""
            { "access_token": "at-123", "token_type": "bearer", "expires_in": 86400 }
            """);
        var service = new AdobeAuthenticationService(new FakeCredentialManager(), http);

        var token = await service.AuthenticateAsync(NewCredential());

        Assert.Equal("at-123", token.AccessToken);
        Assert.Equal("Bearer", token.TokenType); // Normalized from IMS's lowercase "bearer".
        Assert.Equal(86400, token.ExpiresIn);
        Assert.Equal(new Uri("https://partners.adobe.io/"), token.BaseUrl);
    }

    [Theory]
    [InlineData("", "good-secret", "Production")]
    [InlineData("   ", "good-secret", "Production")]
    [InlineData("good-key", "", "Production")]
    [InlineData("good-key", "   ", "Production")]
    [InlineData("good-key", "good-secret", "")]
    [InlineData("good-key", "good-secret", "   ")]
    public async Task AuthenticateAsync_WithWhitespaceRequiredField_Throws(string apiKey, string clientSecret, string environment)
    {
        var http = StubHttpClientFactory.ReturningJson("""
            { "access_token": "at", "token_type": "bearer", "expires_in": 86400 }
            """);
        var service = new AdobeAuthenticationService(new FakeCredentialManager(), http);
        var credential = new AdobeCredential
        {
            ImsUrl = new Uri("https://ims-na1.adobelogin.com/"),
            ApiKey = apiKey,
            ClientSecret = clientSecret,
            BaseUrl = new Uri("https://partners.adobe.io/"),
            Environment = environment,
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.AuthenticateAsync(credential));
        Assert.Null(http.LastRequest);
    }

    [Fact]
    public async Task AuthenticateAsync_NormalizesLowercaseTokenType()
    {
        // Adobe IMS returns "token_type":"bearer" (lowercase); the token we
        // build should expose "Bearer" (TitleCase) for interop with HTTP
        // servers that gate on exact scheme casing.
        var http = StubHttpClientFactory.ReturningJson("""
            { "access_token": "at", "token_type": "bearer", "expires_in": 86400 }
            """);
        var service = new AdobeAuthenticationService(new FakeCredentialManager(), http);

        var token = await service.AuthenticateAsync(NewCredential());

        Assert.Equal("Bearer", token.TokenType);
        Assert.Equal("Bearer at", token.GetAuthorizationHeader());
    }

    [Fact]
    public async Task AuthenticateAsync_NormalizesAllCapsTokenType()
    {
        var http = StubHttpClientFactory.ReturningJson("""
            { "access_token": "at", "token_type": "BEARER", "expires_in": 86400 }
            """);
        var service = new AdobeAuthenticationService(new FakeCredentialManager(), http);

        var token = await service.AuthenticateAsync(NewCredential());

        Assert.Equal("Bearer", token.TokenType);
    }

    [Fact]
    public async Task AuthenticateAsync_NullCredential_Throws()
    {
        var http = StubHttpClientFactory.ReturningJson("{}");
        var service = new AdobeAuthenticationService(new FakeCredentialManager(), http);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.AuthenticateAsync((AdobeCredential)null!));
    }

    [Fact]
    public async Task AuthenticateAsync_WhenImsReturnsError_IncludesResponseBodyInException()
    {
        var http = StubHttpClientFactory.ReturningJson(
            """{ "error": "invalid_client", "error_description": "client_id invalid" }""",
            HttpStatusCode.BadRequest);
        var service = new AdobeAuthenticationService(new FakeCredentialManager(), http);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.AuthenticateAsync(NewCredential()));

        Assert.Contains("invalid_client", ex.Message, StringComparison.Ordinal);
        Assert.Contains("BadRequest", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticateAsync_NoCredentialSelected_Throws()
    {
        var http = StubHttpClientFactory.ReturningJson("{}");
        var manager = new FakeCredentialManager { SelectedCredentialJson = null };
        var service = new AdobeAuthenticationService(manager, http);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AuthenticateAsync());
        Assert.Contains("No Adobe credential selected", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticateAsync_SelectedJsonMalformed_Throws()
    {
        var http = StubHttpClientFactory.ReturningJson("{}");
        var manager = new FakeCredentialManager { SelectedCredentialJson = "{ not json" };
        var service = new AdobeAuthenticationService(manager, http);

        await Assert.ThrowsAsync<JsonException>(() => service.AuthenticateAsync());
    }

    [Fact]
    public async Task AuthenticateAsync_FromSelectedCredential_ExchangesForToken()
    {
        var credential = NewCredential();
        var manager = new FakeCredentialManager
        {
            SelectedCredentialJson = JsonSerializer.Serialize(credential, AdobeCredential.JsonOptions),
        };
        var http = StubHttpClientFactory.ReturningJson("""
            { "access_token": "at-from-store", "token_type": "bearer", "expires_in": 3600 }
            """);
        var service = new AdobeAuthenticationService(manager, http);

        var token = await service.AuthenticateAsync();

        Assert.Equal("at-from-store", token.AccessToken);
        Assert.Equal(new Uri("https://partners.adobe.io/"), token.BaseUrl);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsTrue_ForFreshToken()
    {
        var http = StubHttpClientFactory.ReturningJson("{}");
        var service = new AdobeAuthenticationService(new FakeCredentialManager(), http);
        var token = new AdobeToken
        {
            AccessToken = "x",
            TokenType = "bearer",
            ExpiresIn = 3600,
            BaseUrl = new Uri("https://partners.adobe.io/"),
        };

        Assert.True(await service.ValidateTokenAsync(token));
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsFalse_ForExpiredToken()
    {
        var http = StubHttpClientFactory.ReturningJson("{}");
        var service = new AdobeAuthenticationService(new FakeCredentialManager(), http);
        var token = new AdobeToken
        {
            AccessToken = "x",
            TokenType = "bearer",
            ExpiresIn = 0,
            BaseUrl = new Uri("https://partners.adobe.io/"),
        };

        Assert.False(await service.ValidateTokenAsync(token));
    }

    [Fact]
    public async Task ValidateTokenAsync_NullToken_Throws()
    {
        var http = StubHttpClientFactory.ReturningJson("{}");
        var service = new AdobeAuthenticationService(new FakeCredentialManager(), http);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.ValidateTokenAsync(null!));
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
