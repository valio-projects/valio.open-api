using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.FSharp.Tests.TypeMappers;

[TestFixture]
public class FSharpIrTypeMapperTests
{
    private readonly FSharpIrTypeMapper _mapper = new();

    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    [TestCase(IrPrimitiveKind.String, "string")]
    [TestCase(IrPrimitiveKind.Integer, "int")]
    [TestCase(IrPrimitiveKind.Long, "int64")]
    [TestCase(IrPrimitiveKind.Number, "float")]
    [TestCase(IrPrimitiveKind.Boolean, "bool")]
    [TestCase(IrPrimitiveKind.Uuid, "string")]
    [TestCase(IrPrimitiveKind.Void, "unit")]
    public void PrimitiveTypes_MapToFSharpBuiltins(IrPrimitiveKind kind, string expected)
    {
        var mapped = _mapper.MapType(Prim(kind));
        mapped.Should().BeOfType<SimpleTypeNode>();
        ((SimpleTypeNode)mapped).Name.Name.Should().Be(expected);
    }

    [Test]
    public void DateTime_MapsToSystemDateTime()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.DateTime));
        ((SimpleTypeNode)mapped).Name.Name.Should().Be("System.DateTime");
    }

    [Test]
    public void Time_MapsToSystemTimeSpan()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.Time));
        ((SimpleTypeNode)mapped).Name.Name.Should().Be("System.TimeSpan");
    }

    [TestCase(IrPrimitiveKind.Binary)]
    [TestCase(IrPrimitiveKind.ByteArray)]
    [TestCase(IrPrimitiveKind.File)]
    public void BinaryFamily_MapsToByteArray(IrPrimitiveKind kind)
    {
        var mapped = _mapper.MapType(Prim(kind));
        ((SimpleTypeNode)mapped).Name.Name.Should().Be("byte[]");
    }

    [Test]
    public void AnyKind_MapsToObj()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.Any));
        ((SimpleTypeNode)mapped).Name.Name.Should().Be("obj");
    }

    [Test]
    public void ArrayType_MapsToGenericListNode()
    {
        var mapped = _mapper.MapType(new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) });
        mapped.Should().BeOfType<GenericTypeNode>();
        var gen = (GenericTypeNode)mapped;
        gen.Name.Name.Should().Be("list");
        gen.TypeArguments.Should().HaveCount(1);
        ((SimpleTypeNode)gen.TypeArguments[0]).Name.Name.Should().Be("string");
    }

    [Test]
    public void DictionaryType_MapsToGenericMapNode()
    {
        var mapped = _mapper.MapType(new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = Prim(IrPrimitiveKind.Integer),
        });
        mapped.Should().BeOfType<GenericTypeNode>();
        var gen = (GenericTypeNode)mapped;
        gen.Name.Name.Should().Be("Map");
        gen.TypeArguments.Should().HaveCount(2);
    }

    [Test]
    public void NullableType_WrapsInNullableTypeNode()
    {
        var mapped = _mapper.MapType(new IrNullableType { InnerType = Prim(IrPrimitiveKind.Integer) });
        mapped.Should().BeOfType<NullableTypeNode>();
    }

    [Test]
    public void OptionalType_WrapsInNullableTypeNode()
    {
        var mapped = _mapper.MapType(new IrOptionalType { InnerType = Prim(IrPrimitiveKind.Integer) });
        mapped.Should().BeOfType<NullableTypeNode>();
    }

    [Test]
    public void ModelReference_MapsToSimpleTypeNodeWithName()
    {
        var mapped = _mapper.MapType(new IrModelReference { ModelName = "User" });
        mapped.Should().BeOfType<SimpleTypeNode>();
        ((SimpleTypeNode)mapped).Name.Name.Should().Be("User");
    }

    [Test]
    public void UnionType_FallsBackToObj()
    {
        var mapped = _mapper.MapType(new IrUnionType
        {
            Types = new IrType[] { Prim(IrPrimitiveKind.String), Prim(IrPrimitiveKind.Integer) },
        });
        ((SimpleTypeNode)mapped).Name.Name.Should().Be("obj");
    }

    [Test]
    public void IntersectionType_FallsBackToObj()
    {
        var mapped = _mapper.MapType(new IrIntersectionType
        {
            Types = new IrType[] { Prim(IrPrimitiveKind.String), Prim(IrPrimitiveKind.Integer) },
        });
        ((SimpleTypeNode)mapped).Name.Name.Should().Be("obj");
    }

    [Test]
    public void GetImportTarget_ReturnsModelName()
    {
        var target = _mapper.GetImportTarget(new IrModelReference { ModelName = "User" });
        target.Should().Be("User");
    }
}
