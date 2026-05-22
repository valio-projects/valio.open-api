---
title: Десктоп-приложение
description: Запуск ValIO.Desktop на Windows, macOS или Linux.
translationKey: desktop
---

`ValIO.Desktop` — приложение на Avalonia 11. Оно использует тот же
движок генерации, что и CLI и веб-приложение, и предоставляет нативную
форму для настройки всех опций.

## Локальный запуск

```bash
dotnet run --project src/ValIO.Desktop
```

Avalonia автоматически определяет хост-платформу — один и тот же
проект собирается под Windows, macOS и X11/Wayland на Linux.

## Структура

- Страница **Generate** — выбор схемы через нативный диалог, целевой
  язык, опции плагина, папка или `.zip` для вывода, кнопка
  *Generate*.
- Страница **Settings** — смена культуры интерфейса (`en`, `ru`, `de`,
  `zh-CN`). Сохраняется в `%APPDATA%/ValIO/desktop-settings.json` или
  `~/.config/ValIO/desktop-settings.json` на не-Windows-платформах.

## Формат вывода

`Folder` использует `DiskOutputSink`. `Zip archive` использует
`ZipFileOutputSink`. Если выбранный путь оканчивается на `.zip`, то
независимо от выбранного переключателя применяется zip-приёмник.

## Стек технологий

- Avalonia 11 + FluentTheme + шрифт Inter
- `CommunityToolkit.Mvvm` — source-генераторы для `ObservableObject` /
  `RelayCommand`
- Нативные диалоги выбора файлов через `IStorageProvider`
- `JsonSettingsStore` для сохранения настроек
