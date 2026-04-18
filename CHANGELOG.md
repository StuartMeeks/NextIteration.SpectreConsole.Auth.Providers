# Changelog

All notable changes to the three provider packages are documented in this file.

Each provider versions **independently** via per-package tag prefixes (`adobe-v*`, `airtable-v*`, `softwareone-v*`) — see [RELEASING.md](RELEASING.md) for mechanics. To date, all three have shipped in lockstep with identical version numbers; future releases may diverge, and sections below will call out per-package differences when that happens.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and each package adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[0.2.0]: https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases
[0.1.1]: https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases
[0.1.0]: https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases
