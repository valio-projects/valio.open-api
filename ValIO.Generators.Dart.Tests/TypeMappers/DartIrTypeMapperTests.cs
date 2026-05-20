using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.Dart.Options;

namespace ValIO.Generators.Dart.Tests.TypeMappers;

/// <summary>
/// Exhaustive type-mapping coverage for <see cref="DartIrTypeMapper"/>.
/// </summary>
[TestFixture]
public class DartIrTypeMapperTests
{
    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    private DartIrTypeMapper Default => new();
    private DartIrTypeMapper WithObjectAny => new(new DartGenerationOptions
        { AnyMapping = DartOptionIds.AnyMapping.Object });
    private DartIrTypeMapper WithStringAny => new(new DartGenerationOptions
        { AnyMapping = DartOptionIds.AnyMapping.String });

    [TestCase(IrPrimitiveKind.String,    "String")]
    [TestCase(IrPrimitiveKind.Integer,   "int")]
    [TestCase(IrPrimitiveKind.Long,      "int")]
    [TestCase(IrPrimitiveKind.Number,    "double")]
    [TestCase(IrPrimitiveKind.Boolean,   "bool")]
    [TestCase(IrPrimitiveKind.Date,      "DateTime")]
    [TestCase(IrPrimitiveKind.DateTime,  "DateTime")]
    [TestCase(IrPrimitiveKind.Time,      "DateTime")]
    [TestCase(IrPrimitiveKind.Uuid,      "String")]
    [TestCase(IrPrimitiveKind.Binary,    "Uint8List")]
    [TestCase(IrPrimitiveKind.ByteArray, "Uint8List")]
    [TestCase(IrPrimitiveKind.File,      "Uint8List")]
    [TestCase(IrPrimitiveKind.Void,      "void")]
    public void AllPrimitives_MapToDartType(IrPrimitiveKind kind, string expected)
    {
        var t = (SimpleTypeNode)Default.MapType(Prim(kind));
        t.Name.Name.Should().Be(expected);
    }

    [Test]
    public void Any_DefaultMapping_IsDynamic()
    {
        ((SimpleTypeNode)Default.MapType(Prim(IrPrimitiveKind.Any))).Name.Name.Should().Be("dynamic");
    }

    [Test]
    public void Any_ObjectMapping_IsObject()
    {
        ((SimpleTypeNode)WithObjectAny.MapType(Prim(IrPrimitiveKind.Any))).Name.Name.Should().Be("Object");
    }

    [Test]
    public void Any_StringMapping_IsString()
    {
        ((SimpleTypeNode)WithStringAny.MapType(Prim(IrPrimitiveKind.Any))).Name.Name.Should().Be("String");
    }

    [Test]
    public void Array_BecomesListGeneric()
    {
        var t = new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) };
        var g = (GenericTypeNode)Default.MapType(t);
        g.Name.Name.Should().Be("List");
        ((SimpleTypeNode)g.TypeArguments[0]).Name.Name.Should().Be("String");
    }

    [Test]
    public void Dictionary_BecomesMapGeneric_With2Args()
    {
        var t = new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = new IrModelReference { ModelName = "Pet" },
        };
        var g = (GenericTypeNode)Default.MapType(t);
        g.Name.Name.Should().Be("Map");
        g.TypeArguments.Should().HaveCount(2);
    }

    [Test]
    public void Nullable_WrapsAsNullableNode()
    {
        var t = new IrNullableType { InnerType = Prim(IrPrimitiveKind.String) };
        Default.MapType(t).Should().BeOfType<NullableTypeNode>();
    }

    [Test]
    public void Optional_StripsWrapper()
    {
        var t = new IrOptionalType { InnerType = Prim(IrPrimitiveKind.Integer) };
        ((SimpleTypeNode)Default.MapType(t)).Name.Name.Should().Be("int");
    }

    [Test]
    public void ModelReference_KeepsClassName()
    {
        var t = new IrModelReference { ModelName = "Pet" };
        ((SimpleTypeNode)Default.MapType(t)).Name.Name.Should().Be("Pet");
    }

    [Test]
    public void Union_DegradesViaAnyMapping()
    {
        var t = new IrUnionType { Types = [Prim(IrPrimitiveKind.String), Prim(IrPrimitiveKind.Integer)] };
        ((SimpleTypeNode)Default.MapType(t)).Name.Name.Should().Be("dynamic");
        ((SimpleTypeNode)WithObjectAny.MapType(t)).Name.Name.Should().Be("Object");
    }

    [Test]
    public void Intersection_DegradesViaAnyMapping()
    {
        var t = new IrIntersectionType
        {
            Types = [new IrModelReference { ModelName = "A" }, new IrModelReference { ModelName = "B" }]
        };
        ((SimpleTypeNode)Default.MapType(t)).Name.Name.Should().Be("dynamic");
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
    public void GetImportTarget_OnDictWithModelValue_ResolvesValueFirst()
    {
        var t = new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = new IrModelReference { ModelName = "Order" },
        };
        Default.GetImportTarget(t).Should().Be("Order");
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
