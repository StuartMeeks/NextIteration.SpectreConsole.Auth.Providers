using NextIteration.SpectreConsole.Auth.Persistence;
using NextIteration.SpectreConsole.Auth.Services;
using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.Airtable
{
    /// <summary>
    /// Pass-through authentication service. Airtable Personal Access Tokens
    /// are long-lived and issued out-of-band via the user's Airtable account
    /// settings — there is no exchange or refresh flow, so this simply
    /// projects the selected <see cref="AirtableCredential"/> into an
    /// <see cref="AirtableToken"/>.
    /// </summary>
    public sealed class AirtableAuthenticationService : IAuthenticationService<AirtableCredential, AirtableToken>
    {
        /// <summary>
        /// Airtable's public API base. Fixed; Airtable does not publish
        /// regional endpoints. Exposed so consumers (and tests) can refer
        /// to it by symbolic name rather than hardcoding the string.
        /// </summary>
        public static readonly Uri ApiBaseUrl = new("https://api.airtable.com/");

        private readonly ICredentialManager _credentialManager;

        /// <summary>DI constructor.</summary>
        public AirtableAuthenticationService(ICredentialManager credentialManager)
        {
            ArgumentNullException.ThrowIfNull(credentialManager);
            _credentialManager = credentialManager;
        }

        /// <inheritdoc />
        public async Task<AirtableToken> AuthenticateAsync()
        {
            var credentialJson = await _credentialManager
                .GetSelectedCredentialAsync(AirtableCredential.ProviderName)
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(credentialJson))
            {
                throw new InvalidOperationException($"No {AirtableCredential.ProviderName} credential selected.");
            }

            var credential = JsonSerializer.Deserialize<AirtableCredential>(credentialJson, AirtableCredential.JsonOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize {AirtableCredential.ProviderName} credential.");

            return await AuthenticateAsync(credential).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Task<AirtableToken> AuthenticateAsync(AirtableCredential credential)
        {
            ArgumentNullException.ThrowIfNull(credential);
            ValidateCredential(credential);

            return Task.FromResult(new AirtableToken
            {
                AccessToken = credential.AccessToken,
                BaseUrl = ApiBaseUrl,
            });
        }

        /// <inheritdoc />
        public Task<bool> ValidateTokenAsync(AirtableToken token)
        {
            ArgumentNullException.ThrowIfNull(token);
            return Task.FromResult(!token.IsExpired);
        }

        // =========================================================
        // Helpers
        // =========================================================

        /// <summary>
        /// Guards against stored credentials whose
        /// <see cref="AirtableCredential.AccessToken"/> or
        /// <see cref="AirtableCredential.Environment"/> are
        /// present-but-empty (e.g. a hand-edited keystore file). The
        /// collector already rejects empty input interactively; this is
        /// belt-and-braces so downstream code fails fast with a clear
        /// message rather than sending an empty bearer token to Airtable
        /// and receiving an opaque 401.
        /// </summary>
        private static void ValidateCredential(AirtableCredential credential)
        {
            if (string.IsNullOrWhiteSpace(credential.AccessToken))
            {
                throw new ArgumentException(
                    $"{nameof(AirtableCredential.AccessToken)} is required and must not be whitespace.",
                    nameof(credential));
            }
            if (string.IsNullOrWhiteSpace(credential.Environment))
            {
                throw new ArgumentException(
                    $"{nameof(AirtableCredential.Environment)} is required and must not be whitespace.",
                    nameof(credential));
            }
        }
    }
}
