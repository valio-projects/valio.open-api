using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi;
using ValIO.Core.Models;
using ValIO.Core.Services;
using ValIO.Generators.TypeScript.Models;

namespace ValIO.Generators.TypeScript.Tests.TestHelpers;

/// <summary>
/// Минимальный harness, прогоняющий <see cref="TypeScriptCodeGenerator"/> на встроенной OpenAPI-схеме
/// с одним GET и одним POST endpoint, чтобы тесты могли проверять содержимое
/// <c>*.hook.ts</c> файлов для разных значений <see cref="TanStackFramework"/>.
/// </summary>
public static class HookGenerationHarness
{
    private const string SchemaJson = """
        {
          "openapi": "3.0.0",
          "info": { "title": "TestApi", "version": "1.0.0" },
          "paths": {
            "/users/{id}": {
              "get": {
                "operationId": "getUserById",
                "parameters": [
                  { "name": "id", "in": "path", "required": true, "schema": { "type": "integer" } }
                ],
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": { "schema": { "$ref": "#/components/schemas/User" } }
                    }
                  }
                }
              }
            },
            "/pets": {
              "post": {
                "operationId": "createPet",
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": { "schema": { "$ref": "#/components/schemas/Pet" } }
                  }
                },
                "responses": {
                  "201": {
                    "description": "ok",
                    "content": {
                      "application/json": { "schema": { "$ref": "#/components/schemas/Pet" } }
                    }
                  }
                }
              }
            }
          },
          "components": {
            "schemas": {
              "User": { "type": "object", "required": ["id", "name"], "properties": { "id": { "type": "integer" }, "name": { "type": "string" } } },
              "Pet": { "type": "object", "required": ["name"], "properties": { "name": { "type": "string" } } }
            }
          }
        }
        """;

    /// <summary>
    /// Запускает TypeScript генератор для встроенной схемы с указанным framework.
    /// Возвращает все сгенерированные файлы.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> GenerateAsync(
        TanStackFramework framework,
        TypeScriptHttpTransports transports = TypeScriptHttpTransports.Fetch)
    {
        var bytes = Encoding.UTF8.GetBytes(SchemaJson);
        await using var stream = new MemoryStream(bytes);
        var readResult = await OpenApiDocument.LoadAsync(stream, "json");
        var document = readResult.Document ?? throw new InvalidOperationException("Failed to parse schema");

        var namingConverter = new NamingConverter();
        var typeMapper = new TypeScriptTypeMapper(namingConverter);
        var generator = new TypeScriptCodeGenerator(
            typeMapper,
            namingConverter,
            new FileWriter(),
            NullLogger<TypeScriptCodeGenerator>.Instance,
            NullLoggerFactory.Instance);

        var config = new GenerationConfiguration
        {
            Language = GenerationLanguage.TypeScript,
            OutputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            GenerateModels = true,
            GenerateClients = true,
            GenerateEnums = true,
            Options = new GenerationOptions
            {
                TypeScriptOptions = new TypeScriptGenerationOptions
                {
                    ModelFormat = TypeScriptModelFormat.Interfaces,
                    HttpTransports = transports,
                    UseTanStackQuery = true,
                    TanStackFramework = framework,
                },
            },
            FileOrganization = new FileOrganization
            {
                SeparateModels = true,
                SeparateClients = true,
                ModelsPath = "models",
                ClientsPath = "endpoints",
            },
        };

        var result = await generator.GenerateAsync(document, config);
        if (!result.Success)
            throw new InvalidOperationException("Generation failed: " + string.Join("; ", result.Errors));

        try { Directory.Delete(config.OutputPath, recursive: true); }
        catch { /* best-effort cleanup */ }

        return result.GeneratedFiles;
    }

    /// <summary>Возвращает содержимое hook файла для указанной операции, либо null.</summary>
    public static string? FindHook(IReadOnlyDictionary<string, string> files, string operationName)
    {
        return files
            .Where(kv => kv.Key.Replace('\\', '/').EndsWith($"/{operationName}/{operationName}.hook.ts", StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Value)
            .FirstOrDefault();
    }
}
