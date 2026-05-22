# ValIO npm distribution

This directory contains the npm package layout for distributing the ValIO CLI
(`valio`, built from `src/ValIO.Cli`) via the npm registry.

## Layout

```
packaging/npm/
├── README.md                       # This file (maintainer-facing)
├── valio/                          # Main package published as `valio`
│   ├── package.json                # Lists 5 platform packages in optionalDependencies
│   ├── tsconfig.json
│   ├── src/
│   │   ├── valio.ts                # Shebang shim; resolves platform binary, spawns it
│   │   └── install.ts              # postinstall: chmod +x on Unix, soft-warn on miss
│   └── ...
└── platforms/                      # One npm package per RID, published as @valio/cli-<rid>
    ├── win32-x64/                  # → @valio/cli-win32-x64    (valio.exe)
    ├── linux-x64/                  # → @valio/cli-linux-x64    (valio)
    ├── linux-arm64/                # → @valio/cli-linux-arm64  (valio)
    ├── darwin-x64/                 # → @valio/cli-darwin-x64   (valio)
    └── darwin-arm64/               # → @valio/cli-darwin-arm64 (valio)
```

## How it works (esbuild-style pattern)

1. User runs `npm i -g valio`.
2. npm reads `valio/package.json`, sees 5 entries in `optionalDependencies`,
   and installs only the one whose `os`/`cpu` fields match the host. The other
   four are silently skipped (that's what `optionalDependencies` is for).
3. npm runs the `postinstall` script (`dist/install.js`), which `chmod +x`es
   the binary on non-Windows hosts.
4. When the user runs `valio`, npm dispatches to the `bin` entry
   (`dist/valio.js`), which `createRequire`s the platform package's
   `package.json`, resolves the binary path next to it, and `spawn`s it with
   `stdio: 'inherit'`, forwarding the exit code.

## Build & publish flow (CI)

The binaries are **not** built here. GitLab CI does the following for each RID:

```
dotnet publish src/ValIO.Cli -r <rid> -c Release \
  --self-contained -p:PublishSingleFile=true
```

Then it copies the resulting `valio` (or `valio.exe`) into
`packaging/npm/platforms/<rid>/` and runs `npm publish` for each platform
package, followed by `npm publish` for the main `valio` package.

## Local maintainer workflow

```sh
cd packaging/npm/valio
npm install
npm run build           # tsc → dist/
```

The `valio` package will not actually run until the platform packages exist on
the registry (or are linked locally) and contain real binaries.

## Versioning

The main `valio` package and all `@valio/cli-*` platform packages must share
the same semver. CI bumps them together.
