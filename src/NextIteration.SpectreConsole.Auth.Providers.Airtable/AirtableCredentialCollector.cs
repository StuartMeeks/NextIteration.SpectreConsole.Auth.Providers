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
        public async Task<(string credentialData, string environment)> CollectAsync(CancellationToken cancellationToken = default)
        {
            var accessToken = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter Access Token:")
                    .Secret()
                    .Validate(ValidateAccessToken), cancellationToken).ConfigureAwait(false);

            var environment = await AnsiConsole.PromptAsync(
                new SelectionPrompt<string>()
                    .Title("Select environment:")
                    .AddChoices(AirtableCredential.SupportedEnvironments), cancellationToken).ConfigureAwait(false);

            var credential = new AirtableCredential
            {
                AccessToken = accessToken,
                Environment = environment,
            };

            return (JsonSerializer.Serialize(credential, AirtableCredential.JsonOptions), credential.Environment);
        }
        /// <summary>
        /// Rejects an empty or whitespace-only access token.
        /// </summary>
        /// <remarks>
        /// Factored out of the prompt's inline lambda so it can be tested
        /// directly, matching how the other three providers expose their URL
        /// validators.
        /// </remarks>
        internal static ValidationResult ValidateAccessToken(string value)
            => string.IsNullOrWhiteSpace(value)
                ? ValidationResult.Error("Access token cannot be empty")
                : ValidationResult.Success();

    }
}
