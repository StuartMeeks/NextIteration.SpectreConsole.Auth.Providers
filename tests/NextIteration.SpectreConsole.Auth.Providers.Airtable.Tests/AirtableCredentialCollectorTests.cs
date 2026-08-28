using Xunit;

namespace NextIteration.SpectreConsole.Auth.Providers.Airtable.Tests
{
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

        [Fact]
        public async Task CollectAsync_HonoursAlreadyCancelledToken()
        {
            // Every AnsiConsole.PromptAsync call in CollectAsync must receive
            // the token, or a host cancelling `accounts add` is ignored until
            // the *next* prompt runs — the user stays stuck on a blocking
            // stdin read. An already-cancelled token faults at the first
            // prompt (the access-token prompt, which was one of the six sites missing the token), so this pins
            // the entry point without needing a Spectre test console.
            var collector = new AirtableCredentialCollector();
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => collector.CollectAsync(cts.Token));
        }

    }
}
