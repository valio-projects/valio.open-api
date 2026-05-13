using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Statements;

/// <summary>
/// Узел объявления переменной (statement)
/// Используется в C# для объявления переменных внутри методов
/// </summary>
public sealed class VariableDeclarationStatementNode : StatementNode
{
    /// <summary>
    /// Имя переменной
    /// </summary>
    public IdentifierNode Name { get; }
    
    /// <summary>
    /// Тип переменной (опциональный, для var inference)
    /// </summary>
    public TypeNode? Type { get; }
    
    /// <summary>
    /// Инициализатор
    /// </summary>
    public ExpressionNode? Initializer { get; }
    
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null)
                children.Insert(0, Documentation);
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
    public VariableDeclarationStatementNode(
        IdentifierNode name,
        TypeNode? type = null,
        ExpressionNode? initializer = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(documentation, metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type;
        Initializer = initializer;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitVariableDeclarationStatement(this);
    }
    
    public override AstNode Clone()
    {
        return new VariableDeclarationStatementNode(
            (IdentifierNode)Name.Clone(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Initializer != null ? (ExpressionNode)Initializer.Clone() : null,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
