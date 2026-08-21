using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.Adobe.Tests
{
    public sealed class AdobeCredentialCollectorTests
    {
        // The interactive CollectAsync flow is driven by Spectre's console
        // prompts — not reasonably unit-testable without a full Spectre
        // test-console harness. Cover what's cheaply coverable here and leave
        // the prompt flow to manual smoke via `accounts add`.

        [Fact]
        public void ProviderName_MatchesCredential()
        {
            var collector = new AdobeCredentialCollector();

            Assert.Equal(AdobeCredential.ProviderName, collector.ProviderName);
            Assert.Equal("Adobe", collector.ProviderName);
        }

        [Theory]
        [InlineData("https://ims-na1.adobelogin.com/")]
        [InlineData("https://partners.adobe.io/")]
        [InlineData("https://example.com:8443/")]
        public void ValidateSecureUrl_AcceptsHttps(string url)
        {
            var result = AdobeCredentialCollector.ValidateSecureUrl(url);
            Assert.True(result.Successful);
        }

        [Theory]
        [InlineData("http://127.0.0.1:8080/")]
        [InlineData("http://localhost:5000/")]
        [InlineData("http://[::1]:9000/")]
        public void ValidateSecureUrl_AcceptsHttpLoopback(string url)
        {
            // Loopback http allowed so devs can point the collector at a local
            // mock IMS without compromising the credential in real deployments.
            var result = AdobeCredentialCollector.ValidateSecureUrl(url);
            Assert.True(result.Successful);
        }

        [Theory]
        [InlineData("http://ims-na1.adobelogin.com/")]
        [InlineData("http://example.com/")]
        [InlineData("http://10.0.0.1/")]
        public void ValidateSecureUrl_RejectsHttpForNonLoopback(string url)
        {
            var result = AdobeCredentialCollector.ValidateSecureUrl(url);
            Assert.False(result.Successful);
            Assert.Contains("https", result.Message ?? string.Empty, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("ftp://example.com/")]
        [InlineData("not-a-url")]
        [InlineData("")]
        public void ValidateSecureUrl_RejectsNonHttpSchemesAndGarbage(string url)
        {
            var result = AdobeCredentialCollector.ValidateSecureUrl(url);
            Assert.False(result.Successful);
        }
    }
}
