using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.Dart.Ast.Nodes;

namespace ValIO.Generators.Dart.Ast.Builders;

/// <summary>
/// Builds a Dart class for one <see cref="IrModel"/> — immutable by default
/// (<c>final</c> fields + a single named-args constructor). Maps optional
/// properties to nullable fields.
/// </summary>
public sealed class DartModelAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly DartIrTypeMapper _typeMapper;

    public DartModelAstBuilder(INamingConverter namingConverter, DartIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _typeMapper = typeMapper;
    }

    public DartFileNode BuildFile(IrModel model)
    {
        var className = _namingConverter.ToPascalCase(model.Name);
        var fields = new List<DartFieldNode>(model.Properties.Count);
        var ctorParams = new List<DartParameterNode>(model.Properties.Count);

        foreach (var prop in model.Properties)
        {
            var fieldName = _namingConverter.ToCamelCase(prop.Name);
            var safe = DartReservedNames.Avoid(fieldName);
            var mapped = _typeMapper.MapType(prop.Type);
            var isOptional = _typeMapper.IsOptionalInLanguage(prop);

            fields.Add(new DartFieldNode(
                new IdentifierNode(safe),
                mapped,
                isNullable: isOptional,
                isFinal: true,
                jsonKey: !string.Equals(prop.OriginalName, safe, StringComparison.Ordinal)
                    ? prop.OriginalName
                    : null,
                documentation: !string.IsNullOrWhiteSpace(prop.Description)
                    ? new CommentNode(prop.Description!, CommentKind.MultiLine)
                    : null));

            ctorParams.Add(new DartParameterNode(
                new IdentifierNode("this." + safe),
                mapped,
                isRequired: !isOptional,
                isNamed: true));
        }

        var ctor = new DartMethodNode(
            new IdentifierNode(className),
            returnType: null,
            parameters: ctorParams,
            bodyLines: null,
            isAsync: false,
            isStatic: false,
            isConstructor: true);

        var cls = new DartClassDeclarationNode(
            new IdentifierNode(className),
            fields: fields,
            methods: new[] { ctor },
            documentation: !string.IsNullOrWhiteSpace(model.Description)
                ? new CommentNode(model.Description!, CommentKind.MultiLine)
                : null);

        var imports = new List<DartImportNode> { new("dart:typed_data") };
        return new DartFileNode(declarations: new[] { cls }, imports: imports);
    }
}
