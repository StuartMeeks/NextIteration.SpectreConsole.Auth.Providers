using NextIteration.SpectreConsole.Auth.Persistence;

namespace NextIteration.SpectreConsole.Auth.Providers.Airtable.Tests
{
    /// <summary>
    /// Minimal in-memory <see cref="ICredentialManager"/> double. Only
    /// <see cref="GetSelectedCredentialAsync"/> is exercised by the tests in
    /// this project; everything else throws so accidental reliance fails
    /// loudly rather than silently returning defaults.
    /// </summary>
    internal sealed class FakeCredentialManager : ICredentialManager
    {
        public string? SelectedCredentialJson { get; set; }

        public Task<string?> GetSelectedCredentialAsync(string providerName, CancellationToken cancellationToken = default)
            => Task.FromResult(SelectedCredentialJson);

        public Task<string?> GetCredentialByIdAsync(string providerName, string accountId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IEnumerable<CredentialSummary>> ListCredentialsAsync(string providerName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<string> AddCredentialAsync(string providerName, string accountName, string environment, string credentialData, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteCredentialAsync(string accountId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> SelectCredentialAsync(string accountId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IEnumerable<string>> GetProviderNamesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<CredentialExport>> ExportCredentialsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RestoreCredentialAsync(CredentialExport credential, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
