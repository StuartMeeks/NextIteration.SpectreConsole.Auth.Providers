# CLAUDE.md — NextIteration.SpectreConsole.Auth.Providers

## This package

Four credential-provider packages for CLI tools built on
`NextIteration.SpectreConsole.Auth`: **Adobe** (VIP Marketplace, OAuth2
client-credentials against Adobe IMS), **Airtable** (personal-access tokens),
**GitHub** (OAuth device flow, GitHub.com and Enterprise Server), and **SoftwareOne**.
Each ships an `ICredentialCollector` that drives the `accounts add` prompt and an
`ICredentialSummaryProvider` that renders `accounts list`, plus a `ServiceCollectionExtensions`
registration method. They collect credentials; the core Auth package owns encryption
and on-disk storage. They consume it through a capped range (`[0.7.1,1.0.0)`).

## Things that are easy to get wrong here

- **This is the estate's only multi-package repo.** The four packages version
  **independently** and release through **per-package tag prefixes** (`adobe-v*`,
  `airtable-v*`, `softwareone-v*`, `github-v*`). `ci.yml`'s `publish` job packs all four
  but deletes every artifact except the tagged package's before pushing, so a tag ships
  exactly its own package. That extra step is a documented deviation from the canonical
  workflow (`NextIteration.Standards` EXCEPTIONS.md §3.0.1 and §3.2) — keep it, and keep
  the rest of the job identical to the template.
- **The core dependency is a capped range, `[0.7.1,1.0.0)`.** The cap stops a `1.0.0`
  silently changing the `ICredentialCollector` / `ICredentialSummaryProvider` contracts
  underneath the providers. Floored at 0.7.1 (the first core release with per-TFM floors);
  against 0.7.0 the provider floors resolve to a downgrade. Bump the upper bound only after
  validating against the next core major.
- **Per-TFM floors are deliberate.** `Microsoft.Extensions.DependencyInjection.Abstractions`
  and `Microsoft.Extensions.Http` floor at 8.0.x for `net8.0` and 10.0.x for `net10.0`.
  Raising the net8 floor to a 10.x version drags every net8 LTS consumer off its servicing
  line. Dependabot is configured never to propose it; do not do it by hand.
- **Collectors and summary providers register against the core interfaces.** They are
  resolved via `IEnumerable<ICredentialCollector>` / `IEnumerable<ICredentialSummaryProvider>`,
  so a registration bug shows up as a provider silently missing from `accounts add`/`list`,
  not as a compile error. The `ServiceCollectionExtensions` tests guard this.

## Repository baseline

This repo conforms to
[NextIteration.Standards](https://github.com/StuartMeeks/NextIteration.Standards).
Build properties, test stack, CI shape, and branch protection are defined there, not
here. Before changing any of those, read `STANDARD.md`; if this repo needs to deviate,
that is an `EXCEPTIONS.md` entry in the standards repo, not a local difference.

## Non-negotiables

- **The build must be clean.** `TreatWarningsAsErrors` is on and analyzers run at
  `latest`. A warning is a build failure.
- **Tests must pass on every shipped target framework** (`net8.0` and `net10.0`). A change
  that only passes on one is not finished. Shipping a target you do not test is a defect,
  not a scoping decision.
- **Dependency floors are deliberate and per-TFM.** A `PackageReference` version in a
  library is a *minimum* NuGet forces on every consumer, so raising a floor is a
  consumer-visible change even when nothing in the code needs it. Never raise one to
  silence a warning.
- **Public API changes need XML docs.** `GenerateDocumentationFile` is on and the public
  surface is fully documented.
- **Update `CHANGELOG.md`** under `[Unreleased]`, saying what changed and why.

## Dependabot

Minor and patch updates auto-merge behind CI. Major updates stay open for a human — that
is deliberate, not a backlog to clear. The two per-TFM floored packages have major updates
suppressed entirely via `ignore`; bump those by hand when a new .NET major lands.

## After opening a pull request

Watch CI to completion, report the real check results, then **offer to merge** in the same
message. Do not stop silently and wait to be asked.

- If branch protection blocks the merge, say so and offer `gh pr merge --admin`. These
  repos require a code-owner review only the maintainer can give, which is why `--admin` is
  the tool — but that mechanic is not the reason the offer is wanted. The reason is simply
  that the maintainer has grown comfortable delegating this to an agent, so treat the
  latest instruction as authoritative over this file.
- **Merge only on an explicit yes.** The offer is pre-approved; the action is not.
- Never offer while checks are failing or still running. Report that state instead.
- Report the checks that actually ran. A skipped check is not a passing check, and branch
  protection treats them differently from how they read in a summary.

## CI

The single required status check is `ci` — an aggregating gate over `build` and `test`.
Renaming those jobs is safe; the ruleset never names them. Do not make them required
checks directly.
