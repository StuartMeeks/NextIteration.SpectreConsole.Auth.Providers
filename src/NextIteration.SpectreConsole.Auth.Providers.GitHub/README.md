# NextIteration.SpectreConsole.Auth.Providers.GitHub

[![NuGet](https://img.shields.io/nuget/v/NextIteration.SpectreConsole.Auth.Providers.GitHub.svg)](https://www.nuget.org/packages/NextIteration.SpectreConsole.Auth.Providers.GitHub/)
[![Downloads](https://img.shields.io/nuget/dt/NextIteration.SpectreConsole.Auth.Providers.GitHub.svg)](https://www.nuget.org/packages/NextIteration.SpectreConsole.Auth.Providers.GitHub/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![CI](https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/actions/workflows/ci.yml/badge.svg)](https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/actions/workflows/ci.yml)

GitHub credential provider for [**NextIteration.SpectreConsole.Auth**](https://www.nuget.org/packages/NextIteration.SpectreConsole.Auth).

Drops a ready-to-use `GitHubCredential`, `GitHubToken`, authentication service, a Spectre.Console `accounts add` collector that runs the OAuth **device flow** (the same flow `gh auth login` uses by default), and an `accounts list` display formatter into a CLI that already uses the core auth package.

---

## Install

```bash
dotnet add package NextIteration.SpectreConsole.Auth.Providers.GitHub
```

Requires the core package (`NextIteration.SpectreConsole.Auth`) — NuGet pulls it in transitively.

---

## Prerequisites

You need an **OAuth App** with **device flow enabled**:

1. Create an OAuth App under *GitHub → Settings → Developer settings → OAuth Apps* (or your org's settings).
2. Tick **Enable Device Flow** in the app's settings.
3. Note its **Client ID** — it's public (not a secret) and is what the collector prompts for.

No client secret is involved: the device flow authenticates the *user*, not the app's secret.

---

## Quick start

```csharp
using NextIteration.SpectreConsole.Auth;
using NextIteration.SpectreConsole.Auth.Providers.GitHub;

services.AddCredentialStore(opts =>
{
    opts.CredentialsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".my-cli", "credentials");
});

services.AddHttpClient();            // IHttpClientFactory is required
services.AddGitHubAuthProvider();

// ... then hook up the accounts branch in your Spectre.Console configurator
// (from the core package):
//
//   config.AddAccountsBranch();
```

From that point on:

```
my-cli accounts add        # prompts for GitHub (and any other registered providers)
my-cli accounts list       # shows the authenticated user, scopes, host, masked token
my-cli accounts select <id>
my-cli accounts delete <id>
```

Resolving a token in consumer code:

```csharp
public sealed class GitHubClient(GitHubAuthenticationService auth, IHttpClientFactory http)
{
    public async Task<string> GetRepositoriesAsync()
    {
        var token = await auth.AuthenticateAsync();
        using var client = http.CreateClient();
        client.BaseAddress = token.BaseUrl;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.AccessToken);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("my-cli/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return await client.GetStringAsync("user/repos");
    }
}
```

---

## The `accounts add` flow

The collector runs the OAuth device flow end to end:

1. **Prompts** for the GitHub host (default `github.com`), the OAuth App **client id**, and the requested **scopes** (default `repo read:org`).
2. **Requests a device code** — `POST {host}/login/device/code`.
3. **Shows you the user code and verification URL** (e.g. `https://github.com/login/device`). Open it in a browser and enter the code.
4. **Polls** `POST {host}/login/oauth/access_token` honouring the server `interval`, the `slow_down` back-off, and `authorization_pending`, until you authorise (or the code expires).
5. **Enriches** the credential — `GET {api}/user` confirms "authenticated as X" and records your login/name for the list display.

---

## What gets stored

Each `accounts add` run serialises a `GitHubCredential` into the encrypted keystore:

| Field                  | Source                | Notes                                                                            |
|------------------------|-----------------------|----------------------------------------------------------------------------------|
| `ClientId`             | prompt                | The OAuth App's public client id                                                 |
| `AccessToken`          | device flow           | The user access token                                                            |
| `RefreshToken`         | device flow           | Only present if the OAuth App issues **expiring** tokens; otherwise `null`        |
| `AccessTokenExpiresAt` | device flow           | Only present for expiring tokens; otherwise `null`                               |
| `Scopes`               | token response        | Space-delimited granted scopes                                                   |
| `WebBaseUrl`           | derived from host     | `https://github.com/` or `https://{host}/` for GitHub Enterprise Server          |
| `ApiBaseUrl`           | derived from host     | `https://api.github.com/` or `https://{host}/api/v3/` for Enterprise Server      |
| `Login` / `Name`       | `GET /user`           | The authenticated user's handle and display name                                |
| `Environment`          | derived from host     | `GitHubCom` / `Enterprise`                                                       |

`accounts list` shows the account (`login (name)`), the granted scopes, the API host, and the token masked as `xxxx...xxxx`.

---

## Authentication model

- **Classic OAuth App (non-expiring tokens).** The stored access token is used forever; `GitHubAuthenticationService.AuthenticateAsync()` is a straight pass-through. A revoked token surfaces as a `401` on first use.
- **OAuth App with expiring user tokens.** The device flow also returns a refresh token and expiry. When the stored access token is expired (with a 30-second clock-skew buffer), the auth service refreshes it via `POST {host}/login/oauth/access_token` (`grant_type=refresh_token`) before returning a fresh `GitHubToken`.

### Known limitation

A refreshed access token is **not** written back to the keystore in this version — each authenticate call that needs a refresh performs one. Consumers that authenticate frequently against an expiring app should cache the returned `GitHubToken` for its lifetime. (Persisting the refreshed token would require an update API on the core credential store.)

---

## GitHub Enterprise Server

Enter your instance host (e.g. `ghe.example.com`) at the host prompt. The provider derives the web base (`https://ghe.example.com/`) and the REST API base (`https://ghe.example.com/api/v3/`) automatically, and labels the environment `Enterprise`.

---

## Named HttpClient

The collector and the refresh path resolve an `HttpClient` via `IHttpClientFactory.CreateClient("GitHub Credential Validator")`. Consumers who want to pre-configure it (proxies, retry handlers, custom user-agent) can:

```csharp
services.AddHttpClient(GitHubCredentialCollector.HttpClientName, c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("my-cli/1.0");
});
```

The provider does not mutate `BaseAddress` or `DefaultRequestHeaders` on the client — every endpoint is passed as an absolute URI per request, and a `User-Agent` is set per request (GitHub rejects API calls without one).

---

## Supported platforms

Whatever the core package supports (currently Windows, macOS, Linux on .NET 8 and .NET 10).

---

## License

[MIT](../../LICENSE) © Stuart Meeks
