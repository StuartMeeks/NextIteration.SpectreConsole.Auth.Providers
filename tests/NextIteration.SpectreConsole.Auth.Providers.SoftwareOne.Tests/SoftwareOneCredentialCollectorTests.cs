using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne.Tests;

public sealed class SoftwareOneCredentialCollectorTests
{
    // The interactive CollectAsync flow is driven by Spectre's console
    // prompts — not reasonably unit-testable without a full Spectre
    // test-console harness. Cover what's cheaply coverable here and leave
    // the prompt flow to manual smoke via `accounts add`.

    [Fact]
    public void ProviderName_MatchesCredential()
    {
        var collector = new SoftwareOneCredentialCollector(
            StubHttpClientFactory.ReturningJson("""{ "data": [] }"""));

        Assert.Equal(SoftwareOneCredential.ProviderName, collector.ProviderName);
        Assert.Equal("SoftwareOne", collector.ProviderName);
    }
}
