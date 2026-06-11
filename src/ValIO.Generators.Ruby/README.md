# ValIO.Generators.Ruby

The **Ruby** generator plugin (`LanguageId = "ruby"`). Generates models and a Faraday-based client for
Ruby 3+.

- HTTP client: Faraday. Dynamically typed, so the only option is the output `moduleName` — see `Options/`.

Layout: `*GeneratorPlugin.cs`, `Steps/`, `Ast/`, `Options/`.
See the [Developer Guide](../../docs/DEVELOPER_GUIDE.md).
