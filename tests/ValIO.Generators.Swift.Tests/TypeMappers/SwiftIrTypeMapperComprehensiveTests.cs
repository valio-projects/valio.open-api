using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.Swift.Options;

namespace ValIO.Generators.Swift.Tests.TypeMappers;

/// <summary>
/// Exhaustive Swift IR-to-AST coverage: every primitive,
/// composites, plus the configurable <c>Any</c> handling
/// (<see cref="SwiftOptionIds.AnyMapping"/>).
/// </summary>
[TestFixture]
public class SwiftIrTypeMapperComprehensiveTests
{
    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    private SwiftIrTypeMapper Default => new();
    private SwiftIrTypeMapper WithStringAny => new(new SwiftGenerationOptions
        { AnyMapping = SwiftOptionIds.AnyMapping.String });

    [TestCase(IrPrimitiveKind.String,    "String")]
    [TestCase(IrPrimitiveKind.Integer,   "Int")]
    [TestCase(IrPrimitiveKind.Long,      "Int64")]
    [TestCase(IrPrimitiveKind.Number,    "Double")]
    [TestCase(IrPrimitiveKind.Boolean,   "Bool")]
    [TestCase(IrPrimitiveKind.Date,      "Date")]
    [TestCase(IrPrimitiveKind.DateTime,  "Date")]
    [TestCase(IrPrimitiveKind.Time,      "Date")]
    [TestCase(IrPrimitiveKind.Uuid,      "String")]
    [TestCase(IrPrimitiveKind.Binary,    "Data")]
    [TestCase(IrPrimitiveKind.ByteArray, "Data")]
    [TestCase(IrPrimitiveKind.File,      "Data")]
    [TestCase(IrPrimitiveKind.Void,      "Void")]
    public void AllPrimitives_MapToSwiftType(IrPrimitiveKind kind, string expected)
    {
        var simple = (SimpleTypeNode)Default.MapType(Prim(kind));
        simple.Name.Name.Should().Be(expected);
    }

    [Test]
    public void Any_DefaultMapping_BecomesAnyCodable()
    {
        var t = Prim(IrPrimitiveKind.Any);
        ((SimpleTypeNode)Default.MapType(t)).Name.Name.Should().Be("AnyCodable");
    }

    [Test]
    public void Any_StringMapping_BecomesString()
    {
        var t = Prim(IrPrimitiveKind.Any);
        ((SimpleTypeNode)WithStringAny.MapType(t)).Name.Name.Should().Be("String");
    }

    [Test]
    public void Array_BecomesArrayTypeNode()
    {
        var t = new IrArrayType { ElementType = Prim(IrPrimitiveKind.Integer) };
        var a = (ArrayTypeNode)Default.MapType(t);
        ((SimpleTypeNode)a.ElementType).Name.Name.Should().Be("Int");
    }

    [Test]
    public void Dictionary_BecomesDictionaryTypeNode()
    {
        var t = new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = new IrModelReference { ModelName = "Pet" },
        };
        var d = (DictionaryTypeNode)Default.MapType(t);
        ((SimpleTypeNode)d.KeyType).Name.Name.Should().Be("String");
        ((SimpleTypeNode)d.ValueType).Name.Name.Should().Be("Pet");
    }

    [Test]
    public void Nullable_WrapsAsNullable()
    {
        var t = new IrNullableType { InnerType = Prim(IrPrimitiveKind.String) };
        Default.MapType(t).Should().BeOfType<NullableTypeNode>();
    }

    [Test]
    public void Optional_StripsToInner()
    {
        var t = new IrOptionalType { InnerType = Prim(IrPrimitiveKind.Integer) };
        ((SimpleTypeNode)Default.MapType(t)).Name.Name.Should().Be("Int");
    }

    [Test]
    public void UnionAndIntersection_DegradeViaAnyMapping()
    {
        // Default: AnyCodable.
        ((SimpleTypeNode)Default.MapType(new IrUnionType
        {
            Types = [Prim(IrPrimitiveKind.String), Prim(IrPrimitiveKind.Integer)],
        })).Name.Name.Should().Be("AnyCodable");

        ((SimpleTypeNode)Default.MapType(new IrIntersectionType
        {
            Types = [new IrModelReference { ModelName = "A" }, new IrModelReference { ModelName = "B" }],
        })).Name.Name.Should().Be("AnyCodable");

        // Switched mapping: String.
        ((SimpleTypeNode)WithStringAny.MapType(new IrUnionType
        {
            Types = [Prim(IrPrimitiveKind.String), Prim(IrPrimitiveKind.Integer)],
        })).Name.Name.Should().Be("String");
    }

    [Test]
    public void GetImportTarget_OnModelReference_ReturnsName()
    {
        Default.GetImportTarget(new IrModelReference { ModelName = "Pet" }).Should().Be("Pet");
    }

    [Test]
    public void GetImportTarget_OnArrayOfModel_RecursesIn()
    {
        var t = new IrArrayType { ElementType = new IrModelReference { ModelName = "Pet" } };
        Default.GetImportTarget(t).Should().Be("Pet");
    }

    [Test]
    public void GetImportTarget_OnPrimitive_ReturnsNull()
    {
        Default.GetImportTarget(Prim(IrPrimitiveKind.Integer)).Should().BeNull();
    }

    [Test]
    public void IsOptionalInLanguage_OptionalOrNotRequired_True()
    {
        var optional = new IrProperty
        {
            Name = "x", OriginalName = "x",
            Type = new IrOptionalType { InnerType = Prim(IrPrimitiveKind.String) },
            IsRequired = true,
        };
        Default.IsOptionalInLanguage(optional).Should().BeTrue();

        var notRequired = new IrProperty
        {
            Name = "x", OriginalName = "x",
            Type = Prim(IrPrimitiveKind.String), IsRequired = false,
        };
        Default.IsOptionalInLanguage(notRequired).Should().BeTrue();
    }
}
