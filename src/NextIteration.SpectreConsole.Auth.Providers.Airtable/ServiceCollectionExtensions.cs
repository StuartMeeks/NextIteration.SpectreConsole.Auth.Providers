using Microsoft.Extensions.DependencyInjection;

using NextIteration.SpectreConsole.Auth.Commands;
using NextIteration.SpectreConsole.Auth.Services;

namespace NextIteration.SpectreConsole.Auth.Providers.Airtable
{
    /// <summary>
    /// DI extensions for wiring the Airtable provider into a NextIteration.SpectreConsole.Auth consumer.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="AirtableAuthenticationService"/> and the Airtable
        /// <see cref="ICredentialCollector"/> so it appears in the
        /// <c>accounts add</c> provider-selection prompt. The auth service
        /// is also registered against the
        /// <see cref="IAuthenticationService{TCredential,TToken}"/> abstraction
        /// so consumers that depend on the interface (rather than the concrete
        /// type) can resolve it.
        /// </summary>
        public static IServiceCollection AddAirtableAuthProvider(this IServiceCollection services)
        {
            services.AddSingleton<AirtableAuthenticationService>();
            // Forward the interface registration to the concrete singleton so
            // both resolution shapes return the same instance.
            services.AddSingleton<IAuthenticationService<AirtableCredential, AirtableToken>>(
                sp => sp.GetRequiredService<AirtableAuthenticationService>());
            services.AddSingleton<ICredentialCollector, AirtableCredentialCollector>();
            services.AddSingleton<ICredentialSummaryProvider, AirtableCredentialSummaryProvider>();
            return services;
        }
    }
}
