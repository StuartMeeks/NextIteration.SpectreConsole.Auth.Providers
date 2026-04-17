# Releasing

The three provider packages in this repository version **independently**. Each release is triggered by pushing a per-package git tag. CI builds, tests, and publishes just that one package to nuget.org.

## Tag format

```
<provider>-v<major>.<minor>.<patch>
```

| Provider     | Tag prefix       | Example         | Publishes to nuget.org                                      |
|--------------|------------------|-----------------|-------------------------------------------------------------|
| Adobe        | `adobe-v`        | `adobe-v0.1.1`  | `NextIteration.SpectreConsole.Auth.Providers.Adobe`         |
| Airtable     | `airtable-v`     | `airtable-v0.2.0` | `NextIteration.SpectreConsole.Auth.Providers.Airtable`    |
| SoftwareOne  | `softwareone-v`  | `softwareone-v0.1.5` | `NextIteration.SpectreConsole.Auth.Providers.SoftwareOne` |

The `<version>` part must match the `<Version>` property in the corresponding `.csproj`. CI does not check this; mismatching them will push whatever version the csproj says.

## Prerequisites

- **`NUGET_API_KEY` repo secret** is set under
  *Settings → Secrets and variables → Actions*.
- The csproj for the provider you're releasing has its `<Version>` bumped and committed to `main`.
- `main` is green on CI (otherwise the tag-triggered build will fail too).

## Release flow

1. **Bump the version** in the provider's csproj on `main`:

   ```xml
   <Version>0.1.1</Version>
   ```

   Commit and push.

2. **Wait for CI to go green** on that commit (build + test, ~30 seconds).

3. **Create and push the tag**:

   ```bash
   # From the repo root, on main, at the commit you want to release:
   git tag adobe-v0.1.1
   git push origin adobe-v0.1.1
   ```

4. **Watch the release workflow**. The tag push triggers a new CI run:
   - `build` job runs (re-builds and tests everything)
   - `publish` job runs (downloads the nupkg, pushes **only the Adobe package** to nuget.org)

   Visit *Actions* on GitHub to watch. Typically ~45 seconds end-to-end.

5. **Verify on nuget.org**:
   ```
   https://www.nuget.org/packages/NextIteration.SpectreConsole.Auth.Providers.Adobe/0.1.1
   ```
   Allow a few minutes for indexing.

6. **(Optional) Create a GitHub release** for human-readable release notes:

   ```bash
   gh release create adobe-v0.1.1 \
     --title "Adobe v0.1.1" \
     --notes "What changed in this release..."
   ```

   This adds the release-notes page at
   `https://github.com/StuartMeeks/NextIteration.SpectreConsole.Auth.Providers/releases/tag/adobe-v0.1.1`
   without re-triggering CI (the tag already exists).

## Publishing multiple packages at once

Push separate tags — one per package. Each triggers its own CI run:

```bash
git tag adobe-v0.1.0
git tag airtable-v0.1.0
git tag softwareone-v0.1.0
git push origin adobe-v0.1.0 airtable-v0.1.0 softwareone-v0.1.0
```

Three CI runs will fire in parallel; each publishes its one package.

## Re-running a failed publish

`dotnet nuget push` runs with `--skip-duplicate`, so if a tag's build succeeded but the publish step failed (e.g. transient nuget.org 5xx), you can re-run the workflow from the *Actions* page — the already-published packages will be skipped and the missing one will go through.

If you need to publish from a different commit, **delete the tag** first (both remote and local), commit the fix, and re-tag:

```bash
git push origin :adobe-v0.1.1     # delete remote
git tag -d adobe-v0.1.1            # delete local
# ... bump csproj, commit, push ...
git tag adobe-v0.1.1
git push origin adobe-v0.1.1
```

## Rolling back

NuGet.org does not support deleting a published package version — only "unlisting" it, which hides it from search but leaves it resolvable for anyone who already referenced it. Unlist via the nuget.org UI: *Manage Package → Listing*.

To publish a fix, bump the version and release again — never re-publish the same version.
