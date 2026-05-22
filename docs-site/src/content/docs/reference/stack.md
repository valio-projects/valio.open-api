---
title: Technology stack
description: Every framework and library that makes ValIO and its docs run.
---

## Engine

| Concern               | Choice                                                |
|-----------------------|-------------------------------------------------------|
| Runtime               | .NET 10                                               |
| Language              | C# 14                                                 |
| OpenAPI parsing       | `Microsoft.OpenApi` (3.0 + 3.1 readers)               |
| Configuration         | `System.Text.Json` (JSON) + `YamlDotNet` (YAML)       |
| Dependency injection  | `Microsoft.Extensions.DependencyInjection`            |
| Logging               | `Microsoft.Extensions.Logging` (console provider)     |
| Localization          | `Microsoft.Extensions.Localization` + `.resx` satellite assemblies |
| HTTP (schema fetch)   | `HttpClient` via `IHttpClientFactory`                 |
| Zip output            | `System.IO.Compression`                               |
| Testing               | NUnit 4 + FluentAssertions                            |

## Frontends

| Frontend              | Stack                                                  |
|-----------------------|--------------------------------------------------------|
| `ValIO.Cli`           | `System.CommandLine` (v2 beta)                         |
| `ValIO.Web`           | ASP.NET Core 10 + Blazor Server + MudBlazor            |
| `ValIO.Desktop`       | Avalonia 11 + FluentTheme + `CommunityToolkit.Mvvm`    |

## Generated code targets

| Language     | Primary HTTP transport(s)                  | Validation / schemas         | Notable extras            |
|--------------|--------------------------------------------|------------------------------|---------------------------|
| TypeScript   | fetch / axios / ky / undici / ofetch / got / angular-rxjs / angular-promise / solid | Zod / Valibot / TypeBox | TanStack Query, SWR, MSW |
| C#           | `HttpClient`                               | DataAnnotations attributes   | OpenTelemetry traces/metrics |
| Java         | OkHttp                                     | —                            | Java 21                  |
| Kotlin       | OkHttp                                     | —                            | Coroutines, kotlinx/moshi |
| Python       | `httpx` (async + sync)                     | Pydantic                     | —                        |
| PHP          | Guzzle                                     | —                            | PHP 8                    |
| Swift        | `URLSession`                               | —                            | async/await              |
| Dart         | `dio`                                      | —                            | —                        |
| F#           | `HttpClient`                               | —                            | Records, computation exprs |
| Ruby         | Faraday                                    | —                            | —                        |
| Rust         | `reqwest`                                  | —                            | —                        |

## Docs site

This site (`docs-site/`) runs on:

| Concern               | Choice                                                |
|-----------------------|-------------------------------------------------------|
| Framework             | Astro                                                 |
| Theme                 | Starlight                                             |
| Diagrams              | Mermaid via `rehype-mermaid` (inline SVG at build)    |
| Link validation       | `starlight-links-validator`                           |
| Image lightbox        | `starlight-image-zoom`                                |
| Code blocks           | `expressiveCode` (GitHub Dark/Light themes)           |
| Locales               | English, Russian, German, Simplified Chinese          |

## Build & release

| Concern               | Choice                                                |
|-----------------------|-------------------------------------------------------|
| Build system          | .NET SDK (`dotnet build`, `dotnet pack`)              |
| CLI distribution      | NuGet (`dotnet tool install --global ValIO.Cli`), npm wrapper, GitHub Releases tarballs |
| Docs deploy           | Static Astro build (any static host: Netlify, Pages, S3) |
