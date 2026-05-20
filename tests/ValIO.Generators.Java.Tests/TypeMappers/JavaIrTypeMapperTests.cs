using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.Java.Tests.TypeMappers;

[TestFixture]
public class JavaIrTypeMapperTests
{
    private readonly JavaIrTypeMapper _mapper = new();

    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    // Java mapper uses boxed wrappers (Integer/Long/Boolean) so they can be
    // nullable and JSON-serialised uniformly via Jackson.
    [TestCase(IrPrimitiveKind.String, "String")]
    [TestCase(IrPrimitiveKind.Integer, "Integer")]
    [TestCase(IrPrimitiveKind.Long, "Long")]
    [TestCase(IrPrimitiveKind.Boolean, "Boolean")]
    public void PrimitiveTypes_MapToJavaBoxedWrappers(IrPrimitiveKind kind, string expected)
    {
        var mapped = _mapper.MapType(Prim(kind));
        mapped.ToString().Should().Contain(expected);
    }

    [Test]
    public void ModelReference_MapsToClassName()
    {
        var mapped = _mapper.MapType(new IrModelReference { ModelName = "Pet" });
        mapped.ToString().Should().Contain("Pet");
    }

    [Test]
    public void ArrayType_MapsToListOfElement()
    {
        var mapped = _mapper.MapType(new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) });
        mapped.ToString().Should().Contain("String");
    }
}
