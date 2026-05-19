using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.CSharp.Ast.Visitors;

namespace ValIO.Generators.CSharp.Ast.Nodes;

/// <summary>
/// Узел объявления конструктора (C#)
/// </summary>
public sealed class ConstructorDeclarationNode : DeclarationNode
{
    /// <summary>
    /// Параметры конструктора
    /// </summary>
    public IReadOnlyList<ParameterDeclarationNode> Parameters { get; }
    
    /// <summary>
    /// Тело конструктора
    /// </summary>
    public BlockNode Body { get; }
    
    /// <summary>
    /// Модификатор доступа
    /// </summary>
    public AccessModifier AccessModifier { get; }
    
    /// <summary>
    /// Аргументы базового конструктора
    /// </summary>
    public IReadOnlyList<ExpressionNode> BaseConstructorArguments { get; }
    
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null)
                children.Add(Documentation);
            children.AddRange(Attributes);
            children.AddRange(Parameters);
            children.Add(Body);
            children.AddRange(BaseConstructorArguments);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ConstructorDeclarationNode(
        IdentifierNode name,
        BlockNode body,
        AccessModifier accessModifier = AccessModifier.Public,
        IReadOnlyList<ParameterDeclarationNode>? parameters = null,
        IReadOnlyList<ExpressionNode>? baseConstructorArguments = null,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes, metadata)
    {
        Body = body ?? throw new ArgumentNullException(nameof(body));
        AccessModifier = accessModifier;
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<ParameterDeclarationNode>.Empty;
        BaseConstructorArguments = baseConstructorArguments?.ToImmutableList() ?? ImmutableList<ExpressionNode>.Empty;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICSharpAstVisitor<TResult> csharpVisitor)
        {
            return csharpVisitor.VisitConstructorDeclaration(this);
        }
        throw new NotSupportedException($"Visitor must implement ICSharpAstVisitor<TResult> to visit ConstructorDeclarationNode");
    }
    
    public override AstNode Clone()
    {
        return new ConstructorDeclarationNode(
            (IdentifierNode)Name.Clone(),
            (BlockNode)Body.Clone(),
            AccessModifier,
            Parameters.Select(p => (ParameterDeclarationNode)p.Clone()).ToImmutableList(),
            BaseConstructorArguments.Select(a => (ExpressionNode)a.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }
}
