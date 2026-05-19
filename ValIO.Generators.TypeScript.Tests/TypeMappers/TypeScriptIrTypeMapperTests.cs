using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.TypeScript;

namespace ValIO.Generators.TypeScript.Tests.TypeMappers;

/// <summary>
/// Unit tests for <c>TypeScriptIrTypeMapper</c> — pure IR-to-TypeNode
/// mapping. No OpenAPI document needed.
/// </summary>
[TestFixture]
public class TypeScriptIrTypeMapperTests
{
    private readonly TypeScriptIrTypeMapper _mapper = new();

    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    [TestCase(IrPrimitiveKind.String, "string")]
    [TestCase(IrPrimitiveKind.Number, "number")]
    [TestCase(IrPrimitiveKind.Integer, "number")]
    [TestCase(IrPrimitiveKind.Long, "bigint")]
    [TestCase(IrPrimitiveKind.Boolean, "boolean")]
    [TestCase(IrPrimitiveKind.DateTime, "string")]
    [TestCase(IrPrimitiveKind.Date, "string")]
    [TestCase(IrPrimitiveKind.Time, "string")]
    [TestCase(IrPrimitiveKind.Uuid, "string")]
    [TestCase(IrPrimitiveKind.Binary, "Blob")]
    [TestCase(IrPrimitiveKind.ByteArray, "Blob")]
    [TestCase(IrPrimitiveKind.File, "File")]
    [TestCase(IrPrimitiveKind.Any, "unknown")]
    [TestCase(IrPrimitiveKind.Void, "void")]
    public void PrimitiveTypes_MapToCorrectTsType(IrPrimitiveKind kind, string expected)
    {
        var mapped = _mapper.MapType(Prim(kind));
        mapped.ToString().Should().Contain(expected);
    }

    [Test]
    public void ModelReference_MapsToSimpleType()
    {
        var mapped = _mapper.MapType(new IrModelReference { ModelName = "Pet" });
        mapped.ToString().Should().Contain("Pet");
    }

    [Test]
    public void ArrayType_MapsToArrayTypeNode()
    {
        var mapped = _mapper.MapType(new IrArrayType
        {
            ElementType = Prim(IrPrimitiveKind.String),
        });
        // ToString of ArrayTypeNode might just include "[]" or wrap in Array<>
        // — either way, expect element type referenced.
        mapped.ToString().Should().Contain("string");
    }

    [Test]
    public void DictionaryType_MapsWithStringKeyAndValueType()
    {
        var mapped = _mapper.MapType(new IrDictionaryType
        {
            KeyType = Prim(IrPrimitiveKind.String),
            ValueType = new IrModelReference { ModelName = "Pet" },
        });
        mapped.ToString().Should().Contain("Pet");
    }

    [Test]
    public void NullableType_MapsToUnionWithNull()
    {
        var mapped = _mapper.MapType(new IrNullableType
        {
            InnerType = Prim(IrPrimitiveKind.String),
        });
        // TS mapper expresses nullable as `T | null` union, not as the
        // `T?` short form.
        mapped.Should().BeOfType<ValIO.Core.Ast.Nodes.Types.UnionTypeNode>();
        var union = (ValIO.Core.Ast.Nodes.Types.UnionTypeNode)mapped;
        union.Types.Select(t => t.ToString()).Should().Contain(s => s!.Contains("null"));
        union.Types.Select(t => t.ToString()).Should().Contain(s => s!.Contains("string"));
    }

    [Test]
    public void OptionalType_UnwrapsInner()
    {
        // IrOptionalType is unwrapped — the `?` modifier is applied via
        // `IsOptionalInLanguage` on the property descriptor instead.
        var mapped = _mapper.MapType(new IrOptionalType
        {
            InnerType = Prim(IrPrimitiveKind.String),
        });
        mapped.Should().BeOfType<ValIO.Core.Ast.Nodes.Types.SimpleTypeNode>();
        mapped.ToString().Should().Contain("string");
    }

    [Test]
    public void IsOptionalInLanguage_FromRequiredFalse_OrIrOptional()
    {
        var requiredProp = new ValIO.Core.IR.IrProperty
        {
            Name = "x", OriginalName = "x", IsRequired = true,
            Type = Prim(IrPrimitiveKind.String),
        };
        var nonRequiredProp = requiredProp with { IsRequired = false };
        var optionalWrapperProp = requiredProp with
        {
            Type = new IrOptionalType { InnerType = Prim(IrPrimitiveKind.String) },
        };

        _mapper.IsOptionalInLanguage(requiredProp).Should().BeFalse();
        _mapper.IsOptionalInLanguage(nonRequiredProp).Should().BeTrue();
        _mapper.IsOptionalInLanguage(optionalWrapperProp).Should().BeTrue();
    }

    [Test]
    public void GetImportTarget_ReturnsModelName_OnlyForReferences()
    {
        _mapper.GetImportTarget(new IrModelReference { ModelName = "Pet" }).Should().Be("Pet");
        _mapper.GetImportTarget(Prim(IrPrimitiveKind.String)).Should().BeNull();
        _mapper.GetImportTarget(new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) }).Should().BeNull();
    }
}
