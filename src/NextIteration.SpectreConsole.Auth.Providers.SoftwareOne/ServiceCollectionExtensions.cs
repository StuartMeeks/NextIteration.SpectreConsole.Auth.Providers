using Microsoft.Extensions.DependencyInjection;
using NextIteration.SpectreConsole.Auth.Commands;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne
{
    /// <summary>
    /// DI extensions for wiring the SoftwareOne provider into a NextIteration.SpectreConsole.Auth consumer.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="SoftwareOneAuthenticationService"/> and the
        /// SoftwareOne <see cref="ICredentialCollector"/> so it appears in the
        /// <c>accounts add</c> provider-selection prompt.
        /// </summary>
        public static IServiceCollection AddSoftwareOneAuthProvider(this IServiceCollection services)
        {
            services.AddSingleton<SoftwareOneAuthenticationService>();
            services.AddSingleton<ICredentialCollector, SoftwareOneCredentialCollector>();
            services.AddSingleton<ICredentialSummaryProvider, SoftwareOneCredentialSummaryProvider>();
            return services;
        }
    }
}
