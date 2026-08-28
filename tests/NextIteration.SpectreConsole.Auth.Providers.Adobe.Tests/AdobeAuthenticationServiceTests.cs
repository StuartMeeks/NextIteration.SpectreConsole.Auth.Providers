using System.Net;
using System.Text.Json;

using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.Adobe.Tests
{
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

            await service.AuthenticateAsync(NewCredential());

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

            await service.AuthenticateAsync(NewCredential());

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
                () => service.AuthenticateAsync(null!));
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

        [Theory]
        // A secret needing no escaping — all three redacted forms coincide.
        [InlineData("super-secret")]
        // Adobe IMS client secrets are base64-ish and commonly carry these, so
        // the encoded spellings are the norm rather than the edge case.
        [InlineData("p8e-AbC+dEf/123=")]
        [InlineData("s3cr3t/with+slashes=")]
        [InlineData("secret with spaces")]
        public async Task AuthenticateAsync_WhenImsEchoesThePostedForm_RedactsTheClientSecret(string secret)
        {
            // Adobe is the only one of the four providers that posts a true
            // client secret. A TLS-terminating proxy returning a debug page that
            // echoes the posted form hands it straight back, and truncation
            // alone kept it — client_secret= sits well inside the first 512
            // characters.
            var percentEncoded = Uri.EscapeDataString(secret);
            var formEncoded = percentEncoded.Replace("%20", "+", StringComparison.Ordinal);
            var body = $"echo: client_id=abc-api-key&client_secret={formEncoded} (raw={secret}, pct={percentEncoded})";

            var http = StubHttpClientFactory.ReturningJson(body, HttpStatusCode.BadRequest);
            var service = new AdobeAuthenticationService(new FakeCredentialManager(), http);

            var ex = await Assert.ThrowsAsync<HttpRequestException>(
                () => service.AuthenticateAsync(NewCredential(clientSecret: secret)));

            Assert.DoesNotContain(secret, ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(percentEncoded, ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(formEncoded, ex.Message, StringComparison.Ordinal);
            Assert.Contains("<redacted>", ex.Message, StringComparison.Ordinal);
            // Non-credential diagnostics survive — sanitisation must not blind
            // the operator to what actually failed.
            Assert.Contains("client_id=abc-api-key", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        // `required` is satisfied by a property being *present*, not non-null,
        // and RespectNullableAnnotations is off — so every one of these
        // deserializes cleanly into an AdobeTokenDto today.
        [InlineData("""{ "access_token": null, "token_type": "bearer", "expires_in": 3600 }""", "access_token")]
        [InlineData("""{ "access_token": "", "token_type": "bearer", "expires_in": 3600 }""", "access_token")]
        [InlineData("""{ "access_token": "   ", "token_type": "bearer", "expires_in": 3600 }""", "access_token")]
        [InlineData("""{ "access_token": "tok", "token_type": null, "expires_in": 3600 }""", "token_type")]
        [InlineData("""{ "access_token": "tok", "token_type": "", "expires_in": 3600 }""", "token_type")]
        [InlineData("""{ "access_token": "tok", "token_type": "bearer", "expires_in": 0 }""", "expires_in")]
        [InlineData("""{ "access_token": "tok", "token_type": "bearer", "expires_in": -5 }""", "expires_in")]
        public async Task AuthenticateAsync_WhenImsReturns200WithUnusableTokenFields_Throws(
            string body, string expectedField)
        {
            // Without the guard this is the worst kind of bug: the failure is
            // silent and lands far from its cause. AdobeToken.IsExpired reports
            // false for the token's whole lifetime while GetAuthorizationHeader
            // produces " ", so every downstream call 401s and the retry-on-expiry
            // path never re-authenticates.
            var http = StubHttpClientFactory.ReturningJson(body);
            var service = new AdobeAuthenticationService(new FakeCredentialManager(), http);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AuthenticateAsync(NewCredential()));

            Assert.Contains(expectedField, ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AuthenticateAsync_WhenImsReturns200WithNullBody_Throws()
        {
            // Pins the `?? throw` on the deserialize. Every success stub in this
            // file supplies a complete body, so deleting that guard used to break
            // nothing.
            var http = StubHttpClientFactory.ReturningJson("null");
            var service = new AdobeAuthenticationService(new FakeCredentialManager(), http);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AuthenticateAsync(NewCredential()));

            Assert.Contains("did not deserialize", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void SanitiseErrorBody_RedactsAllThreeSpellingsOfTheSecret()
        {
            const string secret = "p8e-AbC+dEf/123=";
            var percentEncoded = Uri.EscapeDataString(secret);
            var formEncoded = percentEncoded.Replace("%20", "+", StringComparison.Ordinal);

            var sanitised = AdobeAuthenticationService.SanitiseErrorBody(
                $"{secret}|{percentEncoded}|{formEncoded}", secret);

            Assert.Equal("<redacted>|<redacted>|<redacted>", sanitised);
        }

        [Fact]
        public void SanitiseErrorBody_EmptySecret_StillTruncates()
        {
            var sanitised = AdobeAuthenticationService.SanitiseErrorBody(new string('x', 4000), string.Empty);

            Assert.Contains("[truncated]", sanitised, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AuthenticateAsync_WhenImsReturnsLargeErrorBody_TruncatesItInException()
        {
            // Build a body well over the 512-char cap so the exception message
            // ends up bounded in size — keeps log noise low if an upstream proxy
            // echoes the full request back.
            var bigBody = new string('x', 4000);
            var http = StubHttpClientFactory.ReturningJson(bigBody, HttpStatusCode.BadGateway);
            var service = new AdobeAuthenticationService(new FakeCredentialManager(), http);

            var ex = await Assert.ThrowsAsync<HttpRequestException>(
                () => service.AuthenticateAsync(NewCredential()));

            Assert.Contains("[truncated]", ex.Message, StringComparison.Ordinal);
            // The status preamble plus "[truncated]" suffix push the message a
            // bit past 512, but it should be far short of the 4000-char body.
            Assert.True(ex.Message.Length < 1024, $"Expected truncated message, was {ex.Message.Length} chars");
        }

        [Theory]
        [InlineData("http://ims-na1.adobelogin.com/", "https://partners.adobe.io/")]
        [InlineData("https://ims-na1.adobelogin.com/", "http://partners.adobe.io/")]
        public async Task AuthenticateAsync_RejectsCredentialWithCleartextHttpUrl(string imsUrl, string baseUrl)
        {
            // A hand-edited keystore that downgrades either URL to cleartext
            // http should be rejected before any token leaves the process.
            var http = StubHttpClientFactory.ReturningJson("{}");
            var service = new AdobeAuthenticationService(new FakeCredentialManager(), http);
            var credential = new AdobeCredential
            {
                ImsUrl = new Uri(imsUrl, UriKind.Absolute),
                ApiKey = "abc-api-key",
                ClientSecret = "super-secret",
                BaseUrl = new Uri(baseUrl, UriKind.Absolute),
                Environment = "Production",
            };

            await Assert.ThrowsAsync<ArgumentException>(() => service.AuthenticateAsync(credential));
            Assert.Null(http.LastRequest);
        }

        [Fact]
        public async Task AuthenticateAsync_AllowsHttpLoopback()
        {
            // Loopback http stays usable so devs can point the auth service at
            // a local mock IMS — the credential never leaves the box.
            var http = StubHttpClientFactory.ReturningJson("""
            { "access_token": "at", "token_type": "bearer", "expires_in": 3600 }
            """);
            var service = new AdobeAuthenticationService(new FakeCredentialManager(), http);
            var credential = new AdobeCredential
            {
                ImsUrl = new Uri("http://127.0.0.1:5000/"),
                ApiKey = "abc-api-key",
                ClientSecret = "super-secret",
                BaseUrl = new Uri("http://localhost:6000/"),
                Environment = "Sandbox",
            };

            var token = await service.AuthenticateAsync(credential);

            Assert.Equal("at", token.AccessToken);
            Assert.Equal(new Uri("http://localhost:6000/"), token.BaseUrl);
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

        [Theory]
        [InlineData("{ not json")]
        [InlineData("<html><body>Sign in to continue</body></html>")]
        // Note: an empty/whitespace stored value is not covered here — it hits
        // the earlier "No … credential selected" guard, which is its own test.
        public async Task AuthenticateAsync_SelectedJsonMalformed_ThrowsActionableException(string stored)
        {
            var http = StubHttpClientFactory.ReturningJson("{}");
            var manager = new FakeCredentialManager { SelectedCredentialJson = stored };
            var service = new AdobeAuthenticationService(manager, http);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AuthenticateAsync());

            Assert.Contains("accounts add", ex.Message, StringComparison.Ordinal);
            Assert.IsType<JsonException>(ex.InnerException);
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

        private static AdobeCredential NewCredential(string clientSecret = "super-secret") => new()
        {
            ImsUrl = new Uri("https://ims-na1.adobelogin.com/"),
            ApiKey = "abc-api-key",
            ClientSecret = clientSecret,
            BaseUrl = new Uri("https://partners.adobe.io/"),
            Environment = "Production",
        };
        [Fact]
        public async Task AuthenticateAsync_AsksTheManagerForThisProvidersCredential()
        {
            // The real ICredentialManager keys its store by providerName, so a
            // double that discards the argument satisfies the signature and not
            // the contract. Nothing asserted it until now.
            var http = StubHttpClientFactory.ReturningJson(
                """{ "access_token": "tok", "token_type": "bearer", "expires_in": 3600 }""");
            var manager = new FakeCredentialManager
            {
                SelectedCredentialJson = JsonSerializer.Serialize(NewCredential(), AdobeCredential.JsonOptions),
            };
            var service = new AdobeAuthenticationService(manager, http);

            await service.AuthenticateAsync();

            Assert.Equal(AdobeCredential.ProviderName, manager.RequestedProviderName);
        }

    }
}
