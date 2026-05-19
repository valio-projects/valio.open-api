using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.Php.Ast.Nodes;

namespace ValIO.Generators.Php.Ast.Builders;

/// <summary>
/// Строит <see cref="PhpSourceFileNode"/> с DTO-классом для одной IR-модели.
/// </summary>
public sealed class PhpModelAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly PhpIrTypeMapper _typeMapper;

    public PhpModelAstBuilder(INamingConverter namingConverter, PhpIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
    }

    public PhpSourceFileNode BuildSourceFile(IrModel model, string @namespace)
    {
        var ns = NormalizeNamespace($"{@namespace}\\Models");

        var promoted = new List<PhpPromotedParameter>();
        foreach (var prop in model.Properties)
        {
            promoted.Add(BuildPromotedProperty(prop));
        }

        var docLines = new List<string>();
        if (!string.IsNullOrWhiteSpace(model.Description))
            docLines.Add(model.Description!);
        // PHPDoc shape для удобства потребителей (минимальный набор).
        var shape = BuildPhpStanShape(model);
        if (shape != null)
        {
            if (docLines.Count > 0) docLines.Add("");
            docLines.Add($"@phpstan-type {model.Name}Data {shape}");
        }

        var classDoc = docLines.Count > 0
            ? new CommentNode(string.Join("\n", docLines), CommentKind.MultiLine)
            : null;

        var ctor = new PhpConstructorDeclarationNode(
            promotedParameters: promoted,
            body: null,
            documentation: null);

        var cls = new PhpClassDeclarationNode(
            name: new IdentifierNode(model.Name),
            isFinal: true,
            constructor: ctor,
            members: ImmutableList<DeclarationNode>.Empty,
            documentation: classDoc);

        return new PhpSourceFileNode(
            @namespace: ns,
            uses: ImmutableList<PhpUseStatementNode>.Empty,
            declarations: new DeclarationNode[] { cls }.ToImmutableList());
    }

    private PhpPromotedParameter BuildPromotedProperty(IrProperty property)
    {
        var typeNode = _typeMapper.MapType(property.Type);
        var isOptional = _typeMapper.IsOptionalInLanguage(property);

        // Для опциональных свойств — добавляем nullable + default null, чтобы PHP читал отсутствие значения.
        TypeNode finalType = typeNode;
        ExpressionNode? defaultValue = null;
        if (isOptional)
        {
            if (finalType is not NullableTypeNode)
                finalType = new NullableTypeNode(finalType);
            defaultValue = new ValIO.Core.Ast.Nodes.Expressions.LiteralExpressionNode(null, LiteralKind.Null);
        }

        var phpDocType = _typeMapper.GetPhpDocArrayType(property.Type);

        var param = new ParameterDeclarationNode(
            name: new IdentifierNode(property.Name),
            type: finalType,
            isRequired: property.IsRequired,
            defaultValue: defaultValue);

        return new PhpPromotedParameter(
            visibility: "public",
            isReadonly: true,
            parameter: param,
            phpDocType: phpDocType);
    }

    private string? BuildPhpStanShape(IrModel model)
    {
        if (model.Properties.Count == 0) return null;
        var parts = new List<string>(model.Properties.Count);
        foreach (var p in model.Properties)
        {
            var key = p.IsRequired ? p.OriginalName : $"{p.OriginalName}?";
            var t = GetSimpleShapeType(p.Type);
            parts.Add($"{key}: {t}");
        }
        return $"array{{{string.Join(", ", parts)}}}";
    }

    private string GetSimpleShapeType(IrType t) => t switch
    {
        IrPrimitiveType pt => pt.PrimitiveKind switch
        {
            ValIO.Core.IR.Enums.IrPrimitiveKind.String   => "string",
            ValIO.Core.IR.Enums.IrPrimitiveKind.Integer  => "int",
            ValIO.Core.IR.Enums.IrPrimitiveKind.Long     => "int",
            ValIO.Core.IR.Enums.IrPrimitiveKind.Number   => "float",
            ValIO.Core.IR.Enums.IrPrimitiveKind.Boolean  => "bool",
            ValIO.Core.IR.Enums.IrPrimitiveKind.Date     => "string",
            ValIO.Core.IR.Enums.IrPrimitiveKind.DateTime => "string",
            ValIO.Core.IR.Enums.IrPrimitiveKind.Uuid     => "string",
            _ => "mixed",
        },
        IrModelReference r => r.ModelName,
        IrArrayType _ => "array",
        IrDictionaryType _ => "array",
        IrNullableType n => $"{GetSimpleShapeType(n.InnerType)}|null",
        IrOptionalType o => GetSimpleShapeType(o.InnerType),
        _ => "mixed",
    };

    private static string NormalizeNamespace(string ns) => ns.Trim('\\').Replace('/', '\\');
}
