using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Utilities;
using ValIO.Generators.TypeScript.Zod.Ast.Enums;
using ValIO.Generators.TypeScript.Zod.Ast.Nodes;
using ValIO.Generators.TypeScript.Zod.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Zod.Ast.Renderers;

/// <summary>
/// Рендерер AST для генерации Zod схем
/// </summary>
public class ZodAstRenderer : ZodAstVisitorBase<string>, IZodAstVisitor<string>, IZodAstRenderer
{
    private readonly StringBuilder _sb = new();
    private int _indentLevel = 0;
    
    /// <summary>
    /// Рендерит ZodObjectNode
    /// </summary>
    public string Render(ZodObjectNode node)
    {
        _sb.Clear();
        _indentLevel = 0;
        
        var rendered = VisitZodObject(node);
        return _sb.ToString();
    }
    
    public override string VisitZodSchema(ZodSchemaNode node)
    {
        // Делегируем конкретному типу узла
        return Visit((AstNode)node);
    }
    
    public override string VisitZodObject(ZodObjectNode node)
    {
        _sb.Append("z.object({");
        CodeFormatter.AppendLine(_sb);
        _indentLevel++;
        
        for (int i = 0; i < node.Properties.Count; i++)
        {
            var prop = node.Properties[i];
            CodeFormatter.Append(_sb, _indentLevel);
            
            _sb.Append(prop.Name.Name);
            _sb.Append(": ");
            
            var savedSb = _sb;
            _sb = new StringBuilder();
            var schema = VisitZodProperty(prop);
            _sb = savedSb;
            _sb.Append(schema);
            
            if (i < node.Properties.Count - 1)
            {
                _sb.Append(",");
            }
            CodeFormatter.AppendLine(_sb);
        }
        
        _indentLevel--;
        CodeFormatter.AppendLine(_sb);
        CodeFormatter.Append(_sb, _indentLevel, "})");
        
        return _sb.ToString();
    }
    
    public override string VisitZodProperty(ZodPropertyNode node)
    {
        var savedSb = _sb;
        _sb = new StringBuilder();
        
        // Рендерим схему
        var schema = Visit((AstNode)node.Schema);
        _sb.Append(schema);
        
        // Применяем модификаторы
        if (!node.IsRequired && node.IsOptional)
        {
            _sb.Append(".optional()");
        }
        
        if (node.IsNullable)
        {
            _sb.Append(".nullable()");
        }
        
        var result = _sb.ToString();
        _sb = savedSb;
        return result;
    }
    
    public override string VisitZodArray(ZodArrayNode node)
    {
        var savedSb = _sb;
        _sb = new StringBuilder();
        
        var elementSchema = Visit((AstNode)node.ElementSchema);
        _sb.Append($"z.array({elementSchema})");
        
        var result = _sb.ToString();
        _sb = savedSb;
        return result;
    }
    
    public override string VisitZodUnion(ZodUnionNode node)
    {
        var savedSb = _sb;
        _sb = new StringBuilder();
        
        var variants = node.Variants.Select(v => Visit((AstNode)v));
        _sb.Append($"z.union([{string.Join(", ", variants)}])");
        
        var result = _sb.ToString();
        _sb = savedSb;
        return result;
    }
    
    public override string VisitZodIntersection(ZodIntersectionNode node)
    {
        var savedSb = _sb;
        _sb = new StringBuilder();
        
        var schemas = node.Schemas.Select(s => Visit((AstNode)s));
        _sb.Append($"z.intersection({string.Join(", ", schemas)})");
        
        var result = _sb.ToString();
        _sb = savedSb;
        return result;
    }
    
    public override string VisitZodEnum(ZodEnumNode node)
    {
        var savedSb = _sb;
        _sb = new StringBuilder();
        
        var values = node.Values.Select(v =>
        {
            var value = VisitLiteralExpression(v.Value);
            return value;
        });
        
        _sb.Append($"z.enum([{string.Join(", ", values)}])");
        
        var result = _sb.ToString();
        _sb = savedSb;
        return result;
    }
    
    public override string VisitZodEnumValue(ZodEnumValueNode node)
    {
        var savedSb = _sb;
        _sb = new StringBuilder();
        
        var value = VisitLiteralExpression(node.Value);
        _sb.Append(value);
        
        var result = _sb.ToString();
        _sb = savedSb;
        return result;
    }
    
    public override string VisitZodEffects(ZodEffectsNode node)
    {
        var savedSb = _sb;
        _sb = new StringBuilder();
        
        var innerSchema = Visit((AstNode)node.InnerSchema);
        _sb.Append(innerSchema);
        
        // Применяем эффект в зависимости от типа
        switch (node.EffectKind)
        {
            case ZodEffectKind.Refine:
                if (node.EffectFunction != null)
                {
                    var effectFn = Visit(node.EffectFunction);
                    _sb.Append($".refine({effectFn})");
                }
                break;
            case ZodEffectKind.Transform:
                if (node.EffectFunction != null)
                {
                    var effectFn = Visit(node.EffectFunction);
                    _sb.Append($".transform({effectFn})");
                }
                break;
            case ZodEffectKind.SuperRefine:
                if (node.EffectFunction != null)
                {
                    var effectFn = Visit(node.EffectFunction);
                    _sb.Append($".superRefine({effectFn})");
                }
                break;
        }
        
        var result = _sb.ToString();
        _sb = savedSb;
        return result;
    }
    
    // Вспомогательные методы для рендеринга примитивных типов
    
    private string RenderZodPrimitive(ZodSchemaKind kind)
    {
        return kind switch
        {
            ZodSchemaKind.String => "z.string()",
            ZodSchemaKind.Number => "z.number()",
            ZodSchemaKind.Integer => "z.number().int()",
            ZodSchemaKind.Boolean => "z.boolean()",
            ZodSchemaKind.Null => "z.null()",
            ZodSchemaKind.Any => "z.any()",
            _ => "z.any()"
        };
    }
    
    // Переопределяем базовые методы для корректной работы с выражениями
    
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
    
    public override string VisitIdentifierExpression(IdentifierExpressionNode node)
    {
        return node.Identifier.Name;
    }
    
    // Обработка ZodPrimitiveSchemaNode
    public override string VisitDefault(AstNode node)
    {
        if (node is ZodPrimitiveSchemaNode primitiveNode)
        {
            return RenderZodPrimitive(primitiveNode.SchemaKind);
        }
        
        return base.VisitDefault(node);
    }
}
