using System.Text.Json;
using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne.Tests;

public sealed class SoftwareOneAuthenticationServiceTests
{
    [Fact]
    public void Constructor_NullCredentialManager_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SoftwareOneAuthenticationService(null!));
    }

    [Fact]
    public async Task AuthenticateAsync_WithCredential_ProjectsAllFieldsIntoToken()
    {
        var credential = new SoftwareOneCredential
        {
            ApiToken = "abc-123",
            BaseUrl = new Uri("https://api.softwareone.com/"),
            Environment = "Production",
            Actor = "Operations",
        };
        var service = new SoftwareOneAuthenticationService(new FakeCredentialManager());

        var token = await service.AuthenticateAsync(credential);

        Assert.Equal(credential.ApiToken, token.ApiToken);
        Assert.Equal(credential.BaseUrl, token.BaseUrl);
        Assert.Equal(credential.Environment, token.Environment);
        Assert.Equal(credential.Actor, token.Actor);
    }

    [Fact]
    public async Task AuthenticateAsync_NullCredential_Throws()
    {
        var service = new SoftwareOneAuthenticationService(new FakeCredentialManager());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.AuthenticateAsync((SoftwareOneCredential)null!));
    }

    [Fact]
    public async Task AuthenticateAsync_WhenNoCredentialSelected_Throws()
    {
        var manager = new FakeCredentialManager { SelectedCredentialJson = null };
        var service = new SoftwareOneAuthenticationService(manager);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AuthenticateAsync());
        Assert.Contains("No SoftwareOne credential selected", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenSelectedJsonIsEmpty_Throws()
    {
        var manager = new FakeCredentialManager { SelectedCredentialJson = string.Empty };
        var service = new SoftwareOneAuthenticationService(manager);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AuthenticateAsync());
    }

    [Fact]
    public async Task AuthenticateAsync_WhenSelectedJsonIsMalformed_Throws()
    {
        var manager = new FakeCredentialManager { SelectedCredentialJson = "{ not json" };
        var service = new SoftwareOneAuthenticationService(manager);

        await Assert.ThrowsAsync<JsonException>(() => service.AuthenticateAsync());
    }

    [Fact]
    public async Task AuthenticateAsync_WithStoredCredential_ReturnsProjectedToken()
    {
        var credential = new SoftwareOneCredential
        {
            ApiToken = "abc-123",
            BaseUrl = new Uri("https://staging.softwareone.com/"),
            Environment = "Staging",
            Actor = "Vendor",
        };
        var manager = new FakeCredentialManager
        {
            SelectedCredentialJson = JsonSerializer.Serialize(credential, SoftwareOneCredential.JsonOptions),
        };
        var service = new SoftwareOneAuthenticationService(manager);

        var token = await service.AuthenticateAsync();

        Assert.Equal("abc-123", token.ApiToken);
        Assert.Equal(new Uri("https://staging.softwareone.com/"), token.BaseUrl);
        Assert.Equal("Staging", token.Environment);
        Assert.Equal("Vendor", token.Actor);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsTrue_ForNonExpiredToken()
    {
        var token = new SoftwareOneToken
        {
            ApiToken = "x",
            Actor = "Operations",
            Environment = "Production",
            BaseUrl = new Uri("https://api.softwareone.com/"),
        };
        var service = new SoftwareOneAuthenticationService(new FakeCredentialManager());

        Assert.True(await service.ValidateTokenAsync(token));
    }

    [Fact]
    public async Task ValidateTokenAsync_NullToken_Throws()
    {
        var service = new SoftwareOneAuthenticationService(new FakeCredentialManager());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.ValidateTokenAsync(null!));
    }
}
