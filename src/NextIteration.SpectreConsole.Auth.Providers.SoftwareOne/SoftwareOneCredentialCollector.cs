using Spectre.Console;
using NextIteration.SpectreConsole.Auth.Commands;
using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne
{
    /// <summary>
    /// Interactive collector for SoftwareOne credentials. Prompts for the
    /// API token (hidden), base URL, environment, and actor. Registered
    /// automatically by
    /// <see cref="ServiceCollectionExtensions.AddSoftwareOneAuthProvider"/>.
    /// </summary>
    public sealed class SoftwareOneCredentialCollector : ICredentialCollector
    {
        private const string DefaultBaseUrl = "https://api.softwareone.com/";

        /// <inheritdoc />
        public string ProviderName => SoftwareOneCredential.ProviderName;

        /// <inheritdoc />
        public async Task<(string credentialData, string environment)> CollectAsync()
        {
            var apiToken = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter API Token:")
                    .Secret()
                    .Validate(value => string.IsNullOrWhiteSpace(value)
                        ? ValidationResult.Error("API token cannot be empty")
                        : ValidationResult.Success())).ConfigureAwait(false);

            var baseUrlInput = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter Base URL:")
                    .DefaultValue(DefaultBaseUrl)
                    .Validate(value => Uri.TryCreate(value, UriKind.Absolute, out var parsed)
                            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Must be a valid absolute http(s) URL"))).ConfigureAwait(false);

            var environment = await AnsiConsole.PromptAsync(
                new SelectionPrompt<string>()
                    .Title("Select environment:")
                    .AddChoices(SoftwareOneCredential.SupportedEnvironments)).ConfigureAwait(false);

            var actor = await AnsiConsole.PromptAsync(
                new SelectionPrompt<string>()
                    .Title("Select actor:")
                    .AddChoices(SoftwareOneCredential.SupportedActors)).ConfigureAwait(false);

            var credential = new SoftwareOneCredential
            {
                ApiToken = apiToken,
                BaseUrl = new Uri(baseUrlInput, UriKind.Absolute),
                Environment = environment,
                Actor = actor,
            };

            return (JsonSerializer.Serialize(credential, SoftwareOneCredential.JsonOptions), credential.Environment);
        }
    }
}
