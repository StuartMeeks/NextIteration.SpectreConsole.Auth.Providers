# NextIteration.SpectreConsole.Auth.Providers

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![CI](https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/actions/workflows/ci.yml/badge.svg)](https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/actions/workflows/ci.yml)

Provider packages for [**NextIteration.SpectreConsole.Auth**](https://www.nuget.org/packages/NextIteration.SpectreConsole.Auth) — the generic credential-storage and Spectre.Console command layer for .NET CLI tools.

Each package here ships the concrete types for one third-party service: its `ICredential`, `IToken`, `IAuthenticationService`, the `ICredentialCollector` that drives the interactive `accounts add` prompt, and an `ICredentialSummaryProvider` that renders safe display fields for `accounts list`. Drop a package in, call one DI extension, and the `accounts` branch of your CLI picks it up.

---

## Providers

| Package                                                              | Service                                       | Status      |
|----------------------------------------------------------------------|-----------------------------------------------|-------------|
| `NextIteration.SpectreConsole.Auth.Providers.SoftwareOne`            | SoftwareOne Marketplace API                   | Ready       |
| `NextIteration.SpectreConsole.Auth.Providers.Adobe`                  | Adobe VIP Marketplace API (OAuth2 via Adobe IMS) | Ready       |
| `NextIteration.SpectreConsole.Auth.Providers.Airtable`               | Airtable API (Personal Access Token)          | Ready       |

Each project has its own README with install instructions, the prompts the collector runs, the fields it stores, and the authentication model (refresh vs. pass-through) — see [`src/`](src).

---

## How a provider fits in

The core package (`NextIteration.SpectreConsole.Auth`) gives you:

- An encrypted credential store on disk, DPAPI on Windows, macOS Keychain, or Linux libsecret.
- A Spectre.Console `accounts` branch with `add | list | select | delete` subcommands.
- Extensibility points: `ICredentialCollector`, `ICredentialSummaryProvider`, `IAuthenticationService<TCredential, TToken>`.

A provider package plugs into those extensibility points for one specific service. You can register as many providers as you want in the same CLI — the `accounts add` prompt lets the user pick one at runtime.

```csharp
using NextIteration.SpectreConsole.Auth;
using NextIteration.SpectreConsole.Auth.Providers.Adobe;
using NextIteration.SpectreConsole.Auth.Providers.Airtable;
using NextIteration.SpectreConsole.Auth.Providers.SoftwareOne;

services.AddCredentialStore(opts =>
{
    opts.CredentialsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".my-cli", "credentials");
});

// Adobe's auth service hits Adobe IMS, so it needs IHttpClientFactory.
// The other two are pass-through and don't require it — but registering
// it once is harmless.
services.AddHttpClient();

// One line per provider you want to support:
services.AddAdobeAuthProvider();
services.AddAirtableAuthProvider();
services.AddSoftwareOneAuthProvider();

// And register the accounts branch against your Spectre.Console configurator:
//     config.AddAccountsBranch();
```

From a consumer:

```
my-cli accounts add             # provider prompt shows every registered provider
my-cli accounts list            # masked display fields from each ICredentialSummaryProvider
my-cli accounts select <id>
my-cli accounts delete <id>
```

---

## Repository layout

```
/
├── src/                   ← provider packages, one folder per provider
├── tests/                 ← xUnit test projects (one per provider)
└── NextIteration.SpectreConsole.Auth.Providers.slnx
```

---

## Contributing a new provider

The canonical recipe is one of the existing provider projects under [`src/`](src). In short:

1. New class library targeting the same framework as the core package.
2. `PackageReference` to `NextIteration.SpectreConsole.Auth` at the current minor.
3. Types: `XxxCredential : ICredential`, `XxxToken : IToken`, `XxxAuthenticationService : IAuthenticationService<XxxCredential, XxxToken>`, `XxxCredentialCollector : ICredentialCollector`, `XxxCredentialSummaryProvider : ICredentialSummaryProvider`.
4. A DI extension `AddXxxAuthProvider(this IServiceCollection)` that registers all of the above.
5. A per-provider README that documents the prompts, stored fields, and authentication model.
6. Tests covering credential round-trip, token projection, and summary-provider display fields.

---

## License

MIT © Stuart Meeks
