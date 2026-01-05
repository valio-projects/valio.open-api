using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.Ast.Visitors;

/// <summary>
/// Интерфейс посетителя для TypeScript специфичных AST узлов
/// </summary>
public interface ITypeScriptAstVisitor<TResult> : IBaseAstVisitor<TResult>
{
    // Enum (общий для C# и TypeScript)
    TResult VisitEnumDeclaration(EnumDeclarationNode node);
    
    // Interface (общий для C# и TypeScript)
    TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node);
    
    // TypeScript специфичные узлы
    TResult VisitSourceFile(SourceFileNode node);
    TResult VisitTypeAliasDeclaration(TypeAliasDeclarationNode node);
    TResult VisitFunctionDeclaration(FunctionDeclarationNode node);
    TResult VisitVariableDeclaration(VariableDeclarationNode node);
    TResult VisitObjectLiteralExpression(ObjectLiteralExpressionNode node);
    TResult VisitPropertyAssignment(PropertyAssignmentNode node);
    TResult VisitTemplateLiteralExpression(TemplateLiteralExpressionNode node);
    TResult VisitGenericParameter(GenericParameterNode node);
    TResult VisitPropertySignature(PropertySignatureNode node);
}
