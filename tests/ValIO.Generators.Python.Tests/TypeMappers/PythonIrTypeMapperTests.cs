using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.Python.Tests.TypeMappers;

/// <summary>
/// Unit tests for the Python IR type mapper (PEP 585 generics, T | None).
/// </summary>
[TestFixture]
public class PythonIrTypeMapperTests
{
    private readonly PythonIrTypeMapper _mapper = new();

    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    [TestCase(IrPrimitiveKind.String, "str")]
    [TestCase(IrPrimitiveKind.Integer, "int")]
    [TestCase(IrPrimitiveKind.Long, "int")]
    [TestCase(IrPrimitiveKind.Boolean, "bool")]
    public void PrimitiveTypes_MapToPythonBuiltin(IrPrimitiveKind kind, string expected)
    {
        var mapped = _mapper.MapType(Prim(kind));
        mapped.ToString().Should().Contain(expected);
    }

    [Test]
    public void ArrayType_MapsToListGeneric()
    {
        var mapped = _mapper.MapType(new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) });
        mapped.Should().BeOfType<GenericTypeNode>();
        ((GenericTypeNode)mapped).Name.Name.Should().Be("list");
    }

    [Test]
    public void DictionaryType_MapsToDictGeneric()
    {
        var mapped = _mapper.MapType(new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = new IrModelReference { ModelName = "Pet" },
        });
        mapped.Should().BeOfType<GenericTypeNode>();
        ((GenericTypeNode)mapped).Name.Name.Should().Be("dict");
    }

    [Test]
    public void NullableType_MapsToUnionWithNone()
    {
        var mapped = _mapper.MapType(new IrNullableType { InnerType = Prim(IrPrimitiveKind.String) });
        mapped.Should().BeOfType<UnionTypeNode>();
        var types = ((UnionTypeNode)mapped).Types.Select(t => t.ToString());
        types.Should().Contain(s => s!.Contains("None", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public void ModelReference_MapsToBareClassName()
    {
        var mapped = _mapper.MapType(new IrModelReference { ModelName = "Pet" });
        mapped.ToString().Should().Contain("Pet");
    }
}
