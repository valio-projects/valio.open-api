using System.Collections.Immutable;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Базовый класс для всех объявлений
/// </summary>
public abstract class DeclarationNode : AstNode
{
    /// <summary>
    /// Имя объявления
    /// </summary>
    public IdentifierNode Name { get; }
    
    /// <summary>
    /// Документация (комментарий)
    /// </summary>
    public CommentNode? Documentation { get; }
    
    /// <summary>
    /// Атрибуты/декораторы
    /// </summary>
    public IReadOnlyList<AttributeNode> Attributes { get; }
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null)
                children.Add(Documentation);
            children.AddRange(Attributes);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    protected DeclarationNode(
        IdentifierNode name,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Documentation = documentation;
        Attributes = attributes?.ToImmutableList() ?? ImmutableList<AttributeNode>.Empty;
    }
    
    public override string ToString() => $"{GetType().Name}({Name.Name})";
}
