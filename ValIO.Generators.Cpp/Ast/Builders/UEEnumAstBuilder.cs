using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Ast.Nodes.UE;
using ValIO.Generators.Cpp.Models;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Ast.Builders;

/// <summary>
/// Строитель AST для UE enum (UENUM) из IR представления.
/// Генерирует CppHeaderFileNode, содержащий UEnumDeclarationNode.
/// </summary>
public sealed class UEEnumAstBuilder
{
    private readonly UENamingConvention _naming;
    private readonly CppGenerationOptions _options;

    public UEEnumAstBuilder(UENamingConvention naming, CppGenerationOptions options)
    {
        _naming = naming ?? throw new ArgumentNullException(nameof(naming));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Строит CppHeaderFileNode из IrEnum
    /// </summary>
    public CppHeaderFileNode Build(IrEnum irEnum)
    {
        var enumName = _naming.EnumName(irEnum.Name);
        var fileName = _naming.EnumFileName(irEnum.Name);

        // UE Blueprints требуют uint8 как базовый тип для UENUM
        var underlyingType = new SimpleTypeNode(new IdentifierNode("uint8"), isPrimitive: true);

        var values = irEnum.Values
            .Select((v, i) => new UEnumValueNode(
                name: new IdentifierNode(_naming.PropertyName(v.Name)),
                value: new LiteralExpressionNode(i, LiteralKind.Number),
                displayName: v.Description ?? v.Name))
            .ToList();

        var enumNode = new UEnumDeclarationNode(
            name: new IdentifierNode(enumName),
            underlyingType: underlyingType,
            values: values,
            documentation: !string.IsNullOrWhiteSpace(irEnum.Description)
                ? new CommentNode(irEnum.Description, CommentKind.MultiLine)
                : null);

        var includes = new List<CppIncludeDirectiveNode>
        {
            new("CoreMinimal.h"),
            new(fileName.Replace(".h", _options.GeneratedHeaderSuffix))
        };

        return new CppHeaderFileNode(
            fileName: fileName,
            headerComment: _options.Copyright != null ? new CommentNode(_options.Copyright, CommentKind.MultiLine) : null,
            includes: includes,
            declarations: new AstNode[] { enumNode });
    }
}
