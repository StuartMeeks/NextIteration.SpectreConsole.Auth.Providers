using System.Net;
using System.Text;

namespace NextIteration.SpectreConsole.Auth.Providers.GitHub.Tests
{
    /// <summary>
    /// Minimal <see cref="IHttpClientFactory"/> + <see cref="HttpMessageHandler"/>
    /// doubles that capture every outgoing request (and its body) and return a
    /// canned response. The GitHub device flow makes several calls in sequence —
    /// device-code, one or more pending polls, success, then <c>/user</c> — so the
    /// stub records the full request list and supports a sequenced responder.
    /// </summary>
    internal sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, string?, HttpResponseMessage> _responder;

        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string?> RequestBodies { get; } = [];
        public HttpRequestMessage? LastRequest => Requests.Count > 0 ? Requests[^1] : null;

        public StubHttpClientFactory(Func<HttpRequestMessage, string?, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public HttpClient CreateClient(string name) => new(new CapturingHandler(this));

        // Convenience: 200 OK with the given JSON body for every call.
        public static StubHttpClientFactory ReturningJson(string json, HttpStatusCode status = HttpStatusCode.OK)
            => new((_, _) => JsonResponse(json, status));

        // Returns each response in order; the last response is reused for any
        // extra calls beyond the supplied set.
        public static StubHttpClientFactory Sequence(params Func<HttpResponseMessage>[] responses)
        {
            var index = 0;
            return new StubHttpClientFactory((_, _) =>
            {
                var resolved = responses[Math.Min(index, responses.Length - 1)]();
                index++;
                return resolved;
            });
        }

        public static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK)
            => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        private sealed class CapturingHandler : HttpMessageHandler
        {
            private readonly StubHttpClientFactory _owner;

            public CapturingHandler(StubHttpClientFactory owner)
            {
                _owner = owner;
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken);

                _owner.Requests.Add(request);
                _owner.RequestBodies.Add(body);
                return _owner._responder(request, body);
            }
        }
    }
}
