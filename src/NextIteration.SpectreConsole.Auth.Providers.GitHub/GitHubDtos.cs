using System.Text.Json.Serialization;

namespace NextIteration.SpectreConsole.Auth.Providers.GitHub
{
    /// <summary>
    /// Response of <c>POST {web}/login/device/code</c>: the device + user
    /// verification codes and the polling parameters.
    /// </summary>
    internal sealed class GitHubDeviceCodeDto
    {
        [JsonPropertyName("device_code")]
        public required string DeviceCode { get; init; }

        [JsonPropertyName("user_code")]
        public required string UserCode { get; init; }

        [JsonPropertyName("verification_uri")]
        public required string VerificationUri { get; init; }

        [JsonPropertyName("expires_in")]
        public required int ExpiresIn { get; init; }

        [JsonPropertyName("interval")]
        public required int Interval { get; init; }
    }

    /// <summary>
    /// Response of <c>POST {web}/login/oauth/access_token</c>. GitHub returns a
    /// <c>200</c> with an <see cref="Error"/> field for the pending/transient
    /// states (<c>authorization_pending</c>, <c>slow_down</c>) as well as for
    /// terminal failures, so both the success and error shapes are modelled here.
    /// </summary>
    internal sealed class GitHubAccessTokenDto
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; }

        [JsonPropertyName("scope")]
        public string? Scope { get; init; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; init; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; init; }
    }

    /// <summary>
    /// Slice of <c>GET {api}user</c> used to enrich the credential with the
    /// authenticated user's identity.
    /// </summary>
    internal sealed class GitHubUserDto
    {
        [JsonPropertyName("login")]
        public required string Login { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("id")]
        public long Id { get; init; }
    }
}
