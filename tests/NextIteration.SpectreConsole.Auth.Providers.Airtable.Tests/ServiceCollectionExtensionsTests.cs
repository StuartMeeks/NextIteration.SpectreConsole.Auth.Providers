using Microsoft.Extensions.DependencyInjection;
using NextIteration.SpectreConsole.Auth.Commands;
using NextIteration.SpectreConsole.Auth.Persistence;
using NextIteration.SpectreConsole.Auth.Services;
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
        var collector = Assert.Single(collectors);
        Assert.IsType<AirtableCredentialCollector>(collector);
    }

    [Fact]
    public void AddAirtableAuthProvider_RegistersSummaryProviderOnICredentialSummaryProvider()
    {
        var services = new ServiceCollection();

        services.AddAirtableAuthProvider();

        using var sp = services.BuildServiceProvider();
        var summaries = sp.GetServices<ICredentialSummaryProvider>().ToList();
        var summary = Assert.Single(summaries);
        Assert.IsType<AirtableCredentialSummaryProvider>(summary);
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

    [Fact]
    public void AddAirtableAuthProvider_ResolvesAuthenticationServiceViaInterface()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICredentialManager, FakeCredentialManager>();

        services.AddAirtableAuthProvider();

        using var sp = services.BuildServiceProvider();
        var viaInterface = sp.GetRequiredService<IAuthenticationService<AirtableCredential, AirtableToken>>();
        Assert.IsType<AirtableAuthenticationService>(viaInterface);
    }

    [Fact]
    public void AddAirtableAuthProvider_InterfaceForwardsToSameSingletonAsConcrete()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICredentialManager, FakeCredentialManager>();

        services.AddAirtableAuthProvider();

        using var sp = services.BuildServiceProvider();
        var concrete = sp.GetRequiredService<AirtableAuthenticationService>();
        var viaInterface = sp.GetRequiredService<IAuthenticationService<AirtableCredential, AirtableToken>>();
        Assert.Same(concrete, viaInterface);
    }
}
