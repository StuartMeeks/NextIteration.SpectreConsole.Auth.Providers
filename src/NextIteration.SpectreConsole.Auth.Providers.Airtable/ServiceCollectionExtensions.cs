using Microsoft.Extensions.DependencyInjection;
using NextIteration.SpectreConsole.Auth.Commands;

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
        /// <c>accounts add</c> provider-selection prompt.
        /// </summary>
        public static IServiceCollection AddAirtableAuthProvider(this IServiceCollection services)
        {
            services.AddSingleton<AirtableAuthenticationService>();
            services.AddSingleton<ICredentialCollector, AirtableCredentialCollector>();
            services.AddSingleton<ICredentialSummaryProvider, AirtableCredentialSummaryProvider>();
            return services;
        }
    }
}
