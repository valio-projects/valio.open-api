# ValIO.Generators.CSharp

The **C#** generator plugin (`LanguageId = "csharp"`). Generates POCO/record models and a typed
`HttpClient` client using `System.Text.Json`.

- HTTP client: `HttpClient`. Serialization: System.Text.Json.
- Options (e.g. `namespace`, model type, accessors, validation attributes) — see `Options/`.

Layout: `*GeneratorPlugin.cs` (entry point), `Steps/`, `Ast/` (nodes, builders, renderers), `Options/`.
See the [Developer Guide](../../docs/DEVELOPER_GUIDE.md).
