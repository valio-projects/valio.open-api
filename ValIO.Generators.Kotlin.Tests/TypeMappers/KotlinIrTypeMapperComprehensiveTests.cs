using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.Kotlin.Tests.TypeMappers;

/// <summary>
/// Exhaustive Kotlin IR-to-AST coverage: all primitives + composite shapes.
/// </summary>
[TestFixture]
public class KotlinIrTypeMapperComprehensiveTests
{
    private readonly KotlinIrTypeMapper _mapper = new();

    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    [TestCase(IrPrimitiveKind.String,    "String")]
    [TestCase(IrPrimitiveKind.Integer,   "Int")]
    [TestCase(IrPrimitiveKind.Long,      "Long")]
    [TestCase(IrPrimitiveKind.Number,    "Double")]
    [TestCase(IrPrimitiveKind.Boolean,   "Boolean")]
    [TestCase(IrPrimitiveKind.Date,      "LocalDate")]
    [TestCase(IrPrimitiveKind.DateTime,  "Instant")]
    [TestCase(IrPrimitiveKind.Time,      "LocalTime")]
    [TestCase(IrPrimitiveKind.Uuid,      "UUID")]
    [TestCase(IrPrimitiveKind.Binary,    "ByteArray")]
    [TestCase(IrPrimitiveKind.ByteArray, "ByteArray")]
    [TestCase(IrPrimitiveKind.File,      "File")]
    [TestCase(IrPrimitiveKind.Any,       "Any")]
    [TestCase(IrPrimitiveKind.Void,      "Unit")]
    public void AllPrimitives_MapToKotlinName(IrPrimitiveKind kind, string expected)
    {
        var simple = (SimpleTypeNode)_mapper.MapType(Prim(kind));
        simple.Name.Name.Should().Be(expected);
    }

    [Test]
    public void Array_BecomesListGeneric()
    {
        var t = new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) };
        var g = (GenericTypeNode)_mapper.MapType(t);
        g.Name.Name.Should().Be("List");
        ((SimpleTypeNode)g.TypeArguments[0]).Name.Name.Should().Be("String");
    }

    [Test]
    public void Dictionary_BecomesMapGeneric_With2Args()
    {
        var t = new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = Prim(IrPrimitiveKind.Integer),
        };
        var g = (GenericTypeNode)_mapper.MapType(t);
        g.Name.Name.Should().Be("Map");
        g.TypeArguments.Should().HaveCount(2);
    }

    [Test]
    public void Nullable_WrapsAsNullableNode()
    {
        var t = new IrNullableType { InnerType = Prim(IrPrimitiveKind.String) };
        _mapper.MapType(t).Should().BeOfType<NullableTypeNode>();
    }

    [Test]
    public void Optional_StripsToInner()
    {
        var t = new IrOptionalType { InnerType = Prim(IrPrimitiveKind.Integer) };
        ((SimpleTypeNode)_mapper.MapType(t)).Name.Name.Should().Be("Int");
    }

    [Test]
    public void UnionAndIntersection_DegradeToAny()
    {
        var u = new IrUnionType { Types = [Prim(IrPrimitiveKind.String), Prim(IrPrimitiveKind.Integer)] };
        ((SimpleTypeNode)_mapper.MapType(u)).Name.Name.Should().Be("Any");
        var i = new IrIntersectionType
        {
            Types = [new IrModelReference { ModelName = "A" }, new IrModelReference { ModelName = "B" }],
        };
        ((SimpleTypeNode)_mapper.MapType(i)).Name.Name.Should().Be("Any");
    }

    [Test]
    public void GetImportTarget_OnModelReference_ReturnsName()
    {
        _mapper.GetImportTarget(new IrModelReference { ModelName = "Pet" }).Should().Be("Pet");
    }

    [Test]
    public void GetImportTarget_OnListOfModel_RecursesIn()
    {
        var t = new IrArrayType { ElementType = new IrModelReference { ModelName = "Pet" } };
        _mapper.GetImportTarget(t).Should().Be("Pet");
    }

    [Test]
    public void GetImportTarget_OnPrimitive_ReturnsNull()
    {
        _mapper.GetImportTarget(Prim(IrPrimitiveKind.String)).Should().BeNull();
    }

    [Test]
    public void IsOptionalInLanguage_Optional_True()
    {
        var p = new IrProperty
        {
            Name = "x", OriginalName = "x",
            Type = new IrOptionalType { InnerType = Prim(IrPrimitiveKind.String) },
            IsRequired = true,
        };
        _mapper.IsOptionalInLanguage(p).Should().BeTrue();
    }

    [Test]
    public void IsOptionalInLanguage_NotRequired_True()
    {
        var p = new IrProperty
        {
            Name = "x", OriginalName = "x",
            Type = Prim(IrPrimitiveKind.String), IsRequired = false,
        };
        _mapper.IsOptionalInLanguage(p).Should().BeTrue();
    }
}
