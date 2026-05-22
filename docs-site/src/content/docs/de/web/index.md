---
title: Web-UI
description: ValIO.Web im Browser ausführen.
translationKey: web
---

`ValIO.Web` ist eine ASP.NET-Core-10- + Blazor-Server-Anwendung. Sie
stellt dieselbe Generator-Pipeline wie die CLI bereit, gesteuert über
ein Formular im Browser.

## Lokal ausführen

```bash
dotnet run --project src/ValIO.Web
# http://localhost:5080
```

## Funktionen

- **Schema-Eingabe** — URL einfügen oder `.json`/`.yaml`-Datei
  hochladen (max. 10 MB).
- **Sprachauswahl** — jedes über `AddAllGeneratorPlugins` registrierte
  Plugin erscheint im Dropdown.
- **Dynamisches Plugin-Optionsformular** — Steuerelemente werden aus
  der `OptionDescription`-Liste jedes Plugins gebaut. Bool → Switch,
  EnumSingle → Dropdown, EnumMulti → Chip-Set usw.
- **Ausgabe als ZIP** — der Server streamt ein im Speicher erstelltes
  ZIP-Archiv über `ZipStreamOutputSink`; der Browser lädt es per
  Blob-URL herunter.
- **Sprachumschalter** — Menü oben rechts setzt ein Cookie und lädt
  die Seite mit der gewählten Locale neu (`en`, `ru`, `de`, `zh-CN`).

## API-Endpunkt

Das Formular postet `POST /api/generate` mit JSON:

```json
{
  "schemaUrl":            "https://example.com/openapi.json",
  "schemaFileName":       null,
  "schemaContentBase64":  null,
  "languageId":           "typescript",
  "namespace":            null,
  "generateModels":       true,
  "generateClients":      true,
  "generateEnums":        true,
  "pluginOptions":        { "transports": ["fetch","axios"] }
}
```

Entweder `schemaUrl` **oder** `schemaContentBase64` muss gesetzt sein,
nicht beides. Die Antwort ist `application/zip` mit
`Content-Disposition: attachment`.

## Sicherheit

- Der URL-Fetcher weist Nicht-`http(s)`-URLs sowie jeden Host ab, der
  sich auf Loopback-, Link-Local- oder private RFC1918-Bereiche
  auflöst.
- Upload-Größe auf 10 MB begrenzt.
- Alle Anfragen laufen im Request-Scope; kein Zustand zwischen
  Circuits.
