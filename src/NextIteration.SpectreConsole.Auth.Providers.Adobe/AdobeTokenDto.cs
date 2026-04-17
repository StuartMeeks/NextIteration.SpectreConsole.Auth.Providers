using System.Text.Json.Serialization;

namespace NextIteration.SpectreConsole.Auth.Providers.Adobe
{
    /// <summary>
    /// Wire-format DTO matching the JSON body returned by the Adobe IMS
    /// token endpoint. Deserialized into <see cref="AdobeToken"/> by
    /// <see cref="AdobeAuthenticationService"/>.
    /// </summary>
    public sealed class AdobeTokenDto
    {
        /// <summary>The opaque access token.</summary>
        [JsonPropertyName("access_token")]
        public required string AccessToken { get; init; }

        /// <summary>Token type (typically <c>bearer</c>).</summary>
        [JsonPropertyName("token_type")]
        public required string TokenType { get; init; }

        /// <summary>Lifetime of the token in seconds.</summary>
        [JsonPropertyName("expires_in")]
        public required int ExpiresIn { get; init; }

    }
}
