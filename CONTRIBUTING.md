# Contributing to ValIO

Thanks for your interest in ValIO! We welcome contributions of all kinds — bug fixes, new generators, documentation, translations, examples.

This guide describes how to set up your development environment, run tests, and submit pull requests.
For the architecture and how to extend the generator (IR, AST, builders, renderers, plugins, options,
testing, and adding new options/transports/validation libraries/stacks/languages), see the
**[Developer Guide](./docs/DEVELOPER_GUIDE.md)**.

## Code of Conduct

This project follows the [Contributor Covenant 2.1](./CODE_OF_CONDUCT.md). By participating you agree to abide by it.

## Reporting issues

- **Bugs** → use [bug_report.yml](./.github/ISSUE_TEMPLATE/bug_report.yml) template.
- **Feature requests** → use [feature_request.yml](./.github/ISSUE_TEMPLATE/feature_request.yml) template.
- **Questions** → [GitHub Discussions](https://github.com/valio-projects/valio.open-api/discussions).
- **Security vulnerabilities** → see [SECURITY.md](./SECURITY.md) (please do not file public issues for these).

## Development setup

### Prerequisites

| Tool        | Version       | Used for                                       |
| ----------- | ------------- | ---------------------------------------------- |
| .NET SDK    | 10.0.100+     | All C# projects (Core, generators, frontends)  |
| Node.js     | 22+           | docs-site and npm wrapper                      |
| npm         | 10+           | docs-site and npm wrapper                      |
| Git         | latest        | Source control                                 |

Optional: PowerShell 7+, Visual Studio 2026/JetBrains Rider, VS Code with the C# Dev Kit.

### Clone and build

```sh
git clone https://github.com/valio-projects/valio.open-api.git
cd valio.open-api
dotnet restore
dotnet build
dotnet test
```

On Windows you can also use the helper scripts in the repo root:
`build.bat`, `test.bat`, `pack.bat`, `clean.bat`.

### Running the three frontends locally

```sh
# CLI
dotnet run --project src/ValIO.Cli -- generate -s ./valio.config.example.json -o ./out -l typescript

# Web (http://localhost:5000)
dotnet run --project src/ValIO.Web

# Desktop
dotnet run --project src/ValIO.Desktop
```

Convenience wrappers: `run-cli.bat`, `run-web.bat`, `run-desktop.bat`.

### Working on docs-site

```sh
cd docs-site
npm install
npm run dev     # http://localhost:4321
npm run build
```

Convenience: `docs-site/install.bat`, `dev.bat`, `build.bat`, `preview.bat`.

## Project layout

```
valio/
├── src/
│   ├── ValIO.Core/                  # IR / AST / plugin contracts
│   ├── ValIO.Parsers/               # OpenAPI 3.x document parsing
│   ├── ValIO.Configuration/         # valio.config.* loading
│   ├── ValIO.Application/           # orchestration engine
│   ├── ValIO.Output/                # output sinks (Disk / Zip / Stream / InMemory)
│   ├── ValIO.Localization/          # CLI / UI strings (en / ru / de / zh-CN)
│   ├── ValIO.Plugins.Bootstrap/     # AddAllGeneratorPlugins() DI helper
│   ├── ValIO.Cli/                   # System.CommandLine CLI (publishes as `valio`)
│   ├── ValIO.Web/                   # ASP.NET Core 10 + Blazor Server + MudBlazor
│   ├── ValIO.Desktop/               # Avalonia 11 + CommunityToolkit.Mvvm
│   └── ValIO.Generators.{Lang}/     # 12 language generators
├── tests/                           # NUnit 4 + FluentAssertions
├── docs/                            # internal design docs (architecture, refactoring)
├── docs-site/                       # public Astro Starlight site
├── packaging/npm/                   # npm distribution wrapper
├── Examples/                        # reference inputs and sample outputs
└── ValIO.sln
```

Deeper architecture notes live in [`docs/refactoring/`](./docs/refactoring/).

## Adding a new language generator

1. **Scaffold the project**: copy an existing generator (e.g. `src/ValIO.Generators.Ruby`) and rename to `src/ValIO.Generators.<Lang>`.
2. **Implement `IGeneratorPlugin`**: define `Id`, `OptionsDescriptor`, and the pipeline of `ICodeGenerationStep` instances.
3. **Add tests**: create `tests/ValIO.Generators.<Lang>.Tests/` mirroring an existing test project structure (NUnit 4, FluentAssertions).
4. **Register the plugin**: add it to `AddAllGeneratorPlugins()` in `src/ValIO.Plugins.Bootstrap/PluginRegistrations.cs`.
5. **Add to solution**: `dotnet sln add src/ValIO.Generators.<Lang>/ValIO.Generators.<Lang>.csproj` and the test project.
6. **Document**: add a page at `docs-site/src/content/docs/languages/<lang>.md`.
7. **Localize the language name**: add a string in each `src/ValIO.Localization/Resources/*.resx`.

## Branching model

Three long-lived branches, each mapped to a publishing channel (see
[docs/CI_CD.md](./docs/CI_CD.md)):

- `main` — always releasable; every merge can publish a stable version.
- `develop` — integration branch; every push publishes an `-alpha` preview.
- `release/X.Y.Z` — stabilisation for one version; every push publishes an `-rc`.

Work happens on short-lived branches cut from `develop`:

- `feat/<short-description>` — new feature
- `fix/<short-description>` — bug fix
- `docs/<short-description>` — documentation only
- `chore/<short-description>` — refactor / tooling / no behaviour change
- `test/<short-description>` — adding or improving tests

Keep them short-lived (ideally < 1 week) and rebase on `develop` before opening a
PR. Hotfixes branch from `main` and merge back into both `main` and `develop`.

## Commit style

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>
```

Types: `feat`, `fix`, `docs`, `chore`, `refactor`, `test`, `perf`, `build`, `ci`, `revert`.
Scope is optional but encouraged (e.g. `typescript`, `cli`, `web`, `docs-site`, `core`).

Examples:

```
feat(typescript): add OFetch transport
fix(cli): correct exit code when --output is missing
docs(architecture): describe IPluginRegistry discovery
chore(layout): drop stale net9.0 root duplicates of FSharp generators
```

## PR process

1. Open a fork (external contributors) or a feature branch (maintainers).
2. Make your changes; keep commits focused and well-described.
3. Ensure tests pass locally: `dotnet test` and (if docs touched) `cd docs-site && npm run build`.
4. Push and open a PR using the [PR template](./.github/PULL_REQUEST_TEMPLATE.md).
5. Wait for CI to go green. At least one approval is required; maintainers squash-merge.

## Code style

- C# code style: [`CODING_RULES.md`](./CODING_RULES.md) (currently in Russian; English translation tracked as a follow-up).
- `Nullable=enable`, `LangVersion=latest`, `ImplicitUsings=enable`.
- Run `dotnet format` before committing.
- No warnings as errors yet (`TreatWarningsAsErrors=false`) — we plan to flip this in v0.2.
- For TypeScript/JS in `docs-site` and `packaging/npm`: Prettier + standard.

## Tests

- Every generator change must keep `dotnet test` green and add at least one new test in the corresponding `tests/ValIO.Generators.<Lang>.Tests/` project.
- Use NUnit 4 (`[Test]`) + FluentAssertions (`actual.Should().Be(expected)`).
- Integration tests live under `*/Integration/`; pure unit tests under `*/Renderers/`, `*/TypeMappers/`, etc.

## Documentation

When you change user-visible behaviour (CLI flags, config keys, generator output), update:

1. The English page in `docs-site/src/content/docs/...`.
2. The corresponding translations in `docs-site/src/content/docs/{ru,de,zh-cn}/...` if they already exist (otherwise the English page becomes the canonical fallback).
3. The `CHANGELOG.md` under `[Unreleased]`.

If you're adding entirely new pages, prefer placing them under an existing section (`/languages/`, `/configuration/`, `/architecture/`, `/recipes/`, `/reference/`) so the sidebar stays consistent.

## Branches and publishing

Publishing is branch-driven — there is no manual release job to run and no tag to
push by hand. See [docs/CI_CD.md](./docs/CI_CD.md) for the full pipeline.

| Branch       | Version                  | Goes to                           | npm dist-tag |
| ------------ | ------------------------ | --------------------------------- | ------------ |
| `develop`    | `X.Y.Z-alpha.<run>`      | GitHub Packages                   | `dev`        |
| `release/**` | `X.Y.Z-rc.<run>`         | GitHub Packages                   | `next`       |
| `main`       | `X.Y.Z` (from `VERSION`) | nuget.org + npmjs.com + a Release | `latest`     |

The npm package is `@valio-projects/valio` on both registries; it installs the
`valio` command.

## Releasing (maintainers only)

1. Cut `release/X.Y.Z` from `develop`. Every push to it publishes an `-rc.N`
   build to GitHub Packages for verification.
2. Bump the repo-root [`VERSION`](./VERSION) file to `X.Y.Z`. This single file
   drives both `dotnet pack` and the npm manifests.
3. Move entries in `CHANGELOG.md` from `[Unreleased]` to a new
   `[X.Y.Z] — YYYY-MM-DD` section and update the compare links at the bottom.
4. Open a PR from the release branch into `main` and squash-merge it once CI is
   green.
5. The merge publishes to nuget.org and npmjs.com, tags `vX.Y.Z`, and opens a
   GitHub Release with the packages and per-platform binaries attached. Nothing
   further is required.
6. Merge `main` back into `develop` so the bumped `VERSION` is carried forward.
7. Announce in [Discussions](https://github.com/valio-projects/valio.open-api/discussions).

Publishing to `main` is idempotent: if `VERSION` has not changed since the last
release, the packages are already in the registries and every publish step is
skipped. Re-running a workflow is therefore always safe.

Secrets used by the pipeline (`NUGET_API_KEY`, `NPM_TOKEN`) live in GitHub
repository secrets and should be rotated annually. Prerelease channels need no
secrets — they authenticate with the workflow's own `GITHUB_TOKEN`. See
[SECURITY.md](./SECURITY.md).

## Security

Please report vulnerabilities privately — see [SECURITY.md](./SECURITY.md).

## License

By contributing you agree that your contributions will be licensed under the [GNU Lesser General Public License v2.1](./LICENSE).
