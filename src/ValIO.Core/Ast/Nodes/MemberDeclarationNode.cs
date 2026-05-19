using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Базовый класс для членов типа (properties, methods, fields, etc.)
/// </summary>
public abstract class MemberDeclarationNode : DeclarationNode
{
    protected MemberDeclarationNode(
        IdentifierNode name,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes, metadata)
    {
    }
}
