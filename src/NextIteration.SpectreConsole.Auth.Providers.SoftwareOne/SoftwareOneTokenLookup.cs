using System.Text.Json.Serialization;

namespace NextIteration.SpectreConsole.Auth.Providers.SoftwareOne
{
    /// <summary>
    /// Wire-format DTO for the SoftwareOne Marketplace
    /// <c>GET /v1/accounts/api-tokens?eq(token,'…')&amp;limit=2</c>
    /// response. The API returns an envelope with a <c>data</c> array of
    /// matching token records plus pagination metadata we don't consume.
    /// Kept internal — implementation detail of
    /// <see cref="SoftwareOneCredentialCollector"/>.
    /// </summary>
    internal sealed class SoftwareOneTokenSearchResult
    {
        [JsonPropertyName("data")]
        public required List<SoftwareOneTokenDto> Data { get; init; }
    }

    internal sealed class SoftwareOneTokenDto
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("account")]
        public required SoftwareOneAccountDto Account { get; init; }
    }

    internal sealed class SoftwareOneAccountDto
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("type")]
        public required string Type { get; init; }
    }
}
