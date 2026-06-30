using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.Ruby.Options;

namespace ValIO.Generators.Ruby;

/// <summary>
/// Maps IR types to Ruby AST type nodes. Ruby is duck-typed, so the rendered
/// type strings are mostly used for YARD-style documentation comments.
/// </summary>
public sealed class RubyIrTypeMapper : IIrTypeMapper
{
    private readonly RubyGenerationOptions _options;

    public RubyIrTypeMapper(RubyGenerationOptions? options = null)
    {
        _options = options ?? new RubyGenerationOptions();
    }

    public TypeNode MapType(IrType irType) => irType switch
    {
        IrPrimitiveType p     => MapPrimitive(p),
        IrArrayType arr       => new GenericTypeNode(new IdentifierNode("Array"), [MapType(arr.ElementType)]),
        IrDictionaryType dict => new GenericTypeNode(new IdentifierNode("Hash"), [MapType(dict.KeyType), MapType(dict.ValueType)]),
        IrNullableType n      => new NullableTypeNode(MapType(n.InnerType)),
        IrOptionalType o      => MapType(o.InnerType),
        IrModelReference r    => new SimpleTypeNode(new IdentifierNode(r.ModelName)),
        // Ruby has no native union / intersection types — fall back to Object.
        IrUnionType        => AnyType(),
        IrIntersectionType => AnyType(),
        _                  => AnyType(),
    };

    private static SimpleTypeNode MapPrimitive(IrPrimitiveType p) => p.PrimitiveKind switch
    {
        IrPrimitiveKind.String    => new SimpleTypeNode(new IdentifierNode("String"),  isPrimitive: true),
        IrPrimitiveKind.Integer   => new SimpleTypeNode(new IdentifierNode("Integer"), isPrimitive: true),
        IrPrimitiveKind.Long      => new SimpleTypeNode(new IdentifierNode("Integer"), isPrimitive: true),
        IrPrimitiveKind.Number    => new SimpleTypeNode(new IdentifierNode("Float"),   isPrimitive: true),
        IrPrimitiveKind.Boolean   => new SimpleTypeNode(new IdentifierNode("Boolean"), isPrimitive: true),
        IrPrimitiveKind.Date      => new SimpleTypeNode(new IdentifierNode("String"),  isPrimitive: true),
        IrPrimitiveKind.DateTime  => new SimpleTypeNode(new IdentifierNode("String"),  isPrimitive: true),
        IrPrimitiveKind.Time      => new SimpleTypeNode(new IdentifierNode("String"),  isPrimitive: true),
        IrPrimitiveKind.Uuid      => new SimpleTypeNode(new IdentifierNode("String"),  isPrimitive: true),
        // Ruby doesn't have a typed equivalent for raw bytes — fall back to String.
        IrPrimitiveKind.Binary    => new SimpleTypeNode(new IdentifierNode("String"),  isPrimitive: true),
        IrPrimitiveKind.ByteArray => new SimpleTypeNode(new IdentifierNode("String"),  isPrimitive: true),
        IrPrimitiveKind.File      => new SimpleTypeNode(new IdentifierNode("String"),  isPrimitive: true),
        IrPrimitiveKind.Any       => AnyType(),
        IrPrimitiveKind.Void      => new SimpleTypeNode(new IdentifierNode("NilClass"), isPrimitive: true),
        IrPrimitiveKind.Null      => new SimpleTypeNode(new IdentifierNode("NilClass"), isPrimitive: true),
        _                         => AnyType(),
    };

    private static SimpleTypeNode AnyType() =>
        new SimpleTypeNode(new IdentifierNode("Object"), isPrimitive: true);

    public bool IsOptionalInLanguage(IrProperty property) =>
        property.Type is IrOptionalType || !property.IsRequired;

    /// <summary>
    /// Returns the model/enum name referenced by this IR type (for <c>require</c>
    /// collection), or null when no require is required.
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
