# CI/CD

ValIO builds and publishes on GitHub Actions. Everything is branch-driven: there
is no manual release job, and no tag has to be pushed by hand.

## Workflows

| Workflow                                            | Trigger                                        | Purpose                                               |
| --------------------------------------------------- | ---------------------------------------------- | ----------------------------------------------------- |
| [`ci.yml`](../.github/workflows/ci.yml)             | PRs, pushes to ordinary branches, `workflow_call` | Build, test, pack-check, CLI smoke test, npm shim, docs |
| [`release.yml`](../.github/workflows/release.yml)   | Pushes to `main`, `develop`, `release/**`      | Calls CI, then versions, packs and publishes           |
| [`docs.yml`](../.github/workflows/docs.yml)         | Pushes to `main` touching `docs-site/`         | Deploys the Starlight site to GitHub Pages            |
| [`codeql.yml`](../.github/workflows/codeql.yml)     | PRs, pushes to release branches, weekly        | CodeQL analysis for C# and TypeScript                 |

`release.yml` invokes `ci.yml` through `workflow_call` rather than duplicating
it, so nothing is ever published from code that has not compiled and passed the
test suite on Linux, Windows and macOS. `ci.yml` deliberately excludes
`main` / `develop` / `release/**` from its own `push` trigger to avoid running
twice on those refs.

## Pipeline

```
                 ┌──────────────────────────────────────────┐
push ───────────▶│ ci  (build & test ×3 OS, pack, smoke,    │
                 │      npm shim, docs site)                │
                 └────────────────────┬─────────────────────┘
                                      │
      version ────────────────────────┼──────────────┐
                                      ▼              ▼
                                    pack          binaries ×5
                                      │              │
                                      ▼              ▼
                              publish-nuget   publish-npm
                                      └──────┬───────┘
                                             ▼
                                     github-release  (main only)
```

## Channels

The version and its destination are derived entirely from the branch by
[`compute-version.mjs`](../.github/scripts/compute-version.mjs).

| Branch       | Version                 | NuGet           | npm             | dist-tag |
| ------------ | ----------------------- | --------------- | --------------- | -------- |
| `main`       | `X.Y.Z` from `VERSION`  | nuget.org       | npmjs.com       | `latest` |
| `release/**` | `X.Y.Z-rc.<run>`        | GitHub Packages | GitHub Packages | `next`   |
| `develop`    | `X.Y.Z-alpha.<run>`     | GitHub Packages | GitHub Packages | `dev`    |
| PRs, others  | `X.Y.Z-pr.N.<run>` etc. | not published   | not published   | —        |

A legacy `master` branch is built and tested like any other branch but never
publishes, so pushing it cannot trigger a release by accident.

`<run>` is `github.run_number`, which is monotonic per workflow. Every preview
upload is therefore a fresh, ordered version that can never collide with an
existing one.

For `release/**` the branch suffix wins when it parses as a version
(`release/1.4`, `release/v2.0.1` → base `1.4.0` / `2.0.1`), which lets a release
branch stabilise a version independently of what `main` currently carries.
Anything else (`release/2026-q1`) falls back to the `VERSION` file.

### Versioning source of truth

The repo-root [`VERSION`](../VERSION) file holds a plain `X.Y.Z` — no prerelease
suffix. `Directory.Build.props` reads it into `VersionPrefix`, so a local
`dotnet pack` produces the same number the pipeline would, and CI overrides it
with `-p:Version=…` to append the channel suffix.

**Cutting a stable release means bumping `VERSION`.** Publishing from `main` is
idempotent: `dotnet nuget push --skip-duplicate` and an `npm view` existence
check mean a push to `main` that does not change `VERSION` republishes nothing
and stays green. Re-running a workflow is always safe.

## npm package naming

The CLI ships as a thin wrapper plus five per-platform packages carrying one
prebuilt binary each:

| Package                            | Contents                              |
| ---------------------------------- | ------------------------------------- |
| `@valio-projects/valio`            | Wrapper; installs the `valio` command |
| `@valio-projects/cli-<platform>`   | One prebuilt binary, `os`/`cpu`-gated |

The names are the same on both channels — only the registry differs. The scope
must equal the repository owner because GitHub Packages requires it, and using
the same scope on npmjs.com keeps a package's identity stable no matter where it
was published from. (The bare name `valio` is not available on npmjs.com; it is
taken by an unrelated project.)

[`prepare-npm.mjs`](../.github/scripts/prepare-npm.mjs) stamps versions,
registry and licence into the manifests at publish time. The wrapper does not
hardcode the scope — it discovers the platform package name at runtime from its
own `optionalDependencies`
(see [`platform.ts`](../packaging/npm/valio/src/platform.ts)), so renaming the
scope needs no source change.

Platform packages are published before the wrapper, because the wrapper pins
exact versions of them.

### Consuming

```bash
# npmjs.com — stable
npm install -g @valio-projects/valio

# GitHub Packages — previews
npm config set @valio-projects:registry https://npm.pkg.github.com
npm install -g @valio-projects/valio@dev    # develop
npm install -g @valio-projects/valio@next   # release/**
```

Both registries host the same package name, so switching channels means pointing
the scope at a different registry, not installing a differently named package.

```bash
dotnet nuget add source https://nuget.pkg.github.com/valio-projects/index.json \
  --name valio-preview --username <you> --password <a PAT with read:packages>
```

Reading from GitHub Packages requires authentication even for public
repositories — that is a GitHub limitation, not a ValIO one.

## Secrets

| Secret           | Needed for                        | Notes                                          |
| ---------------- | --------------------------------- | ---------------------------------------------- |
| `NUGET_API_KEY`  | Stable NuGet pushes to nuget.org  | Repository secret; rotate annually             |
| `NPM_TOKEN`      | Stable npm pushes to npmjs.com    | Automation token with publish rights           |
| `GITHUB_TOKEN`   | All GitHub Packages pushes, tags  | Provided automatically; no setup required      |

Preview channels need no configured secrets at all. Both publish jobs validate
their credentials before uploading anything, so a missing secret fails fast with
an explicit message instead of a mid-publish `401`.

The publish jobs run in the `production` (stable) and `preview` (prerelease)
environments, so approval gates or environment-scoped secrets can be added later
without touching the workflow.

## First-time repository setup

One-off steps that live in GitHub's UI rather than in this repository:

1. **Secrets** — Settings → Secrets and variables → Actions:
   `NUGET_API_KEY` and `NPM_TOKEN`. Only needed before the first push to `main`;
   preview channels work without them.
2. **Workflow permissions** — Settings → Actions → General → Workflow
   permissions must allow read *and* write, otherwise `github-release` cannot
   push the `vX.Y.Z` tag and the publish jobs cannot write to GitHub Packages.
3. **Pages** — Settings → Pages → Source: *GitHub Actions*, so `docs.yml` can
   deploy.
4. **npm organisation** — the `valio-projects` scope must exist on npmjs.com and
   the account behind `NPM_TOKEN` must be able to publish to it.
5. **Branch protection** (optional) — require the CI checks on `main` and
   `develop`.

## Local equivalents

```bash
dotnet build ValIO.sln -c Release
dotnet test ValIO.sln -c Release --no-build
dotnet pack ValIO.sln -c Release -o nupkgs
node .github/scripts/compute-version.mjs   # print what this ref would publish
```

`compute-version.mjs` reads `GITHUB_REF_NAME`, `GITHUB_EVENT_NAME` and
`GITHUB_RUN_NUMBER`, so the channel rules can be checked locally:

```bash
GITHUB_REF_NAME=release/1.4 GITHUB_RUN_NUMBER=7 node .github/scripts/compute-version.mjs
```
