using NextIteration.SpectreConsole.Auth.Tokens;

namespace NextIteration.SpectreConsole.Auth.Providers.Airtable
{
    /// <summary>
    /// Airtable bearer token. Pass-through wrapper around the stored
    /// <see cref="AirtableCredential.AccessToken"/> plus the Airtable API
    /// base URL, matching the shape of the other provider tokens so
    /// consumer code can treat them uniformly.
    /// </summary>
    /// <remarks>
    /// The token is never serialized to disk — the credential is. All
    /// properties are therefore left un-annotated with JSON attributes.
    /// <see cref="IsExpired"/> is hard-coded to <see langword="false"/>:
    /// Airtable personal access tokens don't expire on their own, but
    /// they can be revoked in the Airtable account UI. A revoked token
    /// surfaces as a 401 on the first API call; consumers should handle
    /// that path regardless.
    /// </remarks>
    public sealed class AirtableToken : IToken
    {
        /// <summary>The Airtable personal access token.</summary>
        public required string AccessToken { get; init; }

        /// <summary>Base URL of the Airtable API the token targets.</summary>
        public required Uri BaseUrl { get; init; }

        /// <summary>Token scheme used in the <c>Authorization</c> header.</summary>
        public const string TokenType = "Bearer";

        /// <inheritdoc />
        public bool IsExpired => false;

        /// <inheritdoc />
        public string GetAuthorizationHeader() => $"Bearer {AccessToken}";
    }
}
