using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.Ruby.Tests.TypeMappers;

/// <summary>
/// Verifies that <see cref="RubyIrTypeMapper"/> maps every IR primitive and
/// composite type to the expected Ruby AST type node.
/// </summary>
[TestFixture]
public class RubyIrTypeMapperTests
{
    private readonly RubyIrTypeMapper _mapper = new();

    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    [TestCase(IrPrimitiveKind.String,    "String")]
    [TestCase(IrPrimitiveKind.Integer,   "Integer")]
    [TestCase(IrPrimitiveKind.Long,      "Integer")]
    [TestCase(IrPrimitiveKind.Number,    "Float")]
    [TestCase(IrPrimitiveKind.Boolean,   "Boolean")]
    [TestCase(IrPrimitiveKind.Date,      "String")]
    [TestCase(IrPrimitiveKind.DateTime,  "String")]
    [TestCase(IrPrimitiveKind.Time,      "String")]
    [TestCase(IrPrimitiveKind.Uuid,      "String")]
    [TestCase(IrPrimitiveKind.Binary,    "String")]
    [TestCase(IrPrimitiveKind.ByteArray, "String")]
    [TestCase(IrPrimitiveKind.File,      "String")]
    [TestCase(IrPrimitiveKind.Void,      "NilClass")]
    public void PrimitiveKinds_MapToExpectedSimpleType(IrPrimitiveKind kind, string expected)
    {
        var mapped = _mapper.MapType(Prim(kind));
        mapped.Should().BeOfType<SimpleTypeNode>();
        ((SimpleTypeNode)mapped).Name.Name.Should().Be(expected);
    }

    [Test]
    public void AnyPrimitive_MapsToObject()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.Any));
        mapped.Should().BeOfType<SimpleTypeNode>();
        ((SimpleTypeNode)mapped).Name.Name.Should().Be("Object");
    }

    [Test]
    public void ArrayType_MapsToRubyArrayGeneric()
    {
        var mapped = _mapper.MapType(new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) });
        mapped.Should().BeOfType<GenericTypeNode>();
        var generic = (GenericTypeNode)mapped;
        generic.Name.Name.Should().Be("Array");
        generic.TypeArguments.Should().HaveCount(1);
        ((SimpleTypeNode)generic.TypeArguments[0]).Name.Name.Should().Be("String");
    }

    [Test]
    public void DictionaryType_MapsToRubyHashGeneric()
    {
        var mapped = _mapper.MapType(new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = Prim(IrPrimitiveKind.Integer),
        });
        mapped.Should().BeOfType<GenericTypeNode>();
        var generic = (GenericTypeNode)mapped;
        generic.Name.Name.Should().Be("Hash");
        generic.TypeArguments.Should().HaveCount(2);
        ((SimpleTypeNode)generic.TypeArguments[0]).Name.Name.Should().Be("String");
        ((SimpleTypeNode)generic.TypeArguments[1]).Name.Name.Should().Be("Integer");
    }

    [Test]
    public void NullableType_WrapsInnerWithNullableTypeNode()
    {
        var mapped = _mapper.MapType(new IrNullableType { InnerType = Prim(IrPrimitiveKind.String) });
        mapped.Should().BeOfType<NullableTypeNode>();
        var inner = ((NullableTypeNode)mapped).InnerType;
        ((SimpleTypeNode)inner).Name.Name.Should().Be("String");
    }

    [Test]
    public void OptionalType_StripsOptionalWrapper()
    {
        var mapped = _mapper.MapType(new IrOptionalType { InnerType = Prim(IrPrimitiveKind.Integer) });
        // OptionalType passes through to the inner mapped form (Ruby has no
        // syntactic distinction between optional and required).
        mapped.Should().BeOfType<SimpleTypeNode>();
        ((SimpleTypeNode)mapped).Name.Name.Should().Be("Integer");
    }

    [Test]
    public void UnionType_FallsBackToObject()
    {
        var mapped = _mapper.MapType(new IrUnionType { Types = [Prim(IrPrimitiveKind.String), Prim(IrPrimitiveKind.Integer)] });
        mapped.Should().BeOfType<SimpleTypeNode>();
        ((SimpleTypeNode)mapped).Name.Name.Should().Be("Object");
    }

    [Test]
    public void IntersectionType_FallsBackToObject()
    {
        var mapped = _mapper.MapType(new IrIntersectionType { Types = [Prim(IrPrimitiveKind.String), Prim(IrPrimitiveKind.Integer)] });
        mapped.Should().BeOfType<SimpleTypeNode>();
        ((SimpleTypeNode)mapped).Name.Name.Should().Be("Object");
    }

    [Test]
    public void ModelReference_MapsToSimpleTypeNamedAfterModel()
    {
        var mapped = _mapper.MapType(new IrModelReference { ModelName = "User" });
        mapped.Should().BeOfType<SimpleTypeNode>();
        ((SimpleTypeNode)mapped).Name.Name.Should().Be("User");
    }

    [Test]
    public void GetImportTarget_ForModelReference_ReturnsModelName()
    {
        _mapper.GetImportTarget(new IrModelReference { ModelName = "User" }).Should().Be("User");
    }

    [Test]
    public void GetImportTarget_ForNestedArray_DrillsIntoElement()
    {
        var t = new IrArrayType { ElementType = new IrModelReference { ModelName = "Pet" } };
        _mapper.GetImportTarget(t).Should().Be("Pet");
    }

    [Test]
    public void GetImportTarget_ForPrimitive_ReturnsNull()
    {
        _mapper.GetImportTarget(Prim(IrPrimitiveKind.String)).Should().BeNull();
    }

    [Test]
    public void IsOptionalInLanguage_TreatsNonRequiredAsOptional()
    {
        var prop = new ValIO.Core.IR.IrProperty
        {
            OriginalName = "id",
            Name = "id",
            Type = Prim(IrPrimitiveKind.Integer),
            IsRequired = false,
        };
        _mapper.IsOptionalInLanguage(prop).Should().BeTrue();
    }

    [Test]
    public void IsOptionalInLanguage_TreatsRequiredAsNotOptional()
    {
        var prop = new ValIO.Core.IR.IrProperty
        {
            OriginalName = "id",
            Name = "id",
            Type = Prim(IrPrimitiveKind.Integer),
            IsRequired = true,
        };
        _mapper.IsOptionalInLanguage(prop).Should().BeFalse();
    }
}
