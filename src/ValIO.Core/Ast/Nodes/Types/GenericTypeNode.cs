using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Types;

/// <summary>
/// Узел generic типа
/// </summary>
public sealed class GenericTypeNode : TypeNode
{
    /// <summary>
    /// Имя generic типа
    /// </summary>
    public IdentifierNode Name { get; }
    
    /// <summary>
    /// Аргументы типа
    /// </summary>
    public IReadOnlyList<TypeNode> TypeArguments { get; }
    
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            children.AddRange(TypeArguments);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public GenericTypeNode(
        IdentifierNode name,
        IReadOnlyList<TypeNode> typeArguments,
        bool isNullable = false,
        bool isOptional = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(isNullable, isOptional, metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeArguments = typeArguments?.ToImmutableList() ?? throw new ArgumentNullException(nameof(typeArguments));
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitGenericType(this);
    }
    
    public override AstNode Clone()
    {
        return new GenericTypeNode(
            (IdentifierNode)Name.Clone(),
            TypeArguments.Select(t => (TypeNode)t.Clone()).ToImmutableList(),
            IsNullable,
            IsOptional,
            Metadata.ToImmutableDictionary());
    }
    
    public override string ToString()
    {
        var argsStr = string.Join(", ", TypeArguments);
        var nullableStr = IsNullable ? "?" : "";
        return $"{Name.Name}<{argsStr}>{nullableStr}";
    }
}
