# NextIteration.SpectreConsole.Auth.Providers.Adobe

Adobe VIP Marketplace credential provider for [**NextIteration.SpectreConsole.Auth**](https://www.nuget.org/packages/NextIteration.SpectreConsole.Auth).

Drops a ready-to-use `AdobeCredential`, `AdobeToken`, authentication service (OAuth2 client-credentials against Adobe IMS), Spectre.Console `accounts add` prompt collector, and `accounts list` display formatter into a CLI that already uses the core auth package.

---

## Install

```bash
dotnet add package NextIteration.SpectreConsole.Auth.Providers.Adobe
```

Requires the core package (`NextIteration.SpectreConsole.Auth` ≥ 0.4.1) — NuGet pulls it in transitively.

---

## Quick start

```csharp
using NextIteration.SpectreConsole.Auth;
using NextIteration.SpectreConsole.Auth.Providers.Adobe;

services.AddCredentialStore(opts =>
{
    opts.CredentialsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".my-cli", "credentials");
});

services.AddHttpClient();           // IHttpClientFactory is required
services.AddAdobeAuthProvider();

// ... then hook up the accounts branch in your Spectre.Console configurator
// (from the core package):
//
//   config.AddAccountsBranch();
```

From that point on:

```
my-cli accounts add        # prompts for Adobe (and any other registered providers)
my-cli accounts list       # shows API key, IMS URL, base URL, masked client secret
my-cli accounts select <id>
my-cli accounts delete <id>
```

Resolving a token in consumer code:

```csharp
public sealed class AdobeMarketplaceClient(AdobeAuthenticationService auth, IHttpClientFactory http)
{
    public async Task<string> GetOrganizationsAsync()
    {
        var token = await auth.AuthenticateAsync();
        using var client = http.CreateClient();
        client.BaseAddress = token.BaseUrl;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(token.TokenType, token.AccessToken);
        client.DefaultRequestHeaders.Add("x-api-key", /* your API key */);
        return await client.GetStringAsync("/v3/organizations");
    }
}
```

---

## What gets stored

Each `accounts add` run serialises an `AdobeCredential` into the encrypted keystore:

| Field          | Source              | Notes                                                                             |
|----------------|---------------------|-----------------------------------------------------------------------------------|
| `ImsUrl`       | prompt (validated)  | Absolute http(s) URL, default `https://ims-na1.adobelogin.com/`. Change for EU / APAC regions (`ims-eu1`, …). |
| `ApiKey`       | plain prompt        | OAuth2 `client_id` — public, not masked                                           |
| `ClientSecret` | hidden prompt       | The actual secret                                                                 |
| `BaseUrl`      | prompt (validated)  | Absolute http(s) URL, default `https://partners.adobe.io/` for VIP Marketplace    |
| `Environment`  | selection prompt    | `Production` / `Sandbox`                                                          |

`accounts list` shows the API Key in plain text (it's a public identifier useful for disambiguating accounts) and masks the Client Secret as `xxxx...xxxx`.

---

## Authentication model

Adobe IMS issues short-lived bearer tokens via the OAuth2 **client-credentials** flow:

1. `AdobeAuthenticationService.AuthenticateAsync()` reads the selected credential.
2. It POSTs `grant_type=client_credentials&client_id=…&client_secret=…&scope=…` to `{ImsUrl}/ims/token/v3`.
3. IMS returns `{ "access_token": "…", "token_type": "bearer", "expires_in": 86400 }`.
4. The service wraps it in an `AdobeToken` with a 30-second clock-skew buffer on `IsExpired`.

The hardcoded scope set is `openid,AdobeID,read_organizations` — enough to list organisations the service account belongs to. **This is typically not sufficient for real VIP Marketplace operations** (transactions, SKU catalogue, projected product context). If your flow needs richer scopes, you'll currently need to wrap this service. Configurable scopes are a planned follow-up.

---

## Named HttpClient

The service resolves an `HttpClient` via `IHttpClientFactory.CreateClient("Adobe Authenticator")`. Consumers who want to pre-configure it (proxies, retry handlers, custom user-agent) can:

```csharp
services.AddHttpClient(AdobeAuthenticationService.HttpClientName, c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("my-cli/1.0");
});
```

The service does not mutate `BaseAddress` or `DefaultRequestHeaders` on the client — the token endpoint is passed as an absolute URI per request.

---

## Supported platforms

Whatever the core package supports (currently Windows, macOS, Linux on .NET 10).

---

## License

[MIT](../../LICENSE) © Stuart Meeks
