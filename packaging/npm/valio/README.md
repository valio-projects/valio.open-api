# valio

Generate type-safe HTTP clients for 12 languages from any OpenAPI spec.

## Install

```sh
npm install -g valio
```

This pulls in a small prebuilt native binary for your platform (one of
`@valio/cli-{win32-x64,linux-x64,linux-arm64,darwin-x64,darwin-arm64}`) via
npm's optional dependencies — you don't need .NET installed.

## Usage

```sh
valio --help
```

See the full guide and language matrix at <https://valio.dev>.

## Alternative installs

If your platform isn't covered by a prebuilt binary, install via the .NET tool:

```sh
dotnet tool install --global ValIO.Cli
```

## License

MIT
