using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Utilities;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Nodes;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Visitors;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Renderers;

/// <summary>
/// Рендерер AST для генерации TanStack Query кода
/// </summary>
public class TanStackQueryAstRenderer : TanStackQueryAstVisitorBase<string>, ITanStackQueryAstVisitor<string>, ITanStackQueryAstRenderer
{
    /// <summary>
    /// Рендерит UseQueryNode
    /// </summary>
    public string Render(UseQueryNode node)
    {
        return VisitUseQuery(node);
    }
    
    /// <summary>
    /// Рендерит UseMutationNode
    /// </summary>
    public string Render(UseMutationNode node)
    {
        return VisitUseMutation(node);
    }
    
    public override string VisitQueryKey(QueryKeyNode node)
    {
        var sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < node.Parts.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            
            var part = Visit(node.Parts[i]);
            sb.Append(part);
        }
        sb.Append("]");
        return sb.ToString();
    }
    
    public override string VisitUseQuery(UseQueryNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        sb.Append("useQuery(");
        
        // Query options
        sb.Append("{");
        CodeFormatter.AppendLine(sb, indentLevel, "");
        indentLevel++;
        
        // queryKey
        CodeFormatter.Append(sb, indentLevel, "queryKey: ");
        var queryKey = VisitQueryKey(node.QueryKey);
        sb.Append(queryKey);
        sb.Append(",");
        CodeFormatter.AppendLine(sb, indentLevel, "");
        
        // queryFn
        CodeFormatter.Append(sb, indentLevel, "queryFn: ");
        var queryFn = Visit(node.QueryFn);
        sb.Append(queryFn);
        sb.Append(",");
        CodeFormatter.AppendLine(sb, indentLevel, "");
        
        // Опции (если есть)
        if (node.Options != null)
        {
            var options = VisitQueryOptions(node.Options);
            // TODO: Добавить опции в объект
        }
        
        indentLevel--;
        CodeFormatter.AppendLine(sb, indentLevel, "");
        CodeFormatter.Append(sb, indentLevel, "}");
        
        sb.Append(")");
        return sb.ToString();
    }
    
    public override string VisitUseMutation(UseMutationNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        sb.Append("useMutation(");
        
        // Mutation options
        sb.Append("{");
        CodeFormatter.AppendLine(sb, indentLevel, "");
        indentLevel++;
        
        // mutationFn
        CodeFormatter.Append(sb, indentLevel, "mutationFn: ");
        var mutationFn = Visit(node.MutationFn);
        sb.Append(mutationFn);
        
        // Опции (если есть)
        if (node.Options != null)
        {
            sb.Append(",");
            CodeFormatter.AppendLine(sb, indentLevel, "");
            var options = VisitMutationOptions(node.Options);
            // TODO: Добавить опции в объект
        }
        
        indentLevel--;
        CodeFormatter.AppendLine(sb, indentLevel, "");
        CodeFormatter.Append(sb, indentLevel, "}");
        
        sb.Append(")");
        return sb.ToString();
    }
    
    public override string VisitQueryOptions(QueryOptionsNode node)
    {
        var result = new StringBuilder();
        // TODO: Реализовать рендеринг опций
        return result.ToString();
    }
    
    public override string VisitMutationOptions(MutationOptionsNode node)
    {
        var result = new StringBuilder();
        // TODO: Реализовать рендеринг опций
        return result.ToString();
    }
    
    // Переопределяем базовые методы для корректной работы с выражениями
    
    public override string VisitCallExpression(CallExpressionNode node)
    {
        var sb = new StringBuilder();
        var target = Visit(node.Target);
        sb.Append(target);
        sb.Append("(");
        
        for (int i = 0; i < node.Arguments.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            
            var arg = Visit(node.Arguments[i]);
            sb.Append(arg);
        }
        
        sb.Append(")");
        return sb.ToString();
    }
    
    public override string VisitIdentifierExpression(IdentifierExpressionNode node)
    {
        return node.Identifier.Name;
    }
    
    public override string VisitMemberAccessExpression(MemberAccessExpressionNode node)
    {
        var obj = Visit(node.Object);
        return $"{obj}.{node.Member.Name}";
    }
    
    // Реализация всех абстрактных методов BaseAstVisitor
    
    public override string VisitLiteralExpression(LiteralExpressionNode node)
    {
        return RenderLiteral(node);
    }
    
    /// <summary>
    /// Рендерит литеральное выражение
    /// </summary>
    private string RenderLiteral(LiteralExpressionNode node)
    {
        return node.LiteralKind switch
        {
            LiteralKind.String => $"\"{EscapeString(node.Value?.ToString() ?? "")}\"",
            LiteralKind.Number => node.Value?.ToString() ?? "0",
            LiteralKind.Boolean => node.Value?.ToString()?.ToLowerInvariant() ?? "false",
            LiteralKind.Null => "null",
            _ => node.Value?.ToString() ?? "null"
        };
    }
    
    /// <summary>
    /// Экранирует строку для TypeScript
    /// </summary>
    private string EscapeString(string value)
    {
        return ValIO.Core.Utilities.CodeEscapeHelper.EscapeTypeScriptString(value);
    }
    public override string VisitSimpleType(SimpleTypeNode node) => VisitDefault(node);
    public override string VisitTypeAliasDeclaration(TypeAliasDeclarationNode node) => VisitDefault(node);
    public override string VisitExpressionStatement(ExpressionStatementNode node) => VisitDefault(node);
    public override string VisitTypeDeclaration(TypeDeclarationNode node) => VisitDefault(node);
    public override string VisitNullableType(NullableTypeNode node) => VisitDefault(node);
    public override string VisitThrowStatement(ThrowStatementNode node) => VisitDefault(node);
    public override string VisitVariableDeclaration(VariableDeclarationNode node) => VisitDefault(node);
    public override string VisitArrayLiteralExpression(ArrayLiteralExpressionNode node) => VisitDefault(node);
    public override string VisitIfStatement(IfStatementNode node) => VisitDefault(node);
    public override string VisitDictionaryType(DictionaryTypeNode node) => VisitDefault(node);
    public override string VisitComment(CommentNode node) => VisitDefault(node);
    public override string VisitOptionalType(OptionalTypeNode node) => VisitDefault(node);
    public override string VisitEnumMemberDeclaration(EnumMemberDeclarationNode node) => VisitDefault(node);
    public override string VisitReturnStatement(ReturnStatementNode node) => VisitDefault(node);
    public override string VisitUnionType(UnionTypeNode node) => VisitDefault(node);
    public override string VisitPropertySignature(PropertySignatureNode node) => VisitDefault(node);
    public override string VisitGenericType(GenericTypeNode node) => VisitDefault(node);
    public override string VisitImport(ImportNode node) => VisitDefault(node);
    public override string VisitFunctionDeclaration(FunctionDeclarationNode node) => VisitDefault(node);
    public override string VisitAttribute(AttributeNode node) => VisitDefault(node);
    public override string VisitIdentifier(IdentifierNode node) => VisitDefault(node);
    public override string VisitConditionalExpression(ConditionalExpressionNode node) => VisitDefault(node);
    public override string VisitExport(ExportNode node) => VisitDefault(node);
    public override string VisitIntersectionType(IntersectionTypeNode node) => VisitDefault(node);
    public override string VisitUsingStatement(UsingStatementNode node) => VisitDefault(node);
    public override string VisitCastExpression(CastExpressionNode node) => VisitDefault(node);
    public override string VisitForEachStatement(ForEachStatementNode node) => VisitDefault(node);
    public override string VisitAwaitExpression(AwaitExpressionNode node) => VisitDefault(node);
    public override string VisitPropertyAssignment(PropertyAssignmentNode node) => VisitDefault(node);
    public override string VisitPropertyDeclaration(PropertyDeclarationNode node) => VisitDefault(node);
    public override string VisitArrayType(ArrayTypeNode node) => VisitDefault(node);
    public override string VisitCatchClause(CatchClauseNode node) => VisitDefault(node);
    public override string VisitParameterDeclaration(ParameterDeclarationNode node) => VisitDefault(node);
    public override string VisitObjectLiteralExpression(ObjectLiteralExpressionNode node) => VisitDefault(node);
    public override string VisitVariableDeclarationStatement(VariableDeclarationStatementNode node) => VisitDefault(node);
    public override string VisitMethodDeclaration(MethodDeclarationNode node) => VisitDefault(node);
    public override string VisitBlock(BlockNode node) => VisitDefault(node);
    public override string VisitBinaryExpression(BinaryExpressionNode node) => VisitDefault(node);
    public override string VisitTryCatchStatement(TryCatchStatementNode node) => VisitDefault(node);
    public override string VisitTemplateLiteralExpression(TemplateLiteralExpressionNode node) => VisitDefault(node);
    public override string VisitGenericParameter(GenericParameterNode node) => VisitDefault(node);
    public override string VisitSourceFile(SourceFileNode node) => VisitDefault(node);
}
