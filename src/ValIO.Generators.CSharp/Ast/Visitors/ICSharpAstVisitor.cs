using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.CSharp.Ast.Nodes;

namespace ValIO.Generators.CSharp.Ast.Visitors;

/// <summary>
/// Интерфейс посетителя для C# специфичных AST узлов
/// Наследуется от IAstVisitor для поддержки общих узлов (Enum, Interface)
/// </summary>
public interface ICSharpAstVisitor<TResult> : IAstVisitor<TResult>
{
    // C# специфичные узлы
    TResult VisitCompilationUnit(CompilationUnitNode node);
    TResult VisitUsingDirective(UsingDirectiveNode node);
    TResult VisitRecordDeclaration(RecordDeclarationNode node);
    TResult VisitConstructorDeclaration(ConstructorDeclarationNode node);

    // Namespace/Class/Struct/Field — универсальные узлы Core
    // (ValIO.Core.Ast.Nodes), их Visit-методы наследуются из IAstVisitor.
}
