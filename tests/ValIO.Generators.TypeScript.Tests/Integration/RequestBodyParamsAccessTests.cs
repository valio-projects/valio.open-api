using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Integration;

/// <summary>
/// Регрессия: тело запроса всегда лежит в params.body (JSON / прочее) или params.form
/// (multipart/url-encoded) — даже когда тело единственная группа параметров (flat-интерфейс).
/// Раньше flat-режим в axios/fetch обращался к самому `params`, из-за чего
/// `Schema.parse(params)` валидировал { body: ... } вместо тела, а `data`/`body` уходили
/// неверными. Проверяем axios и fetch, JSON и form, flat и aggregate.
/// </summary>
[TestFixture]
public class RequestBodyParamsAccessTests
{
    // /employees POST — ТОЛЬКО тело (flat). /employees/{id} PUT — тело + route (aggregate).
    private const string Doc = """
    {
      "openapi":"3.0.3",
      "info":{"title":"T","version":"1.0.0"},
      "paths":{
        "/employees":{
          "post":{
            "operationId":"CreateEmployee",
            "requestBody":{"required":true,"content":{"application/json":{"schema":{"$ref":"#/components/schemas/Employee"}}}},
            "responses":{"201":{"description":"ok","content":{"application/json":{"schema":{"$ref":"#/components/schemas/Employee"}}}}}
          }
        },
        "/employees/{id}":{
          "put":{
            "operationId":"UpdateEmployee",
            "parameters":[{"name":"id","in":"path","required":true,"schema":{"type":"integer"}}],
            "requestBody":{"required":true,"content":{"application/json":{"schema":{"$ref":"#/components/schemas/Employee"}}}},
            "responses":{"200":{"description":"ok","content":{"application/json":{"schema":{"$ref":"#/components/schemas/Employee"}}}}}
          }
        },
        "/employees/upload":{
          "post":{
            "operationId":"UploadEmployeePhoto",
            "requestBody":{"required":true,"content":{"multipart/form-data":{"schema":{"type":"object","properties":{"file":{"type":"string","format":"binary"},"caption":{"type":"string"}}}}}},
            "responses":{"204":{"description":"ok"}}
          }
        }
      },
      "components":{"schemas":{
        "Employee":{"type":"object","required":["name"],"properties":{"id":{"type":"integer"},"name":{"type":"string"}}}
      }}
    }
    """;

    private static string Norm(string p) => p.Replace('\\', '/');

    [Test]
    public async Task Axios_FlatJsonBody_ValidatesAndSendsParamsBody()
    {
        using var harness = new PipelineTestHarness();
        var doc = await PipelineTestHarness.LoadDocumentFromJsonAsync(Doc);
        var ctx = await harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["axios"], ValidationLibrary = "zod4" });

        var f = ctx.GeneratedFiles.Single(x => Norm(x.RelativePath) == "api/createEmployee/createEmployee.axios.ts").Content;
        f.Should().Contain("parse(params.body)");
        // Точный баг: parse(params) с немедленной закрывающей скобкой.
        f.Should().NotContain("parse(params)");
    }

    [Test]
    public async Task Axios_AggregateJsonBody_UsesParamsBodyAndParamsRoute()
    {
        using var harness = new PipelineTestHarness();
        var doc = await PipelineTestHarness.LoadDocumentFromJsonAsync(Doc);
        var ctx = await harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["axios"], ValidationLibrary = "zod4" });

        var f = ctx.GeneratedFiles.Single(x => Norm(x.RelativePath) == "api/updateEmployee/updateEmployee.axios.ts").Content;
        f.Should().Contain("parse(params.body)");
        f.Should().Contain("params.route");
        f.Should().NotContain("parse(params)");
    }

    [Test]
    public async Task Fetch_FlatJsonBody_StringifiesParamsBody()
    {
        using var harness = new PipelineTestHarness();
        var doc = await PipelineTestHarness.LoadDocumentFromJsonAsync(Doc);
        var ctx = await harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"], ValidationLibrary = "zod4" });

        var f = ctx.GeneratedFiles.Single(x => Norm(x.RelativePath) == "api/createEmployee/createEmployee.fetch.ts").Content;
        f.Should().Contain("parse(params.body)");
        f.Should().NotContain("parse(params)");
    }

    [Test]
    public async Task Axios_FlatJsonBody_NoValidation_SendsParamsBodyAsData()
    {
        using var harness = new PipelineTestHarness();
        var doc = await PipelineTestHarness.LoadDocumentFromJsonAsync(Doc);
        var ctx = await harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["axios"] });

        var f = ctx.GeneratedFiles.Single(x => Norm(x.RelativePath) == "api/createEmployee/createEmployee.axios.ts").Content;
        // Без валидации тело идёт прямо в axios data как params.body.
        f.Should().Contain("data: params.body");
    }

    [Test]
    public async Task Axios_FlatFormBody_UsesParamsForm()
    {
        using var harness = new PipelineTestHarness();
        var doc = await PipelineTestHarness.LoadDocumentFromJsonAsync(Doc);
        var ctx = await harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["axios"], ValidationLibrary = "zod4" });

        var f = ctx.GeneratedFiles.Single(x => Norm(x.RelativePath) == "api/uploadEmployeePhoto/uploadEmployeePhoto.axios.ts").Content;
        f.Should().Contain("params.form");
        f.Should().NotContain("parse(params)");
        f.Should().NotContain("params.body");
    }
}
