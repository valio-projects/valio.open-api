using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Utilities;
using ValIO.Generators.TypeScript.Axios.Ast.Nodes;
using ValIO.Generators.TypeScript.Axios.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Axios.Ast.Renderers;

/// <summary>
/// Рендерер AST для генерации Axios кода
/// </summary>
public class AxiosAstRenderer : AxiosAstVisitorBase<string>, IAxiosAstVisitor<string>, IAxiosAstRenderer
{
    private readonly StringBuilder _sb = new();
    private int _indentLevel = 0;
    
    /// <summary>
    /// Рендерит AxiosCallExpressionNode
    /// </summary>
    public string Render(AxiosCallExpressionNode node)
    {
        _sb.Clear();
        _indentLevel = 0;
        
        var rendered = VisitAxiosCall(node);
        return _sb.ToString();
    }
    
    public override string VisitAxiosCall(AxiosCallExpressionNode node)
    {
        _sb.Clear();
        
        // Рендерим instance
        var instance = Visit(node.Instance);
        _sb.Append(instance);
        
        // Рендерим вызов request с конфигурацией
        _sb.Append(".request(");
        var config = VisitAxiosConfig(node.Config);
        _sb.Append(config);
        _sb.Append(")");
        
        return _sb.ToString();
    }
    
    public override string VisitAxiosConfig(AxiosConfigNode node)
    {
        var configSb = new StringBuilder();
        configSb.Append("{");
        CodeFormatter.AppendLine(configSb);
        var configIndent = _indentLevel + 1;
        
        // method
        CodeFormatter.Append(configSb, configIndent, "method: ");
        configSb.Append($"'{node.Method.Method.ToString().ToUpperInvariant()}'");
        configSb.Append(",");
        CodeFormatter.AppendLine(configSb);
        
        // url
        CodeFormatter.Append(configSb, configIndent, "url: ");
        var urlSb = new StringBuilder();
        var savedSb = _sb;
        _sb = urlSb;
        var url = Visit(node.Url);
        _sb = savedSb;
        configSb.Append(url);
        configSb.Append(",");
        CodeFormatter.AppendLine(configSb);
        
        // params (query parameters)
        if (node.QueryParameters.Count > 0)
        {
            CodeFormatter.Append(configSb, configIndent, "params: ");
            var paramsObject = BuildQueryParamsObject(node.QueryParameters);
            configSb.Append(paramsObject);
            configSb.Append(",");
            CodeFormatter.AppendLine(configSb);
        }
        
        // headers
        if (node.Headers.Count > 0)
        {
            CodeFormatter.Append(configSb, configIndent, "headers: ");
            var headersObject = BuildHeadersObject(node.Headers);
            configSb.Append(headersObject);
            configSb.Append(",");
            CodeFormatter.AppendLine(configSb);
        }
        
        // data (body)
        if (node.Body != null)
        {
            CodeFormatter.Append(configSb, configIndent, "data: ");
            // TODO: Использовать параметр из функции
            configSb.Append("data");
            configSb.Append(",");
            CodeFormatter.AppendLine(configSb);
        }
        
        // Удаляем последнюю запятую
        if (configSb.Length > 0 && configSb[configSb.Length - 1] == ',')
        {
            configSb.Length--;
        }
        
        CodeFormatter.AppendLine(configSb);
        CodeFormatter.Append(configSb, _indentLevel, "}");
        
        return configSb.ToString();
    }
    
    private string BuildQueryParamsObject(IReadOnlyList<ValIO.Core.Ast.Nodes.Http.QueryParameterNode> queryParameters)
    {
        var sb = new StringBuilder();
        sb.Append("{");
        
        var first = true;
        foreach (var qp in queryParameters)
        {
            if (!first)
                sb.Append(", ");
            first = false;
            
            sb.Append(qp.Name.Name);
            sb.Append(": ");
            sb.Append(qp.Name.Name);
        }
        
        sb.Append("}");
        return sb.ToString();
    }
    
    private string BuildHeadersObject(IReadOnlyList<ValIO.Core.Ast.Nodes.Http.HeaderParameterNode> headers)
    {
        var sb = new StringBuilder();
        sb.Append("{");
        
        var first = true;
        foreach (var header in headers)
        {
            if (!first)
                sb.Append(", ");
            first = false;
            
            sb.Append($"'{header.Name.Name}': ");
            sb.Append(header.Name.Name);
        }
        
        sb.Append("}");
        return sb.ToString();
    }
    
    // Переопределяем базовые методы для корректной работы с выражениями
    
    public override string VisitTemplateLiteralExpression(TemplateLiteralExpressionNode node)
    {
        var result = new StringBuilder();
        result.Append("`");
        foreach (var part in node.Parts)
        {
            if (part.IsExpression)
            {
                result.Append("${");
                if (part.Expression != null)
                {
                    var savedSb = _sb;
                    _sb = new StringBuilder();
                    var expr = Visit(part.Expression);
                    _sb = savedSb;
                    result.Append(expr);
                }
                result.Append("}");
            }
            else
            {
                result.Append(part.Text ?? "");
            }
        }
        result.Append("`");
        return result.ToString();
    }
    
    public override string VisitIdentifierExpression(IdentifierExpressionNode node)
    {
        return node.Identifier.Name;
    }
    
    public override string VisitLiteralExpression(LiteralExpressionNode node)
    {
        return node.Kind switch
        {
            LiteralKind.String => $"\"{node.Value}\"",
            LiteralKind.Number => node.Value?.ToString() ?? "0",
            LiteralKind.Boolean => node.Value?.ToString()?.ToLowerInvariant() ?? "false",
            LiteralKind.Null => "null",
            _ => node.Value?.ToString() ?? "null"
        };
    }
    
    public override string VisitObjectLiteralExpression(ObjectLiteralExpressionNode node)
    {
        var result = new StringBuilder();
        result.Append("{");
        CodeFormatter.AppendLine(result);
        var objIndent = _indentLevel + 1;
        
        for (int i = 0; i < node.Properties.Count; i++)
        {
            var prop = node.Properties[i];
            CodeFormatter.Append(result, objIndent);
            
            var savedSb = _sb;
            _sb = new StringBuilder();
            var propName = Visit(prop.Name);
            _sb = savedSb;
            result.Append(propName);
            result.Append(": ");
            
            _sb = new StringBuilder();
            var propValue = Visit(prop.Value);
            _sb = savedSb;
            result.Append(propValue);
            
            if (i < node.Properties.Count - 1)
            {
                result.Append(",");
            }
            CodeFormatter.AppendLine(result);
        }
        
        CodeFormatter.AppendLine(result);
        CodeFormatter.Append(result, _indentLevel, "}");
        
        return result.ToString();
    }
    
    public override string VisitPropertyAssignment(PropertyAssignmentNode node)
    {
        var savedSb = _sb;
        _sb = new StringBuilder();
        var name = Visit(node.Name);
        _sb.Append(name);
        _sb.Append(": ");
        var value = Visit(node.Value);
        _sb.Append(value);
        var result = _sb.ToString();
        _sb = savedSb;
        return result;
    }
    
    // Остальные методы используют базовую реализацию
}
