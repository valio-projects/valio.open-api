using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.Cpp.Ast.Builders.Std;
using ValIO.Generators.Cpp.Ast.Renderers;
using ValIO.Generators.Cpp.Models;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Tests.Builders.Std;

/// <summary>
/// Тесты std-билдеров W4: чистая модель (без следов JSON), enum + wire-маппинг,
/// JSON-кодек (единственное место с wire-именами), наследование allOf, рекурсия.
/// </summary>
[TestFixture]
public class StdBuildersTests
{
    private readonly CppAstRenderer _renderer = new();
    private readonly StdNamingConvention _naming = new();
    private readonly CppGenerationOptions _options = new();

    private StdCppTypeMapper CreateMapper(IReadOnlySet<string> enumNames)
    {
        var mapper = new StdCppTypeMapper(_naming);
        mapper.SetEnumNamesForDocument(enumNames);
        return mapper;
    }

    private string Render(ValIO.Core.Ast.Nodes.AstNode node)
        => _renderer.Render(node).Replace("\r\n", "\n");

    private static IrProperty Prop(string originalName, string pascalName, IrPrimitiveKind kind, bool required = true) => new()
    {
        OriginalName = originalName,
        Name = pascalName,
        Type = new IrPrimitiveType { PrimitiveKind = kind },
        IsRequired = required,
    };

    // ===== Модель: чистый хедер =====

    [Test]
    public void Model_HeaderIsJsonFree_StdTypesAndOptional()
    {
        var model = new IrModel
        {
            Name = "Order",
            OriginalName = "orderDto",
            Properties =
            [
                Prop("buildingId", "BuildingId", IrPrimitiveKind.Long),
                Prop("displayName", "DisplayName", IrPrimitiveKind.String, required: false),
                Prop("count", "Count", IrPrimitiveKind.Integer),
            ],
        };

        var builder = new StdModelAstBuilder(CreateMapper(new HashSet<string>()), _naming);
        var content = Render(builder.Build(model, new HashSet<string>(), new Dictionary<string, IrModel>(), "Petstore"));

        content.Should().Contain("struct Order");
        content.Should().Contain("std::int64_t BuildingId = 0;");
        content.Should().Contain("std::optional<std::string> DisplayName;");
        content.Should().Contain("std::int32_t Count = 0;");
        content.Should().Contain("namespace Petstore");

        // Чистота: ни JSON, ни third-party — только std-инклуды.
        content.Should().NotContainAny(["nlohmann", "json", "to_json", "from_json"]);
        content.Should().Contain("#include <cstdint>");
        content.Should().Contain("#include <optional>");
        content.Should().Contain("#include <string>");
        content.Should().NotContain("#include \"");
    }

    [Test]
    public void Model_NestedReferences_AddQuotedIncludes()
    {
        var models = new Dictionary<string, IrModel>
        {
            ["Customer"] = new() { Name = "Customer", Properties = [Prop("name", "Name", IrPrimitiveKind.String)] },
        };
        var order = new IrModel
        {
            Name = "Order",
            Properties =
            [
                new IrProperty
                {
                    OriginalName = "buyer", Name = "Buyer",
                    Type = new IrModelReference { ModelName = "Customer" },
                    IsRequired = false,
                },
                new IrProperty
                {
                    OriginalName = "status", Name = "Status",
                    Type = new IrModelReference { ModelName = "OrderStatus" },
                    IsRequired = true,
                },
            ],
        };
        var enumNames = new HashSet<string> { "OrderStatus" };

        var builder = new StdModelAstBuilder(CreateMapper(enumNames), _naming);
        var content = Render(builder.Build(order, enumNames, models, "Petstore"));

        content.Should().Contain("#include \"Models/Customer.h\"");
        content.Should().Contain("#include \"Enums/OrderStatus.h\"");
        content.Should().Contain("std::optional<Customer> Buyer;");
        content.Should().Contain("OrderStatus Status;");
    }

    [Test]
    public void Model_AllOfSingleRefBase_RendersInheritance_OnlyOwnFields()
    {
        var baseModel = new IrModel
        {
            Name = "Animal",
            Properties = [Prop("name", "Name", IrPrimitiveKind.String)],
        };
        var derived = new IrModel
        {
            Name = "Dog",
            Properties =
            [
                Prop("name", "Name", IrPrimitiveKind.String), // приходит из flattening
                Prop("breed", "Breed", IrPrimitiveKind.String),
            ],
            Composition = new IrComposition
            {
                Kind = IrCompositionKind.AllOf,
                Types = [new IrModelReference { ModelName = "Animal" }],
            },
        };
        var lookup = new Dictionary<string, IrModel> { ["Animal"] = baseModel };

        var builder = new StdModelAstBuilder(CreateMapper(new HashSet<string>()), _naming);
        var content = Render(builder.Build(derived, new HashSet<string>(), lookup, "Petstore"));

        content.Should().Contain("struct Dog : public Animal");
        content.Should().Contain("std::string Breed;");
        content.Should().NotContain("std::string Name;"); // унаследовано — не переобъявляется
        content.Should().Contain("#include \"Models/Animal.h\"");
    }

    [Test]
    public void Model_RecursiveProperty_SkippedWithWarning()
    {
        var model = new IrModel
        {
            Name = "Node",
            Properties =
            [
                Prop("id", "Id", IrPrimitiveKind.Long),
                new IrProperty
                {
                    OriginalName = "child", Name = "Child",
                    Type = new IrModelReference { ModelName = "Node" },
                    IsRequired = false,
                },
            ],
        };

        var builder = new StdModelAstBuilder(CreateMapper(new HashSet<string>()), _naming);
        var warnings = new List<string>();
        var content = Render(builder.Build(model, new HashSet<string>(), new Dictionary<string, IrModel>(), "Petstore", warnings));

        content.Should().NotContain("Child");
        warnings.Should().Contain(w => w.Contains("recursive property 'child'"));
    }

    // ===== Кодек модели =====

    [Test]
    public void ModelJson_WireNamesFromOriginalName_SemanticsRequiredOptional()
    {
        var model = new IrModel
        {
            Name = "BuildingModel",
            OriginalName = "buildingModel",
            Properties =
            [
                Prop("buildingId", "BuildingId", IrPrimitiveKind.Long),
                Prop("displayName", "DisplayName", IrPrimitiveKind.String, required: false),
            ],
        };

        var builder = new StdModelJsonAstBuilder(_naming);
        var (header, source) = builder.Build(
            model, new HashSet<string>(), new Dictionary<string, IrModel>(), "Petstore", _options);

        var headerContent = Render(header);
        headerContent.Should().Contain("#include <nlohmann/json_fwd.hpp>");
        headerContent.Should().Contain("#include \"Models/BuildingModel.h\"");
        headerContent.Should().Contain("void to_json(nlohmann::json& j, const BuildingModel& value);");
        headerContent.Should().Contain("void from_json(const nlohmann::json& j, BuildingModel& value);");

        var sourceContent = Render(source);
        // wire-имена — OriginalName (не sanitized C++-имена!)
        sourceContent.Should().Contain("j[\"buildingId\"] = value.BuildingId;");
        sourceContent.Should().Contain("j.at(\"buildingId\").get_to(value.BuildingId);");
        sourceContent.Should().Contain("if (value.DisplayName)");
        sourceContent.Should().Contain("j[\"displayName\"] = *value.DisplayName;");
        sourceContent.Should().Contain("if (j.contains(\"displayName\") && !j.at(\"displayName\").is_null())");
        sourceContent.Should().Contain("j.at(\"displayName\").get_to(value.DisplayName);");
        // полный nlohmann — только в исходнике
        sourceContent.Should().Contain($"#include <{_options.JsonInclude}>");
    }

    [Test]
    public void ModelJson_Inheritance_CascadesThroughStaticCast()
    {
        var baseModel = new IrModel { Name = "Animal", Properties = [Prop("name", "Name", IrPrimitiveKind.String)] };
        var derived = new IrModel
        {
            Name = "Dog",
            Properties = [Prop("breed", "Breed", IrPrimitiveKind.String)],
            Composition = new IrComposition
            {
                Kind = IrCompositionKind.AllOf,
                Types = [new IrModelReference { ModelName = "Animal" }],
            },
        };
        var lookup = new Dictionary<string, IrModel> { ["Animal"] = baseModel };

        var builder = new StdModelJsonAstBuilder(_naming);
        var (_, source) = builder.Build(derived, new HashSet<string>(), lookup, "Petstore", _options);
        var content = Render(source);

        content.Should().Contain("to_json(j, static_cast<const Animal&>(value));");
        content.Should().Contain("from_json(j, static_cast<Animal&>(value));");
        content.Should().Contain("#include \"Serializers/AnimalJson.h\"");
    }

    [Test]
    public void ModelJson_NestedModelAndEnum_IncludeTheirCodecs()
    {
        var order = new IrModel
        {
            Name = "Order",
            Properties =
            [
                new IrProperty
                {
                    OriginalName = "status", Name = "Status",
                    Type = new IrModelReference { ModelName = "OrderStatus" },
                    IsRequired = true,
                },
                new IrProperty
                {
                    OriginalName = "buyer", Name = "Buyer",
                    Type = new IrModelReference { ModelName = "Customer" },
                    IsRequired = true,
                },
            ],
        };
        var lookup = new Dictionary<string, IrModel> { ["Customer"] = new() { Name = "Customer", Properties = [] } };
        var enumNames = new HashSet<string> { "OrderStatus" };

        var builder = new StdModelJsonAstBuilder(_naming);
        var (_, source) = builder.Build(order, enumNames, lookup, "Petstore", _options);
        var content = Render(source);

        content.Should().Contain("#include \"Serializers/CustomerJson.h\"");
        content.Should().Contain("#include \"Serializers/OrderStatusJson.h\"");
        content.Should().Contain("j.at(\"status\").get_to(value.Status);");
    }

    // ===== Enum =====

    [Test]
    public void Enum_StringBase_WireMappingWithoutJson()
    {
        var irEnum = new IrEnum
        {
            Name = "OrderStatus",
            BaseType = IrEnumBaseType.String,
            Values =
            [
                new IrEnumValue { Name = "placed", Value = "placed" },
                new IrEnumValue { Name = "approved", Value = "approved" },
            ],
        };

        var builder = new StdEnumAstBuilder(_naming);
        var content = Render(builder.Build(irEnum, "Petstore"));

        content.Should().Contain("enum class OrderStatus : std::int32_t");
        content.Should().Contain("Placed = 0,");
        content.Should().Contain("Approved = 1");
        content.Should().Contain("std::string_view ToWire(OrderStatus v)");
        content.Should().Contain("if (v == OrderStatus::Placed)");
        content.Should().Contain("return \"placed\";");
        content.Should().Contain("std::optional<OrderStatus> FromWire(std::string_view w)");
        content.Should().Contain("if (w == \"approved\")");
        content.Should().Contain("return OrderStatus::Approved;");
        content.Should().Contain("return std::nullopt;");
        // чистота: без JSON
        content.Should().NotContainAny(["nlohmann", "json"]);
        content.Should().Contain("#include <string_view>");
    }

    [Test]
    public void Enum_IntegerBases_WireNumbers()
    {
        var irEnum = new IrEnum
        {
            Name = "Level",
            BaseType = IrEnumBaseType.Integer,
            Values =
            [
                new IrEnumValue { Name = "low", Value = 1 },
                new IrEnumValue { Name = "high", Value = 2 },
            ],
        };

        var builder = new StdEnumAstBuilder(_naming);
        var content = Render(builder.Build(irEnum, "Petstore"));

        content.Should().Contain("Low = 1,");
        content.Should().Contain("High = 2");
        content.Should().Contain("std::int32_t ToWire(Level v)");
        content.Should().Contain("std::optional<Level> FromWire(std::int32_t w)");
        content.Should().Contain("if (w == 2)");
    }

    [Test]
    public void EnumJson_CodecUsesToWireFromWireAndThrowsOnUnknown()
    {
        var irEnum = new IrEnum
        {
            Name = "OrderStatus",
            BaseType = IrEnumBaseType.String,
            Values = [new IrEnumValue { Name = "placed", Value = "placed" }],
        };

        var builder = new StdEnumAstBuilder(_naming);
        var (header, source) = builder.BuildJson(irEnum, "Petstore", _options);

        var headerContent = Render(header);
        headerContent.Should().Contain("#include <nlohmann/json_fwd.hpp>");
        headerContent.Should().Contain("#include \"Enums/OrderStatus.h\"");
        headerContent.Should().Contain("void to_json(nlohmann::json& j, const OrderStatus& value);");
        headerContent.Should().Contain("void from_json(const nlohmann::json& j, OrderStatus& value);");

        var sourceContent = Render(source);
        sourceContent.Should().Contain("j = ToWire(value);");
        sourceContent.Should().Contain("FromWire(j.get<std::string>())");
        sourceContent.Should().Contain("if (parsed)");
        sourceContent.Should().Contain("value = *parsed;");
        sourceContent.Should().Contain("throw nlohmann::json::out_of_range::create(1701, \"unknown OrderStatus wire value\");");
    }
}
