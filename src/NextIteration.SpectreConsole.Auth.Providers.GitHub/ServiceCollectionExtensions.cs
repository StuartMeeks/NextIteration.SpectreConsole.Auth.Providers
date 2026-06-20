using Microsoft.Extensions.DependencyInjection;
using NextIteration.SpectreConsole.Auth.Commands;
using NextIteration.SpectreConsole.Auth.Services;

namespace NextIteration.SpectreConsole.Auth.Providers.GitHub
{
    /// <summary>
    /// DI extensions for wiring the GitHub provider into a NextIteration.SpectreConsole.Auth consumer.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="GitHubAuthenticationService"/> and the GitHub
        /// <see cref="ICredentialCollector"/> so it appears in the
        /// <c>accounts add</c> provider-selection prompt. The auth service is
        /// also registered against the
        /// <see cref="IAuthenticationService{TCredential,TToken}"/> abstraction
        /// so consumers that depend on the interface (rather than the concrete
        /// type) can resolve it.
        /// </summary>
        /// <remarks>
        /// The collector and the auth service's refresh path both use
        /// <c>IHttpClientFactory</c>, so consumers must also call
        /// <c>services.AddHttpClient()</c>.
        /// </remarks>
        public static IServiceCollection AddGitHubAuthProvider(this IServiceCollection services)
        {
            services.AddSingleton<GitHubAuthenticationService>();
            // Forward the interface registration to the concrete singleton so
            // both resolution shapes return the same instance.
            services.AddSingleton<IAuthenticationService<GitHubCredential, GitHubToken>>(
                sp => sp.GetRequiredService<GitHubAuthenticationService>());
            services.AddSingleton<ICredentialCollector, GitHubCredentialCollector>();
            services.AddSingleton<ICredentialSummaryProvider, GitHubCredentialSummaryProvider>();
            return services;
        }
    }
}
