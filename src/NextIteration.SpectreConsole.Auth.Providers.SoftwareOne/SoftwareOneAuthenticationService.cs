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
            ValidateCredential(credential);

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

        /// <summary>
        /// Guards against stored credentials whose required string fields
        /// are present-but-empty (e.g. a hand-edited keystore file). The
        /// collector's SoftwareOne API validation pre-fills the metadata
        /// fields and the required modifier enforces presence at
        /// deserialization time, so this is belt-and-braces — it surfaces
        /// a clear message rather than letting the consumer wrap an empty
        /// bearer token in an HTTP request and receive an opaque 401.
        /// </summary>
        private static void ValidateCredential(SoftwareOneCredential credential)
        {
            RequireNonWhitespace(credential.ApiToken, nameof(SoftwareOneCredential.ApiToken));
            RequireNonWhitespace(credential.Environment, nameof(SoftwareOneCredential.Environment));
            RequireNonWhitespace(credential.Actor, nameof(SoftwareOneCredential.Actor));
            RequireNonWhitespace(credential.TokenId, nameof(SoftwareOneCredential.TokenId));
            RequireNonWhitespace(credential.TokenName, nameof(SoftwareOneCredential.TokenName));
            RequireNonWhitespace(credential.AccountId, nameof(SoftwareOneCredential.AccountId));
            RequireNonWhitespace(credential.AccountName, nameof(SoftwareOneCredential.AccountName));
            RequireNonWhitespace(credential.AccountType, nameof(SoftwareOneCredential.AccountType));

            // Local helper — `fieldName` is the paramName so CA2208 is happy.
            // The field shows up as the ArgumentException.ParamName, which
            // matches what consumers would expect ("which field was bad?").
            static void RequireNonWhitespace(string value, string fieldName)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException(
                        $"{fieldName} is required and must not be whitespace.",
                        fieldName);
                }
            }
        }
    }
}
