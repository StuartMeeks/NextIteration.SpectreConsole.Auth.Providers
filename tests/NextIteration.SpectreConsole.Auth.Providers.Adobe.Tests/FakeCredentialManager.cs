using NextIteration.SpectreConsole.Auth.Persistence;

namespace NextIteration.SpectreConsole.Auth.Providers.Adobe.Tests;

/// <summary>
/// Minimal in-memory <see cref="ICredentialManager"/> double. Only
/// <see cref="GetSelectedCredentialAsync"/> is exercised by the tests in
/// this project; everything else throws so accidental reliance fails
/// loudly rather than silently returning defaults.
/// </summary>
internal sealed class FakeCredentialManager : ICredentialManager
{
    public string? SelectedCredentialJson { get; set; }

    public Task<string?> GetSelectedCredentialAsync(string providerName)
        => Task.FromResult(SelectedCredentialJson);

    public Task<IEnumerable<CredentialSummary>> ListCredentialsAsync(string providerName)
        => throw new NotSupportedException();

    public Task<string> AddCredentialAsync(string providerName, string accountName, string environment, string credentialData)
        => throw new NotSupportedException();

    public Task<bool> DeleteCredentialAsync(string accountId)
        => throw new NotSupportedException();

    public Task<bool> SelectCredentialAsync(string accountId)
        => throw new NotSupportedException();

    public Task<IEnumerable<string>> GetProviderNamesAsync()
        => throw new NotSupportedException();
}
