# ValIO.Generators.Kotlin

The **Kotlin** generator plugin (`LanguageId = "kotlin"`). Generates data classes
(kotlinx.serialization) and an OkHttp client.

- HTTP client: OkHttp. Serialization: kotlinx.serialization. Options (e.g. `package`, `serialization`,
  `generateBuildFiles`) — see `Options/`.

Layout: `*GeneratorPlugin.cs`, `Steps/`, `Ast/`, `Options/`.
See the [Developer Guide](../../docs/DEVELOPER_GUIDE.md).
