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
        public async Task<AirtableToken> AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            var credentialJson = await _credentialManager
                .GetSelectedCredentialAsync(AirtableCredential.ProviderName, cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(credentialJson))
            {
                throw new InvalidOperationException($"No {AirtableCredential.ProviderName} credential selected.");
            }

            var credential = DeserializeOrThrow<AirtableCredential>(
                credentialJson,
                AirtableCredential.JsonOptions,
                $"Failed to deserialize the stored {AirtableCredential.ProviderName} credential. It may have been written by an incompatible version — delete it and re-run `accounts add`.");

            return await AuthenticateAsync(credential, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Task<AirtableToken> AuthenticateAsync(AirtableCredential credential, CancellationToken cancellationToken = default)
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
        public Task<bool> ValidateTokenAsync(AirtableToken token, CancellationToken cancellationToken = default)
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
        /// <summary>
        /// Deserializes <paramref name="json"/>, turning both a literal
        /// <c>null</c> body and a <see cref="JsonException"/> into the same
        /// <see cref="InvalidOperationException"/>.
        /// </summary>
        /// <remarks>
        /// The bare <c>?? throw</c> this replaces only fired for a body that is
        /// the literal <c>null</c>. Anything merely malformed — an HTML captive
        /// portal interstitial, a truncated response, a payload missing a
        /// required member — escaped as a raw <see cref="JsonException"/> whose
        /// message names a System.Text.Json path and nothing the user can act
        /// on. The original exception is preserved as the inner exception.
        /// </remarks>
        private static TValue DeserializeOrThrow<TValue>(
            string json, JsonSerializerOptions? options, string failureMessage)
        {
            TValue? value;

            try
            {
                value = JsonSerializer.Deserialize<TValue>(json, options);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(failureMessage, ex);
            }

            return value ?? throw new InvalidOperationException(failureMessage);
        }

    }
}
