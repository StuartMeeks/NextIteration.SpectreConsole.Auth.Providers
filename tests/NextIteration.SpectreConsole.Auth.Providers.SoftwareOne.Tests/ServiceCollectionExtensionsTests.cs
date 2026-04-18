using Microsoft.Extensions.DependencyInjection;
using NextIteration.SpectreConsole.Auth.Commands;
using NextIteration.SpectreConsole.Auth.Persistence;
using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSoftwareOneAuthProvider_RegistersAuthenticationService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICredentialManager, FakeCredentialManager>();

        services.AddSoftwareOneAuthProvider();

        using var sp = services.BuildServiceProvider();
        var svc = sp.GetService<SoftwareOneAuthenticationService>();
        Assert.NotNull(svc);
    }

    [Fact]
    public void AddSoftwareOneAuthProvider_RegistersCollectorOnICredentialCollector()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();

        services.AddSoftwareOneAuthProvider();

        using var sp = services.BuildServiceProvider();
        // Collectors are registered against the ICredentialCollector interface
        // so the core package's AddCredentialCommand can resolve them all via
        // IEnumerable<ICredentialCollector>.
        var collectors = sp.GetServices<ICredentialCollector>().ToList();
        Assert.Single(collectors);
        Assert.IsType<SoftwareOneCredentialCollector>(collectors[0]);
    }

    [Fact]
    public void AddSoftwareOneAuthProvider_RegistersSummaryProviderOnICredentialSummaryProvider()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();

        services.AddSoftwareOneAuthProvider();

        using var sp = services.BuildServiceProvider();
        var summaries = sp.GetServices<ICredentialSummaryProvider>().ToList();
        Assert.Single(summaries);
        Assert.IsType<SoftwareOneCredentialSummaryProvider>(summaries[0]);
    }

    [Fact]
    public void AddSoftwareOneAuthProvider_RegistersAsSingletons()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICredentialManager, FakeCredentialManager>();

        services.AddSoftwareOneAuthProvider();

        using var sp = services.BuildServiceProvider();
        var a = sp.GetRequiredService<SoftwareOneAuthenticationService>();
        var b = sp.GetRequiredService<SoftwareOneAuthenticationService>();
        Assert.Same(a, b);
    }
}
