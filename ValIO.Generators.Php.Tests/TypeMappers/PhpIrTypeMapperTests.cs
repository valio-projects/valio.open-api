using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.Php.Tests.TypeMappers;

[TestFixture]
public class PhpIrTypeMapperTests
{
    private readonly PhpIrTypeMapper _mapper = new();

    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    [TestCase(IrPrimitiveKind.String, "string")]
    [TestCase(IrPrimitiveKind.Integer, "int")]
    [TestCase(IrPrimitiveKind.Long, "int")]
    [TestCase(IrPrimitiveKind.Boolean, "bool")]
    public void PrimitiveTypes_MapToPhp8Builtins(IrPrimitiveKind kind, string expected)
    {
        var mapped = _mapper.MapType(Prim(kind));
        mapped.ToString().Should().Contain(expected);
    }

    [Test]
    public void ModelReference_MapsToBareClassName()
    {
        var mapped = _mapper.MapType(new IrModelReference { ModelName = "User" });
        mapped.ToString().Should().Contain("User");
    }

    [Test]
    public void ArrayType_MapsToPhpArray()
    {
        var mapped = _mapper.MapType(new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) });
        // PHP arrays are untyped at runtime — `array` is the bare keyword.
        mapped.ToString().Should().ContainEquivalentOf("array");
    }
}
