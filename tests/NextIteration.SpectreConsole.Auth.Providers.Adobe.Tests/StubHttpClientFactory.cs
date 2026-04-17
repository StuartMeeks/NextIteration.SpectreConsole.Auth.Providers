using System.Net;

namespace NextIteration.SpectreConsole.Auth.Providers.Adobe.Tests;

/// <summary>
/// Minimal <see cref="IHttpClientFactory"/> + <see cref="HttpMessageHandler"/>
/// doubles that capture the outgoing request and return a canned response.
/// Used by the authentication-service tests to exercise the IMS call without
/// hitting the network.
/// </summary>
internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    public StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public HttpClient CreateClient(string name)
    {
        return new HttpClient(new CapturingHandler(this));
    }

    // Convenience factory for the common case: a 200 OK with the given JSON body.
    public static StubHttpClientFactory ReturningJson(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new StubHttpClientFactory(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly StubHttpClientFactory _owner;

        public CapturingHandler(StubHttpClientFactory owner) => _owner = owner;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _owner.LastRequest = request;
            if (request.Content is not null)
            {
                _owner.LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return _owner._responder(request);
        }
    }
}
