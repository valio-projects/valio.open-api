using ValIO.Core.Ast.Visitors;
using ValIO.Generators.FSharp.Ast.Nodes;

namespace ValIO.Generators.FSharp.Ast.Visitors;

/// <summary>
/// Visitor for F#-specific AST nodes.
/// </summary>
public interface IFSharpAstVisitor<TResult> : IAstVisitor<TResult>
{
    TResult VisitFSharpFile(FSharpFileNode node);
    TResult VisitFSharpRecord(FSharpRecordNode node);
    TResult VisitFSharpDU(FSharpDuNode node);
    TResult VisitFSharpFunction(FSharpFunctionNode node);

    /// <summary>Visits a FSharpTaskExpressionNode syntax node.</summary>
    /// <param name="node">The F# syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitFSharpTaskExpression(FSharpTaskExpressionNode node) => DefaultVisit(node);

    /// <summary>Visits a FSharpAwaitBindingNode syntax node.</summary>
    /// <param name="node">The F# syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitFSharpAwaitBinding(FSharpAwaitBindingNode node) => DefaultVisit(node);

    /// <summary>Visits a FSharpMatchOptionNode syntax node.</summary>
    /// <param name="node">The F# syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitFSharpMatchOption(FSharpMatchOptionNode node) => DefaultVisit(node);

    /// <summary>Visits a FSharpApplicationNode syntax node.</summary>
    /// <param name="node">The F# syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitFSharpApplication(FSharpApplicationNode node) => DefaultVisit(node);
}
