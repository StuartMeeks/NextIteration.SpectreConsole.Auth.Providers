# Security policy

## Reporting a vulnerability

Report privately through GitHub's **Report a vulnerability** button under this
repository's Security tab, which opens a private advisory visible only to the
maintainers. Please do not open a public issue for a suspected vulnerability.

Include the affected package and version, what an attacker can achieve, and a
reproduction if you have one.

You can expect an acknowledgement within 7 days, an assessment within 14, and
credit in the advisory and changelog unless you ask otherwise.

## Supported versions

Only the latest released minor of each package receives security fixes. These are
pre-1.0 libraries and there are no long-term support branches. The four provider
packages version independently.

## Scope

These packages **collect** provider credentials — running OAuth flows (Adobe IMS
client-credentials, the GitHub device flow) or prompting for API tokens — and hand
the result to the core `NextIteration.SpectreConsole.Auth` store, which owns
encryption and on-disk persistence. The trust boundaries for credentials *at rest*
are therefore documented in that package's security policy, not here.

Two things are explicitly **not** claimed by the providers:

- **In-transit and in-memory exposure of secrets during collection.** A credential
  is held in process memory while a flow runs and is sent to the provider's token or
  identity endpoint over TLS. Nothing here defends against a compromised host, a
  debugger attached to the process, a heap dump taken mid-flow, or a consumer that
  disables certificate validation on the `HttpClient` it supplies.
- **Trust in the values a caller supplies.** Client ids, hosts, base URLs and scopes
  come from the calling application; a provider does not vet them beyond what the
  remote endpoint enforces. Pointing a provider at a hostile host is a caller error,
  not a vulnerability in these packages.

Reports demonstrating a break *within* those stated boundaries — a token logged in
cleartext, a flow that ignores a failed TLS handshake, a credential written somewhere
unexpected — are in scope and welcome. Reports that only restate a documented
limitation are not vulnerabilities.
