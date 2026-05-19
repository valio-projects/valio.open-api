---
title: Erste Schritte
description: ValIO installieren, OpenAPI-Schema anbinden, Clients generieren.
---

ValIO besteht aus drei Frontends auf demselben Generator-Engine:

- **CLI** — `valio`-Programm (eine .NET-10-Binary).
- **Web** — ASP.NET Core 10 + Blazor Server (`ValIO.Web`).
- **Desktop** — Avalonia 11 (`ValIO.Desktop`), plattformübergreifend.

## Schneller CLI-Lauf

```bash
# TypeScript-Clients in einen Ordner generieren
valio generate -s ./openapi.json -o ./generated -l typescript

# Dasselbe als ZIP-Archiv
valio generate -s ./openapi.json -o ./generated.zip -l typescript

# Oder --zip nutzen, dann wird die .zip-Endung automatisch angehängt
valio generate -s ./openapi.json -o ./generated --zip -l typescript

# UI-Sprache umstellen (CLI-Meldungen + Web-/Desktop-UI)
valio --lang de generate -s ./openapi.json -o ./generated -l typescript
```

Die Sprachübersicht steht im [CLI-Handbuch](/de/cli/) und in der
[Konfigurationsreferenz](/de/configuration/) (weitere Seiten sind aktuell
auf Englisch und werden übersetzt).
