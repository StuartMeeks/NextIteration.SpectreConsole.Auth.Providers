using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Net;

using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne.Tests
{
    /// <summary>
    /// The Marketplace token-lookup endpoint takes the API token in an
    /// <c>eq(token,'…')</c> query filter, and Microsoft.Extensions.Http logs the
    /// request URI through its own pipeline logger. On the 8.0.x floor this repo
    /// declares for net8.0 that URI is logged unredacted at Information level, so
    /// any consumer with an information-level logger would write the plaintext,
    /// long-lived token into its log on every `accounts add`.
    /// </summary>
    public sealed class SoftwareOneHttpLoggingTests
    {
        private const string TokenQueryUrl =
            "https://api.example.com/v1/accounts/api-tokens?eq(token,'idt%3AsEcReT%2Bvalue')&limit=2";

        [Fact]
        public async Task NamedClient_RegisteredByTheProvider_LogsNothingAboutTheRequest()
        {
            var (sp, sink) = BuildProvider(configureExtraClient: null);
            using var scope = sp;

            var client = sp.GetRequiredService<IHttpClientFactory>()
                .CreateClient(SoftwareOneCredentialCollector.HttpClientName);
            using var response = await client.GetAsync(new Uri(TokenQueryUrl));

            Assert.Empty(sink);
        }

        [Fact]
        public async Task NamedClient_KeepsSuppression_WhenTheConsumerAlsoConfiguresIt()
        {
            // The package README documents pre-configuring the named client
            // (proxy, retry handler, user-agent). That must not put the default
            // URI logger back.
            var (sp, sink) = BuildProvider(configureExtraClient: services =>
                services.AddHttpClient(
                    SoftwareOneCredentialCollector.HttpClientName,
                    c => c.Timeout = TimeSpan.FromSeconds(30)));
            using var scope = sp;

            var client = sp.GetRequiredService<IHttpClientFactory>()
                .CreateClient(SoftwareOneCredentialCollector.HttpClientName);
            using var response = await client.GetAsync(new Uri(TokenQueryUrl));

            Assert.Empty(sink);
        }

        [Fact]
        public async Task AnUnsuppressedClient_DoesLogTheRequest()
        {
            // Control: without the suppression the pipeline logger is active, so
            // the assertions above are pinning a real behaviour rather than an
            // absence of logging infrastructure. This also proves suppression is
            // scoped to the collector's own named client.
            var (sp, sink) = BuildProvider(configureExtraClient: services =>
                services.AddHttpClient("consumer-client")
                    .ConfigurePrimaryHttpMessageHandler(() => new AlwaysOkHandler()));
            using var scope = sp;

            var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient("consumer-client");
            using var response = await client.GetAsync(new Uri(TokenQueryUrl));

            Assert.NotEmpty(sink);
        }

        private static (ServiceProvider Provider, List<string> Sink) BuildProvider(
            Action<IServiceCollection>? configureExtraClient)
        {
            var sink = new List<string>();
            var services = new ServiceCollection();

            services.AddLogging(b =>
            {
                b.SetMinimumLevel(LogLevel.Trace);
                b.AddProvider(new CapturingLoggerProvider(sink));
            });

            services.AddSoftwareOneAuthProvider();

            // Give the provider's own named client a handler that never touches
            // the network.
            services.AddHttpClient(SoftwareOneCredentialCollector.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new AlwaysOkHandler());

            configureExtraClient?.Invoke(services);

            return (services.BuildServiceProvider(), sink);
        }

        private sealed class AlwaysOkHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        private sealed class CapturingLoggerProvider : ILoggerProvider
        {
            private readonly List<string> _sink;

            public CapturingLoggerProvider(List<string> sink)
            {
                _sink = sink;
            }

            // Only the HttpClient pipeline categories matter here; anything else
            // would be noise that makes Assert.Empty meaningless.
            public ILogger CreateLogger(string categoryName)
                => categoryName.StartsWith("System.Net.Http.HttpClient", StringComparison.Ordinal)
                    ? new CapturingLogger(_sink)
                    : NullLogger.Instance;

            public void Dispose()
            {
            }
        }

        private sealed class NullLogger : ILogger
        {
            public static readonly NullLogger Instance = new();

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => false;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
            }
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly List<string> _sink;

            public CapturingLogger(List<string> sink)
            {
                _sink = sink;
            }

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
                => _sink.Add(formatter(state, exception));
        }
    }
}
