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
public sealed class DartModelAstBuilder(INamingConverter namingConverter, DartIrTypeMapper typeMapper)
{

    /// <summary>Builds the model and the imports for related property types.</summary>
    /// <param name="model">The effective model, including inherited properties.</param>
    /// <param name="context">The original model and enum names in the document.</param>
    public DartFileNode BuildFile(IrModel model, IrDocumentContext context)
    {
        var className = namingConverter.ToPascalCase(model.Name);
        var fields = new List<DartFieldNode>(model.Properties.Count);
        var ctorParams = new List<DartParameterNode>(model.Properties.Count);

        foreach (var prop in model.Properties)
        {
            var fieldName = namingConverter.ToCamelCase(prop.Name);
            var safe = DartReservedNames.Avoid(fieldName);
            var mapped = typeMapper.MapType(prop.Type);
            var isOptional = typeMapper.IsOptionalInLanguage(prop);

            fields.Add(new DartFieldNode(
                new IdentifierNode(safe),
                mapped,
                isNullable: isOptional && mapped is not NullableTypeNode,
                isFinal: true,
                jsonKey: !string.Equals(prop.OriginalName, safe, StringComparison.Ordinal)
                    ? prop.OriginalName
                    : null,
                documentation: !string.IsNullOrWhiteSpace(prop.Description)
                    ? new CommentNode(prop.Description!, CommentKind.MultiLine)
                    : null));

            ctorParams.Add(new DartParameterNode(
                new IdentifierNode(safe),
                mapped,
                isRequired: !isOptional,
                isNamed: true,
                isFieldFormal: true));
        }

        var ctor = new DartMethodNode(
            new IdentifierNode(className),
            returnType: null,
            parameters: ctorParams,
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

        var imports = DartImports.Build(context, namingConverter, model.Properties.Select(property => property.Type), "", model.Name);
        return new DartFileNode(declarations: new[] { cls }, imports: imports);
    }
}
