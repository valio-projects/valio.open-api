using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Interfaces;

namespace ValIO.Generators.Python;

/// <summary>
/// Маппит IR типы в Python AST type nodes (PEP 585 generics, X | None).
/// </summary>
public sealed class PythonIrTypeMapper : IIrTypeMapper
{
    public TypeNode MapType(IrType irType) => irType switch
    {
        IrPrimitiveType p      => MapPrimitive(p),
        IrArrayType arr        => new GenericTypeNode(new IdentifierNode("list"), [MapType(arr.ElementType)]),
        IrDictionaryType dict  => new GenericTypeNode(new IdentifierNode("dict"), [MapType(dict.KeyType), MapType(dict.ValueType)]),
        IrNullableType n       => new UnionTypeNode([MapType(n.InnerType), new SimpleTypeNode(new IdentifierNode("None"), isPrimitive: true)]),
        IrOptionalType o       => MapType(o.InnerType),
        IrModelReference r     => new SimpleTypeNode(new IdentifierNode(r.ModelName)),
        IrUnionType u          => new UnionTypeNode([.. u.Types.Select(MapType)]),
        IrIntersectionType i   => MapIntersection(i),
        _                      => new SimpleTypeNode(new IdentifierNode("Any"), isPrimitive: true)
    };

    private static SimpleTypeNode MapPrimitive(IrPrimitiveType p) => p.PrimitiveKind switch
    {
        IrPrimitiveKind.String    => new SimpleTypeNode(new IdentifierNode("str"), isPrimitive: true),
        IrPrimitiveKind.Integer   => new SimpleTypeNode(new IdentifierNode("int"), isPrimitive: true),
        IrPrimitiveKind.Long      => new SimpleTypeNode(new IdentifierNode("int"), isPrimitive: true),
        IrPrimitiveKind.Number    => new SimpleTypeNode(new IdentifierNode("float"), isPrimitive: true),
        IrPrimitiveKind.Boolean   => new SimpleTypeNode(new IdentifierNode("bool"), isPrimitive: true),
        IrPrimitiveKind.Date      => new SimpleTypeNode(new IdentifierNode("date"), isPrimitive: true),
        IrPrimitiveKind.DateTime  => new SimpleTypeNode(new IdentifierNode("datetime"), isPrimitive: true),
        IrPrimitiveKind.Time      => new SimpleTypeNode(new IdentifierNode("time"), isPrimitive: true),
        IrPrimitiveKind.Uuid      => new SimpleTypeNode(new IdentifierNode("UUID"), isPrimitive: true),
        IrPrimitiveKind.Binary    => new SimpleTypeNode(new IdentifierNode("bytes"), isPrimitive: true),
        IrPrimitiveKind.ByteArray => new SimpleTypeNode(new IdentifierNode("bytes"), isPrimitive: true),
        IrPrimitiveKind.File      => new SimpleTypeNode(new IdentifierNode("bytes"), isPrimitive: true),
        IrPrimitiveKind.Any       => new SimpleTypeNode(new IdentifierNode("Any"), isPrimitive: true),
        IrPrimitiveKind.Void      => new SimpleTypeNode(new IdentifierNode("None"), isPrimitive: true),
        IrPrimitiveKind.Null      => new SimpleTypeNode(new IdentifierNode("None"), isPrimitive: true),
        _                         => new SimpleTypeNode(new IdentifierNode("Any"), isPrimitive: true)
    };

    private TypeNode MapIntersection(IrIntersectionType i)
    {
        // Python не имеет intersection types; в худшем случае берём первый ModelReference,
        // иначе Any.
        var modelRef = i.Types.OfType<IrModelReference>().FirstOrDefault();
        if (modelRef != null)
            return MapType(modelRef);

        var first = i.Types.FirstOrDefault();
        return first != null
            ? MapType(first)
            : new SimpleTypeNode(new IdentifierNode("Any"), isPrimitive: true);
    }

    public bool IsOptionalInLanguage(IrProperty property) =>
        property.Type is IrOptionalType || !property.IsRequired;

    /// <summary>
    /// Возвращает имя модели для генерации Python-импорта, либо null если импорт не нужен.
    /// </summary>
    public string? GetImportTarget(IrType irType) =>
        irType switch
        {
            IrModelReference r => r.ModelName,
            IrArrayType arr => GetImportTarget(arr.ElementType),
            IrDictionaryType dict => GetImportTarget(dict.ValueType) ?? GetImportTarget(dict.KeyType),
            IrNullableType n => GetImportTarget(n.InnerType),
            IrOptionalType o => GetImportTarget(o.InnerType),
            _ => null,
        };
}
