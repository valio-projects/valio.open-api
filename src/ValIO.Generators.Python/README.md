# ValIO.Generators.Python

The **Python** generator plugin (`LanguageId = "python"`). Generates Pydantic models and an `httpx`
client (async or sync).

- HTTP client: httpx (`AsyncClient` / `Client`). Validation/models: Pydantic v2.
- Options (e.g. `pythonVersion`, `httpxMode`, `validator`, `rootModule`) — see `Options/`.

Layout: `*GeneratorPlugin.cs`, `Steps/`, `Ast/`, `Options/`.
See the [Developer Guide](../../docs/DEVELOPER_GUIDE.md).
