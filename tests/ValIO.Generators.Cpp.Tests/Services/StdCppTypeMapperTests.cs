using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Tests.Services;

/// <summary>
/// Таблица маппинга IrType → std-C++ для std-профиля (вся таблица из плана W3).
/// </summary>
[TestFixture]
public class StdCppTypeMapperTests
{
    private readonly StdCppTypeMapper _mapper = new(new StdNamingConvention());

    private static IrPrimitiveType Prim(IrPrimitiveKind kind) => new() { PrimitiveKind = kind };

    private string SimpleName(IrType type) => ((SimpleTypeNode)_mapper.MapType(type)).Name.Name;

    [TestCase(IrPrimitiveKind.String, "std::string")]
    [TestCase(IrPrimitiveKind.Number, "double")]
    [TestCase(IrPrimitiveKind.Integer, "std::int32_t")]
    [TestCase(IrPrimitiveKind.Long, "std::int64_t")]
    [TestCase(IrPrimitiveKind.Boolean, "bool")]
    [TestCase(IrPrimitiveKind.DateTime, "std::string")]
    [TestCase(IrPrimitiveKind.Date, "std::string")]
    [TestCase(IrPrimitiveKind.Time, "std::string")]
    [TestCase(IrPrimitiveKind.Uuid, "std::string")]
    [TestCase(IrPrimitiveKind.Void, "void")]
    public void Primitives_MapToStdTypes(IrPrimitiveKind kind, string expected)
        => SimpleName(Prim(kind)).Should().Be(expected);

    [TestCase(IrPrimitiveKind.Binary)]
    [TestCase(IrPrimitiveKind.ByteArray)]
    [TestCase(IrPrimitiveKind.File)]
    public void BinaryTypes_MapToByteVector(IrPrimitiveKind kind)
    {
        var t = (GenericTypeNode)_mapper.MapType(Prim(kind));
        t.Name.Name.Should().Be("std::vector");
        ((SimpleTypeNode)t.TypeArguments[0]).Name.Name.Should().Be("std::uint8_t");
    }

    [Test]
    public void Any_MapsToString_WithWarning()
    {
        SimpleName(Prim(IrPrimitiveKind.Any)).Should().Be("std::string");
        _mapper.Warnings.Should().Contain(w => w.Contains("Any"));
    }

    [Test]
    public void Array_MapsToVector()
    {
        var t = (GenericTypeNode)_mapper.MapType(new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) });
        t.Name.Name.Should().Be("std::vector");
        ((SimpleTypeNode)t.TypeArguments[0]).Name.Name.Should().Be("std::string");
    }

    [Test]
    public void Dictionary_MapsToUnorderedMap()
    {
        var t = (GenericTypeNode)_mapper.MapType(new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = Prim(IrPrimitiveKind.Integer),
        });
        t.Name.Name.Should().Be("std::unordered_map");
        t.TypeArguments.Should().HaveCount(2);
    }

    [Test]
    public void Nullable_MapsToOptional_InnerPreserved()
    {
        var t = (GenericTypeNode)_mapper.MapType(new IrNullableType { InnerType = Prim(IrPrimitiveKind.String) });
        t.Name.Name.Should().Be("std::optional");
        ((SimpleTypeNode)t.TypeArguments[0]).Name.Name.Should().Be("std::string");
    }

    [Test]
    public void Optional_WrapsInner_InOptional()
    {
        var t = (GenericTypeNode)_mapper.MapType(new IrOptionalType { InnerType = Prim(IrPrimitiveKind.Long) });
        t.Name.Name.Should().Be("std::optional");
        ((SimpleTypeNode)t.TypeArguments[0]).Name.Name.Should().Be("std::int64_t");
    }

    [Test]
    public void ModelReference_MapsToPlainName_StructVsEnum()
    {
        _mapper.SetEnumNamesForDocument(new HashSet<string> { "OrderStatus" });

        SimpleName(new IrModelReference { ModelName = "BuildingModel" }).Should().Be("BuildingModel");
        SimpleName(new IrModelReference { ModelName = "OrderStatus" }).Should().Be("OrderStatus");
    }

    [Test]
    public void Union_MapsToRawJsonString_WithWarning()
    {
        SimpleName(new IrUnionType { Types = [Prim(IrPrimitiveKind.String), Prim(IrPrimitiveKind.Integer)] })
            .Should().Be("std::string");
        _mapper.Warnings.Should().Contain(w => w.Contains("union"));
    }

    [Test]
    public void Intersection_MapsToRawJsonString_WithWarning()
    {
        SimpleName(new IrIntersectionType { Types = [Prim(IrPrimitiveKind.String)] })
            .Should().Be("std::string");
        _mapper.Warnings.Should().Contain(w => w.Contains("intersection"));
    }

    [TestCase(IrPrimitiveKind.Number, "0.0")]
    [TestCase(IrPrimitiveKind.Integer, "0")]
    [TestCase(IrPrimitiveKind.Long, "0")]
    [TestCase(IrPrimitiveKind.Boolean, "false")]
    [TestCase(IrPrimitiveKind.String, null)]
    public void DefaultValue_ForPrimitives(IrPrimitiveKind kind, string? expected)
        => _mapper.GetDefaultValue(Prim(kind)).Should().Be(expected);

    [Test]
    public void FormatQueryLiteral_BoolIntegerEnum()
    {
        _mapper.SetEnumNamesForDocument(new HashSet<string> { "OrderStatus" });

        _mapper.FormatQueryLiteral(Prim(IrPrimitiveKind.Boolean), "IncludeDeleted")
            .Should().Be("(IncludeDeleted ? \"true\" : \"false\")");
        _mapper.FormatQueryLiteral(Prim(IrPrimitiveKind.Long), "Id")
            .Should().Be("std::to_string(Id)");
        _mapper.FormatQueryLiteral(new IrModelReference { ModelName = "OrderStatus" }, "Status")
            .Should().Be("std::string(ToWire(Status))");
        _mapper.FormatQueryLiteral(Prim(IrPrimitiveKind.String), "Filter")
            .Should().Be("Filter");
    }

    [Test]
    public void FormatQueryLiteral_ModelReference_SerializesToJsonDump()
    {
        _mapper.SetEnumNamesForDocument(new HashSet<string> { "OrderStatus" });

        // Модель в query-параметре — компилируемое wire-представление: JSON-строка.
        _mapper.FormatQueryLiteral(new IrModelReference { ModelName = "BuildingModel" }, "Filter")
            .Should().Be("ValIO::Dump(Filter)");
    }

    [Test]
    public void StdIncludesFor_CollectsByUsage()
    {
        var type = new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = new IrOptionalType { InnerType = Prim(IrPrimitiveKind.Long) },
        };

        StdCppTypeMapper.StdIncludesFor(type)
            .Should().BeEquivalentTo(["<cstdint>", "<optional>", "<string>", "<unordered_map>"]);
    }
}
