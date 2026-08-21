using Microsoft.Extensions.DependencyInjection;

using NextIteration.SpectreConsole.Auth.Commands;
using NextIteration.SpectreConsole.Auth.Persistence;
using NextIteration.SpectreConsole.Auth.Services;

using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.Adobe.Tests
{
    public sealed class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddAdobeAuthProvider_RegistersAuthenticationService()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ICredentialManager, FakeCredentialManager>();
            services.AddHttpClient();

            services.AddAdobeAuthProvider();

            using var sp = services.BuildServiceProvider();
            var svc = sp.GetService<AdobeAuthenticationService>();
            Assert.NotNull(svc);
        }

        [Fact]
        public void AddAdobeAuthProvider_RegistersCollectorOnICredentialCollector()
        {
            var services = new ServiceCollection();

            services.AddAdobeAuthProvider();

            using var sp = services.BuildServiceProvider();
            // Collectors are registered against the ICredentialCollector interface
            // so the core package's AddCredentialCommand can resolve them all via
            // IEnumerable<ICredentialCollector>.
            var collectors = sp.GetServices<ICredentialCollector>().ToList();
            var collector = Assert.Single(collectors);
            Assert.IsType<AdobeCredentialCollector>(collector);
        }

        [Fact]
        public void AddAdobeAuthProvider_RegistersSummaryProviderOnICredentialSummaryProvider()
        {
            var services = new ServiceCollection();

            services.AddAdobeAuthProvider();

            using var sp = services.BuildServiceProvider();
            var summaries = sp.GetServices<ICredentialSummaryProvider>().ToList();
            var summary = Assert.Single(summaries);
            Assert.IsType<AdobeCredentialSummaryProvider>(summary);
        }

        [Fact]
        public void AddAdobeAuthProvider_RegistersAsSingletons()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ICredentialManager, FakeCredentialManager>();
            services.AddHttpClient();

            services.AddAdobeAuthProvider();

            using var sp = services.BuildServiceProvider();
            var a = sp.GetRequiredService<AdobeAuthenticationService>();
            var b = sp.GetRequiredService<AdobeAuthenticationService>();
            Assert.Same(a, b);
        }

        [Fact]
        public void AddAdobeAuthProvider_ResolvesAuthenticationServiceViaInterface()
        {
            // Consumers depending on the IAuthenticationService<,> abstraction
            // (rather than the concrete type) must be able to resolve it.
            var services = new ServiceCollection();
            services.AddSingleton<ICredentialManager, FakeCredentialManager>();
            services.AddHttpClient();

            services.AddAdobeAuthProvider();

            using var sp = services.BuildServiceProvider();
            var viaInterface = sp.GetRequiredService<IAuthenticationService<AdobeCredential, AdobeToken>>();
            Assert.IsType<AdobeAuthenticationService>(viaInterface);
        }

        [Fact]
        public void AddAdobeAuthProvider_InterfaceForwardsToSameSingletonAsConcrete()
        {
            // The interface registration must forward to the concrete singleton
            // (not register a second instance), so resolving by either shape
            // returns the same object.
            var services = new ServiceCollection();
            services.AddSingleton<ICredentialManager, FakeCredentialManager>();
            services.AddHttpClient();

            services.AddAdobeAuthProvider();

            using var sp = services.BuildServiceProvider();
            var concrete = sp.GetRequiredService<AdobeAuthenticationService>();
            var viaInterface = sp.GetRequiredService<IAuthenticationService<AdobeCredential, AdobeToken>>();
            Assert.Same(concrete, viaInterface);
        }
    }
}
