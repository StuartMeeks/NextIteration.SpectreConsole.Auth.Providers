using System.Text.Json;

using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.GitHub.Tests
{
    public sealed class GitHubCredentialSummaryProviderTests
    {
        private static string Serialize(GitHubCredential credential)
            => JsonSerializer.Serialize(credential, GitHubCredential.JsonOptions);

        private static GitHubCredential Credential(string? name = "The Octocat", string accessToken = "gho_1234567890abcdef")
            => new()
            {
                ClientId = "id",
                AccessToken = accessToken,
                Scopes = "repo read:org",
                WebBaseUrl = new Uri("https://github.com/"),
                ApiBaseUrl = new Uri("https://api.github.com/"),
                Login = "octocat",
                Name = name,
                Environment = "GitHubCom",
            };

        [Fact]
        public void ProviderName_IsGitHub() => Assert.Equal("GitHub", new GitHubCredentialSummaryProvider().ProviderName);

        [Fact]
        public void GetDisplayFields_ShowsAccountScopesHostAndMaskedToken()
        {
            var fields = new GitHubCredentialSummaryProvider().GetDisplayFields(Serialize(Credential()));
            var map = fields.ToDictionary(kv => kv.Key, kv => kv.Value);

            Assert.Equal("octocat (The Octocat)", map["Account"]);
            Assert.Equal("repo read:org", map["Scopes"]);
            Assert.Equal("https://api.github.com/", map["Host"]);
            Assert.Equal("gho_...cdef", map["Token"]);
        }

        [Fact]
        public void GetDisplayFields_OmitsNameWhenAbsent()
        {
            var fields = new GitHubCredentialSummaryProvider().GetDisplayFields(Serialize(Credential(name: null)));
            var account = fields.Single(kv => kv.Key == "Account").Value;

            Assert.Equal("octocat", account);
        }

        [Fact]
        public void GetDisplayFields_MasksShortTokenWithoutLeakingLength()
        {
            var fields = new GitHubCredentialSummaryProvider().GetDisplayFields(Serialize(Credential(accessToken: "short")));
            var token = fields.Single(kv => kv.Key == "Token").Value;

            Assert.Equal("****", token);
        }

        [Fact]
        public void GetDisplayFields_ReturnsMarker_OnUnreadableJson()
        {
            var fields = new GitHubCredentialSummaryProvider().GetDisplayFields("{ not json");

            var single = Assert.Single(fields);
            Assert.Equal("Status", single.Key);
            Assert.Equal("<unreadable credential>", single.Value);
        }
        [Fact]
        public void GetDisplayFields_JsonNullLiteral_ReturnsUnreadableMarker()
        {
            // The three sibling providers each have this fact; GitHub only had
            // the malformed-JSON one, which reaches the catch(JsonException)
            // arm. A literal "null" deserializes successfully *to null*, so it
            // exercises the separate `credential is null` guard — without which
            // one bad row throws a NullReferenceException inside the Spectre
            // render loop and takes down the whole `accounts list`.
            var provider = new GitHubCredentialSummaryProvider();

            var fields = provider.GetDisplayFields("null");

            var field = Assert.Single(fields);
            Assert.Equal("Status", field.Key);
            Assert.Equal("<unreadable credential>", field.Value);
        }

    }
}
