using Microsoft.Extensions.DependencyInjection;
using NextIteration.SpectreConsole.Auth.Commands;
using NextIteration.SpectreConsole.Auth.Persistence;
using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.Adobe.Tests;

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
        Assert.Single(collectors);
        Assert.IsType<AdobeCredentialCollector>(collectors[0]);
    }

    [Fact]
    public void AddAdobeAuthProvider_RegistersSummaryProviderOnICredentialSummaryProvider()
    {
        var services = new ServiceCollection();

        services.AddAdobeAuthProvider();

        using var sp = services.BuildServiceProvider();
        var summaries = sp.GetServices<ICredentialSummaryProvider>().ToList();
        Assert.Single(summaries);
        Assert.IsType<AdobeCredentialSummaryProvider>(summaries[0]);
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
}
