---
title: valio generate
description: 从 OpenAPI 架构生成代码。
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

## 选项

| 选项                  | 说明                                                                 |
|-----------------------|----------------------------------------------------------------------|
| `-s` / `--schema`     | OpenAPI 架构的路径或 URL。不使用 `--config` 时必填。                 |
| `-o` / `--output`     | 文件夹或 `.zip` 文件路径。不使用 `--config` 时必填。                 |
| `-l` / `--language`   | 生成器 ID（`typescript`、`csharp` 等）。不使用 `--config` 时必填。   |
| `-c` / `--config`     | 包含一个或多个项目的 JSON 或 YAML 配置文件。                         |
| `-p` / `--projects`   | 配置文件中的项目名称。默认值：全部。                                 |
| `--format`            | `folder`、`zip` 或 `auto`。默认 `auto`。                             |
| `--zip`               | `--format zip` 的快捷方式。                                          |

## 输出格式的解析规则

根据 `--output` 扩展名自动判断：

| `--output`     | `--format` | 结果                                       |
|----------------|------------|--------------------------------------------|
| `./out`        | （默认）   | 文件夹 `./out/`                            |
| `./out.zip`    | （默认）   | zip 文件 `./out.zip`                       |
| `./out`        | `zip`      | zip 文件 `./out.zip`（自动追加 `.zip`）     |
| `./out.zip`    | `folder`   | 名为 `./out.zip/` 的文件夹                 |

## 示例

```bash
# 输出到文件夹
valio generate -s ./openapi.json -o ./generated -l typescript

# 输出为 zip（自动识别）
valio generate -s ./openapi.json -o ./generated.zip -l typescript

# 通过配置文件运行多个项目
valio generate -c ./valio.config.json -p web,mobile
```
