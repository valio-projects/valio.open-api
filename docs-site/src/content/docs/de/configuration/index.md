---
title: Konfigurationsdatei
description: Referenz für die Mehrprojekt-Datei valio.config.json.
translationKey: configuration
---

Mit einer Konfigurationsdatei lassen sich mehrere Generierungsziele
gleichzeitig beschreiben (z. B. eine TypeScript-App und ein
C#-Server), die dann mit einem einzigen
`valio generate -c valio.config.json` ausgeführt werden.

## Format

```json
{
  "projects": {
    "web": {
      "language":       "typescript",
      "schema":         "./openapi.json",
      "outputPath":     "./apps/web/src/api",
      "namespace":      "@my-org/api",
      "generateModels": true,
      "generateClients": true,
      "options": {
        "transports":       ["fetch", "axios"],
        "validationLibrary": "zod",
        "queryWrapper":      "tanstack"
      }
    },
    "server": {
      "language":   "csharp",
      "schema":     "./openapi.json",
      "outputPath": "./MyOrg.Api.Client/Generated",
      "namespace":  "MyOrg.Api.Client"
    }
  }
}
```

## Felder

| Feld              | Typ                  | Hinweise |
|-------------------|----------------------|----------|
| `language`        | string               | Generator-ID, z. B. `typescript`, `csharp`. |
| `schema`          | string               | Pfad oder URL. |
| `outputPath`      | string               | Ordner oder `.zip`-Pfad. |
| `namespace`       | string               | Namespace für C#/Java/Kotlin/PHP bzw. Modulpräfix für TS. |
| `generateModels`  | bool (Standard true) | |
| `generateClients` | bool (Standard true) | |
| `generateEnums`   | bool (Standard true) | |
| `fileOrganization`| object               | Dateistruktur pro Ziel überschreiben. |
| `namingConvention`| object               | Schreibweisen je Identifikatortyp überschreiben. |
| `options`         | object               | Plugin-spezifische Optionen. Siehe [Sprachen](/de/languages/). |

## Ausführung

```bash
# Alle Projekte
valio generate -c ./valio.config.json

# Bestimmte Projekte
valio generate -c ./valio.config.json --projects web,server
```

YAML wird ebenfalls akzeptiert (`valio.config.yaml`).
