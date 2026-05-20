---
title: Desktop app
description: Running ValIO.Desktop on Windows, macOS, or Linux.
---

`ValIO.Desktop` is an Avalonia 11 application. It bundles the same
generator engine as the CLI and the Web UI, with a native form for
configuring every option.

## Running locally

```bash
dotnet run --project src/ValIO.Desktop
```

Avalonia auto-detects the host platform — the same project builds for
Windows, macOS, and X11/Wayland on Linux.

## Layout

- **Generate** page — choose a schema (native file picker), pick a target
  language, tweak plugin options, choose a folder or `.zip` destination,
  click *Generate*.
- **Settings** page — switch the UI culture (`en`, `ru`, `de`, `zh-CN`).
  Persisted to `%APPDATA%/ValIO/desktop-settings.json` (or
  `~/.config/ValIO/desktop-settings.json` on non-Windows).

## Output format

`Folder` uses `DiskOutputSink`. `Zip archive` uses `ZipFileOutputSink`.
If the entered output path ends with `.zip`, the zip sink is used
regardless of the radio selection.

## Tech stack

- Avalonia 11 + FluentTheme + Inter font
- `CommunityToolkit.Mvvm` source generators for `ObservableObject` /
  `RelayCommand`
- Native file pickers via `IStorageProvider`
- `JsonSettingsStore` for persistence
