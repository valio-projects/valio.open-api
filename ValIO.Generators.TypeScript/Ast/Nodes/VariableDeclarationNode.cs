using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Nodes;

/// <summary>
/// Узел объявления переменной (TypeScript)
/// </summary>
public sealed class VariableDeclarationNode : DeclarationNode
{
    /// <summary>
    /// Тип переменной
    /// </summary>
    public TypeNode? Type { get; }
    
    /// <summary>
    /// Инициализатор
    /// </summary>
    public ExpressionNode? Initializer { get; }
    
    /// <summary>
    /// Тип объявления (let/const/var)
    /// </summary>
    public VariableDeclarationKind DeclarationKind { get; }
    
    public override AstNodeKind Kind => AstNodeKind.VariableDeclaration;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null)
                children.Add(Documentation);
            children.AddRange(Attributes);
            if (Type != null)
                children.Add(Type);
            if (Initializer != null)
                children.Add(Initializer);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public VariableDeclarationNode(
        IdentifierNode name,
        VariableDeclarationKind declarationKind = VariableDeclarationKind.Const,
        TypeNode? type = null,
        ExpressionNode? initializer = null,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes, metadata)
    {
        DeclarationKind = declarationKind;
        Type = type;
        Initializer = initializer;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITypeScriptAstVisitor<TResult> typescriptVisitor)
        {
            return typescriptVisitor.VisitVariableDeclaration(this);
        }
        throw new NotSupportedException($"Visitor must implement ITypeScriptAstVisitor<TResult> to visit VariableDeclarationNode");
    }
    
    public override AstNode Clone()
    {
        return new VariableDeclarationNode(
            (IdentifierNode)Name.Clone(),
            DeclarationKind,
            Type != null ? (TypeNode)Type.Clone() : null,
            Initializer != null ? (ExpressionNode)Initializer.Clone() : null,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }
}
