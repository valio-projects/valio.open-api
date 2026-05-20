using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.Java.Tests.TypeMappers;

/// <summary>
/// Exhaustive Java type-mapping coverage:
/// - all 14 primitives (default boxed),
/// - <c>preferUnboxed</c> variant,
/// - composites,
/// - import discovery (single + recursive).
/// </summary>
[TestFixture]
public class JavaIrTypeMapperComprehensiveTests
{
    private readonly JavaIrTypeMapper _mapper = new();

    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    [TestCase(IrPrimitiveKind.String,    "String")]
    [TestCase(IrPrimitiveKind.Integer,   "Integer")]
    [TestCase(IrPrimitiveKind.Long,      "Long")]
    [TestCase(IrPrimitiveKind.Number,    "Double")]
    [TestCase(IrPrimitiveKind.Boolean,   "Boolean")]
    [TestCase(IrPrimitiveKind.Date,      "LocalDate")]
    [TestCase(IrPrimitiveKind.DateTime,  "OffsetDateTime")]
    [TestCase(IrPrimitiveKind.Time,      "OffsetTime")]
    [TestCase(IrPrimitiveKind.Uuid,      "UUID")]
    [TestCase(IrPrimitiveKind.Binary,    "byte[]")]
    [TestCase(IrPrimitiveKind.ByteArray, "byte[]")]
    [TestCase(IrPrimitiveKind.File,      "byte[]")]
    [TestCase(IrPrimitiveKind.Any,       "Object")]
    [TestCase(IrPrimitiveKind.Void,      "void")]
    public void AllPrimitives_DefaultBoxedForm(IrPrimitiveKind kind, string expected)
    {
        var simple = (SimpleTypeNode)_mapper.MapType(Prim(kind));
        simple.Name.Name.Should().Be(expected);
    }

    [TestCase(IrPrimitiveKind.Integer, "int")]
    [TestCase(IrPrimitiveKind.Long,    "long")]
    [TestCase(IrPrimitiveKind.Number,  "double")]
    [TestCase(IrPrimitiveKind.Boolean, "boolean")]
    public void NumericPrimitives_PreferUnboxed_GivesPrimitive(IrPrimitiveKind kind, string expected)
    {
        var simple = (SimpleTypeNode)_mapper.MapType(Prim(kind), preferUnboxed: true);
        simple.Name.Name.Should().Be(expected);
    }

    [Test]
    public void StringPrimitive_AlwaysBoxed_EvenWithPreferUnboxed()
    {
        var simple = (SimpleTypeNode)_mapper.MapType(Prim(IrPrimitiveKind.String), preferUnboxed: true);
        simple.Name.Name.Should().Be("String");
    }

    [Test]
    public void Array_BecomesListGeneric_WithBoxedElement()
    {
        var t = new IrArrayType { ElementType = Prim(IrPrimitiveKind.Integer) };
        var generic = (GenericTypeNode)_mapper.MapType(t);
        generic.Name.Name.Should().Be("List");
        ((SimpleTypeNode)generic.TypeArguments[0]).Name.Name.Should().Be("Integer");
    }

    [Test]
    public void Dictionary_BecomesMapGeneric()
    {
        var t = new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = new IrModelReference { ModelName = "Pet" },
        };
        var generic = (GenericTypeNode)_mapper.MapType(t);
        generic.Name.Name.Should().Be("Map");
        generic.TypeArguments.Should().HaveCount(2);
        ((SimpleTypeNode)generic.TypeArguments[1]).Name.Name.Should().Be("Pet");
    }

    [Test]
    public void Nullable_AndOptional_StripWrapperAndKeepBoxedInner()
    {
        var nullable = new IrNullableType { InnerType = Prim(IrPrimitiveKind.Integer) };
        ((SimpleTypeNode)_mapper.MapType(nullable)).Name.Name.Should().Be("Integer");
        var optional = new IrOptionalType { InnerType = Prim(IrPrimitiveKind.Boolean) };
        ((SimpleTypeNode)_mapper.MapType(optional)).Name.Name.Should().Be("Boolean");
    }

    [Test]
    public void UnionAndIntersection_DegradeToObject()
    {
        var union = new IrUnionType { Types = [Prim(IrPrimitiveKind.String), Prim(IrPrimitiveKind.Integer)] };
        ((SimpleTypeNode)_mapper.MapType(union)).Name.Name.Should().Be("Object");
        var intersection = new IrIntersectionType
        {
            Types = [new IrModelReference { ModelName = "A" }, new IrModelReference { ModelName = "B" }],
        };
        ((SimpleTypeNode)_mapper.MapType(intersection)).Name.Name.Should().Be("Object");
    }

    [Test]
    public void ModelReference_KeepsClassName()
    {
        var simple = (SimpleTypeNode)_mapper.MapType(new IrModelReference { ModelName = "Pet" });
        simple.Name.Name.Should().Be("Pet");
    }

    [TestCase(IrPrimitiveKind.Date,     "java.time.LocalDate")]
    [TestCase(IrPrimitiveKind.DateTime, "java.time.OffsetDateTime")]
    [TestCase(IrPrimitiveKind.Time,     "java.time.OffsetTime")]
    [TestCase(IrPrimitiveKind.Uuid,     "java.util.UUID")]
    public void GetImportTarget_TimeAndUuid_ReturnsFullyQualified(IrPrimitiveKind kind, string expected)
    {
        _mapper.GetImportTarget(Prim(kind)).Should().Be(expected);
    }

    [Test]
    public void GetImportTarget_StringInteger_ReturnsNull()
    {
        _mapper.GetImportTarget(Prim(IrPrimitiveKind.String)).Should().BeNull();
        _mapper.GetImportTarget(Prim(IrPrimitiveKind.Integer)).Should().BeNull();
    }

    [Test]
    public void CollectImports_ListOfOffsetDateTime_YieldsBothImports()
    {
        var t = new IrArrayType { ElementType = Prim(IrPrimitiveKind.DateTime) };
        var imports = _mapper.CollectImports(t).ToList();
        imports.Should().Contain("java.util.List");
        imports.Should().Contain("java.time.OffsetDateTime");
    }

    [Test]
    public void CollectImports_MapOfStringToUuid_YieldsMapAndUuid()
    {
        var t = new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = Prim(IrPrimitiveKind.Uuid),
        };
        var imports = _mapper.CollectImports(t).ToList();
        imports.Should().Contain("java.util.Map");
        imports.Should().Contain("java.util.UUID");
    }

    [Test]
    public void CollectImports_NestedListOfMap_YieldsAllRequired()
    {
        var t = new IrArrayType
        {
            ElementType = new IrDictionaryType
            {
                KeyType = Prim(IrPrimitiveKind.String),
                ValueType = Prim(IrPrimitiveKind.DateTime),
            }
        };
        var imports = _mapper.CollectImports(t).ToList();
        imports.Should().Contain("java.util.List");
        imports.Should().Contain("java.util.Map");
        imports.Should().Contain("java.time.OffsetDateTime");
    }

    [Test]
    public void IsOptionalInLanguage_RequiredFalse_True()
    {
        var p = new IrProperty
        {
            Name = "x", OriginalName = "x",
            Type = Prim(IrPrimitiveKind.String), IsRequired = false,
        };
        _mapper.IsOptionalInLanguage(p).Should().BeTrue();
    }
}
