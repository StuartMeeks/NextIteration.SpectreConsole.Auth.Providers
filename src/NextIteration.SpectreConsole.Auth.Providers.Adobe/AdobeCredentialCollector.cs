using Spectre.Console;

using NextIteration.SpectreConsole.Auth.Commands;

using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.Adobe
{
    /// <summary>
    /// Interactive collector for Adobe credentials. Prompts the user for
    /// IMS URL, API key (client ID), client secret, base URL, and
    /// environment. Registered automatically by
    /// <see cref="ServiceCollectionExtensions.AddAdobeAuthProvider"/>.
    /// </summary>
    /// <remarks>
    /// The API Key is the OAuth2 <c>client_id</c> — a public identifier,
    /// not a secret. Its prompt is plain-text so typos can be spotted;
    /// only the Client Secret is masked.
    /// </remarks>
    public sealed class AdobeCredentialCollector : ICredentialCollector
    {
        private const string DefaultImsUrl = "https://ims-na1.adobelogin.com/";
        private const string DefaultBaseUrl = "https://partners.adobe.io/";

        /// <inheritdoc />
        public string ProviderName => AdobeCredential.ProviderName;

        /// <inheritdoc />
        public async Task<(string credentialData, string environment)> CollectAsync()
        {
            var imsUrlInput = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter IMS URL:")
                    .DefaultValue(DefaultImsUrl)
                    .Validate(ValidateSecureUrl)).ConfigureAwait(false);

            var apiKey = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter API Key (OAuth2 client_id):")
                    .Validate(ValidateNonEmpty("API Key"))).ConfigureAwait(false);

            var clientSecret = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter Client Secret:")
                    .Secret()
                    .Validate(ValidateNonEmpty("Client Secret"))).ConfigureAwait(false);

            var baseUrlInput = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter Base URL:")
                    .DefaultValue(DefaultBaseUrl)
                    .Validate(ValidateSecureUrl)).ConfigureAwait(false);

            var environment = await AnsiConsole.PromptAsync(
                new SelectionPrompt<string>()
                    .Title("Select environment:")
                    .AddChoices(AdobeCredential.SupportedEnvironments)).ConfigureAwait(false);

            var credential = new AdobeCredential
            {
                ImsUrl = new Uri(imsUrlInput, UriKind.Absolute),
                ApiKey = apiKey,
                ClientSecret = clientSecret,
                BaseUrl = new Uri(baseUrlInput, UriKind.Absolute),
                Environment = environment,
            };

            return (JsonSerializer.Serialize(credential, AdobeCredential.JsonOptions), credential.Environment);
        }

        /// <summary>
        /// Accept the URL only if it's an absolute https URI, or an http
        /// loopback (so devs can point the collector at a local mock or
        /// proxy without compromising the OAuth2 client secret over the
        /// wire in real deployments).
        /// </summary>
        internal static ValidationResult ValidateSecureUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed))
            {
                return ValidationResult.Error("Must be a valid absolute http(s) URL");
            }

            if (parsed.Scheme == Uri.UriSchemeHttps)
            {
                return ValidationResult.Success();
            }

            if (parsed.Scheme == Uri.UriSchemeHttp && parsed.IsLoopback)
            {
                return ValidationResult.Success();
            }

            return ValidationResult.Error(
                "Must use https. http is only accepted for loopback addresses (the OAuth2 client secret is POSTed to this URL and must not traverse the network in cleartext).");
        }

        private static Func<string, ValidationResult> ValidateNonEmpty(string fieldName)
            => value => string.IsNullOrWhiteSpace(value)
                ? ValidationResult.Error($"{fieldName} cannot be empty")
                : ValidationResult.Success();
    }
}
