# ValIO.Generators.Swift

The **Swift** generator plugin (`LanguageId = "swift"`). Generates `Codable` models and a `URLSession`
client for iOS/macOS.

- HTTP client: URLSession. Serialization: Codable. Options (e.g. `moduleName`, `anyMapping`,
  `generateBuildFiles`) — see `Options/`.

Layout: `*GeneratorPlugin.cs`, `Steps/`, `Ast/`, `Options/`.
See the [Developer Guide](../../docs/DEVELOPER_GUIDE.md).
