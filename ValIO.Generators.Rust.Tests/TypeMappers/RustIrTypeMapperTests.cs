using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.Rust.Tests.TypeMappers;

[TestFixture]
public class RustIrTypeMapperTests
{
    private readonly RustIrTypeMapper _mapper = new();

    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    [Test]
    public void String_MapsToString()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.String));
        mapped.Should().BeOfType<SimpleTypeNode>()
            .Which.Name.Name.Should().Be("String");
    }

    [Test]
    public void Integer_MapsToI32()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.Integer));
        mapped.Should().BeOfType<SimpleTypeNode>()
            .Which.Name.Name.Should().Be("i32");
    }

    [Test]
    public void Long_MapsToI64()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.Long));
        mapped.Should().BeOfType<SimpleTypeNode>()
            .Which.Name.Name.Should().Be("i64");
    }

    [Test]
    public void Number_MapsToF64()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.Number));
        mapped.Should().BeOfType<SimpleTypeNode>()
            .Which.Name.Name.Should().Be("f64");
    }

    [Test]
    public void Boolean_MapsToBool()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.Boolean));
        mapped.Should().BeOfType<SimpleTypeNode>()
            .Which.Name.Name.Should().Be("bool");
    }

    [Test]
    public void Uuid_MapsToString()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.Uuid));
        mapped.Should().BeOfType<SimpleTypeNode>()
            .Which.Name.Name.Should().Be("String");
    }

    [Test]
    public void Date_MapsToChronoNaiveDate()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.Date));
        mapped.Should().BeOfType<SimpleTypeNode>()
            .Which.Name.Name.Should().Be("chrono::NaiveDate");
    }

    [Test]
    public void DateTime_MapsToChronoDateTimeUtc()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.DateTime));
        mapped.Should().BeOfType<SimpleTypeNode>()
            .Which.Name.Name.Should().Be("chrono::DateTime<chrono::Utc>");
    }

    [Test]
    public void Time_MapsToChronoNaiveTime()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.Time));
        mapped.Should().BeOfType<SimpleTypeNode>()
            .Which.Name.Name.Should().Be("chrono::NaiveTime");
    }

    [Test]
    public void Binary_MapsToVecU8()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.Binary));
        mapped.Should().BeOfType<GenericTypeNode>()
            .Which.Name.Name.Should().Be("Vec");
        var generic = (GenericTypeNode)mapped;
        generic.TypeArguments.Should().HaveCount(1);
        ((SimpleTypeNode)generic.TypeArguments[0]).Name.Name.Should().Be("u8");
    }

    [Test]
    public void ByteArray_MapsToVecU8()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.ByteArray));
        mapped.Should().BeOfType<GenericTypeNode>()
            .Which.Name.Name.Should().Be("Vec");
    }

    [Test]
    public void File_MapsToVecU8()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.File));
        mapped.Should().BeOfType<GenericTypeNode>()
            .Which.Name.Name.Should().Be("Vec");
    }

    [Test]
    public void Any_MapsToSerdeJsonValue()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.Any));
        mapped.Should().BeOfType<SimpleTypeNode>()
            .Which.Name.Name.Should().Be("serde_json::Value");
    }

    [Test]
    public void Void_MapsToUnit()
    {
        var mapped = _mapper.MapType(Prim(IrPrimitiveKind.Void));
        mapped.Should().BeOfType<SimpleTypeNode>()
            .Which.Name.Name.Should().Be("()");
    }

    [Test]
    public void Array_MapsToVecGeneric()
    {
        var mapped = _mapper.MapType(new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) });
        mapped.Should().BeOfType<GenericTypeNode>()
            .Which.Name.Name.Should().Be("Vec");
        var g = (GenericTypeNode)mapped;
        g.TypeArguments.Should().ContainSingle();
        ((SimpleTypeNode)g.TypeArguments[0]).Name.Name.Should().Be("String");
    }

    [Test]
    public void Dictionary_MapsToHashMap()
    {
        var mapped = _mapper.MapType(new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = Prim(IrPrimitiveKind.Integer),
        });
        mapped.Should().BeOfType<GenericTypeNode>()
            .Which.Name.Name.Should().Be("std::collections::HashMap");
    }

    [Test]
    public void Nullable_WrapsInOption()
    {
        var mapped = _mapper.MapType(new IrNullableType { InnerType = Prim(IrPrimitiveKind.Integer) });
        mapped.Should().BeOfType<GenericTypeNode>()
            .Which.Name.Name.Should().Be("Option");
    }

    [Test]
    public void Optional_WrapsInOption()
    {
        var mapped = _mapper.MapType(new IrOptionalType { InnerType = Prim(IrPrimitiveKind.Integer) });
        mapped.Should().BeOfType<GenericTypeNode>()
            .Which.Name.Name.Should().Be("Option");
    }

    [Test]
    public void ModelReference_MapsToSimpleTypeWithModelName()
    {
        var mapped = _mapper.MapType(new IrModelReference { ModelName = "User" });
        mapped.Should().BeOfType<SimpleTypeNode>()
            .Which.Name.Name.Should().Be("User");
    }

    [Test]
    public void UnionType_DegradesToSerdeJsonValue()
    {
        var mapped = _mapper.MapType(new IrUnionType
        {
            Types = new[] { (IrType)Prim(IrPrimitiveKind.String), Prim(IrPrimitiveKind.Integer) }
        });
        mapped.Should().BeOfType<SimpleTypeNode>()
            .Which.Name.Name.Should().Be("serde_json::Value");
    }

    [Test]
    public void IntersectionType_DegradesToSerdeJsonValue()
    {
        var mapped = _mapper.MapType(new IrIntersectionType
        {
            Types = new[] { (IrType)Prim(IrPrimitiveKind.String), Prim(IrPrimitiveKind.Integer) }
        });
        mapped.Should().BeOfType<SimpleTypeNode>()
            .Which.Name.Name.Should().Be("serde_json::Value");
    }
}
