---
title: 快速上手
description: 安装 ValIO，提供 OpenAPI 架构，生成客户端代码。
---

ValIO 提供三种共享同一生成器内核的前端：

- **CLI** —— `valio` 可执行文件（单个 .NET 10 二进制）。
- **Web** —— ASP.NET Core 10 + Blazor Server（`ValIO.Web`）。
- **桌面** —— Avalonia 11（`ValIO.Desktop`），跨平台。

## 快速命令行示例

```bash
# 将 TypeScript 客户端输出到文件夹
valio generate -s ./openapi.json -o ./generated -l typescript

# 直接打包为 zip
valio generate -s ./openapi.json -o ./generated.zip -l typescript

# 或使用 --zip，会自动附加 .zip 后缀
valio generate -s ./openapi.json -o ./generated --zip -l typescript

# 切换界面语言（CLI 消息以及 Web/桌面 UI）
valio --lang zh-CN generate -s ./openapi.json -o ./generated -l typescript
```

更多语言细节请参阅 [CLI 文档](/zh-cn/cli/) 和
[配置文件参考](/zh-cn/configuration/)（其余页面暂时为英文版本，翻译稍后补齐）。
