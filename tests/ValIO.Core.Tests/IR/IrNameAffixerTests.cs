using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR.Builders;
using ValIO.Core.IR.Types;
using ValIO.Core.Tests.TestHelpers;

namespace ValIO.Core.Tests.IR;

/// <summary>
/// Проверяет IR-преобразование affix (prefix/suffix имён моделей/методов/enum'ов):
/// переименование сущностей, синхронное обновление ссылок, разграничение model/enum-affix,
/// сохранение исходных имён в Original*, и полную no-op семантику при пустом affix.
/// </summary>
[TestFixture]
public class IrNameAffixerTests
{
    // Схема Widget совпадает по имени с operationId "Widget" — это и есть исходная коллизия.
    private const string Doc = """
    {
      "openapi":"3.0.3",
      "info":{"title":"T","version":"1.0.0"},
      "paths":{
        "/widgets":{
          "post":{
            "operationId":"Widget",
            "requestBody":{"content":{"application/json":{"schema":{"$ref":"#/components/schemas/Widget"}}}},
            "responses":{"200":{"description":"ok","content":{"application/json":{"schema":{"$ref":"#/components/schemas/Widget"}}}}}
          }
        }
      },
      "components":{"schemas":{
        "Color":{"type":"string","enum":["red","green"]},
        "Gadget":{"type":"object","properties":{"id":{"type":"string"}}},
        "Widget":{"type":"object","properties":{
          "color":{"$ref":"#/components/schemas/Color"},
          "child":{"$ref":"#/components/schemas/Gadget"}
        }}
      }}
    }
    """;

    [Test]
    public async Task ModelAndEnumAffixes_RenameDefinitionsAndReferences_WithDistinctAffixes()
    {
        var ir = await IrTestHarness.BuildIrAsync(Doc, new NameAffixOptions
        {
            ModelSuffix = "Model",
            EnumSuffix = "Enum",
        });

        // Определения переименованы; исходные имена сохранены в Original*.
        var widget = ir.Models.Single(m => m.Name == "WidgetModel");
        widget.OriginalName.Should().Be("Widget");
        ir.Models.Should().Contain(m => m.Name == "GadgetModel" && m.OriginalName == "Gadget");

        var color = ir.Enums.Single(e => e.Name == "ColorEnum");
        color.OriginalName.Should().Be("Color");

        // Ссылка на МОДЕЛЬ получает model-affix; ссылка на ENUM получает enum-affix
        // (разграничение по виду цели — критично для C++ и импортов).
        var childRef = (IrModelReference)widget.Properties.Single(p => p.Name == "child").Type;
        childRef.ModelName.Should().Be("GadgetModel");
        childRef.OriginalModelName.Should().Be("Gadget");

        var colorRef = (IrModelReference)widget.Properties.Single(p => p.Name == "color").Type;
        colorRef.ModelName.Should().Be("ColorEnum");
        colorRef.OriginalModelName.Should().Be("Color");

        // Метод не затронут model/enum-affix; ссылки в теле/ответе синхронны.
        var op = ir.Clients.SelectMany(c => c.Operations).Single();
        op.MethodName.Should().Be("Widget");
        op.OriginalMethodName.Should().Be("Widget");
        ((IrModelReference)op.RequestBody!.Type).ModelName.Should().Be("WidgetModel");
        ((IrModelReference)op.Responses.Single(r => r.Type != null).Type!).ModelName.Should().Be("WidgetModel");

        // Граф зависимостей построен на affix-именах (совпадает с тем, что читают генераторы).
        ir.TypeDependencies.TopologicalOrder.Should().Contain("WidgetModel");
        ir.TypeDependencies.TopologicalOrder.Should().NotContain("Widget");
        ir.TypeDependencies.Dependencies["WidgetModel"].Should().Contain(new[] { "GadgetModel", "ColorEnum" });
    }

    [Test]
    public async Task MethodAffix_RenamesOperation_LeavesModelsUntouched()
    {
        var ir = await IrTestHarness.BuildIrAsync(Doc, new NameAffixOptions { MethodSuffix = "Method" });

        var op = ir.Clients.SelectMany(c => c.Operations).Single();
        op.MethodName.Should().Be("WidgetMethod");
        op.OriginalMethodName.Should().Be("Widget");

        // Модель не затронута method-affix → имя функции (WidgetMethod) и тип модели (Widget) различаются.
        ir.Models.Should().Contain(m => m.Name == "Widget");
    }

    [Test]
    public async Task PrefixAndSuffix_Compose()
    {
        var ir = await IrTestHarness.BuildIrAsync(Doc, new NameAffixOptions
        {
            ModelPrefix = "Api",
            ModelSuffix = "Dto",
        });

        ir.Models.Should().Contain(m => m.Name == "ApiWidgetDto" && m.OriginalName == "Widget");
        var widget = ir.Models.Single(m => m.Name == "ApiWidgetDto");
        ((IrModelReference)widget.Properties.Single(p => p.Name == "child").Type).ModelName.Should().Be("ApiGadgetDto");
    }

    [Test]
    public async Task EmptyAffix_IsNoOp()
    {
        var ir = await IrTestHarness.BuildIrAsync(Doc, NameAffixOptions.None);

        foreach (var m in ir.Models)
            m.OriginalName.Should().Be(m.Name);
        foreach (var e in ir.Enums)
            e.OriginalName.Should().Be(e.Name);
        foreach (var op in ir.Clients.SelectMany(c => c.Operations))
            op.OriginalMethodName.Should().Be(op.MethodName);

        ir.Models.Should().Contain(m => m.Name == "Widget");
        ir.Enums.Should().Contain(e => e.Name == "Color");
    }
}
