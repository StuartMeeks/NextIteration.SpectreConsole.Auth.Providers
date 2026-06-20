using Microsoft.Extensions.DependencyInjection;
using NextIteration.SpectreConsole.Auth.Commands;
using NextIteration.SpectreConsole.Auth.Persistence;
using NextIteration.SpectreConsole.Auth.Services;
using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.GitHub.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGitHubAuthProvider_RegistersAuthenticationService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICredentialManager, FakeCredentialManager>();
        services.AddHttpClient();

        services.AddGitHubAuthProvider();

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<GitHubAuthenticationService>());
    }

    [Fact]
    public void AddGitHubAuthProvider_RegistersCollectorOnICredentialCollector()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();

        services.AddGitHubAuthProvider();

        using var sp = services.BuildServiceProvider();
        var collectors = sp.GetServices<ICredentialCollector>().ToList();
        Assert.Single(collectors);
        Assert.IsType<GitHubCredentialCollector>(collectors[0]);
    }

    [Fact]
    public void AddGitHubAuthProvider_RegistersSummaryProviderOnICredentialSummaryProvider()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();

        services.AddGitHubAuthProvider();

        using var sp = services.BuildServiceProvider();
        var summaries = sp.GetServices<ICredentialSummaryProvider>().ToList();
        Assert.Single(summaries);
        Assert.IsType<GitHubCredentialSummaryProvider>(summaries[0]);
    }

    [Fact]
    public void AddGitHubAuthProvider_RegistersAsSingletons()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICredentialManager, FakeCredentialManager>();
        services.AddHttpClient();

        services.AddGitHubAuthProvider();

        using var sp = services.BuildServiceProvider();
        var a = sp.GetRequiredService<GitHubAuthenticationService>();
        var b = sp.GetRequiredService<GitHubAuthenticationService>();
        Assert.Same(a, b);
    }

    [Fact]
    public void AddGitHubAuthProvider_InterfaceForwardsToSameSingletonAsConcrete()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICredentialManager, FakeCredentialManager>();
        services.AddHttpClient();

        services.AddGitHubAuthProvider();

        using var sp = services.BuildServiceProvider();
        var concrete = sp.GetRequiredService<GitHubAuthenticationService>();
        var viaInterface = sp.GetRequiredService<IAuthenticationService<GitHubCredential, GitHubToken>>();
        Assert.Same(concrete, viaInterface);
    }
}
