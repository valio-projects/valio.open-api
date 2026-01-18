using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Statements;

/// <summary>
/// Узел try-catch statement
/// </summary>
public sealed class TryCatchStatementNode : StatementNode
{
    /// <summary>
    /// Тело try блока
    /// </summary>
    public BlockNode TryBlock { get; }
    
    /// <summary>
    /// Catch блоки
    /// </summary>
    public IReadOnlyList<CatchClauseNode> CatchClauses { get; }
    
    /// <summary>
    /// Finally блок (опционально)
    /// </summary>
    public BlockNode? FinallyBlock { get; }
    
    public override AstNodeKind Kind => AstNodeKind.TryCatchStatement;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { TryBlock };
            children.AddRange(CatchClauses);
            if (FinallyBlock != null)
                children.Add(FinallyBlock);
            if (Documentation != null)
                children.Insert(0, Documentation);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public TryCatchStatementNode(
        BlockNode tryBlock,
        IReadOnlyList<CatchClauseNode>? catchClauses = null,
        BlockNode? finallyBlock = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(documentation, metadata)
    {
        TryBlock = tryBlock ?? throw new ArgumentNullException(nameof(tryBlock));
        CatchClauses = catchClauses?.ToImmutableList() ?? ImmutableList<CatchClauseNode>.Empty;
        FinallyBlock = finallyBlock;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitTryCatchStatement(this);
    }
    
    public override AstNode Clone()
    {
        return new TryCatchStatementNode(
            (BlockNode)TryBlock.Clone(),
            CatchClauses.Select(c => (CatchClauseNode)c.Clone()).ToImmutableList(),
            FinallyBlock != null ? (BlockNode)FinallyBlock.Clone() : null,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}

/// <summary>
/// Узел catch clause
/// </summary>
public sealed class CatchClauseNode : AstNode
{
    /// <summary>
    /// Тип исключения (опционально, для catch без типа)
    /// </summary>
    public TypeNode? ExceptionType { get; }
    
    /// <summary>
    /// Имя переменной исключения (опционально)
    /// </summary>
    public IdentifierNode? ExceptionVariable { get; }
    
    /// <summary>
    /// Тело catch блока
    /// </summary>
    public BlockNode Body { get; }
    
    public override AstNodeKind Kind => AstNodeKind.CatchClause;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Body };
            if (ExceptionType != null)
                children.Insert(0, ExceptionType);
            if (ExceptionVariable != null)
                children.Insert(1, ExceptionVariable);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public CatchClauseNode(
        BlockNode body,
        TypeNode? exceptionType = null,
        IdentifierNode? exceptionVariable = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Body = body ?? throw new ArgumentNullException(nameof(body));
        ExceptionType = exceptionType;
        ExceptionVariable = exceptionVariable;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitCatchClause(this);
    }
    
    public override AstNode Clone()
    {
        return new CatchClauseNode(
            (BlockNode)Body.Clone(),
            ExceptionType != null ? (TypeNode)ExceptionType.Clone() : null,
            ExceptionVariable != null ? (IdentifierNode)ExceptionVariable.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
