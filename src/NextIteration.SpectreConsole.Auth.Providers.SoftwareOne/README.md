# NextIteration.SpectreConsole.Auth.Providers.SoftwareOne

[![NuGet](https://img.shields.io/nuget/v/NextIteration.SpectreConsole.Auth.Providers.SoftwareOne.svg)](https://www.nuget.org/packages/NextIteration.SpectreConsole.Auth.Providers.SoftwareOne/)
[![Downloads](https://img.shields.io/nuget/dt/NextIteration.SpectreConsole.Auth.Providers.SoftwareOne.svg)](https://www.nuget.org/packages/NextIteration.SpectreConsole.Auth.Providers.SoftwareOne/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![CI](https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/actions/workflows/ci.yml/badge.svg)](https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/actions/workflows/ci.yml)

SoftwareOne Marketplace credential provider for [**NextIteration.SpectreConsole.Auth**](https://www.nuget.org/packages/NextIteration.SpectreConsole.Auth).

Drops a ready-to-use `SoftwareOneCredential`, `SoftwareOneToken`, authentication service, Spectre.Console `accounts add` prompt collector, and `accounts list` display formatter into a CLI that already uses the core auth package.

---

## Install

```bash
dotnet add package NextIteration.SpectreConsole.Auth.Providers.SoftwareOne
```

Requires the core package (`NextIteration.SpectreConsole.Auth` ≥ 0.4.1) — NuGet pulls it in transitively.

---

## Quick start

```csharp
using NextIteration.SpectreConsole.Auth;
using NextIteration.SpectreConsole.Auth.Providers.SoftwareOne;

services.AddCredentialStore(opts =>
{
    opts.CredentialsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".my-cli", "credentials");
});

services.AddSoftwareOneAuthProvider();

// ... then hook up the accounts branch in your Spectre.Console configurator
// (from the core package):
//
//   config.AddAccountsBranch();
```

From that point on:

```
my-cli accounts add        # prompts for SoftwareOne (and any other registered providers)
my-cli accounts list       # shows stored credentials with Actor, Base URL, masked token
my-cli accounts select <id>
my-cli accounts delete <id>
```

Resolving a token in consumer code:

```csharp
public sealed class MarketplaceClient(SoftwareOneAuthenticationService auth, IHttpClientFactory http)
{
    public async Task<string> GetOrdersAsync()
    {
        var token = await auth.AuthenticateAsync();
        using var client = http.CreateClient();
        client.BaseAddress = token.BaseUrl;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(SoftwareOneToken.TokenType, token.ApiToken);
        // ... token.Actor / token.Environment let you branch on which flow to use
        return await client.GetStringAsync($"/v1/{token.Actor.ToLowerInvariant()}/orders");
    }
}
```

---

## What gets stored

Each `accounts add` run serialises a `SoftwareOneCredential` into the encrypted keystore:

| Field         | Source              | Notes                                         |
|---------------|---------------------|-----------------------------------------------|
| `ApiToken`    | hidden prompt       | Long-lived portal-issued token                |
| `BaseUrl`     | prompt (validated)  | Absolute http(s) URL, default `https://api.softwareone.com/` |
| `Environment` | selection prompt    | `Production` / `Staging` / `Test`             |
| `Actor`       | selection prompt    | `Operations` / `Vendor`                       |

The token never leaves the credential store unencrypted. `accounts list` renders a masked fingerprint (first four + last four characters) and four stars for anything shorter than 10 chars.

---

## Authentication model

SoftwareOne tokens are **long-lived** and issued out-of-band via the Marketplace portal — there's no exchange or refresh flow. `SoftwareOneAuthenticationService.AuthenticateAsync()` is therefore a pass-through: it deserialises the selected credential and projects it into a `SoftwareOneToken`. `IsExpired` is hard-coded to `false`, but tokens **can be revoked** in the portal — a revoked token surfaces as a 401 on the first API call. Consumers should treat 401 as "prompt for a fresh credential" rather than relying on `IsExpired`.

---

## Supported platforms

Whatever the core package supports (currently Windows, macOS, Linux on .NET 10).

---

## References

- **SoftwareOne Marketplace REST API** — <https://docs.platform.softwareone.com/developer-resources/rest-api>
  Endpoints, request/response shapes, authentication model, and how to generate API tokens from the Marketplace portal.

---

## License

[MIT](../../LICENSE) © Stuart Meeks
