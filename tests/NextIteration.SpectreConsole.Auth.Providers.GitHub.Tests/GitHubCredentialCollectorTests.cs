using System.Net;

using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.GitHub.Tests
{
    public sealed class GitHubCredentialCollectorTests
    {
        // The interactive CollectAsync flow is driven by Spectre's console prompts
        // and is not reasonably unit-testable without a full Spectre test-console
        // harness. The device-flow building blocks (device-code request, polling,
        // user enrichment) are factored into internal methods and covered here;
        // the prompt orchestration is left to manual smoke via `accounts add`.

        private static GitHubCredentialCollector Collector(
            IHttpClientFactory factory,
            out List<TimeSpan> delays,
            DateTimeOffset? start = null)
        {
            var now = start ?? DateTimeOffset.UnixEpoch;
            var captured = new List<TimeSpan>();
            delays = captured;
            return new GitHubCredentialCollector(
                factory,
                (ts, _) => { captured.Add(ts); now += ts; return Task.CompletedTask; },
                () => now);
        }

        [Fact]
        public void ProviderName_MatchesCredential()
        {
            var collector = new GitHubCredentialCollector(
                StubHttpClientFactory.ReturningJson("{}"));

            Assert.Equal(GitHubCredential.ProviderName, collector.ProviderName);
            Assert.Equal("GitHub", collector.ProviderName);
        }

        [Theory]
        [InlineData("github.com", "https://github.com/", "https://api.github.com/", "GitHubCom")]
        [InlineData("GitHub.com", "https://GitHub.com/", "https://api.github.com/", "GitHubCom")]
        [InlineData("ghe.example.com", "https://ghe.example.com/", "https://ghe.example.com/api/v3/", "Enterprise")]
        [InlineData("ghe.example.com/", "https://ghe.example.com/", "https://ghe.example.com/api/v3/", "Enterprise")]
        public void HostDerivation_ProducesWebApiAndEnvironment(string host, string web, string api, string environment)
        {
            Assert.Equal(new Uri(web), GitHubCredentialCollector.DeriveWebBaseUrl(host));
            Assert.Equal(new Uri(api), GitHubCredentialCollector.DeriveApiBaseUrl(host));
            Assert.Equal(environment, GitHubCredentialCollector.DeriveEnvironment(host));
        }

        [Theory]
        // Accepted: a bare host, an optional numeric port, a trailing slash the
        // collector already normalises away, and IP literals.
        [InlineData("github.com", true)]
        [InlineData("GitHub.com", true)]
        [InlineData("ghe.example.com", true)]
        [InlineData("ghe.example.com/", true)]
        [InlineData("ghe.example.com:8443", true)]
        [InlineData("github.com:443", true)]
        [InlineData("192.168.1.10", true)]
        [InlineData("192.168.1.10:8443", true)]
        [InlineData("[::1]", true)]
        [InlineData("[::1]:8443", true)]
        // Rejected: empty.
        [InlineData("", false)]
        [InlineData("   ", false)]
        // Rejected: userinfo. Uri reads the host as whatever follows the '@',
        // so these would send the access token — and every later refresh POST
        // — to the trailing host while still reading as github.com.
        [InlineData("github.com@evil.example.com", false)]
        [InlineData("user:pw@evil.example.com", false)]
        // Rejected: a path, query or fragment riding along on the host.
        [InlineData("evil.example.com/path", false)]
        [InlineData("ghe.example.com?q=1", false)]
        [InlineData("ghe.example.com#frag", false)]
        [InlineData("ghe.example.com\\path", false)]
        // Rejected: a pasted scheme — Uri parses this with Host == "https".
        [InlineData("https://ghe.example.com", false)]
        [InlineData("http://ghe.example.com", false)]
        // Rejected: malformed ports and hosts.
        [InlineData("ghe.example.com:notaport", false)]
        [InlineData("ghe.example.com:", false)]
        [InlineData("ghe.example.com:0", false)]
        [InlineData("[::1]:", false)]
        [InlineData("ghe.example.com:99999", false)]
        [InlineData("gh ub.com", false)]
        [InlineData("-bad.example.com", false)]
        public void ValidateHost_AcceptsBareHostsOnly(string host, bool expectedOk)
        {
            var result = GitHubCredentialCollector.ValidateHost(host);
            Assert.Equal(expectedOk, result.Successful);
        }

        [Theory]
        [InlineData("github.com@evil.example.com")]
        [InlineData("user:pw@evil.example.com")]
        [InlineData("evil.example.com/path")]
        [InlineData("https://ghe.example.com")]
        public void ValidateHost_RejectsValuesThatWouldRedirectTheToken(string host)
        {
            // Guards the specific consequence rather than just the verdict: if
            // any of these ever passed validation again, DeriveApiBaseUrl would
            // hand LookupUserAsync a URL whose real host is not what the user
            // typed, and the bearer token would go there.
            Assert.False(GitHubCredentialCollector.ValidateHost(host).Successful);

            var derived = new Uri($"https://{host.Trim().TrimEnd('/')}/", UriKind.Absolute);
            Assert.True(
                derived.UserInfo.Length != 0
                    || derived.AbsolutePath != "/"
                    || !string.Equals(derived.Host, host, StringComparison.OrdinalIgnoreCase),
                $"'{host}' no longer misparses — re-check whether this case still needs rejecting.");
        }

        [Fact]
        public async Task RequestDeviceCodeAsync_ParsesResponse_AndPostsClientIdAndScope()
        {
            var stub = StubHttpClientFactory.ReturningJson(
                """
            { "device_code": "dc123", "user_code": "WXYZ-1234",
              "verification_uri": "https://github.com/login/device",
              "expires_in": 900, "interval": 5 }
            """);
            var collector = Collector(stub, out _);

            var device = await collector.RequestDeviceCodeAsync(
                new Uri("https://github.com/"), "Iv1.clientid", "repo read:org");

            Assert.Equal("dc123", device.DeviceCode);
            Assert.Equal("WXYZ-1234", device.UserCode);
            Assert.Equal(900, device.ExpiresIn);
            Assert.Equal(5, device.Interval);

            Assert.Single(stub.Requests);
            Assert.Equal(
                new Uri("https://github.com/login/device/code"),
                stub.LastRequest!.RequestUri);
            Assert.Contains("client_id=Iv1.clientid", stub.RequestBodies[0], StringComparison.Ordinal);
            Assert.Contains("scope=repo", stub.RequestBodies[0], StringComparison.Ordinal);
        }

        [Fact]
        public async Task RequestDeviceCodeAsync_Throws_OnNonSuccess()
        {
            var stub = StubHttpClientFactory.ReturningJson("nope", HttpStatusCode.BadRequest);
            var collector = Collector(stub, out _);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => collector.RequestDeviceCodeAsync(new Uri("https://github.com/"), "id", "repo"));
            Assert.Contains("device-code request failed", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task PollForTokenAsync_ContinuesOnPending_ThenReturnsToken()
        {
            var stub = StubHttpClientFactory.Sequence(
                () => StubHttpClientFactory.JsonResponse("""{ "error": "authorization_pending" }"""),
                () => StubHttpClientFactory.JsonResponse("""{ "error": "authorization_pending" }"""),
                () => StubHttpClientFactory.JsonResponse("""{ "access_token": "gho_ok", "token_type": "bearer", "scope": "repo" }"""));
            var collector = Collector(stub, out var delays);

            var device = new GitHubDeviceCodeDto
            {
                DeviceCode = "dc",
                UserCode = "code",
                VerificationUri = "https://github.com/login/device",
                ExpiresIn = 900,
                Interval = 5,
            };

            var token = await collector.PollForTokenAsync(new Uri("https://github.com/"), "id", device, CancellationToken.None);

            Assert.Equal("gho_ok", token.AccessToken);
            Assert.Equal(3, stub.Requests.Count);
            // Three polls => three interval delays, all the base 5s (no slow_down).
            Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)], delays);
        }

        [Fact]
        public async Task PollForTokenAsync_AppliesSlowDownBackoff()
        {
            var stub = StubHttpClientFactory.Sequence(
                () => StubHttpClientFactory.JsonResponse("""{ "error": "slow_down" }"""),
                () => StubHttpClientFactory.JsonResponse("""{ "access_token": "gho_ok", "token_type": "bearer" }"""));
            var collector = Collector(stub, out var delays);

            var device = new GitHubDeviceCodeDto
            {
                DeviceCode = "dc",
                UserCode = "code",
                VerificationUri = "https://github.com/login/device",
                ExpiresIn = 900,
                Interval = 5,
            };

            var token = await collector.PollForTokenAsync(new Uri("https://github.com/"), "id", device, CancellationToken.None);

            Assert.Equal("gho_ok", token.AccessToken);
            // First poll at 5s; after slow_down the interval grows by 5s to 10s.
            Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)], delays);
        }

        [Fact]
        public async Task PollForTokenAsync_Throws_OnAccessDenied()
        {
            var stub = StubHttpClientFactory.ReturningJson("""{ "error": "access_denied" }""");
            var collector = Collector(stub, out _);

            var device = NewDevice(expiresIn: 900);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => collector.PollForTokenAsync(new Uri("https://github.com/"), "id", device, CancellationToken.None));
            Assert.Contains("denied", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task PollForTokenAsync_Throws_OnTimeout()
        {
            // Always pending; the injected delay advances the clock past expires_in.
            var stub = StubHttpClientFactory.ReturningJson("""{ "error": "authorization_pending" }""");
            var collector = Collector(stub, out _);

            var device = NewDevice(expiresIn: 3, interval: 5);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => collector.PollForTokenAsync(new Uri("https://github.com/"), "id", device, CancellationToken.None));
            Assert.Contains("Timed out", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task PollForTokenAsync_HonoursCancellation_BeforeFirstPoll()
        {
            // The production _delay seam is Task.Delay(ts, ct), so a cancelled
            // token aborts the poll loop at its first await rather than running
            // to the device code's expiry. CollectAsync forwards its own token
            // here; passing CancellationToken.None instead would make `accounts
            // add` uncancellable for the full expires_in window.
            var stub = StubHttpClientFactory.ReturningJson("""{ "error": "authorization_pending" }""");
            var collector = new GitHubCredentialCollector(
                stub,
                (_, ct) => Task.FromCanceled(ct),
                static () => DateTimeOffset.UnixEpoch);

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => collector.PollForTokenAsync(
                    new Uri("https://github.com/"), "id", NewDevice(expiresIn: 900), cts.Token));

            Assert.Empty(stub.Requests);
        }

        [Fact]
        public async Task LookupUserAsync_ParsesUser_AndSendsAuthAndUserAgent()
        {
            var stub = StubHttpClientFactory.ReturningJson(
                """{ "login": "octocat", "name": "The Octocat", "id": 583231 }""");
            var collector = Collector(stub, out _);

            var user = await collector.LookupUserAsync(new Uri("https://api.github.com/"), "gho_secret");

            Assert.Equal("octocat", user.Login);
            Assert.Equal("The Octocat", user.Name);

            var request = stub.LastRequest!;
            Assert.Equal(new Uri("https://api.github.com/user"), request.RequestUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("gho_secret", request.Headers.Authorization.Parameter);
            Assert.Contains(
                GitHubCredentialCollector.UserAgent,
                request.Headers.UserAgent.ToString(),
                StringComparison.Ordinal);
        }

        [Fact]
        public async Task LookupUserAsync_Throws_OnNonSuccess_AndRedactsToken()
        {
            var stub = StubHttpClientFactory.ReturningJson(
                """{ "message": "Bad credentials gho_secret" }""", HttpStatusCode.Unauthorized);
            var collector = Collector(stub, out _);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => collector.LookupUserAsync(new Uri("https://api.github.com/"), "gho_secret"));

            Assert.Contains("user lookup failed", ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("gho_secret", ex.Message, StringComparison.Ordinal);
            Assert.Contains("<redacted>", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void SanitiseErrorBody_RedactsTokenAndTruncates()
        {
            var token = "gho_supersecret";
            var body = "prefix " + token + " " + new string('x', GitHubCredentialCollector.ErrorBodyMaxChars);

            var safe = GitHubCredentialCollector.SanitiseErrorBody(body, token);

            Assert.DoesNotContain(token, safe, StringComparison.Ordinal);
            Assert.Contains("<redacted>", safe, StringComparison.Ordinal);
            Assert.EndsWith("… [truncated]", safe, StringComparison.Ordinal);
        }

        private static GitHubDeviceCodeDto NewDevice(int expiresIn, int interval = 5) => new()
        {
            DeviceCode = "dc",
            UserCode = "code",
            VerificationUri = "https://github.com/login/device",
            ExpiresIn = expiresIn,
            Interval = interval,
        };
    }
}
