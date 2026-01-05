using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;

namespace ValIO.Core.Ast.Visitors;

/// <summary>
/// Интерфейс посетителя AST (Visitor Pattern)
/// Объединяет все интерфейсы визиторов для поддержки всех типов узлов
/// Используется узлами AST в методе Accept для диспетчеризации
/// </summary>
/// <remarks>
/// Наследует все базовые методы из IBaseAstVisitor.
/// Добавляет только методы для диспетчеризации и общие специфичные узлы.
/// Языково-специфичные узлы (C#, TypeScript) должны быть в соответствующих интерфейсах (ICSharpAstVisitor, ITypeScriptAstVisitor).
/// </remarks>
public interface IAstVisitor<TResult> : IBaseAstVisitor<TResult>
{
    /// <summary>
    /// Посещает объявление (диспетчеризация по типу)
    /// </summary>
    TResult VisitDeclaration(DeclarationNode node);
    
    /// <summary>
    /// Посещает тип (диспетчеризация по типу)
    /// </summary>
    TResult VisitType(TypeNode node);
    
    /// <summary>
    /// Посещает выражение (диспетчеризация по типу)
    /// </summary>
    TResult VisitExpression(ExpressionNode node);
    
    /// <summary>
    /// Посещает statement (диспетчеризация по типу)
    /// </summary>
    TResult VisitStatement(StatementNode node);
    
    // Enum (общий для C# и TypeScript)
    TResult VisitEnumDeclaration(EnumDeclarationNode node);
    
    // Interface (общий для C# и TypeScript)
    TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node);
    
    // HTTP специфичные узлы
    TResult VisitHttpMethod(HttpMethodNode node);
    TResult VisitRoute(RouteNode node);
    TResult VisitPathParameter(PathParameterNode node);
    TResult VisitQueryParameter(QueryParameterNode node);
    TResult VisitHeaderParameter(HeaderParameterNode node);
    TResult VisitRequestBody(RequestBodyNode node);
    TResult VisitResponseType(ResponseTypeNode node);
}
