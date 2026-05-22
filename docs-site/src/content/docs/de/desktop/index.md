---
title: Desktop-App
description: ValIO.Desktop unter Windows, macOS oder Linux ausführen.
translationKey: desktop
---

`ValIO.Desktop` ist eine Avalonia-11-Anwendung. Sie bündelt denselben
Generator-Engine wie CLI und Web-UI und bietet ein natives Formular
für alle Optionen.

## Lokal ausführen

```bash
dotnet run --project src/ValIO.Desktop
```

Avalonia erkennt die Host-Plattform automatisch — dasselbe Projekt
baut für Windows, macOS und X11/Wayland unter Linux.

## Aufbau

- Seite **Generate** — ein Schema auswählen (nativer File-Picker),
  Zielsprache wählen, Plugin-Optionen anpassen, Ordner oder
  `.zip`-Ziel angeben, *Generate* klicken.
- Seite **Settings** — UI-Kultur umstellen (`en`, `ru`, `de`,
  `zh-CN`). Wird in `%APPDATA%/ValIO/desktop-settings.json` (bzw.
  `~/.config/ValIO/desktop-settings.json` auf Nicht-Windows-Systemen)
  persistiert.

## Ausgabeformat

`Folder` nutzt den `DiskOutputSink`. `Zip archive` nutzt den
`ZipFileOutputSink`. Endet der eingegebene Ausgabepfad auf `.zip`,
wird unabhängig von der Radio-Auswahl der Zip-Sink verwendet.

## Tech-Stack

- Avalonia 11 + FluentTheme + Inter-Schrift
- `CommunityToolkit.Mvvm`-Quellgeneratoren für `ObservableObject` /
  `RelayCommand`
- Native File-Picker via `IStorageProvider`
- `JsonSettingsStore` für die Persistenz
