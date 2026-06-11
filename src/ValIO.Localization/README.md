# ValIO.Localization

Localized string resources for the ValIO UIs and option descriptions.

- `Resources/*.resx` — `GenerationStrings`, `ConfigurationStrings`, `ParserStrings`,
  `ValidationStrings`, `PluginStrings`, with locale variants (`*.ru.resx`, `*.de.resx`, `*.zh-CN.resx`).
- `PluginStrings.resx` holds option descriptions referenced by each plugin's options descriptor via
  `DescriptionKey` (e.g. `ts.uuidFormat.desc`). A missing key falls back to the inline description.

Supported locales: English, Русский, Deutsch, 简体中文.
