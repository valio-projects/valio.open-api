using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Php.Ast.Nodes;

namespace ValIO.Generators.Php.Ast.Visitors;

/// <summary>
/// Visitor для PHP-специфичных AST узлов.
/// Наследуется от IAstVisitor чтобы получить базовые узлы (enum, interface, типы и т.д.).
/// </summary>
public interface IPhpAstVisitor<TResult> : IAstVisitor<TResult>
{
    TResult VisitPhpSourceFile(PhpSourceFileNode node);
    TResult VisitPhpClassDeclaration(PhpClassDeclarationNode node);
    TResult VisitPhpConstructor(PhpConstructorDeclarationNode node);
    TResult VisitPhpUseStatement(PhpUseStatementNode node);
}
