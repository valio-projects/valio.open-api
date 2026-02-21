using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Ast.Nodes.UE;

namespace ValIO.Generators.Cpp.Ast.Visitors;

/// <summary>
/// Базовый абстрактный класс для посетителя C++/UE AST узлов.
/// Наследуется от BaseAstVisitor и реализует ICppAstVisitor.
/// </summary>
public abstract class CppAstVisitorBase<TResult> : BaseAstVisitor<TResult>, ICppAstVisitor<TResult>
{
    public override TResult VisitBlankLineStatement(BlankLineStatementNode node) => VisitDefault(node);

    /// <summary>
    /// Переопределяем Visit для корректной диспетчеризации через Accept
    /// </summary>
    public override TResult Visit(AstNode node)
    {
        return node.Accept(this);
    }

    // C++ файловые узлы
    public abstract TResult VisitCppHeaderFile(CppHeaderFileNode node);
    public abstract TResult VisitCppSourceFile(CppSourceFileNode node);
    public abstract TResult VisitCppIncludeDirective(CppIncludeDirectiveNode node);
    public abstract TResult VisitCppForwardDeclaration(CppForwardDeclarationNode node);
    public abstract TResult VisitCppUsingAlias(CppUsingAliasNode node);

    // C++ функции и методы
    public abstract TResult VisitCppFreeFunction(CppFreeFunctionNode node);
    public abstract TResult VisitCppParameter(CppParameterNode node);
    public abstract TResult VisitCppMethodImplementation(CppMethodImplementationNode node);
    public abstract TResult VisitCppPathBuilderImplementation(CppPathBuilderImplementationNode node);
    public abstract TResult VisitCppParseFunction(CppParseFunctionNode node);
    public abstract TResult VisitCppNativeApiFunction(CppNativeApiFunctionNode node);
    public abstract TResult VisitCppAsyncFactoryImplementation(CppAsyncFactoryImplementationNode node);
    public abstract TResult VisitCppAsyncCompleteImplementation(CppAsyncCompleteImplementationNode node);
    public abstract TResult VisitCppAsyncActivateImplementation(CppAsyncActivateImplementationNode node);
    public abstract TResult VisitCppLambdaExpression(CppLambdaExpressionNode node);

    // Unreal Engine специфичные узлы
    public abstract TResult VisitUStruct(UStructDeclarationNode node);
    public abstract TResult VisitUPropertyField(UPropertyFieldNode node);
    public abstract TResult VisitUClass(UClassDeclarationNode node);
    public abstract TResult VisitUFunction(UFunctionDeclarationNode node);
    public abstract TResult VisitUDelegate(UDelegateDeclarationNode node);
    public abstract TResult VisitUEnum(UEnumDeclarationNode node);

    // Enum и Interface (общие для всех языков, определены в IAstVisitor)
    public abstract TResult VisitEnumDeclaration(EnumDeclarationNode node);
    public abstract TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node);

    // HTTP специфичные узлы (из IAstVisitor)
    public abstract TResult VisitHttpMethod(HttpMethodNode node);
    public abstract TResult VisitRoute(RouteNode node);
    public abstract TResult VisitPathParameter(PathParameterNode node);
    public abstract TResult VisitQueryParameter(QueryParameterNode node);
    public abstract TResult VisitHeaderParameter(HeaderParameterNode node);
    public abstract TResult VisitRequestBody(RequestBodyNode node);
    public abstract TResult VisitResponseType(ResponseTypeNode node);
}
