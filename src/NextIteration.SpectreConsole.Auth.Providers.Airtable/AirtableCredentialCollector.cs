using Spectre.Console;
using NextIteration.SpectreConsole.Auth.Commands;
using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.Airtable
{
    /// <summary>
    /// Interactive collector for Airtable credentials. Prompts for the
    /// personal access token (hidden) and the environment. Registered
    /// automatically by
    /// <see cref="ServiceCollectionExtensions.AddAirtableAuthProvider"/>.
    /// </summary>
    public sealed class AirtableCredentialCollector : ICredentialCollector
    {
        /// <inheritdoc />
        public string ProviderName => AirtableCredential.ProviderName;

        /// <inheritdoc />
        public async Task<(string credentialData, string environment)> CollectAsync()
        {
            var accessToken = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter Access Token:")
                    .Secret()
                    .Validate(value => string.IsNullOrWhiteSpace(value)
                        ? ValidationResult.Error("Access token cannot be empty")
                        : ValidationResult.Success())).ConfigureAwait(false);

            var environment = await AnsiConsole.PromptAsync(
                new SelectionPrompt<string>()
                    .Title("Select environment:")
                    .AddChoices(AirtableCredential.SupportedEnvironments)).ConfigureAwait(false);

            var credential = new AirtableCredential
            {
                AccessToken = accessToken,
                Environment = environment,
            };

            return (JsonSerializer.Serialize(credential, AirtableCredential.JsonOptions), credential.Environment);
        }
    }
}
