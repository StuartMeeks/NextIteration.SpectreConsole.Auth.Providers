using Microsoft.Extensions.DependencyInjection;

using NextIteration.SpectreConsole.Auth.Commands;
using NextIteration.SpectreConsole.Auth.Services;

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
        /// <c>accounts add</c> provider-selection prompt. The auth service
        /// is also registered against the
        /// <see cref="IAuthenticationService{TCredential,TToken}"/> abstraction
        /// so consumers that depend on the interface (rather than the concrete
        /// type) can resolve it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The collector looks the API token up against the Marketplace API at
        /// <c>accounts add</c> time and takes <c>IHttpClientFactory</c> as its
        /// only constructor dependency, so consumers should also call
        /// <c>services.AddHttpClient()</c>.
        /// </para>
        /// <para>
        /// This method registers the collector's own named client with default
        /// logging suppressed. The Marketplace token-lookup endpoint takes the
        /// token in an <c>eq(token,'…')</c> query filter, and
        /// <c>Microsoft.Extensions.Http</c> 8.x logs the full request URI at
        /// <c>Information</c> level — so without the suppression any consumer
        /// with an information-level logger writes the plaintext token to its
        /// log on every <c>accounts add</c>. Suppression is scoped to this one
        /// named client and does not affect the consumer's other clients.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddSoftwareOneAuthProvider(this IServiceCollection services)
        {
            // Registering the named client here rather than leaving it to the
            // consumer is what makes the suppression reliable: default logging
            // is on unless something turns it off, and a consumer cannot be
            // expected to know the URL carries a credential.
            services.AddHttpClient(SoftwareOneCredentialCollector.HttpClientName)
                .RemoveAllLoggers();

            services.AddSingleton<SoftwareOneAuthenticationService>();
            // Forward the interface registration to the concrete singleton so
            // both resolution shapes return the same instance.
            services.AddSingleton<IAuthenticationService<SoftwareOneCredential, SoftwareOneToken>>(
                sp => sp.GetRequiredService<SoftwareOneAuthenticationService>());
            services.AddSingleton<ICredentialCollector, SoftwareOneCredentialCollector>();
            services.AddSingleton<ICredentialSummaryProvider, SoftwareOneCredentialSummaryProvider>();
            return services;
        }
    }
}
