# ValIO.Generators.FSharp

The **F#** generator plugin (`LanguageId = "fsharp"`). Generates records / discriminated unions and a
RestSharp-based client.

- HTTP client: RestSharp. Idiomatic discriminated unions. Options (e.g. `namespace`) — see `Options/`.

Layout: `*GeneratorPlugin.cs`, `Steps/`, `Ast/`, `Options/`.
See the [Developer Guide](../../docs/DEVELOPER_GUIDE.md).
