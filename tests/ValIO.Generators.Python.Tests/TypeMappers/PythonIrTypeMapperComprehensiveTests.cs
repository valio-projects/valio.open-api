using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.Python.Tests.TypeMappers;

/// <summary>
/// Exhaustive Python type-mapping coverage: every primitive kind + every
/// composite shape, plus import-target / optional-detection rules.
/// </summary>
[TestFixture]
public class PythonIrTypeMapperComprehensiveTests
{
    private readonly PythonIrTypeMapper _mapper = new();

    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    [TestCase(IrPrimitiveKind.String,    "str")]
    [TestCase(IrPrimitiveKind.Integer,   "int")]
    [TestCase(IrPrimitiveKind.Long,      "int")]
    [TestCase(IrPrimitiveKind.Number,    "float")]
    [TestCase(IrPrimitiveKind.Boolean,   "bool")]
    [TestCase(IrPrimitiveKind.Date,      "date")]
    [TestCase(IrPrimitiveKind.DateTime,  "datetime")]
    [TestCase(IrPrimitiveKind.Time,      "time")]
    [TestCase(IrPrimitiveKind.Uuid,      "UUID")]
    [TestCase(IrPrimitiveKind.Binary,    "bytes")]
    [TestCase(IrPrimitiveKind.ByteArray, "bytes")]
    [TestCase(IrPrimitiveKind.File,      "bytes")]
    [TestCase(IrPrimitiveKind.Any,       "Any")]
    [TestCase(IrPrimitiveKind.Void,      "None")]
    [TestCase(IrPrimitiveKind.Null,      "None")]
    public void AllPrimitives_MapToExpectedBuiltin(IrPrimitiveKind kind, string expected)
    {
        var mapped = _mapper.MapType(Prim(kind)) as SimpleTypeNode;
        mapped.Should().NotBeNull();
        mapped!.Name.Name.Should().Be(expected);
    }

    [Test]
    public void ArrayOfArrayOfInt_BecomesListOfListOfInt()
    {
        var t = new IrArrayType
        {
            ElementType = new IrArrayType { ElementType = Prim(IrPrimitiveKind.Integer) },
        };
        var outer = _mapper.MapType(t) as GenericTypeNode;
        outer!.Name.Name.Should().Be("list");
        var inner = outer.TypeArguments[0] as GenericTypeNode;
        inner!.Name.Name.Should().Be("list");
        var leaf = inner.TypeArguments[0] as SimpleTypeNode;
        leaf!.Name.Name.Should().Be("int");
    }

    [Test]
    public void Dictionary_Has2TypeArguments()
    {
        var t = new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = Prim(IrPrimitiveKind.Integer),
        };
        var d = _mapper.MapType(t) as GenericTypeNode;
        d!.Name.Name.Should().Be("dict");
        d.TypeArguments.Should().HaveCount(2);
    }

    [Test]
    public void Optional_TransparentlyForwardsToInner()
    {
        var t = new IrOptionalType { InnerType = Prim(IrPrimitiveKind.String) };
        (_mapper.MapType(t) as SimpleTypeNode)!.Name.Name.Should().Be("str");
    }

    [Test]
    public void Intersection_FallsBackToFirstModelRef()
    {
        var t = new IrIntersectionType
        {
            Types = [
                Prim(IrPrimitiveKind.String),
                new IrModelReference { ModelName = "Mixin" },
            ]
        };
        (_mapper.MapType(t) as SimpleTypeNode)!.Name.Name.Should().Be("Mixin");
    }

    [Test]
    public void Intersection_NoModelRef_FallsBackToFirstType()
    {
        var t = new IrIntersectionType
        {
            Types = [Prim(IrPrimitiveKind.String), Prim(IrPrimitiveKind.Integer)]
        };
        (_mapper.MapType(t) as SimpleTypeNode)!.Name.Name.Should().Be("str");
    }

    [Test]
    public void GetImportTarget_OnModelRef_ReturnsModelName()
    {
        _mapper.GetImportTarget(new IrModelReference { ModelName = "Pet" }).Should().Be("Pet");
    }

    [Test]
    public void GetImportTarget_OnArrayOfModel_ResolvesElement()
    {
        var t = new IrArrayType { ElementType = new IrModelReference { ModelName = "Pet" } };
        _mapper.GetImportTarget(t).Should().Be("Pet");
    }

    [Test]
    public void GetImportTarget_OnDictWithModelValue_ResolvesValueFirst()
    {
        var t = new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = new IrModelReference { ModelName = "Order" },
        };
        _mapper.GetImportTarget(t).Should().Be("Order");
    }

    [Test]
    public void GetImportTarget_OnNullableOfModel_RecursesInside()
    {
        var t = new IrNullableType { InnerType = new IrModelReference { ModelName = "Pet" } };
        _mapper.GetImportTarget(t).Should().Be("Pet");
    }

    [Test]
    public void GetImportTarget_OnPrimitive_ReturnsNull()
    {
        _mapper.GetImportTarget(Prim(IrPrimitiveKind.Integer)).Should().BeNull();
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
            Type = Prim(IrPrimitiveKind.String),
            IsRequired = false,
        };
        _mapper.IsOptionalInLanguage(p).Should().BeTrue();
    }
}
