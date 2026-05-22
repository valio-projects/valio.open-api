---
title: 桌面应用
description: 在 Windows、macOS 或 Linux 上运行 ValIO.Desktop。
translationKey: desktop
---

`ValIO.Desktop` 是一个基于 Avalonia 11 的桌面应用。它内置与 CLI、
Web UI 相同的生成器内核，并提供原生的表单用于配置全部选项。

## 本地运行

```bash
dotnet run --project src/ValIO.Desktop
```

Avalonia 会自动识别宿主平台 —— 同一个项目可以构建出 Windows、
macOS 以及 Linux 上的 X11/Wayland 版本。

## 布局

- **Generate** 页 —— 通过原生文件选择器挑选架构、选择目标语言、调整
  插件选项、选择输出到文件夹或 `.zip`，然后点击 *Generate*。
- **Settings** 页 —— 切换界面语言（`en`、`ru`、`de`、`zh-CN`）。设置
  会被持久化到 `%APPDATA%/ValIO/desktop-settings.json`（在非 Windows
  系统上为 `~/.config/ValIO/desktop-settings.json`）。

## 输出格式

`Folder` 使用 `DiskOutputSink`。`Zip archive` 使用 `ZipFileOutputSink`。
如果填写的输出路径以 `.zip` 结尾，则无论选择哪个单选按钮都会使用
zip sink。

## 技术栈

- Avalonia 11 + FluentTheme + Inter 字体
- 通过 `CommunityToolkit.Mvvm` 源生成器实现 `ObservableObject` /
  `RelayCommand`
- 通过 `IStorageProvider` 提供原生文件选择器
- 使用 `JsonSettingsStore` 进行设置持久化
