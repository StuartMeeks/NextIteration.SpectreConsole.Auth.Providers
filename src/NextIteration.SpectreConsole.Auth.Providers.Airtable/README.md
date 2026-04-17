# NextIteration.SpectreConsole.Auth.Providers.Airtable

[![NuGet](https://img.shields.io/nuget/v/NextIteration.SpectreConsole.Auth.Providers.Airtable.svg)](https://www.nuget.org/packages/NextIteration.SpectreConsole.Auth.Providers.Airtable/)
[![Downloads](https://img.shields.io/nuget/dt/NextIteration.SpectreConsole.Auth.Providers.Airtable.svg)](https://www.nuget.org/packages/NextIteration.SpectreConsole.Auth.Providers.Airtable/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![CI](https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/actions/workflows/ci.yml/badge.svg)](https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/actions/workflows/ci.yml)

Airtable credential provider for [**NextIteration.SpectreConsole.Auth**](https://www.nuget.org/packages/NextIteration.SpectreConsole.Auth).

Drops a ready-to-use `AirtableCredential`, `AirtableToken`, authentication service, Spectre.Console `accounts add` prompt collector, and `accounts list` display formatter into a CLI that already uses the core auth package.

---

## Install

```bash
dotnet add package NextIteration.SpectreConsole.Auth.Providers.Airtable
```

Requires the core package (`NextIteration.SpectreConsole.Auth` ≥ 0.4.1) — NuGet pulls it in transitively.

---

## Quick start

```csharp
using NextIteration.SpectreConsole.Auth;
using NextIteration.SpectreConsole.Auth.Providers.Airtable;

services.AddCredentialStore(opts =>
{
    opts.CredentialsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".my-cli", "credentials");
});

services.AddAirtableAuthProvider();

// ... then hook up the accounts branch in your Spectre.Console configurator
// (from the core package):
//
//   config.AddAccountsBranch();
```

From that point on:

```
my-cli accounts add        # prompts for Airtable (and any other registered providers)
my-cli accounts list       # shows the masked token
my-cli accounts select <id>
my-cli accounts delete <id>
```

Resolving a token in consumer code:

```csharp
public sealed class AirtableClient(AirtableAuthenticationService auth, IHttpClientFactory http)
{
    public async Task<string> GetBasesAsync()
    {
        var token = await auth.AuthenticateAsync();
        using var client = http.CreateClient();
        client.BaseAddress = token.BaseUrl;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(AirtableToken.TokenType, token.AccessToken);
        return await client.GetStringAsync("/v0/meta/bases");
    }
}
```

---

## What gets stored

Each `accounts add` run serialises an `AirtableCredential` into the encrypted keystore:

| Field         | Source              | Notes                                                      |
|---------------|---------------------|------------------------------------------------------------|
| `AccessToken` | hidden prompt       | Airtable Personal Access Token (PAT)                       |
| `Environment` | selection prompt    | `Production` / `Staging` / `Test`                          |

The token never leaves the credential store unencrypted. `accounts list` renders a masked fingerprint (first four + last four characters) and four stars for anything shorter than 10 chars.

---

## Authentication model

Airtable Personal Access Tokens are **long-lived** and created manually in the Airtable account UI. Scopes (e.g. `data.records:read`, `schema.bases:read`) and base-access restrictions are set at token-creation time. There is no exchange or refresh flow, so `AirtableAuthenticationService.AuthenticateAsync()` is a pass-through: it deserialises the selected credential, validates it, and projects it into an `AirtableToken`. `IsExpired` is hard-coded to `false`, but PATs **can be revoked** from the Airtable UI — a revoked token surfaces as a 401 on the first API call. Consumers should treat 401 as "prompt for a fresh credential" rather than relying on `IsExpired`.

`AirtableAuthenticationService.ApiBaseUrl` is exposed as a static field holding `https://api.airtable.com/` — the token's `BaseUrl` is initialised from it.

---

## Supported platforms

Whatever the core package supports (currently Windows, macOS, Linux on .NET 10).

---

## License

[MIT](../../LICENSE) © Stuart Meeks
