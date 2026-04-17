using Microsoft.Extensions.DependencyInjection;
using NextIteration.SpectreConsole.Auth.Commands;

namespace NextIteration.SpectreConsole.Auth.Providers.Adobe
{
    /// <summary>
    /// DI extensions for wiring the Adobe provider into a NextIteration.SpectreConsole.Auth consumer.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="AdobeAuthenticationService"/> and the Adobe
        /// <see cref="ICredentialCollector"/> so it appears in the
        /// <c>accounts add</c> provider-selection prompt.
        /// </summary>
        public static IServiceCollection AddAdobeAuthProvider(this IServiceCollection services)
        {
            services.AddSingleton<AdobeAuthenticationService>();
            services.AddSingleton<ICredentialCollector, AdobeCredentialCollector>();
            services.AddSingleton<ICredentialSummaryProvider, AdobeCredentialSummaryProvider>();
            return services;
        }
    }
}
