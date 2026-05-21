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
}
