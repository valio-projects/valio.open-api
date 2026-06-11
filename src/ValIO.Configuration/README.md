# ValIO.Configuration

Loads and validates ValIO project configuration (`valio.config.json` / `valio.config.yaml`).

## Contains

- **Models** (`Models/ValIOConfig.cs`) — the config shape: projects, per-project `options`,
  `fileOrganization` (`modelsPath` / `clientsPath` / `schemasPath`), `namingConvention`, …
- **Services** — `ConfigurationLoader` (parse JSON/YAML and map to the engine's
  `GenerationConfiguration`, delegating plugin-specific `options` to each plugin's
  `IGeneratorOptionsDescriptor`) and `ConfigurationValidator`.

Plugin-specific option *parsing* lives in each generator's options descriptor, not here.
