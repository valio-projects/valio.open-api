using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Ast.Nodes.UE;

namespace ValIO.Generators.Cpp.Ast.Visitors;

/// <summary>
/// Интерфейс посетителя для C++/Unreal Engine специфичных AST узлов.
/// Наследуется от IAstVisitor для поддержки общих узлов.
/// </summary>
public interface ICppAstVisitor<TResult> : IAstVisitor<TResult>
{
    // Файловые узлы C++
    TResult VisitCppHeaderFile(CppHeaderFileNode node);
    TResult VisitCppSourceFile(CppSourceFileNode node);
    TResult VisitCppIncludeDirective(CppIncludeDirectiveNode node);
    TResult VisitCppForwardDeclaration(CppForwardDeclarationNode node);
    TResult VisitCppUsingAlias(CppUsingAliasNode node);

    // C++ функции и методы
    TResult VisitCppFreeFunction(CppFreeFunctionNode node);
    TResult VisitCppParameter(CppParameterNode node);
    TResult VisitCppMethodImplementation(CppMethodImplementationNode node);
    TResult VisitCppPathBuilderImplementation(CppPathBuilderImplementationNode node);
    TResult VisitCppParseFunction(CppParseFunctionNode node);
    TResult VisitCppNativeApiFunction(CppNativeApiFunctionNode node);
    TResult VisitCppAsyncFactoryImplementation(CppAsyncFactoryImplementationNode node);
    TResult VisitCppAsyncCompleteImplementation(CppAsyncCompleteImplementationNode node);
    TResult VisitCppAsyncActivateImplementation(CppAsyncActivateImplementationNode node);
    TResult VisitCppLambdaExpression(CppLambdaExpressionNode node);

    /// <summary>Visits a const or reference-qualified C++ type.</summary>
    TResult VisitCppQualifiedType(CppQualifiedTypeNode node);

    /// <summary>Visits a C++ type referenced in an expression.</summary>
    TResult VisitCppTypeExpression(CppTypeExpressionNode node);

    /// <summary>Visits a braced C++ initializer list.</summary>
    TResult VisitCppListInitialization(CppListInitializationNode node);

    // Unreal Engine специфичные узлы
    TResult VisitUStruct(UStructDeclarationNode node);
    TResult VisitUPropertyField(UPropertyFieldNode node);
    TResult VisitUClass(UClassDeclarationNode node);
    TResult VisitUFunction(UFunctionDeclarationNode node);
    TResult VisitUDelegate(UDelegateDeclarationNode node);
    TResult VisitUEnum(UEnumDeclarationNode node);
}
