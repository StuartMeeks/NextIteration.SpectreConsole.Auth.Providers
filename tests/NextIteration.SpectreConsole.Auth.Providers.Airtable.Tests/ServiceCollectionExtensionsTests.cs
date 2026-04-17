using Microsoft.Extensions.DependencyInjection;
using NextIteration.SpectreConsole.Auth.Commands;
using NextIteration.SpectreConsole.Auth.Persistence;
using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.Airtable.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAirtableAuthProvider_RegistersAuthenticationService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICredentialManager, FakeCredentialManager>();

        services.AddAirtableAuthProvider();

        using var sp = services.BuildServiceProvider();
        var svc = sp.GetService<AirtableAuthenticationService>();
        Assert.NotNull(svc);
    }

    [Fact]
    public void AddAirtableAuthProvider_RegistersCollectorOnICredentialCollector()
    {
        var services = new ServiceCollection();

        services.AddAirtableAuthProvider();

        using var sp = services.BuildServiceProvider();
        // Collectors are registered against the ICredentialCollector interface
        // so the core package's AddCredentialCommand can resolve them all via
        // IEnumerable<ICredentialCollector>.
        var collectors = sp.GetServices<ICredentialCollector>().ToList();
        Assert.Single(collectors);
        Assert.IsType<AirtableCredentialCollector>(collectors[0]);
    }

    [Fact]
    public void AddAirtableAuthProvider_RegistersSummaryProviderOnICredentialSummaryProvider()
    {
        var services = new ServiceCollection();

        services.AddAirtableAuthProvider();

        using var sp = services.BuildServiceProvider();
        var summaries = sp.GetServices<ICredentialSummaryProvider>().ToList();
        Assert.Single(summaries);
        Assert.IsType<AirtableCredentialSummaryProvider>(summaries[0]);
    }

    [Fact]
    public void AddAirtableAuthProvider_RegistersAsSingletons()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICredentialManager, FakeCredentialManager>();

        services.AddAirtableAuthProvider();

        using var sp = services.BuildServiceProvider();
        var a = sp.GetRequiredService<AirtableAuthenticationService>();
        var b = sp.GetRequiredService<AirtableAuthenticationService>();
        Assert.Same(a, b);
    }
}
