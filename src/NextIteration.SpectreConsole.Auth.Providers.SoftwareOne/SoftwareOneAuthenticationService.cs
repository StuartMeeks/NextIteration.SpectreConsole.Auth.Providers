using NextIteration.SpectreConsole.Auth.Persistence;
using NextIteration.SpectreConsole.Auth.Services;
using System.Text.Json;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne
{
    /// <summary>
    /// Pass-through authentication service. SoftwareOne API tokens are
    /// long-lived portal-issued tokens — there is no exchange or refresh
    /// step, so this simply projects the selected
    /// <see cref="SoftwareOneCredential"/> into a
    /// <see cref="SoftwareOneToken"/>.
    /// </summary>
    public sealed class SoftwareOneAuthenticationService : IAuthenticationService<SoftwareOneCredential, SoftwareOneToken>
    {
        private readonly ICredentialManager _credentialManager;

        /// <summary>DI constructor.</summary>
        public SoftwareOneAuthenticationService(ICredentialManager credentialManager)
        {
            ArgumentNullException.ThrowIfNull(credentialManager);
            _credentialManager = credentialManager;
        }

        /// <inheritdoc />
        public async Task<SoftwareOneToken> AuthenticateAsync()
        {
            var credentialJson = await _credentialManager
                .GetSelectedCredentialAsync(SoftwareOneCredential.ProviderName)
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(credentialJson))
            {
                throw new InvalidOperationException($"No {SoftwareOneCredential.ProviderName} credential selected.");
            }

            var credential = JsonSerializer.Deserialize<SoftwareOneCredential>(credentialJson, SoftwareOneCredential.JsonOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize {SoftwareOneCredential.ProviderName} credential.");

            return await AuthenticateAsync(credential).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Task<SoftwareOneToken> AuthenticateAsync(SoftwareOneCredential credential)
        {
            ArgumentNullException.ThrowIfNull(credential);

            return Task.FromResult(new SoftwareOneToken
            {
                ApiToken = credential.ApiToken,
                BaseUrl = credential.BaseUrl,
                Environment = credential.Environment,
                Actor = credential.Actor,
            });
        }

        /// <inheritdoc />
        public Task<bool> ValidateTokenAsync(SoftwareOneToken token)
        {
            ArgumentNullException.ThrowIfNull(token);
            return Task.FromResult(!token.IsExpired);
        }
    }
}
