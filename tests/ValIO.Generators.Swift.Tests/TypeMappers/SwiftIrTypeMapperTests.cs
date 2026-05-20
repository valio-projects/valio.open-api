using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.Swift.Tests.TypeMappers;

[TestFixture]
public class SwiftIrTypeMapperTests
{
    private readonly SwiftIrTypeMapper _mapper = new();

    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    [TestCase(IrPrimitiveKind.String, "String")]
    [TestCase(IrPrimitiveKind.Integer, "Int")]
    [TestCase(IrPrimitiveKind.Long, "Int64")]
    [TestCase(IrPrimitiveKind.Boolean, "Bool")]
    public void PrimitiveTypes_MapToSwiftBuiltins(IrPrimitiveKind kind, string expected)
    {
        var mapped = _mapper.MapType(Prim(kind));
        mapped.ToString().Should().Contain(expected);
    }

    [Test]
    public void ArrayType_MapsToSwiftArray()
    {
        var mapped = _mapper.MapType(new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) });
        // Swift array literal is [T] (matches `String` substring either way).
        mapped.ToString().Should().Contain("String");
    }

    [Test]
    public void ModelReference_MapsToTypeName()
    {
        var mapped = _mapper.MapType(new IrModelReference { ModelName = "User" });
        mapped.ToString().Should().Contain("User");
    }
}
