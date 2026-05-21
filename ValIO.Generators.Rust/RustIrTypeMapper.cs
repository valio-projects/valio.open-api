using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.Rust.Options;

namespace ValIO.Generators.Rust;

/// <summary>
/// Maps IR types to Rust AST type nodes. Nullable/optional values are expressed
/// as <c>Option&lt;T&gt;</c> via a <see cref="GenericTypeNode"/>.
/// </summary>
public sealed class RustIrTypeMapper : IIrTypeMapper
{
    private readonly RustGenerationOptions _options;

    public RustIrTypeMapper(RustGenerationOptions? options = null)
    {
        _options = options ?? new RustGenerationOptions();
    }

    public TypeNode MapType(IrType irType) => irType switch
    {
        IrPrimitiveType p     => MapPrimitive(p),
        IrArrayType arr       => new GenericTypeNode(
            new IdentifierNode("Vec"),
            ImmutableList.Create<TypeNode>(MapType(arr.ElementType))),
        IrDictionaryType dict => new GenericTypeNode(
            new IdentifierNode("std::collections::HashMap"),
            ImmutableList.Create<TypeNode>(MapType(dict.KeyType), MapType(dict.ValueType))),
        IrNullableType n      => Wrap("Option", MapType(n.InnerType)),
        IrOptionalType o      => Wrap("Option", MapType(o.InnerType)),
        IrModelReference r    => new SimpleTypeNode(new IdentifierNode(r.ModelName)),
        IrUnionType        => AnyType(),
        IrIntersectionType => AnyType(),
        _                  => AnyType(),
    };

    private TypeNode MapPrimitive(IrPrimitiveType p) => p.PrimitiveKind switch
    {
        IrPrimitiveKind.String    => new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: true),
        IrPrimitiveKind.Integer   => new SimpleTypeNode(new IdentifierNode("i32"),    isPrimitive: true),
        IrPrimitiveKind.Long      => new SimpleTypeNode(new IdentifierNode("i64"),    isPrimitive: true),
        IrPrimitiveKind.Number    => new SimpleTypeNode(new IdentifierNode("f64"),    isPrimitive: true),
        IrPrimitiveKind.Boolean   => new SimpleTypeNode(new IdentifierNode("bool"),   isPrimitive: true),
        IrPrimitiveKind.Date      => new SimpleTypeNode(new IdentifierNode("chrono::NaiveDate"), isPrimitive: true),
        IrPrimitiveKind.DateTime  => new SimpleTypeNode(new IdentifierNode("chrono::DateTime<chrono::Utc>"), isPrimitive: true),
        IrPrimitiveKind.Time      => new SimpleTypeNode(new IdentifierNode("chrono::NaiveTime"), isPrimitive: true),
        IrPrimitiveKind.Uuid      => new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: true),
        IrPrimitiveKind.Binary    => new GenericTypeNode(
            new IdentifierNode("Vec"),
            ImmutableList.Create<TypeNode>(new SimpleTypeNode(new IdentifierNode("u8"), isPrimitive: true))),
        IrPrimitiveKind.ByteArray => new GenericTypeNode(
            new IdentifierNode("Vec"),
            ImmutableList.Create<TypeNode>(new SimpleTypeNode(new IdentifierNode("u8"), isPrimitive: true))),
        IrPrimitiveKind.File      => new GenericTypeNode(
            new IdentifierNode("Vec"),
            ImmutableList.Create<TypeNode>(new SimpleTypeNode(new IdentifierNode("u8"), isPrimitive: true))),
        IrPrimitiveKind.Any       => AnyType(),
        IrPrimitiveKind.Void      => new SimpleTypeNode(new IdentifierNode("()"), isPrimitive: true),
        _                         => AnyType(),
    };

    private TypeNode AnyType() =>
        _options.AnyMapping == RustOptionIds.AnyMapping.String
            ? new SimpleTypeNode(new IdentifierNode("String"),             isPrimitive: true)
            : new SimpleTypeNode(new IdentifierNode("serde_json::Value"),  isPrimitive: true);

    private static GenericTypeNode Wrap(string ctor, TypeNode inner) =>
        new(new IdentifierNode(ctor), ImmutableList.Create<TypeNode>(inner));

    public bool IsOptionalInLanguage(IrProperty property) =>
        property.Type is IrOptionalType || !property.IsRequired;

    /// <summary>
    /// Returns the model/enum name referenced by this IR type (for use-statement collection),
    /// or null when no import is required.
    /// </summary>
    public string? GetImportTarget(IrType irType) => irType switch
    {
        IrModelReference r    => r.ModelName,
        IrArrayType arr       => GetImportTarget(arr.ElementType),
        IrDictionaryType dict => GetImportTarget(dict.ValueType) ?? GetImportTarget(dict.KeyType),
        IrNullableType n      => GetImportTarget(n.InnerType),
        IrOptionalType o      => GetImportTarget(o.InnerType),
        _ => null,
    };
}
