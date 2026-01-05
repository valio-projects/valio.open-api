using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Nodes;

/// <summary>
/// Узел присваивания свойства в object literal (TypeScript)
/// </summary>
public sealed class PropertyAssignmentNode : AstNode
{
    /// <summary>
    /// Имя свойства
    /// </summary>
    public IdentifierNode Name { get; }
    
    /// <summary>
    /// Значение
    /// </summary>
    public ExpressionNode Value { get; }
    
    /// <summary>
    /// Shorthand (для { name } синтаксиса)
    /// </summary>
    public bool IsShorthand { get; }
    
    public override AstNodeKind Kind => AstNodeKind.PropertyAssignment;
    
    public override IReadOnlyList<AstNode> Children => new[] { Name, Value };
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public PropertyAssignmentNode(
        IdentifierNode name,
        ExpressionNode value,
        bool isShorthand = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Value = value ?? throw new ArgumentNullException(nameof(value));
        IsShorthand = isShorthand;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITypeScriptAstVisitor<TResult> typescriptVisitor)
        {
            return typescriptVisitor.VisitPropertyAssignment(this);
        }
        throw new NotSupportedException($"Visitor must implement ITypeScriptAstVisitor<TResult> to visit PropertyAssignmentNode");
    }
    
    public override AstNode Clone()
    {
        return new PropertyAssignmentNode(
            (IdentifierNode)Name.Clone(),
            (ExpressionNode)Value.Clone(),
            IsShorthand,
            Metadata.ToImmutableDictionary());
    }
}
