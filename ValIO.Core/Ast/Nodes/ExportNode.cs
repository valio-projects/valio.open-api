using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Узел экспорта
/// </summary>
public sealed class ExportNode : AstNode
{
    /// <summary>
    /// Экспортируемое объявление
    /// </summary>
    public DeclarationNode Declaration { get; }
    
    /// <summary>
    /// Тип экспорта
    /// </summary>
    public ExportKind ExportKind { get; }
    
    public override AstNodeKind Kind => AstNodeKind.Export;
    
    public override IReadOnlyList<AstNode> Children => new[] { Declaration };
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ExportNode(DeclarationNode declaration, ExportKind exportKind = Enums.ExportKind.Named, IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Declaration = declaration ?? throw new ArgumentNullException(nameof(declaration));
        ExportKind = exportKind;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitExport(this);
    }
    
    public override AstNode Clone()
    {
        return new ExportNode(
            (DeclarationNode)Declaration.Clone(),
            ExportKind,
            Metadata.ToImmutableDictionary());
    }
}
