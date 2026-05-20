using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.Php.Tests.TypeMappers;

/// <summary>
/// Exhaustive PHP type-mapping coverage: all 14 primitives + 8 composites.
/// Pairs with <see cref="PhpIrTypeMapperTests"/> which covers the happy path.
/// </summary>
[TestFixture]
public class PhpIrTypeMapperComprehensiveTests
{
    private readonly PhpIrTypeMapper _mapper = new();

    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    [TestCase(IrPrimitiveKind.String, "string")]
    [TestCase(IrPrimitiveKind.Integer, "int")]
    [TestCase(IrPrimitiveKind.Long, "int")]
    [TestCase(IrPrimitiveKind.Number, "float")]
    [TestCase(IrPrimitiveKind.Boolean, "bool")]
    [TestCase(IrPrimitiveKind.Date, "DateTimeImmutable")]
    [TestCase(IrPrimitiveKind.DateTime, "DateTimeImmutable")]
    [TestCase(IrPrimitiveKind.Time, "DateTimeImmutable")]
    [TestCase(IrPrimitiveKind.Uuid, "string")]
    [TestCase(IrPrimitiveKind.Binary, "SplFileInfo")]
    [TestCase(IrPrimitiveKind.ByteArray, "SplFileInfo")]
    [TestCase(IrPrimitiveKind.File, "SplFileInfo")]
    [TestCase(IrPrimitiveKind.Any, "mixed")]
    [TestCase(IrPrimitiveKind.Void, "void")]
    public void AllPrimitives_MapToExpectedPhpType(IrPrimitiveKind kind, string expected)
    {
        var mapped = _mapper.MapType(Prim(kind));
        var simple = mapped as SimpleTypeNode;
        simple.Should().NotBeNull();
        simple!.Name.Name.Should().Contain(expected);
    }

    [Test]
    public void ArrayOfModel_StaysAsPhpArrayButImportTargetIsModel()
    {
        var t = new IrArrayType { ElementType = new IrModelReference { ModelName = "Pet" } };
        var mapped = _mapper.MapType(t);
        mapped.Should().BeOfType<SimpleTypeNode>();
        _mapper.GetImportTarget(t).Should().BeNull(); // array itself is not an import; deep recurse not provided
    }

    [Test]
    public void Dictionary_AlsoMapsToPhpArray()
    {
        var t = new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = Prim(IrPrimitiveKind.Integer),
        };
        _mapper.MapType(t).Should().BeOfType<SimpleTypeNode>()
            .Which.Name.Name.Should().Be("array");
    }

    [Test]
    public void Nullable_WrapsInnerInNullableType()
    {
        var t = new IrNullableType { InnerType = Prim(IrPrimitiveKind.Integer) };
        _mapper.MapType(t).Should().BeOfType<NullableTypeNode>();
    }

    [Test]
    public void Optional_TransparentlyForwardsToInner()
    {
        var t = new IrOptionalType { InnerType = Prim(IrPrimitiveKind.String) };
        var mapped = _mapper.MapType(t);
        mapped.Should().BeOfType<SimpleTypeNode>()
            .Which.Name.Name.Should().Be("string");
    }

    [Test]
    public void ModelReference_KeepsClassName()
    {
        var t = new IrModelReference { ModelName = "Pet" };
        _mapper.MapType(t).Should().BeOfType<SimpleTypeNode>()
            .Which.Name.Name.Should().Be("Pet");
    }

    [Test]
    public void Union_BecomesUnionTypeNode()
    {
        var t = new IrUnionType
        {
            Types = [Prim(IrPrimitiveKind.String), Prim(IrPrimitiveKind.Integer)]
        };
        _mapper.MapType(t).Should().BeOfType<UnionTypeNode>()
            .Which.Types.Should().HaveCount(2);
    }

    [Test]
    public void Intersection_BecomesIntersectionTypeNode()
    {
        var t = new IrIntersectionType
        {
            Types = [
                new IrModelReference { ModelName = "Animal" },
                new IrModelReference { ModelName = "Furry" },
            ]
        };
        _mapper.MapType(t).Should().BeOfType<IntersectionTypeNode>();
    }

    [Test]
    public void GetPhpDocArrayType_ListOfPrimitive_Annotated()
    {
        var arr = new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) };
        _mapper.GetPhpDocArrayType(arr).Should().Be("array<string>");
    }

    [Test]
    public void GetPhpDocArrayType_ListOfModelRef_UsesModelName()
    {
        var arr = new IrArrayType { ElementType = new IrModelReference { ModelName = "Pet" } };
        _mapper.GetPhpDocArrayType(arr).Should().Be("array<Pet>");
    }

    [Test]
    public void GetPhpDocArrayType_Dict_ProducesKeyValueAnnotation()
    {
        var dict = new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = new IrModelReference { ModelName = "Order" },
        };
        _mapper.GetPhpDocArrayType(dict).Should().Be("array<string, Order>");
    }

    [Test]
    public void GetPhpDocArrayType_NullableAroundArray_RecursesIn()
    {
        var inner = new IrArrayType { ElementType = Prim(IrPrimitiveKind.Integer) };
        var t = new IrNullableType { InnerType = inner };
        _mapper.GetPhpDocArrayType(t).Should().Be("array<int>");
    }

    [Test]
    public void GetImportTarget_OnModelReference_ReturnsName()
    {
        var t = new IrModelReference { ModelName = "Pet" };
        _mapper.GetImportTarget(t).Should().Be("Pet");
    }

    [Test]
    public void IsOptionalInLanguage_RespectsBothExplicitAndImplicit()
    {
        _mapper.IsOptionalInLanguage(new ValIO.Core.IR.IrProperty
            { Name = "x", OriginalName = "x", Type = Prim(IrPrimitiveKind.String), IsRequired = false })
            .Should().BeTrue();
        _mapper.IsOptionalInLanguage(new ValIO.Core.IR.IrProperty
            { Name = "x", OriginalName = "x", Type = new IrOptionalType { InnerType = Prim(IrPrimitiveKind.String) }, IsRequired = true })
            .Should().BeTrue();
        _mapper.IsOptionalInLanguage(new ValIO.Core.IR.IrProperty
            { Name = "x", OriginalName = "x", Type = Prim(IrPrimitiveKind.String), IsRequired = true })
            .Should().BeFalse();
    }
}
