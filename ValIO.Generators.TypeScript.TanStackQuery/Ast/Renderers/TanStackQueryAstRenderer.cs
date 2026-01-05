using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Core.Utilities;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Nodes;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Visitors;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Renderers;

/// <summary>
/// Рендерер AST для генерации TanStack Query кода
/// </summary>
public class TanStackQueryAstRenderer : AstVisitorBase<string>, ITanStackQueryAstVisitor<string>, ITanStackQueryAstRenderer
{
    private readonly StringBuilder _sb = new();
    private int _indentLevel = 0;
    
    /// <summary>
    /// Рендерит UseQueryNode
    /// </summary>
    public string Render(UseQueryNode node)
    {
        _sb.Clear();
        _indentLevel = 0;
        
        var rendered = VisitUseQuery(node);
        return _sb.ToString();
    }
    
    /// <summary>
    /// Рендерит UseMutationNode
    /// </summary>
    public string Render(UseMutationNode node)
    {
        _sb.Clear();
        _indentLevel = 0;
        
        var rendered = VisitUseMutation(node);
        return _sb.ToString();
    }
    
    public override string VisitQueryKey(QueryKeyNode node)
    {
        _sb.Append("[");
        for (int i = 0; i < node.Parts.Count; i++)
        {
            if (i > 0)
                _sb.Append(", ");
            
            var savedSb = _sb;
            _sb = new StringBuilder();
            var part = Visit(node.Parts[i]);
            _sb = savedSb;
            _sb.Append(part);
        }
        _sb.Append("]");
        return _sb.ToString();
    }
    
    public override string VisitUseQuery(UseQueryNode node)
    {
        _sb.Append("useQuery(");
        
        // Query options
        _sb.Append("{");
        CodeFormatter.AppendLine(_sb);
        _indentLevel++;
        
        // queryKey
        CodeFormatter.Append(_sb, _indentLevel, "queryKey: ");
        var savedSb = _sb;
        _sb = new StringBuilder();
        var queryKey = VisitQueryKey(node.QueryKey);
        _sb = savedSb;
        _sb.Append(queryKey);
        _sb.Append(",");
        CodeFormatter.AppendLine(_sb);
        
        // queryFn
        CodeFormatter.Append(_sb, _indentLevel, "queryFn: ");
        _sb = new StringBuilder();
        var queryFn = Visit(node.QueryFn);
        _sb = savedSb;
        _sb.Append(queryFn);
        _sb.Append(",");
        CodeFormatter.AppendLine(_sb);
        
        // Опции (если есть)
        if (node.Options != null)
        {
            var options = VisitQueryOptions(node.Options);
            // TODO: Добавить опции в объект
        }
        
        _indentLevel--;
        CodeFormatter.AppendLine(_sb);
        CodeFormatter.Append(_sb, _indentLevel, "}");
        
        _sb.Append(")");
        return _sb.ToString();
    }
    
    public override string VisitUseMutation(UseMutationNode node)
    {
        _sb.Append("useMutation(");
        
        // Mutation options
        _sb.Append("{");
        CodeFormatter.AppendLine(_sb);
        _indentLevel++;
        
        // mutationFn
        CodeFormatter.Append(_sb, _indentLevel, "mutationFn: ");
        var savedSb = _sb;
        _sb = new StringBuilder();
        var mutationFn = Visit(node.MutationFn);
        _sb = savedSb;
        _sb.Append(mutationFn);
        
        // Опции (если есть)
        if (node.Options != null)
        {
            _sb.Append(",");
            CodeFormatter.AppendLine(_sb);
            var options = VisitMutationOptions(node.Options);
            // TODO: Добавить опции в объект
        }
        
        _indentLevel--;
        CodeFormatter.AppendLine(_sb);
        CodeFormatter.Append(_sb, _indentLevel, "}");
        
        _sb.Append(")");
        return _sb.ToString();
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
        var savedSb = _sb;
        _sb = new StringBuilder();
        var target = Visit(node.Target);
        _sb.Append(target);
        _sb.Append("(");
        
        for (int i = 0; i < node.Arguments.Count; i++)
        {
            if (i > 0)
                _sb.Append(", ");
            
            var arg = Visit(node.Arguments[i]);
            _sb.Append(arg);
        }
        
        _sb.Append(")");
        var result = _sb.ToString();
        _sb = savedSb;
        return result;
    }
    
    public override string VisitIdentifierExpression(IdentifierExpressionNode node)
    {
        return node.Identifier.Name;
    }
    
    public override string VisitMemberAccessExpression(MemberAccessExpressionNode node)
    {
        var savedSb = _sb;
        _sb = new StringBuilder();
        var obj = Visit(node.Object);
        _sb.Append(obj);
        _sb.Append(".");
        _sb.Append(node.Member.Name);
        var result = _sb.ToString();
        _sb = savedSb;
        return result;
    }
}
