# ValIO.Generators.Java

The **Java** generator plugin (`LanguageId = "java"`). Generates Jackson models (with Bean Validation
annotations) and an OkHttp client.

- HTTP client: OkHttp. Serialization: Jackson. Options (e.g. `package`, `useHttpClient`,
  `generateBuildFiles`) — see `Options/`.

Layout: `*GeneratorPlugin.cs`, `Steps/`, `Ast/`, `Options/`.
See the [Developer Guide](../../docs/DEVELOPER_GUIDE.md).
