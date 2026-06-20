# Changelog

All notable changes to the three provider packages are documented in this file.

Each provider versions **independently** via per-package tag prefixes (`adobe-v*`, `airtable-v*`, `softwareone-v*`) — see [RELEASING.md](RELEASING.md) for mechanics. To date, all three have shipped in lockstep with identical version numbers; future releases may diverge, and sections below will call out per-package differences when that happens.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and each package adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [0.2.4 / 0.2.4 / 0.3.4] — 2026-06-20

_Adobe → 0.2.4, Airtable → 0.2.4, SoftwareOne → 0.3.4. Coordinated maintenance release: multi-targeting. No public API or behaviour changes._

### Changed
- **Multi-target `net8.0` and `net10.0`.** All three provider packages now
  ship `lib/net8.0/` and `lib/net10.0/` assets (previously net10.0 only),
  matching the core `NextIteration.SpectreConsole.Auth` 0.7.0 and
  `Spectre.Console` 0.56.0 surfaces. Consumers on an `net8.0` target framework
  can now reference the providers without being forced onto net10.0. The shared
  source compiles unchanged against both targets; there are no API or behaviour
  differences between the two assemblies.
- **Core library floor raised to `[0.7.0,1.0.0)`.** `NextIteration.SpectreConsole.Auth`
  first publishes a `net8.0` asset in 0.7.0, so an `net8.0` build of the
  providers requires at least that version. The upper cap on the next major is
  unchanged.

### Migration notes
- Consumer apps need no source changes. Existing net10.0 consumers resolve the
  `net10.0` asset exactly as before.

---

## [0.2.3 / 0.2.3 / 0.3.3] — 2026-06-10

_Adobe → 0.2.3, Airtable → 0.2.3, SoftwareOne → 0.3.3. Coordinated maintenance release: dependency refresh plus a move to keyless publishing. No public API or behaviour changes._

### Changed
- **Dependencies bumped to latest stable.** Runtime dependencies shipped in
  the provider packages: `Microsoft.Extensions.DependencyInjection.Abstractions`
  and `Microsoft.Extensions.Http` 10.0.5 → 10.0.9, `Spectre.Console`
  0.55.2 → 0.56.0. Build/test tooling: `Microsoft.SourceLink.GitHub`
  8.0.0 → 10.0.300, `Microsoft.NET.Test.Sdk` 17.11.1 → 18.6.0, `xunit`
  2.9.2 → 2.9.3, `xunit.runner.visualstudio` 2.8.2 → 3.1.5,
  `coverlet.collector` 6.0.2 → 10.0.1. The capped `NextIteration.SpectreConsole.Auth`
  range (`[0.6.1,1.0.0)`) is intentionally left unchanged.
- **Publishing switched to NuGet trusted publishing (OIDC).** The release
  workflow no longer uses a long-lived `NUGET_API_KEY` secret. The `publish`
  job requests a GitHub OIDC token (`id-token: write`) and exchanges it via
  `NuGet/login@v1` for a short-lived (1-hour) nuget.org API key at push time.
  See [RELEASING.md](RELEASING.md) for the one-time nuget.org policy and
  `NUGET_USER` secret setup. Packaging is unchanged — consumers see no
  difference.

---

## [0.2.2 / 0.2.2 / 0.3.2] — 2026-05-03

_Adobe → 0.2.2, Airtable → 0.2.2, SoftwareOne → 0.3.2. Coordinated patch release across the four sibling repos to fix symbol-package publishing._

### Changed
- **Symbol packaging.** Switched `<DebugType>` from `embedded` to `portable`
  in all three provider csprojs so the published `.snupkg` actually
  contains `.pdb` files. The previous combination produced an empty
  `.snupkg`; nuget.org rejects empty symbol packages with HTTP 400.
  Until now the workflow's `upload-artifact` filter (`*.nupkg`) silently
  dropped the broken symbol package on its way to the publish job, so
  the failure stayed invisible — but no symbols ever reached nuget.org's
  symbol server. Consumers debugging into any of the providers now get
  sources via the symbol server out of the box.
- **CI artifact path.** `upload-artifact` now captures `*nupkg` (both
  `.nupkg` and `.snupkg`) so the publish job pushes both files for the
  package matching the tag prefix.

---

## [0.2.1 / 0.2.1 / 0.3.1] — 2026-05-03

_Adobe → 0.2.1, Airtable → 0.2.1, SoftwareOne → 0.3.1. Coordinated patch release driven by an external security review._

### Security
- **Reject plain `http` for credential-bearing endpoints.** The Adobe and SoftwareOne collectors and authentication services now require `https` for the IMS URL, the Adobe API base URL, and the SoftwareOne API base URL. `http` is accepted only when the host is a loopback address, so local mock servers and proxies still work during development. The same check runs in the authentication services on every call, so a hand-edited keystore that downgrades a stored credential to `http` is rejected before any request is sent. This closes the specific risk that the SoftwareOne collector ships the API token in the URL query (`eq(token,'…')`) — over plain `http` that token would otherwise traverse the network in cleartext and land in any intermediate access log.
- **Sanitise response bodies before they reach exception messages.** When the SoftwareOne lookup or the Adobe IMS exchange returns a non-success status, the error path used to inline the raw response body verbatim. Both providers now truncate the body to 512 characters before constructing the exception, and the SoftwareOne path additionally redacts the literal token value out of the body — defending against a misbehaving upstream proxy that echoes the request URL (which carries the token in the query string) into an error page that would otherwise reach exception aggregators and log files.
- Airtable carries no URL prompt and made no IMS-style call, so its 0.2.1 picks up the cross-cutting DI / packaging fixes only.

### Changed
- **Register the `IAuthenticationService<TCredential, TToken>` interface mapping** for all three providers. Previously only the concrete `XxxAuthenticationService` was registered with DI, so consumers depending on the abstraction got a runtime resolution failure. The interface registration forwards to the same singleton instance, so this is purely additive — existing consumers depending on the concrete type are unaffected.
- **Core library reference bumped from `[0.5.0,1.0.0)` to `[0.6.1,1.0.0)`.** Picks up the latest core release published to nuget.org. The cap stays on the next major so a breaking 1.0 of the core package doesn't auto-flow into provider consumers — bump the upper bound deliberately when validating against the next major.

### Fixed
- **Stop auto-packing on every build.** The csprojs previously set `GeneratePackageOnBuild=true` with a hardcoded Windows-only `PackageOutputPath` (`C:\nuget-local\`). On non-Windows hosts that path was interpreted as a project-relative directory called `C:\nuget-local\`; on every host the auto-pack ran during `dotnet test`, which is wasteful and meant ordinary local development was producing release nupkgs as a side effect. Both properties are removed; CI now invokes `dotnet pack` explicitly per provider.

### Migration notes
- Consumer apps need no source changes. The DI registration is purely additive; the `https`-only enforcement only rejects URLs you should not have been using to begin with.
- If you were relying on the auto-generated nupkgs landing in the project tree from a local build, switch to `dotnet pack <project> --output ./artifacts` instead.

---

## SoftwareOne — [0.3.0] — 2026-04-18

_Applies to `NextIteration.SpectreConsole.Auth.Providers.SoftwareOne` only. Adobe and Airtable remain at 0.2.0._

### Added
- **Automatic token validation at `accounts add` time.** The collector now performs a live `GET {BaseUrl}/v1/accounts/api-tokens?eq(token,'…')&limit=2` against the SoftwareOne Marketplace API during add. If the lookup returns exactly one match the credential is stored; zero matches or multiple matches or any HTTP/transport error fails the command and the credential is **not** persisted.
- **Credential enriched with Marketplace metadata**: five new required fields — `TokenId`, `TokenName`, `AccountId`, `AccountName`, `AccountType` — populated from the validated lookup response. The `accounts list` display now shows `Account` (`name (type)`) and `Token` (`masked fingerprint — tokenName`) as part of the primary identity.
- **`SoftwareOneCredentialCollector.HttpClientName` const** (`"SoftwareOne Credential Validator"`) so consumers can pre-configure the named `HttpClient` (proxy, retry handler, user-agent).

### Changed (breaking)
- `SoftwareOneCredentialCollector` constructor now requires `IHttpClientFactory`. Consumers must call `services.AddHttpClient()` before `services.AddSoftwareOneAuthProvider()`.
- `SoftwareOneCredential`'s five new metadata fields are `required` — **0.2.0-era keystores will fail to deserialize under 0.3.0** with a `JsonException` for missing required members. Migration = delete the old `Selections.json` + provider-specific credential files and re-run `accounts add` to re-register; the new store will be populated with the validated metadata.

### Migration notes
- Consumer apps must `services.AddHttpClient()` if they weren't already.
- Users with stored 0.2.0 credentials must re-add. There's no auto-migration from the old schema because the new required metadata fields can't be inferred from the old credential alone — they come from the live API call.

---

## [0.2.0] — 2026-04-18

_Applies to Adobe, Airtable, and SoftwareOne._

### Changed
- **Spectre.Console** upgraded from 0.54.0 to **0.55.2** across all three providers.
- **Core library reference** bumped from `[0.4.1,1.0.0)` to `[0.5.0,1.0.0)`. This lower-bound lift is necessary because core 0.5.0 is itself compiled against Spectre 0.55.x; older core versions would drag consumers back to Spectre 0.54 and re-introduce the `TypeLoadException` on `Spectre.Console.Style` that originally motivated this release.

### Migration notes
- Consumer apps already referencing Spectre 0.55.x need no source changes — they get the fix for free once they bump `NextIteration.SpectreConsole.Auth.Providers.*` to 0.2.0.
- Consumer apps pinned to Spectre 0.54.x must upgrade to 0.55.x and bump both the core and provider references together.

---

## [0.1.1] — 2026-04-17

_Applies to Adobe, Airtable, and SoftwareOne._

### Changed
- Refreshed package icons across all three providers (shield-in-circle visual family shared with the core `NextIteration.SpectreConsole.Auth` package).

---

## [0.1.0] — 2026-04-17

_Initial release of all three provider packages._

### Added

#### `NextIteration.SpectreConsole.Auth.Providers.Adobe`
_Adobe VIP Marketplace — OAuth2 client-credentials flow against Adobe IMS._

- `AdobeCredential` — IMS URL, API key (OAuth2 `client_id`), client secret, base URL, environment (`Production` / `Sandbox`).
- `AdobeToken` — short-lived bearer token with a **30-second clock-skew buffer** on `IsExpired` (fires as expired ahead of the exact boundary to avoid the check-then-use race).
- `AdobeAuthenticationService` — POSTs `grant_type=client_credentials` to `ims/token/v3`; propagates the IMS response body into `HttpRequestException` on failure (no more opaque status-code-only errors).
- **Token-type normalisation**: IMS returns lowercase `"bearer"`; the library projects it to TitleCase `"Bearer"` for consumers whose downstream HTTP servers gate on exact scheme casing.
- **Runtime credential validation** (`ValidateCredential`): rejects whitespace-only `ApiKey` / `ClientSecret` / `Environment` before any HTTP call.
- **Named HttpClient** — `AdobeAuthenticationService.HttpClientName = "Adobe Authenticator"`. Consumers pre-configure via `services.AddHttpClient(AdobeAuthenticationService.HttpClientName, c => …)`.
- **Does not mutate HttpClient state** — absolute-URI request rather than `BaseAddress` assignment; concurrency-safe across shared named clients.
- Collector prompts validate both URLs as absolute `http`/`https`, and both required fields as non-empty. The API Key prompt is plain-text (it's a public client_id; hiding it would mask typos).
- Summary-provider shows API Key in plain, Client Secret masked (`xxxx...xxxx`, or `****` for short inputs that would leak length).
- **Known limitation (documented)**: hardcoded scopes `openid,AdobeID,read_organizations` — sufficient for listing organisations, typically not sufficient for real VIP Marketplace operations (SKU catalogue, transactions). Configurable scopes are a planned follow-up.

#### `NextIteration.SpectreConsole.Auth.Providers.Airtable`
_Airtable Personal Access Token — pass-through._

- `AirtableCredential` — access token, environment (`Production` / `Staging` / `Test`).
- `AirtableToken` with `BaseUrl` set from `AirtableAuthenticationService.ApiBaseUrl` (hardcoded `https://api.airtable.com/`) — shape matches the other providers so consumer code is portable.
- `AirtableToken.TokenType` as `public const string = "Bearer"`.
- `AirtableAuthenticationService.ApiBaseUrl` exposed as `public static readonly Uri` so consumers can reference the constant rather than hardcode the URL.
- Same `ValidateCredential` pattern as Adobe (rejects whitespace-only fields at auth time).
- Collector with non-empty validation on the access-token prompt.

#### `NextIteration.SpectreConsole.Auth.Providers.SoftwareOne`
_SoftwareOne Marketplace API token — pass-through._

- `SoftwareOneCredential` — API token, base URL (validated `http(s)` absolute), environment (`Production` / `Staging` / `Test`), actor (`Operations` / `Vendor`).
- `SoftwareOneToken` — pass-through with `TokenType` as `public const string = "Bearer"`.
- `SoftwareOneAuthenticationService` — projects credential into token, no network call.
- Links to the [SoftwareOne Marketplace REST API docs](https://docs.platform.softwareone.com/developer-resources/rest-api) in the per-provider README.

### Cross-cutting infrastructure
- Each provider: sealed public types; shared `XxxCredential.JsonOptions` (camelCase naming policy + indented output) as the single source of on-disk keystore-format truth.
- Per-provider xUnit test suites: Adobe 42 tests, Airtable 34 tests, SoftwareOne 30 tests — all three cover credential JSON round-trip, token projection, auth-service happy/error paths, summary-provider formatting + malformed-JSON defence, and DI registration.
- Per-package README with install, quick start, stored-fields table, consumer snippet, authentication model notes.
- Per-package NuGet metadata: MIT license expression, SourceLink, deterministic builds, embedded symbols, snupkg, capped version ranges for cross-package dependencies.
- GitHub Actions CI with per-package tag-triggered publishing (`adobe-v*` → publishes Adobe only, etc.).

[0.2.1 / 0.2.1 / 0.3.1]: https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases
[0.2.0]: https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases
[0.1.1]: https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases
[0.1.0]: https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases
