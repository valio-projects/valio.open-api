using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.Kotlin.Tests.TypeMappers;

[TestFixture]
public class KotlinIrTypeMapperTests
{
    private readonly KotlinIrTypeMapper _mapper = new();

    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    [TestCase(IrPrimitiveKind.String, "String")]
    [TestCase(IrPrimitiveKind.Integer, "Int")]
    [TestCase(IrPrimitiveKind.Long, "Long")]
    [TestCase(IrPrimitiveKind.Boolean, "Boolean")]
    public void PrimitiveTypes_MapToKotlinBuiltins(IrPrimitiveKind kind, string expected)
    {
        var mapped = _mapper.MapType(Prim(kind));
        mapped.ToString().Should().Contain(expected);
    }

    [Test]
    public void ArrayType_MapsToListGeneric()
    {
        var mapped = _mapper.MapType(new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) });
        mapped.ToString().Should().Contain("List");
    }

    [Test]
    public void NullableType_IsMarkedNullable()
    {
        var mapped = _mapper.MapType(new IrNullableType { InnerType = Prim(IrPrimitiveKind.String) });
        // Kotlin nullable is `T?`. Either IsNullable=true or string contains "?".
        var asString = mapped.ToString() ?? "";
        (mapped.IsNullable || asString.Contains('?')).Should().BeTrue();
    }
}
