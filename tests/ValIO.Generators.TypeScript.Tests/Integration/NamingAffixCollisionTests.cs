using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Models;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Integration;

/// <summary>
/// Регрессия исходного бага: DTO-схема и operation-функция получали одинаковый идентификатор
/// (<c>CreateJournalSchema</c>), из-за чего barrel выдавал TS2308. Настраиваемый model-affix
/// разводит идентификаторы, при этом имена файлов и deep-path импорты остаются прежними.
/// </summary>
[TestFixture]
public class NamingAffixCollisionTests
{
    private const string CollisionDoc = """
    {
      "openapi":"3.0.3",
      "info":{"title":"T","version":"1.0.0"},
      "paths":{
        "/journals/schema":{
          "post":{
            "operationId":"CreateJournalSchema",
            "requestBody":{"required":true,"content":{"application/json":{"schema":{"$ref":"#/components/schemas/CreateJournalSchema"}}}},
            "responses":{"200":{"description":"ok","content":{"application/json":{"schema":{"$ref":"#/components/schemas/CreateJournalSchema"}}}}}
          }
        }
      },
      "components":{"schemas":{
        "CreateJournalSchema":{"type":"object","required":["title"],"properties":{"title":{"type":"string"}}}
      }}
    }
    """;

    private static string Norm(string p) => p.Replace('\\', '/');

    [Test]
    public async Task ModelSuffix_DisambiguatesDtoFromOperationFunction_KeepsFilePaths()
    {
        using var harness = new PipelineTestHarness();
        var doc = await PipelineTestHarness.LoadDocumentFromJsonAsync(CollisionDoc);

        var ctx = await harness.RunAsync(
            doc,
            new TypeScriptGenerationOptions { Transports = ["axios"], ValidationLibrary = "zod4" },
            naming: new NamingConventions { ModelSuffix = "Model" });

        var files = ctx.GeneratedFiles;

        // 1) Имя файла модели НЕ изменилось (deep-path импорты сохранены).
        var modelFile = files.Single(f => Norm(f.RelativePath) == "models/createJournalSchema/createJournalSchema.ts");
        // ...но экспортируемый интерфейс получил affix.
        modelFile.Content.Should().Contain("interface CreateJournalSchemaModel");

        // 2) Функция-операция осталась без model-affix (method-affix не задан).
        var endpointFile = files.Single(f =>
            Norm(f.RelativePath) == "api/createJournalSchema/createJournalSchema.axios.ts");
        endpointFile.Content.Should().Contain("function CreateJournalSchema(");
        // Эндпоинт ссылается на affix-имя модели, импортируя её из ПРЕЖНЕГО пути.
        endpointFile.Content.Should().Contain("CreateJournalSchemaModel");

        // 3) Barrel ре-экспортирует оба файла по их (неизменным) путям; идентификаторы различаются
        //    (interface CreateJournalSchemaModel vs function CreateJournalSchema) → нет TS2308.
        var index = files.Single(f => Norm(f.RelativePath) == "index.ts");
        index.Content.Should().Contain("export * from \"./models/createJournalSchema/createJournalSchema\"");
        index.Content.Should().Contain("export * from \"./api/createJournalSchema/createJournalSchema.axios\"");

        // 4) Affix НЕ просочился в имена файлов/папок (files unchanged).
        files.Should().NotContain(f => Norm(f.RelativePath).Contains("createJournalSchemaModel"));

        // 5) Zod-схема названа по affix-типу, но импортирует модель из прежнего пути.
        var schemaFile = files.Single(f => Norm(f.RelativePath) == "models/createJournalSchema/createJournalSchema.schema.ts");
        schemaFile.Content.Should().Contain("CreateJournalSchemaModelSchema");
        schemaFile.Content.Should().Contain("import type { CreateJournalSchemaModel }");
    }

    [Test]
    public async Task NoAffix_ProducesUnchangedNames()
    {
        using var harness = new PipelineTestHarness();
        var doc = await PipelineTestHarness.LoadDocumentFromJsonAsync(CollisionDoc);

        var ctx = await harness.RunAsync(
            doc,
            new TypeScriptGenerationOptions { Transports = ["axios"] });

        var modelFile = ctx.GeneratedFiles.Single(f => Norm(f.RelativePath) == "models/createJournalSchema/createJournalSchema.ts");
        modelFile.Content.Should().Contain("interface CreateJournalSchema");
        modelFile.Content.Should().NotContain("CreateJournalSchemaModel");
    }
}
