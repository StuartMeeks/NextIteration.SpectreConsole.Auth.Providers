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
                    .Validate(ValidateHttpUrl)).ConfigureAwait(false);

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
                    .Validate(ValidateHttpUrl)).ConfigureAwait(false);

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

        private static ValidationResult ValidateHttpUrl(string value)
            => Uri.TryCreate(value, UriKind.Absolute, out var parsed)
                && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Must be a valid absolute http(s) URL");

        private static Func<string, ValidationResult> ValidateNonEmpty(string fieldName)
            => value => string.IsNullOrWhiteSpace(value)
                ? ValidationResult.Error($"{fieldName} cannot be empty")
                : ValidationResult.Success();
    }
}
