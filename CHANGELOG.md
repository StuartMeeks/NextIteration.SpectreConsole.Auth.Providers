# Changelog

All notable changes to the four provider packages are documented in this file.

Each provider versions **independently** via per-package tag prefixes (`adobe-v*`, `airtable-v*`, `softwareone-v*`, `github-v*`) — see [RELEASING.md](RELEASING.md) for mechanics. Releases may ship coordinated or per-package; sections below call out per-package differences when they diverge.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and each package adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Documentation

- **SoftwareOne's quick start now registers `IHttpClientFactory`.** The package README went
  straight from `AddCredentialStore` to `AddSoftwareOneAuthProvider()` with no
  `services.AddHttpClient()`, unlike Adobe's and GitHub's. `SoftwareOneCredentialCollector`
  takes `IHttpClientFactory` as its only constructor dependency — it looks the API token up
  against the Marketplace API at `accounts add` time — so a verbatim copy of the quick start
  built and started cleanly, then threw *"Unable to resolve service for type
  'System.Net.Http.IHttpClientFactory'"* the first time a user ran `accounts add`. Because
  the failure is at command-invocation time rather than container-build time, a startup
  smoke test did not catch it. The root README's claim that SoftwareOne is "pass-through"
  is corrected (true of its auth service, false of its collector), and
  `AddSoftwareOneAuthProvider` and `AddAdobeAuthProvider` gain the `<remarks>` naming the
  prerequisite that `AddGitHubAuthProvider` already had, so IntelliSense shows it at the
  DI entry point.

### Fixed

- **SoftwareOne: error-body redaction now covers the percent-encoded token.**
  `SanitiseErrorBody` replaced only the literal token value, but the request URL carries
  `Uri.EscapeDataString(apiToken)` — and an upstream proxy echoing that URL back into an
  error page is the exact threat the sanitiser's own comment names. Only tokens made
  purely of unreserved characters were actually redacted; a realistic Marketplace token
  (`idt:AbC+dEf/123=` → `idt%3AAbC%2BdEf%2F123%3D`) survived intact into the
  `InvalidOperationException` message that `accounts add` prints and log aggregators
  capture. The existing regression test used `tok-secret-12345`, which escapes to itself,
  so the suite was green on a case it did not cover.

- **All four collectors: every `accounts add` prompt now honours the cancellation token.**
  Six of the fourteen `AnsiConsole.PromptAsync` calls dropped it — Adobe's API-key and
  client-secret prompts, Airtable's access-token prompt, SoftwareOne's API-token prompt,
  and GitHub's client-id and scopes prompts. Adjacent prompts in the same method disagreed,
  and the split tracked code shape (an inline `.Validate(…)` lambda as the last chained
  member) rather than intent, which marks it as a mechanical edit that missed those sites.
  A host cancelling while the user sat at one of them was ignored: that prompt kept
  blocking on stdin and only the *next* prompt observed the cancellation. The 2.0.0 entry
  below claims the token was threaded "into … the Spectre prompts each collector runs";
  this makes that true.

- **GitHub: the host prompt now rejects anything that is not a bare `host[:port]`.**
  `ValidateHost` only checked `Uri.TryCreate($"https://{host}/")`, which parses almost
  any string, so its own error message ("Must be a bare host…") enforced nothing. A value
  carrying userinfo — `github.com@evil.example.com` — passed validation and produced an
  API base URL whose real host was `evil.example.com`, so the device-flow token, the
  token-bearing `GET /user` call and every later `client_id`+`refresh_token` refresh POST
  went to the attacker's host, while `accounts list` still rendered something that read as
  github.com at a glance. Pasted schemes (`https://ghe.example.com`, parsed with
  `Host == "https"`), paths, queries, fragments and malformed ports were accepted too.
  Validation now splits an optional numeric port (bracketed IPv6 literals included),
  requires `Uri.CheckHostName` to recognise the host, and re-checks the derived URI for
  stray userinfo, path, query or fragment.

- **GitHub: `accounts add` is cancellable again during the device-flow poll.**
  `GitHubCredentialCollector.CollectAsync` passed `CancellationToken.None` into
  `PollForTokenAsync` while forwarding its real token to every neighbouring call. The
  poll loop is fully cancellation-aware — it threads the token into both the interval
  delay and the token POST — so the literal `None` neutered all of it. A host cancelling
  `accounts add` after the device panel rendered was ignored: the loop kept POSTing
  GitHub's token endpoint every 5s until `expires_in` elapsed (900s on github.com),
  leaving the CLI apparently hung for up to 15 minutes.

---

## [2.0.0 / 2.0.0 / 2.0.0 / 2.0.0] — 2026-08-28

_Coordinated major release of all four providers, adopting core `NextIteration.SpectreConsole.Auth` 2.0.0._

### Breaking

- **Adopts core 2.0.0, which adds a `CancellationToken` to three provider-facing interfaces.**
  The dependency range moves from `[1.0.1,2.0.0)` to `[2.0.0,3.0.0)`, so **these providers
  require core 2.x and will not resolve against 1.x** — and 1.x providers will not resolve
  against core 2.x. That refusal is the cap doing its job: the providers call
  `ICredentialManager`, so a 1.x provider assembly running against core 2.x would fail at
  runtime with `MissingMethodException`. The version range turns that into an error you read
  at restore time instead.

- **`ICredentialCollector.CollectAsync`, `IAuthenticationService<,>`'s three members, and the
  `ICredentialManager` surface each take a trailing `CancellationToken cancellationToken =
  default`.** Consumers calling these providers keep compiling, since every added parameter is
  optional. Anyone who has *subclassed* or re-implemented one of these types must update their
  signatures.

### Changed

- **The token is threaded to where it can actually do something**, not merely accepted and
  dropped: into the core credential lookups, the Spectre prompts each collector runs, and the
  outgoing HTTP calls — Adobe's IMS token endpoint, GitHub's device-code and user-lookup
  requests, SoftwareOne's token lookup. The authentication layer is the one that waits on a
  network, so accepting a token there and ignoring it would have been the worst of both.

- **`xUnit1051` is suppressed in the four test projects**, matching the core package and for
  the same reason: it fires at 248 call sites the moment those interfaces accept a token, and
  threading one through every existing assertion would bury the change for a benefit —
  prompter teardown of sub-second tests — that does not apply here.

---

## [1.0.1 / 1.0.1 / 1.0.1 / 1.0.1] — 2026-08-22

_Coordinated patch release of all four providers (Adobe → 1.0.1, Airtable → 1.0.1, SoftwareOne → 1.0.1, GitHub → 1.0.1). Adopts core `NextIteration.SpectreConsole.Auth` 1.0.1; the remaining changes are CI-only and test-only. No public API or runtime behaviour change — consumers need no source changes._

### Changed
- **Core library floor raised to `[1.0.1,2.0.0)`.** Adopts
  `NextIteration.SpectreConsole.Auth` 1.0.1, a safe drop-in maintenance release over 1.0.0
  — no API or behaviour change (an internal null-check hardening in the file backend plus a
  CI-only move to buildless CodeQL). The floor tracks the version the providers are built
  and tested against (STANDARD.md §1.4); the `ICredentialCollector` /
  `ICredentialSummaryProvider` / `ICredentialManager` contracts the providers depend on are
  unchanged, so a provider consumer needs no source changes beyond referencing core 1.0.1.
  The upper cap is unchanged. Validated: clean build and full test suite green on `net8.0`
  and `net10.0`.
- **CodeQL C# analysis switched to `build-mode: none` (buildless).** The workflow's
  `paths-ignore: **/obj/**` was silently inert: GitHub applies path filters to a compiled
  language only when it is analysed without a build, so under the explicit `dotnet build`
  the xUnit auto-generated entry point in `obj/` was analysed and raised
  `cs/missed-ternary-operator` on every target framework. Buildless extraction honours the
  filter, so the generated entry point is genuinely excluded, and it reads source across all
  target frameworks at once. Synced from the corrected `NextIteration.Standards` template
  (§4.4). CI-only change; no package, public API, or behaviour change for consumers.

### Fixed
- **Removed redundant `(TCredential)null!` upcasts in the authentication-service tests**
  (Adobe, Airtable, SoftwareOne), resolving the `cs/useless-upcast` code-scanning alerts.
  Passing an argument already selects the single one-argument `AuthenticateAsync` overload,
  so the cast was unnecessary. Test-only change.

---

## [1.0.0 / 1.0.0 / 1.0.0 / 1.0.0] — 2026-08-21

_First stable release of all four providers (Adobe → 1.0.0, Airtable → 1.0.0, SoftwareOne → 1.0.0, GitHub → 1.0.0). A coordinated 1.0.0: the `ICredentialCollector`, `ICredentialSummaryProvider` and `IAuthenticationService<TCredential, TToken>` surfaces each provider ships are now covered by SemVer, so a breaking change to them will require a 2.0.0. The release adopts core `NextIteration.SpectreConsole.Auth` 1.0.0. Consumers already on the latest 0.x need no source changes; the version jump is a stability commitment, not a set of breaking changes._

### Changed
- **Core library floor raised to `[1.0.0,2.0.0)`.** `NextIteration.SpectreConsole.Auth`
  1.0.0 is the first stable core release; the provider packages now floor there and cap
  before the next major. The bump is a consumer-visible lift of the minimum core
  dependency. 1.0.0 adds the whole-store `accounts export` / `accounts import` feature,
  which lives entirely in the core over `ICredentialManager` (new
  `ExportCredentialsAsync` / `RestoreCredentialAsync` members) — the provider
  `ICredentialCollector` / `ICredentialSummaryProvider` contracts are unchanged, so a
  provider consumer needs no source changes beyond referencing core 1.0.0. The four test
  `FakeCredentialManager` doubles implement the two new interface members (they throw, in
  line with the existing "fail loudly" doubles; the export/import path is exercised by the
  core suite, not here). The upper cap moves from `1.0.0`-exclusive to `2.0.0`-exclusive so
  a breaking core 2.0 does not silently flow into provider consumers.
- **Test stack migrated from xUnit.net v2 (VSTest) to xUnit.net v3 on Microsoft.Testing.Platform.**
  `xunit` 2.9.3 is deprecated and terminal; the suite now uses `xunit.v3` 4.0.0.
  `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio` and `coverlet.collector`
  are removed — all three are VSTest-only, and the .NET 10 SDK no longer runs MTP
  test projects through the legacy VSTest target. A root `global.json` opts
  `dotnet test` into the MTP runner. Test-only change; no package, public API, or
  behaviour change for consumers.

### Repository
- **Re-aligned with the revised `NextIteration.Standards` baseline.** Adopted the canonical
  allow-list `.editorconfig` (§5.2) and turned on `EnforceCodeStyleInBuild` (§1.2.1), so the
  house style — braces always, block-scoped namespaces, `var` throughout, explicit
  accessibility, the naming ruleset, and public-API XML docs (CS1591) — now gates the build
  under `TreatWarningsAsErrors`. Brought all four packages and their test suites to
  style-green; the four test projects add `IDE0005` to `NoWarn` (that rule needs a doc file
  a test project does not generate — the estate-wide §2.7 amendment). No package, public
  API, or runtime-behaviour change for consumers.

---

## [0.4.0 / 0.4.0 / 0.5.0 / 0.2.0] — 2026-07-24

_Adobe → 0.4.0, Airtable → 0.4.0, SoftwareOne → 0.5.0, GitHub → 0.2.0. Coordinated minor release: per-target-framework dependency floors for the runtime-aligned Microsoft platform packages. No public API or behaviour changes._

### Changed
- **Per-target-framework floors for the runtime-aligned Microsoft deps.**
  `Microsoft.Extensions.DependencyInjection.Abstractions` and
  `Microsoft.Extensions.Http` are now floored per target framework instead of
  at a single high major: the `net8.0` dependency group floors at `8.0.x`
  (`8.0.2` / `8.0.1`) and `net10.0` at `10.0.x` (`10.0.10`). In a library a
  `PackageReference` version is a *minimum* NuGet forces on every downstream
  consumer, so a single `10.0.x` floor dragged `net8.0` (LTS) consumers off
  their own runtime-aligned servicing line; each target now floors at the
  latest servicing release of its own major.
- **Core library floor raised to `[0.7.1,1.0.0)`.**
  `NextIteration.SpectreConsole.Auth` 0.7.1 is the first core release to
  declare the same per-target-framework floors (its `net8.0` group floors at
  `8.0.x`). Against 0.7.0 — whose `net8.0` group still floored at `10.0.9` —
  the provider `net8.0` floors above would resolve to a package downgrade. The
  upper cap on the next major is unchanged.
- **`Spectre.Console` floor raised `0.56.0` → `0.57.2`.** This tracks the
  version the core 0.7.1 surface builds against. It stays a single common
  floor (not split per target): as a pre-1.0 package it can make breaking
  changes between minors, so flooring it low would be both meaningless and
  risky.

### Migration notes
- Consumer apps need no source changes. `net10.0` consumers resolve equivalent
  or newer floors than before. `net8.0` consumers are now floored at the
  aligned `8.0.x` Microsoft platform packages instead of `10.0.9`, keeping them
  on their own runtime servicing line.

---

## GitHub — [0.1.0] — 2026-06-20

_Initial release of `NextIteration.SpectreConsole.Auth.Providers.GitHub`._

### Added
- **GitHub provider with OAuth device flow.** Ships `GitHubCredential`,
  `GitHubToken`, `GitHubAuthenticationService`, the `accounts add` collector,
  and the `accounts list` summary provider. The collector runs the OAuth device
  flow — the same flow `gh auth login` uses by default — prompting for the GitHub
  host, the OAuth App client id, and the requested scopes, then polling the token
  endpoint (honouring the server `interval`, `slow_down` back-off, and
  `authorization_pending`) until the user authorises in the browser. Once a token
  is obtained it is validated and enriched via `GET /user`.
- **Configurable host.** Defaults to `github.com`; entering a GitHub Enterprise
  Server host derives the matching web (`https://{host}/`) and REST API
  (`https://{host}/api/v3/`) base URLs.
- **Token refresh.** For OAuth Apps that issue expiring user tokens, an expired
  access token is refreshed via `grant_type=refresh_token` before use (a 30-second
  clock-skew buffer guards the expiry check). Classic non-expiring tokens are
  passed straight through. Note: a refreshed token is not yet persisted back to
  the keystore — see the package README.
- Multi-targets `net8.0` and `net10.0`, in line with the other providers.

---

## [0.3.0 / 0.3.0 / 0.4.0] — 2026-06-20

_Adobe → 0.3.0, Airtable → 0.3.0, SoftwareOne → 0.4.0. Coordinated minor release: multi-targeting adds a new `net8.0` target framework. No public API or behaviour changes._

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

[1.0.0 / 1.0.0 / 1.0.0 / 1.0.0]: https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases
[0.4.0 / 0.4.0 / 0.5.0 / 0.2.0]: https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases
[0.3.0 / 0.3.0 / 0.4.0]: https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases
[0.2.3 / 0.2.3 / 0.3.3]: https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases
[0.2.2 / 0.2.2 / 0.3.2]: https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases
[0.2.1 / 0.2.1 / 0.3.1]: https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases
[0.3.0]: https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases
[0.2.0]: https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases
[0.1.1]: https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases
[0.1.0]: https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases
