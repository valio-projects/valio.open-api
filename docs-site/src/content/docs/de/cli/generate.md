---
title: valio generate
description: Code aus einem OpenAPI-Schema generieren.
translationKey: cli-generate
---

```
valio [--lang <culture>] generate
    [-s|--schema    <path-or-url>]
    [-o|--output    <folder-or-zip-path>]
    [-l|--language  <id>]
    [-c|--config    <config-file>]
    [-p|--projects  <name>...]
    [--format       folder|zip|auto]
    [--zip]
```

## Optionen

| Option                | Beschreibung                                                                 |
|-----------------------|------------------------------------------------------------------------------|
| `-s` / `--schema`     | Pfad oder URL des OpenAPI-Schemas. Pflicht ohne `--config`.                  |
| `-o` / `--output`     | Ordner oder Pfad zur `.zip`-Datei. Pflicht ohne `--config`.                  |
| `-l` / `--language`   | Generator-ID (`typescript`, `csharp`, ...). Pflicht ohne `--config`.         |
| `-c` / `--config`     | JSON- oder YAML-Konfigurationsdatei mit einem oder mehreren Projekten.       |
| `-p` / `--projects`   | Projektname(n) innerhalb der Konfigurationsdatei. Standard: alle.            |
| `--format`            | `folder`, `zip` oder `auto`. Standard `auto`.                                |
| `--zip`               | Abkürzung für `--format zip`.                                                |

## Auflösung des Ausgabeformats

Auto-Erkennung anhand der `--output`-Endung:

| `--output`     | `--format` | Ergebnis                                  |
|----------------|------------|-------------------------------------------|
| `./out`        | (Standard) | Ordner `./out/`                           |
| `./out.zip`    | (Standard) | ZIP `./out.zip`                           |
| `./out`        | `zip`      | ZIP `./out.zip` (`.zip` angehängt)        |
| `./out.zip`    | `folder`   | Ordner namens `./out.zip/`                |

## Beispiele

```bash
# Ordner-Ausgabe
valio generate -s ./openapi.json -o ./generated -l typescript

# ZIP-Ausgabe (automatisch erkannt)
valio generate -s ./openapi.json -o ./generated.zip -l typescript

# Mehrprojekt-Lauf aus einer Konfigurationsdatei
valio generate -c ./valio.config.json -p web,mobile
```
