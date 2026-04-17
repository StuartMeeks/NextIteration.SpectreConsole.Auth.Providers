using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.Airtable.Tests;

public sealed class AirtableCredentialCollectorTests
{
    // The interactive CollectAsync flow is driven by Spectre's console
    // prompts — not reasonably unit-testable without a full Spectre
    // test-console harness. Cover what's cheaply coverable here and leave
    // the prompt flow to manual smoke via `accounts add`.

    [Fact]
    public void ProviderName_MatchesCredential()
    {
        var collector = new AirtableCredentialCollector();

        Assert.Equal(AirtableCredential.ProviderName, collector.ProviderName);
        Assert.Equal("Airtable", collector.ProviderName);
    }
}
