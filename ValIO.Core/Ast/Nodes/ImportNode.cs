using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Узел импорта
/// </summary>
public sealed class ImportNode : AstNode
{
    /// <summary>
    /// Импортируемые элементы
    /// </summary>
    public IReadOnlyList<IdentifierNode> Items { get; }
    
    /// <summary>
    /// Путь/модуль для импорта
    /// </summary>
    public string? From { get; }
    
    /// <summary>
    /// Тип импорта
    /// </summary>
    public ImportKind Kind { get; }
    
    public override AstNodeKind Kind => AstNodeKind.Import;
    
    public override IReadOnlyList<AstNode> Children => Items.Cast<AstNode>().ToImmutableList();
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ImportNode(
        IReadOnlyList<IdentifierNode> items,
        string? from = null,
        ImportKind kind = ImportKind.Named,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Items = items?.ToImmutableList() ?? throw new ArgumentNullException(nameof(items));
        From = from;
        Kind = kind;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitImport(this);
    }
    
    public override AstNode Clone()
    {
        return new ImportNode(
            Items.Select(i => (IdentifierNode)i.Clone()).ToImmutableList(),
            From,
            ImportType,
            Metadata.ToImmutableDictionary());
    }
}
