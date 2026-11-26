using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.Ruby.Ast.Nodes;

namespace ValIO.Generators.Ruby.Ast.Builders;

/// <summary>
/// Builds a Ruby <c>class</c> with one <c>attr_accessor</c> per IR property and
/// an <c>initialize</c> method that takes all attributes as keyword args.
/// </summary>
public sealed class RubyModelAstBuilder(INamingConverter namingConverter, RubyIrTypeMapper typeMapper)
{

    public RubyFileNode BuildFile(IrModel model)
    {
        var members = new List<DeclarationNode>(model.Properties.Count + 1);
        var attributeNames = new List<string>(model.Properties.Count);

        foreach (var prop in model.Properties)
        {
            var snakeName = namingConverter.ToSnakeCase(prop.Name);
            var mappedType = typeMapper.MapType(prop.Type);
            attributeNames.Add(snakeName);

            members.Add(new RubyAttributeNode(
                new IdentifierNode(snakeName),
                mappedType,
                documentation: !string.IsNullOrWhiteSpace(prop.Description)
                    ? new CommentNode(prop.Description!, CommentKind.SingleLine)
                    : null));
        }

        if (attributeNames.Count > 0)
        {
            members.Add(BuildInitializer(attributeNames));
        }
        else
        {
            // Empty initializer for parity with non-empty models.
            members.Add(new RubyMethodNode(
                new IdentifierNode("initialize"),
                parameters: null,
                body: null));
        }

        var className = namingConverter.ToPascalCase(model.Name);
        var classNode = new RubyClassNode(
            new IdentifierNode(className),
            parentClass: null,
            members: members,
            documentation: !string.IsNullOrWhiteSpace(model.Description)
                ? new CommentNode(model.Description!, CommentKind.MultiLine)
                : null);

        return new RubyFileNode(classes: [classNode]);
    }

    /// <summary>Constructs a model from keyword arguments whose default is nil.</summary>
    /// <param name="attributeNames">The instance attributes to initialize.</param>
    /// <returns>The initializer with assignments in declaration order.</returns>
    private static RubyMethodNode BuildInitializer(IReadOnlyList<string> attributeNames)
    {
        var parameters = attributeNames.Select(name => new ParameterDeclarationNode(new(name), type: null,
            defaultValue: new LiteralExpressionNode(null, LiteralKind.Null))).ToArray();
        var assignments = parameters.Select(parameter =>
        {
            var value = new IdentifierExpressionNode(parameter.Name);
            var field = new IdentifierExpressionNode(new IdentifierNode("@" + parameter.Name.Name));
            return new ExpressionStatementNode(new BinaryExpressionNode(field, BinaryOperator.Assign, value));
        }).ToArray();
        return new RubyMethodNode(new("initialize"), parameters, new BlockNode(assignments));
    }
}
