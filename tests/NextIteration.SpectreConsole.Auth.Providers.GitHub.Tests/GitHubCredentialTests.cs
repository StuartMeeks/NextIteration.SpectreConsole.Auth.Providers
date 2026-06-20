using System.Text.Json;
using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.GitHub.Tests;

public sealed class GitHubCredentialTests
{
    [Fact]
    public void ProviderName_IsGitHub()
    {
        Assert.Equal("GitHub", GitHubCredential.ProviderName);
    }

    [Fact]
    public void SupportedEnvironments_AreEnumNames()
    {
        Assert.Equal(["GitHubCom", "Enterprise"], GitHubCredential.SupportedEnvironments);
    }

    [Fact]
    public void Roundtrip_PreservesAllFields_IncludingNullableExpiringTokenFields()
    {
        var original = new GitHubCredential
        {
            ClientId = "Iv1.abc123",
            AccessToken = "gho_token_value",
            RefreshToken = "ghr_refresh_value",
            AccessTokenExpiresAt = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero),
            Scopes = "repo read:org",
            WebBaseUrl = new Uri("https://github.com/"),
            ApiBaseUrl = new Uri("https://api.github.com/"),
            Login = "octocat",
            Name = "The Octocat",
            Environment = "GitHubCom",
        };

        var json = JsonSerializer.Serialize(original, GitHubCredential.JsonOptions);
        var roundtripped = JsonSerializer.Deserialize<GitHubCredential>(json, GitHubCredential.JsonOptions);

        Assert.NotNull(roundtripped);
        Assert.Equal(original.ClientId, roundtripped!.ClientId);
        Assert.Equal(original.AccessToken, roundtripped.AccessToken);
        Assert.Equal(original.RefreshToken, roundtripped.RefreshToken);
        Assert.Equal(original.AccessTokenExpiresAt, roundtripped.AccessTokenExpiresAt);
        Assert.Equal(original.Scopes, roundtripped.Scopes);
        Assert.Equal(original.WebBaseUrl, roundtripped.WebBaseUrl);
        Assert.Equal(original.ApiBaseUrl, roundtripped.ApiBaseUrl);
        Assert.Equal(original.Login, roundtripped.Login);
        Assert.Equal(original.Name, roundtripped.Name);
        Assert.Equal(original.Environment, roundtripped.Environment);
    }

    [Fact]
    public void Roundtrip_NonExpiringToken_LeavesRefreshAndExpiryNull()
    {
        var original = new GitHubCredential
        {
            ClientId = "Iv1.abc123",
            AccessToken = "gho_token_value",
            RefreshToken = null,
            AccessTokenExpiresAt = null,
            Scopes = "repo",
            WebBaseUrl = new Uri("https://github.com/"),
            ApiBaseUrl = new Uri("https://api.github.com/"),
            Login = "octocat",
            Name = null,
            Environment = "GitHubCom",
        };

        var json = JsonSerializer.Serialize(original, GitHubCredential.JsonOptions);
        var roundtripped = JsonSerializer.Deserialize<GitHubCredential>(json, GitHubCredential.JsonOptions);

        Assert.NotNull(roundtripped);
        Assert.Null(roundtripped!.RefreshToken);
        Assert.Null(roundtripped.AccessTokenExpiresAt);
        Assert.Null(roundtripped.Name);
    }

    [Fact]
    public void JsonOptions_UseCamelCase()
    {
        var credential = new GitHubCredential
        {
            ClientId = "id",
            AccessToken = "token",
            Scopes = "repo",
            WebBaseUrl = new Uri("https://github.com/"),
            ApiBaseUrl = new Uri("https://api.github.com/"),
            Login = "octocat",
            Environment = "GitHubCom",
        };

        var json = JsonSerializer.Serialize(credential, GitHubCredential.JsonOptions);

        Assert.Contains("\"accessToken\"", json, StringComparison.Ordinal);
        Assert.Contains("\"apiBaseUrl\"", json, StringComparison.Ordinal);
    }
}
