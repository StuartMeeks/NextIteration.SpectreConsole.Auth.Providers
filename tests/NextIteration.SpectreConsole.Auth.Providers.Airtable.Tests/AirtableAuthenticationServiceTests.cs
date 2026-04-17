using System.Text.Json;
using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.Airtable.Tests;

public sealed class AirtableAuthenticationServiceTests
{
    [Fact]
    public void Constructor_NullCredentialManager_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new AirtableAuthenticationService(null!));
    }

    [Fact]
    public void ApiBaseUrl_IsPublicAirtableEndpoint()
    {
        Assert.Equal(new Uri("https://api.airtable.com/"), AirtableAuthenticationService.ApiBaseUrl);
    }

    [Fact]
    public async Task AuthenticateAsync_WithCredential_ProjectsAccessTokenAndBaseUrlIntoToken()
    {
        var credential = new AirtableCredential
        {
            AccessToken = "pat-abc-123",
            Environment = "Production",
        };
        var service = new AirtableAuthenticationService(new FakeCredentialManager());

        var token = await service.AuthenticateAsync(credential);

        Assert.Equal(credential.AccessToken, token.AccessToken);
        Assert.Equal(AirtableAuthenticationService.ApiBaseUrl, token.BaseUrl);
    }

    [Fact]
    public async Task AuthenticateAsync_NullCredential_Throws()
    {
        var service = new AirtableAuthenticationService(new FakeCredentialManager());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.AuthenticateAsync((AirtableCredential)null!));
    }

    [Theory]
    [InlineData("", "Production")]
    [InlineData("   ", "Production")]
    [InlineData("pat-xyz", "")]
    [InlineData("pat-xyz", "   ")]
    public async Task AuthenticateAsync_WithWhitespaceRequiredField_Throws(string accessToken, string environment)
    {
        var service = new AirtableAuthenticationService(new FakeCredentialManager());
        var credential = new AirtableCredential
        {
            AccessToken = accessToken,
            Environment = environment,
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.AuthenticateAsync(credential));
    }

    [Fact]
    public async Task AuthenticateAsync_WhenNoCredentialSelected_Throws()
    {
        var manager = new FakeCredentialManager { SelectedCredentialJson = null };
        var service = new AirtableAuthenticationService(manager);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AuthenticateAsync());
        Assert.Contains("No Airtable credential selected", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenSelectedJsonIsEmpty_Throws()
    {
        var manager = new FakeCredentialManager { SelectedCredentialJson = string.Empty };
        var service = new AirtableAuthenticationService(manager);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AuthenticateAsync());
    }

    [Fact]
    public async Task AuthenticateAsync_WhenSelectedJsonIsMalformed_Throws()
    {
        var manager = new FakeCredentialManager { SelectedCredentialJson = "{ not json" };
        var service = new AirtableAuthenticationService(manager);

        await Assert.ThrowsAsync<JsonException>(() => service.AuthenticateAsync());
    }

    [Fact]
    public async Task AuthenticateAsync_WithStoredCredential_ReturnsProjectedToken()
    {
        var credential = new AirtableCredential
        {
            AccessToken = "pat-abc-123",
            Environment = "Staging",
        };
        var manager = new FakeCredentialManager
        {
            SelectedCredentialJson = JsonSerializer.Serialize(credential, AirtableCredential.JsonOptions),
        };
        var service = new AirtableAuthenticationService(manager);

        var token = await service.AuthenticateAsync();

        Assert.Equal("pat-abc-123", token.AccessToken);
        Assert.Equal(AirtableAuthenticationService.ApiBaseUrl, token.BaseUrl);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsTrue_ForNonExpiredToken()
    {
        var token = new AirtableToken
        {
            AccessToken = "x",
            BaseUrl = new Uri("https://api.airtable.com/"),
        };
        var service = new AirtableAuthenticationService(new FakeCredentialManager());

        Assert.True(await service.ValidateTokenAsync(token));
    }

    [Fact]
    public async Task ValidateTokenAsync_NullToken_Throws()
    {
        var service = new AirtableAuthenticationService(new FakeCredentialManager());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.ValidateTokenAsync(null!));
    }
}
