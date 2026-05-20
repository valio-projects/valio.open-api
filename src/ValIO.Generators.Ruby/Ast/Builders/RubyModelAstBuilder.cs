using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.Ruby.Ast.Nodes;

namespace ValIO.Generators.Ruby.Ast.Builders;

/// <summary>
/// Builds a Ruby <c>class</c> with one <c>attr_accessor</c> per IR property and
/// an <c>initialize</c> method that takes all attributes as keyword args.
/// </summary>
public sealed class RubyModelAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly RubyIrTypeMapper _typeMapper;

    public RubyModelAstBuilder(INamingConverter namingConverter, RubyIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _typeMapper = typeMapper;
    }

    public RubyFileNode BuildFile(IrModel model)
    {
        var members = new List<DeclarationNode>(model.Properties.Count + 1);
        var attributeNames = new List<string>(model.Properties.Count);

        foreach (var prop in model.Properties)
        {
            var snakeName = _namingConverter.ToSnakeCase(prop.Name);
            var mappedType = _typeMapper.MapType(prop.Type);
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

        var className = _namingConverter.ToPascalCase(model.Name);
        var classNode = new RubyClassNode(
            new IdentifierNode(className),
            parentClass: null,
            members: members,
            documentation: !string.IsNullOrWhiteSpace(model.Description)
                ? new CommentNode(model.Description!, CommentKind.MultiLine)
                : null);

        return new RubyFileNode(classes: [classNode]);
    }

    private static RubyMethodNode BuildInitializer(IReadOnlyList<string> attributeNames)
    {
        // Use keyword arguments with `nil` default — keeps `initialize` callable
        // even when only some attributes are known.
        var parameters = attributeNames.Select(name => $"{name}: nil").ToList();

        var body = new List<string>(attributeNames.Count);
        foreach (var name in attributeNames)
        {
            body.Add($"@{name} = {name}");
        }

        return new RubyMethodNode(
            new IdentifierNode("initialize"),
            parameters: parameters,
            body: body);
    }
}
