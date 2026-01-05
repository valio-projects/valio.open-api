using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

/// <summary>
/// Часть template literal
/// </summary>
public sealed class TemplateLiteralPart
{
    /// <summary>
    /// Текст части (для текстовых частей)
    /// </summary>
    public string? Text { get; }
    
    /// <summary>
    /// Выражение (для выражений внутри ${})
    /// </summary>
    public ExpressionNode? Expression { get; }
    
    /// <summary>
    /// Является ли часть выражением (true для ${...}, false для текста)
    /// </summary>
    public bool IsExpression => Expression != null;
    
    /// <summary>
    /// Конструктор для текстовой части
    /// </summary>
    public TemplateLiteralPart(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Expression = null;
    }
    
    /// <summary>
    /// Конструктор для выражения
    /// </summary>
    public TemplateLiteralPart(ExpressionNode expression)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Text = null;
    }
}

/// <summary>
/// Узел выражения template literal (TypeScript)
/// </summary>
public sealed class TemplateLiteralExpressionNode : ExpressionNode
{
    /// <summary>
    /// Части template literal
    /// </summary>
    public IReadOnlyList<TemplateLiteralPart> Parts { get; }
    
    public override AstNodeKind Kind => AstNodeKind.TemplateLiteralExpression;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            foreach (var part in Parts)
            {
                if (part.Expression != null)
                    children.Add(part.Expression);
            }
            if (Type != null)
                children.Add(Type);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public TemplateLiteralExpressionNode(
        IReadOnlyList<TemplateLiteralPart> parts,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(type, metadata)
    {
        if (parts == null || parts.Count == 0)
            throw new ArgumentException("Template literal must have at least one part", nameof(parts));
        
        Parts = parts.ToImmutableList();
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITypeScriptAstVisitor<TResult> typescriptVisitor)
        {
            return typescriptVisitor.VisitTemplateLiteralExpression(this);
        }
        throw new NotSupportedException($"Visitor must implement ITypeScriptAstVisitor<TResult> to visit TemplateLiteralExpressionNode");
    }
    
    public override AstNode Clone()
    {
        var clonedParts = Parts.Select(part =>
        {
            if (part.IsExpression)
                return new TemplateLiteralPart((ExpressionNode)part.Expression!.Clone());
            return new TemplateLiteralPart(part.Text!);
        }).ToImmutableList();
        
        return new TemplateLiteralExpressionNode(
            clonedParts,
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
