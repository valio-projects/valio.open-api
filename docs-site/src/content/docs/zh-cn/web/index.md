---
title: Web UI
description: 在浏览器中运行 ValIO.Web。
translationKey: web
---

`ValIO.Web` 是一个 ASP.NET Core 10 + Blazor Server 应用。它对外提供
与 CLI 相同的生成管道，但通过浏览器中的表单驱动。

## 本地运行

```bash
dotnet run --project src/ValIO.Web
# http://localhost:5080
```

## 功能

- **架构输入** —— 粘贴 URL 或上传 `.json` / `.yaml` 文件（最大
  10 MB）。
- **语言选择** —— 任何通过 `AddAllGeneratorPlugins` 注册的插件都会
  出现在下拉菜单中。
- **动态插件选项表单** —— 控件由每个插件的 `OptionDescription`
  列表生成。Bool → 开关、EnumSingle → 下拉、EnumMulti → 标签组等。
- **输出为 ZIP** —— 服务端通过 `ZipStreamOutputSink` 在内存中拼装
  zip 归档并以流的形式回传；浏览器通过 blob URL 完成下载。
- **语言切换器** —— 右上角菜单会设置 cookie，并以所选语言（`en`、
  `ru`、`de`、`zh-CN`）重新加载页面。

## API 端点

表单会向 `POST /api/generate` 发送 JSON：

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

必须设置 `schemaUrl` **或** `schemaContentBase64`，但不能同时设置。
响应类型为 `application/zip`，并带有 `Content-Disposition: attachment`
头。

## 安全

- URL 抓取器拒绝非 `http(s)` 协议的 URL，以及任何解析到 loopback、
  link-local 或 RFC1918 私有网段的主机。
- 上传大小上限 10 MB。
- 所有请求都在请求作用域内运行；不存在跨 circuit 的状态。
