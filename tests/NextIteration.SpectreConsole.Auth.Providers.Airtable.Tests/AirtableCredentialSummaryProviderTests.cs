using System.Text.Json;

using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.Airtable.Tests
{
    public sealed class AirtableCredentialSummaryProviderTests
    {
        [Fact]
        public void ProviderName_IsAirtable()
        {
            var provider = new AirtableCredentialSummaryProvider();

            Assert.Equal("Airtable", provider.ProviderName);
        }

        [Fact]
        public void GetDisplayFields_ValidJson_ReturnsMaskedToken()
        {
            var credential = new AirtableCredential
            {
                AccessToken = "abcdefghij-very-long-token-xyz9",
                Environment = "Production",
            };
            var json = JsonSerializer.Serialize(credential, AirtableCredential.JsonOptions);
            var provider = new AirtableCredentialSummaryProvider();

            var fields = provider.GetDisplayFields(json);

            var field = Assert.Single(fields);
            Assert.Equal("Token", field.Key);
            // Long token: first four + ellipsis + last four.
            Assert.Equal("abcd...xyz9", field.Value);
        }

        [Fact]
        public void GetDisplayFields_ShortToken_MasksWithFourStars()
        {
            var credential = new AirtableCredential
            {
                AccessToken = "abc",
                Environment = "Production",
            };
            var json = JsonSerializer.Serialize(credential, AirtableCredential.JsonOptions);
            var provider = new AirtableCredentialSummaryProvider();

            var fields = provider.GetDisplayFields(json);

            Assert.Equal("****", fields[0].Value);
        }

        [Theory]
        // The mask is `value.Length <= 10 ? "****" : first4 + "..." + last4`,
        // so 10 is the last masked length and the fingerprint starts at 11.
        // Existing coverage jumped from 3 to 31, leaving the boundary — and the
        // README's description of it — unpinned.
        [InlineData(9, "****")]
        [InlineData(10, "****")]
        [InlineData(11, "aaaa...aaaa")]
        [InlineData(12, "aaaa...aaaa")]
        public void GetDisplayFields_MasksAtTenCharactersOrShorter(int length, string expected)
        {
            var credential = new AirtableCredential
            {
                AccessToken = new string('a', length),
                Environment = "Production",
            };
            var json = JsonSerializer.Serialize(credential, AirtableCredential.JsonOptions);
            var provider = new AirtableCredentialSummaryProvider();

            var fields = provider.GetDisplayFields(json);

            Assert.Equal(expected, fields[0].Value);
        }

        [Fact]
        public void GetDisplayFields_EmptyToken_ReturnsEmptyString()
        {
            const string payload = """
            { "accessToken": "", "environment": "Production" }
            """;
            var provider = new AirtableCredentialSummaryProvider();

            var fields = provider.GetDisplayFields(payload);

            Assert.Equal(string.Empty, fields[0].Value);
        }

        [Fact]
        public void GetDisplayFields_MalformedJson_ReturnsUnreadableMarker()
        {
            var provider = new AirtableCredentialSummaryProvider();

            var fields = provider.GetDisplayFields("{ not json");

            var field = Assert.Single(fields);
            Assert.Equal("Status", field.Key);
            Assert.Equal("<unreadable credential>", field.Value);
        }

        [Fact]
        public void GetDisplayFields_JsonNullLiteral_ReturnsUnreadableMarker()
        {
            var provider = new AirtableCredentialSummaryProvider();

            var fields = provider.GetDisplayFields("null");

            var field = Assert.Single(fields);
            Assert.Equal("Status", field.Key);
            Assert.Equal("<unreadable credential>", field.Value);
        }
    }
}
