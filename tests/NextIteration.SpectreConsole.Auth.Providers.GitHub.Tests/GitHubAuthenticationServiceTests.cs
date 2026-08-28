using System.Net;
using System.Text.Json;

using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.GitHub.Tests
{
    public sealed class GitHubAuthenticationServiceTests
    {
        private static readonly DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private static GitHubAuthenticationService Service(
            IHttpClientFactory http,
            FakeCredentialManager? credentials = null)
            => new(credentials ?? new FakeCredentialManager(), http, () => Now);

        private static GitHubCredential Credential(
            DateTimeOffset? expiresAt = null,
            string? refreshToken = null,
            string accessToken = "gho_stored",
            string webBaseUrl = "https://github.com/")
            => new()
            {
                ClientId = "Iv1.id",
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = expiresAt,
                Scopes = "repo",
                WebBaseUrl = new Uri(webBaseUrl, UriKind.Absolute),
                ApiBaseUrl = new Uri("https://api.github.com/"),
                Login = "octocat",
                Environment = "GitHubCom",
            };

        [Fact]
        public async Task AuthenticateAsync_PassesThrough_NonExpiringToken()
        {
            var svc = Service(StubHttpClientFactory.ReturningJson("{}"));

            var token = await svc.AuthenticateAsync(Credential());

            Assert.Equal("gho_stored", token.AccessToken);
            Assert.Equal(new Uri("https://api.github.com/"), token.BaseUrl);
            Assert.Null(token.ExpiresAt);
        }

        [Fact]
        public async Task AuthenticateAsync_PassesThrough_WhenExpiredButNoRefreshToken()
        {
            var svc = Service(StubHttpClientFactory.ReturningJson("{}"));

            var token = await svc.AuthenticateAsync(Credential(expiresAt: Now - TimeSpan.FromHours(1)));

            // No refresh token => return the stored (stale) token rather than fail.
            Assert.Equal("gho_stored", token.AccessToken);
        }

        [Fact]
        public async Task AuthenticateAsync_PassesThrough_WhenTokenStillValid()
        {
            var stub = StubHttpClientFactory.ReturningJson("{}");
            var svc = Service(stub);

            var token = await svc.AuthenticateAsync(
                Credential(expiresAt: Now + TimeSpan.FromHours(1), refreshToken: "ghr_x"));

            Assert.Equal("gho_stored", token.AccessToken);
            // Valid token => no refresh call made.
            Assert.Empty(stub.Requests);
        }

        [Fact]
        public async Task AuthenticateAsync_Refreshes_WhenExpiredWithRefreshToken()
        {
            var stub = StubHttpClientFactory.ReturningJson(
                """{ "access_token": "gho_fresh", "token_type": "bearer", "expires_in": 28800, "refresh_token": "ghr_new" }""");
            var svc = Service(stub);

            var token = await svc.AuthenticateAsync(
                Credential(expiresAt: Now - TimeSpan.FromMinutes(1), refreshToken: "ghr_old"));

            Assert.Equal("gho_fresh", token.AccessToken);
            Assert.Equal(Now + TimeSpan.FromSeconds(28800), token.ExpiresAt);

            Assert.Single(stub.Requests);
            Assert.Equal(new Uri("https://github.com/login/oauth/access_token"), stub.LastRequest!.RequestUri);
            Assert.Contains("grant_type=refresh_token", stub.RequestBodies[0], StringComparison.Ordinal);
            Assert.Contains("refresh_token=ghr_old", stub.RequestBodies[0], StringComparison.Ordinal);
        }

        [Fact]
        public async Task AuthenticateAsync_WhenRefreshReturnsNonSuccess_RedactsTheStoredRefreshToken()
        {
            // The only refresh-failure test stubbed a *200* carrying an error
            // field, which lands on the DTO check. The transport branch — the
            // one place SanitiseErrorBody scrubs the stored refresh token from
            // an echoed body — had no coverage at all, while the equivalent
            // access-token path in the collector did.
            var stub = StubHttpClientFactory.ReturningJson(
                """{ "error": "bad gateway", "echo": "grant_type=refresh_token&refresh_token=ghr_old" }""",
                HttpStatusCode.BadGateway);
            var svc = Service(stub);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.AuthenticateAsync(Credential(expiresAt: Now - TimeSpan.FromMinutes(1), refreshToken: "ghr_old")));

            Assert.DoesNotContain("ghr_old", ex.Message, StringComparison.Ordinal);
            Assert.Contains("<redacted>", ex.Message, StringComparison.Ordinal);
            // Non-credential diagnostics survive.
            Assert.Contains("BadGateway", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AuthenticateAsync_WhenRefreshReturns200WithNullBody_Throws()
        {
            var stub = StubHttpClientFactory.ReturningJson("null");
            var svc = Service(stub);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.AuthenticateAsync(Credential(expiresAt: Now - TimeSpan.FromMinutes(1), refreshToken: "ghr_old")));

            Assert.Contains("did not deserialize", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("https://ghe.example.com/", "https://ghe.example.com/login/oauth/access_token")]
        [InlineData("https://ghe.example.com", "https://ghe.example.com/login/oauth/access_token")]
        // DeriveWebBaseUrl always produces a trailing slash, so this is defence
        // for a credential written by an older version or edited by hand.
        [InlineData("https://gw.corp/ghe", "https://gw.corp/ghe/login/oauth/access_token")]
        public async Task AuthenticateAsync_Refresh_PreservesThePathOfTheWebBaseUrl(string webBaseUrl, string expected)
        {
            var stub = StubHttpClientFactory.ReturningJson(
                """{ "access_token": "gho_fresh", "token_type": "bearer", "expires_in": 28800 }""");
            var svc = Service(stub);
            await svc.AuthenticateAsync(Credential(
                expiresAt: Now - TimeSpan.FromMinutes(1),
                refreshToken: "ghr_old",
                webBaseUrl: webBaseUrl));

            Assert.Equal(new Uri(expected), stub.LastRequest!.RequestUri);
        }

        [Fact]
        public async Task AuthenticateAsync_Throws_WhenRefreshRejected()
        {
            var stub = StubHttpClientFactory.ReturningJson("""{ "error": "bad_refresh_token" }""");
            var svc = Service(stub);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.AuthenticateAsync(Credential(expiresAt: Now - TimeSpan.FromMinutes(1), refreshToken: "ghr_old")));
            Assert.Contains("refresh", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AuthenticateAsync_NoSelection_Throws()
        {
            var svc = Service(StubHttpClientFactory.ReturningJson("{}"), new FakeCredentialManager { SelectedCredentialJson = null });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AuthenticateAsync());
            Assert.Contains("No GitHub credential selected", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("{ not json")]
        [InlineData("<html><body>Sign in to continue</body></html>")]
        public async Task AuthenticateAsync_WhenSelectedJsonIsMalformed_ThrowsActionableException(string stored)
        {
            // GitHub was the only one of the four with no coverage for a stale or
            // corrupt stored credential. Like the siblings, it must surface
            // something the user can act on rather than a raw JsonException.
            var svc = Service(
                StubHttpClientFactory.ReturningJson("{}"),
                new FakeCredentialManager { SelectedCredentialJson = stored });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AuthenticateAsync());

            Assert.Contains("accounts add", ex.Message, StringComparison.Ordinal);
            Assert.IsType<JsonException>(ex.InnerException);
        }

        [Fact]
        public async Task AuthenticateAsync_ReadsSelectedCredential_FromManager()
        {
            var json = JsonSerializer.Serialize(Credential(), GitHubCredential.JsonOptions);
            var manager = new FakeCredentialManager { SelectedCredentialJson = json };
            var svc = Service(StubHttpClientFactory.ReturningJson("{}"), manager);

            var token = await svc.AuthenticateAsync();

            Assert.Equal("gho_stored", token.AccessToken);
            // The real ICredentialManager keys its store by providerName, so a
            // double that discards the argument satisfies the signature and not
            // the contract. Nothing asserted it until now.
            Assert.Equal(GitHubCredential.ProviderName, manager.RequestedProviderName);
        }

        [Fact]
        public async Task AuthenticateAsync_Throws_OnEmptyAccessToken()
        {
            var svc = Service(StubHttpClientFactory.ReturningJson("{}"));

            await Assert.ThrowsAsync<ArgumentException>(
                () => svc.AuthenticateAsync(Credential(accessToken: "   ")));
        }

        [Fact]
        public async Task AuthenticateAsync_Throws_OnInsecureApiUrl()
        {
            var credential = new GitHubCredential
            {
                ClientId = "id",
                AccessToken = "gho",
                Scopes = "repo",
                WebBaseUrl = new Uri("https://github.com/"),
                ApiBaseUrl = new Uri("http://api.example.com/"),
                Login = "octocat",
                Environment = "Enterprise",
            };
            var svc = Service(StubHttpClientFactory.ReturningJson("{}"));

            await Assert.ThrowsAsync<ArgumentException>(() => svc.AuthenticateAsync(credential));
        }

        [Fact]
        public async Task ValidateTokenAsync_ReflectsExpiry()
        {
            var svc = Service(StubHttpClientFactory.ReturningJson("{}"));

            var valid = new GitHubToken { AccessToken = "x", BaseUrl = new Uri("https://api.github.com/"), ExpiresAt = null };
            var expired = new GitHubToken { AccessToken = "x", BaseUrl = new Uri("https://api.github.com/"), ExpiresAt = DateTimeOffset.UtcNow - TimeSpan.FromHours(1) };

            Assert.True(await svc.ValidateTokenAsync(valid));
            Assert.False(await svc.ValidateTokenAsync(expired));
        }
    }
}
