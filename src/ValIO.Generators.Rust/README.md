# ValIO.Generators.Rust

The **Rust** generator plugin (`LanguageId = "rust"`). Generates Serde models and a Reqwest client
(tokio async).

- HTTP client: Reqwest. Serialization: Serde. Options (e.g. `crateName`, `anyMapping`) — see `Options/`.

Layout: `*GeneratorPlugin.cs`, `Steps/`, `Ast/`, `Options/`.
See the [Developer Guide](../../docs/DEVELOPER_GUIDE.md).
