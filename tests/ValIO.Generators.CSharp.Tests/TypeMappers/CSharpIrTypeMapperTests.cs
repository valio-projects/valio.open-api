using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.CSharp.Tests.TypeMappers;

/// <summary>
/// Exhaustive C# IR type-mapper coverage:
/// - all 14 primitives,
/// - composites (Array→List, Dictionary, Nullable, Optional),
/// - union/intersection degrade to <c>object</c>,
/// - import-target is always null (C# uses namespaces).
/// </summary>
[TestFixture]
public class CSharpIrTypeMapperTests
{
    private readonly CSharpIrTypeMapper _mapper = new();

    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    [TestCase(IrPrimitiveKind.String,    "string")]
    [TestCase(IrPrimitiveKind.Integer,   "int")]
    [TestCase(IrPrimitiveKind.Long,      "long")]
    [TestCase(IrPrimitiveKind.Number,    "double")]
    [TestCase(IrPrimitiveKind.Boolean,   "bool")]
    [TestCase(IrPrimitiveKind.Date,      "DateTimeOffset")]
    [TestCase(IrPrimitiveKind.DateTime,  "DateTimeOffset")]
    [TestCase(IrPrimitiveKind.Time,      "TimeSpan")]
    [TestCase(IrPrimitiveKind.Uuid,      "Guid")]
    [TestCase(IrPrimitiveKind.Binary,    "Stream")]
    [TestCase(IrPrimitiveKind.ByteArray, "byte[]")]
    [TestCase(IrPrimitiveKind.File,      "Stream")]
    [TestCase(IrPrimitiveKind.Any,       "object")]
    [TestCase(IrPrimitiveKind.Void,      "void")]
    public void Primitives_MapToExpectedCSharpType(IrPrimitiveKind kind, string expected)
    {
        var t = (SimpleTypeNode)_mapper.MapType(Prim(kind));
        t.Name.Name.Should().Be(expected);
    }

    [Test]
    public void Array_BecomesListGeneric()
    {
        var t = new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) };
        var g = (GenericTypeNode)_mapper.MapType(t);
        g.Name.Name.Should().Be("List");
        ((SimpleTypeNode)g.TypeArguments[0]).Name.Name.Should().Be("string");
    }

    [Test]
    public void Dictionary_BecomesDictionaryGenericWith2Args()
    {
        var t = new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = new IrModelReference { ModelName = "Pet" },
        };
        var g = (GenericTypeNode)_mapper.MapType(t);
        g.Name.Name.Should().Be("Dictionary");
        g.TypeArguments.Should().HaveCount(2);
        ((SimpleTypeNode)g.TypeArguments[1]).Name.Name.Should().Be("Pet");
    }

    [Test]
    public void Nullable_WrapsAsNullableType()
    {
        var t = new IrNullableType { InnerType = Prim(IrPrimitiveKind.Integer) };
        _mapper.MapType(t).Should().BeOfType<NullableTypeNode>();
    }

    [Test]
    public void Optional_StripsWrapper()
    {
        var t = new IrOptionalType { InnerType = Prim(IrPrimitiveKind.Boolean) };
        ((SimpleTypeNode)_mapper.MapType(t)).Name.Name.Should().Be("bool");
    }

    [Test]
    public void ModelReference_KeepsClassName()
    {
        var t = new IrModelReference { ModelName = "Pet" };
        ((SimpleTypeNode)_mapper.MapType(t)).Name.Name.Should().Be("Pet");
    }

    [Test]
    public void Union_DegradesToObject()
    {
        var t = new IrUnionType { Types = [Prim(IrPrimitiveKind.String), Prim(IrPrimitiveKind.Integer)] };
        ((SimpleTypeNode)_mapper.MapType(t)).Name.Name.Should().Be("object");
    }

    [Test]
    public void Intersection_DegradesToObject()
    {
        var t = new IrIntersectionType
        {
            Types = [new IrModelReference { ModelName = "A" }, new IrModelReference { ModelName = "B" }]
        };
        ((SimpleTypeNode)_mapper.MapType(t)).Name.Name.Should().Be("object");
    }

    [Test]
    public void GetImportTarget_AlwaysNull_CSharpUsesNamespaces()
    {
        _mapper.GetImportTarget(new IrModelReference { ModelName = "Pet" }).Should().BeNull();
        _mapper.GetImportTarget(Prim(IrPrimitiveKind.Uuid)).Should().BeNull();
        _mapper.GetImportTarget(new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) }).Should().BeNull();
    }

    [Test]
    public void IsOptionalInLanguage_OptionalType_True()
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

    [Test]
    public void IsOptionalInLanguage_RequiredAndPlain_False()
    {
        var p = new IrProperty
        {
            Name = "x", OriginalName = "x",
            Type = Prim(IrPrimitiveKind.String), IsRequired = true,
        };
        _mapper.IsOptionalInLanguage(p).Should().BeFalse();
    }
}
