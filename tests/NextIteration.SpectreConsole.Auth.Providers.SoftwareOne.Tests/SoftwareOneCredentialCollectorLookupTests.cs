using System.Net;

using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne.Tests
{
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
        public async Task LookupTokenAsync_When200DataArrayIsNull_ThrowsInsteadOfNullReference()
        {
            // `required` only pins presence, so {"data":null} deserializes
            // cleanly; result.Data.Count would then be a NullReferenceException
            // rather than something the user can act on.
            var http = StubHttpClientFactory.ReturningJson("""{ "data": null }""");
            var collector = new SoftwareOneCredentialCollector(http);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => collector.LookupTokenAsync(BaseUrl, "abc-123"));

            Assert.Contains("data array was null", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task LookupTokenAsync_When200MatchHasNullAccount_ThrowsInsteadOfNullReference()
        {
            var http = StubHttpClientFactory.ReturningJson(
                """{ "data": [ { "id": "TKN-1", "name": "n", "account": null } ] }""");
            var collector = new SoftwareOneCredentialCollector(http);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => collector.LookupTokenAsync(BaseUrl, "abc-123"));

            Assert.Contains("incomplete", ex.Message, StringComparison.Ordinal);
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
        public async Task LookupTokenAsync_HttpError_RedactsTokenFromBody()
        {
            // Simulate a misbehaving upstream proxy that echoes the request URL
            // (which carries the token in the eq(token,'…') query) back into the
            // error body. The collector must redact the token before it lands in
            // the exception message — otherwise the credential leaks via logs.
            const string tokenValue = "tok-secret-12345";
            var http = StubHttpClientFactory.ReturningJson(
                $$"""{ "error": "bad gateway", "request_url": "/v1/accounts/api-tokens?eq(token,'{{tokenValue}}')&limit=2" }""",
                HttpStatusCode.BadGateway);
            var collector = new SoftwareOneCredentialCollector(http);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => collector.LookupTokenAsync(BaseUrl, tokenValue));

            Assert.DoesNotContain(tokenValue, ex.Message, StringComparison.Ordinal);
            Assert.Contains("<redacted>", ex.Message, StringComparison.Ordinal);
            // The non-credential context ("bad gateway") should still be visible
            // — diagnostics aren't sacrificed for sanitisation.
            Assert.Contains("bad gateway", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        // A token needing no escaping — the case the original test used, and the
        // only one the old literal-only redaction actually covered.
        [InlineData("tok-secret-12345")]
        // Real Marketplace tokens carry an "idt:" prefix and base64 payloads, so
        // ':' '+' '/' '=' are the norm, not the edge case. Each of these escapes
        // to something that does not contain the raw value, so redacting only
        // the literal left the token in the message verbatim.
        [InlineData("idt:AbC+dEf/123=")]
        [InlineData("abc+def/ghi=")]
        [InlineData("idt:AbC-123_x")]
        [InlineData("tok with spaces")]
        public async Task LookupTokenAsync_HttpError_RedactsPercentEncodedTokenFromBody(string tokenValue)
        {
            // The request URL carries Uri.EscapeDataString(apiToken), so that is
            // the form a proxy echoing the URL puts in the body — the exact
            // threat the sanitiser's own comment names.
            var encoded = Uri.EscapeDataString(tokenValue);
            var http = StubHttpClientFactory.ReturningJson(
                $$"""{ "error": "bad gateway", "request_url": "/v1/accounts/api-tokens?eq(token,'{{encoded}}')&limit=2" }""",
                HttpStatusCode.BadGateway);
            var collector = new SoftwareOneCredentialCollector(http);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => collector.LookupTokenAsync(BaseUrl, tokenValue));

            Assert.DoesNotContain(encoded, ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(tokenValue, ex.Message, StringComparison.Ordinal);
            Assert.Contains("<redacted>", ex.Message, StringComparison.Ordinal);
            Assert.Contains("bad gateway", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void SanitiseErrorBody_RedactsBothRawAndEncodedForms()
        {
            const string token = "idt:AbC+dEf/123=";
            var encoded = Uri.EscapeDataString(token);
            Assert.NotEqual(token, encoded);

            var sanitised = SoftwareOneCredentialCollector.SanitiseErrorBody(
                $"raw={token} encoded={encoded}", token);

            Assert.DoesNotContain(token, sanitised, StringComparison.Ordinal);
            Assert.DoesNotContain(encoded, sanitised, StringComparison.Ordinal);
            Assert.Equal("raw=<redacted> encoded=<redacted>", sanitised);
        }

        [Fact]
        public async Task LookupTokenAsync_HttpError_TruncatesLargeBody()
        {
            var bigBody = new string('y', 4000);
            var http = StubHttpClientFactory.ReturningJson(bigBody, HttpStatusCode.InternalServerError);
            var collector = new SoftwareOneCredentialCollector(http);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => collector.LookupTokenAsync(BaseUrl, "abc-123"));

            Assert.Contains("[truncated]", ex.Message, StringComparison.Ordinal);
            Assert.True(ex.Message.Length < 1024, $"Expected truncated message, was {ex.Message.Length} chars");
        }

        [Fact]
        public void SanitiseErrorBody_RedactsTokenWithinKeptWindowAndTruncates()
        {
            // Token sits well inside the first 512 chars so it's both redacted
            // and visible after truncation. Covers the common case: a small
            // error envelope wrapped in a much larger HTML page from a proxy.
            var body = new string('a', 200) + "tok-xyz" + new string('b', 1000);
            var sanitised = SoftwareOneCredentialCollector.SanitiseErrorBody(body, "tok-xyz");

            Assert.DoesNotContain("tok-xyz", sanitised, StringComparison.Ordinal);
            Assert.Contains("<redacted>", sanitised, StringComparison.Ordinal);
            Assert.EndsWith("[truncated]", sanitised, StringComparison.Ordinal);
        }

        [Fact]
        public void SanitiseErrorBody_TokenPastTruncationBoundary_StillRemovedFromOutput()
        {
            // Pathological case: token sits past the 512-char cap. Redaction
            // happens before truncation, but the truncated tail is dropped
            // entirely — so even though the literal "<redacted>" marker isn't
            // visible in the kept window, the token itself MUST NOT be either.
            var body = new string('a', 600) + "tok-xyz" + new string('b', 600);
            var sanitised = SoftwareOneCredentialCollector.SanitiseErrorBody(body, "tok-xyz");

            Assert.DoesNotContain("tok-xyz", sanitised, StringComparison.Ordinal);
            Assert.EndsWith("[truncated]", sanitised, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("https://api.softwareone.com/")]
        [InlineData("https://test.softwareone.com:8443/")]
        public void ValidateSecureBaseUrl_AcceptsHttps(string url)
        {
            var result = SoftwareOneCredentialCollector.ValidateSecureBaseUrl(url);
            Assert.True(result.Successful);
        }

        [Theory]
        [InlineData("http://127.0.0.1:8080/")]
        [InlineData("http://localhost:5000/")]
        public void ValidateSecureBaseUrl_AcceptsHttpLoopback(string url)
        {
            var result = SoftwareOneCredentialCollector.ValidateSecureBaseUrl(url);
            Assert.True(result.Successful);
        }

        [Theory]
        [InlineData("http://api.softwareone.com/")]
        [InlineData("http://example.com/")]
        public void ValidateSecureBaseUrl_RejectsCleartextHttpForNonLoopback(string url)
        {
            // The token rides in the URL query — cleartext http would expose it
            // on every hop and in any access log.
            var result = SoftwareOneCredentialCollector.ValidateSecureBaseUrl(url);
            Assert.False(result.Successful);
            Assert.Contains("https", result.Message ?? string.Empty, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("ftp://example.com/")]
        [InlineData("not-a-url")]
        public void ValidateSecureBaseUrl_RejectsNonHttpSchemesAndGarbage(string url)
        {
            var result = SoftwareOneCredentialCollector.ValidateSecureBaseUrl(url);
            Assert.False(result.Successful);
        }

        [Theory]
        [InlineData("{ not json")]
        // A corporate captive portal answering the lookup with an HTML
        // interstitial is the realistic version of this.
        [InlineData("<html><body>Sign in to continue</body></html>")]
        [InlineData("")]
        public async Task LookupTokenAsync_MalformedJson_ThrowsInvalidOperation(string body)
        {
            // LookupTokenAsync's XML doc promises InvalidOperationException "on
            // any failure mode (HTTP error, zero matches, multiple matches,
            // malformed response)". This test previously asserted JsonException,
            // codifying the divergence rather than catching it.
            var http = StubHttpClientFactory.ReturningJson(body);
            var collector = new SoftwareOneCredentialCollector(http);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => collector.LookupTokenAsync(BaseUrl, "abc-123"));

            Assert.IsType<System.Text.Json.JsonException>(ex.InnerException);
        }

        [Fact]
        public void Constructor_NullHttpClientFactory_Throws() => Assert.Throws<ArgumentNullException>(() => new SoftwareOneCredentialCollector(null!));
    }
}
