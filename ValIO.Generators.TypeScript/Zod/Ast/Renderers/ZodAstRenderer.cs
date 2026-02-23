using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Utilities;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Zod.Ast.Enums;
using ValIO.Generators.TypeScript.Zod.Ast.Nodes;
using ValIO.Generators.TypeScript.Zod.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Zod.Ast.Renderers;

/// <summary>
/// Рендерер AST для генерации Zod схем
/// </summary>
public class ZodAstRenderer : ZodAstVisitorBase<string>, IZodAstVisitor<string>, IZodAstRenderer
{
    /// <summary>
    /// Рендерит ZodObjectNode
    /// </summary>
    public string Render(ZodObjectNode node)
    {
        return VisitZodObject(node);
    }

    /// <summary>
    /// Рендерит ZodSchemaNode (object, union, intersection)
    /// </summary>
    public string RenderSchema(ZodSchemaNode node)
    {
        return VisitZodSchema(node);
    }
    
    public override string VisitZodSchema(ZodSchemaNode node)
    {
        return node switch
        {
            ZodPrimitiveSchemaNode primitive => VisitZodPrimitiveSchema(primitive),
            ZodExpressionSchemaNode exprNode => VisitExpression(exprNode.Expression),
            ZodObjectNode obj => VisitZodObject(obj),
            ZodArrayNode array => VisitZodArray(array),
            ZodUnionNode union => VisitZodUnion(union),
            ZodIntersectionNode intersection => VisitZodIntersection(intersection),
            ZodEnumNode enumNode => VisitZodEnum(enumNode),
            ZodEffectsNode effects => VisitZodEffects(effects),
            _ => throw new NotSupportedException($"Unsupported ZodSchemaNode type: {node.GetType().Name}")
        };
    }

    /// <summary>
    /// Рендерит ExpressionNode (цепочек вызовов Zod)
    /// </summary>
    private string VisitExpression(AstNode node)
    {
        return node switch
        {
            CallExpressionNode call => VisitCallExpression(call),
            MemberAccessExpressionNode ma => VisitMemberAccessExpression(ma),
            IdentifierExpressionNode id => VisitIdentifierExpression(id),
            LiteralExpressionNode lit => VisitLiteralExpression(lit),
            ObjectLiteralExpressionNode obj => VisitObjectLiteralExpression(obj),
            _ => throw new NotSupportedException($"Unsupported expression node: {node.GetType().Name}")
        };
    }

    private string VisitObjectLiteralExpression(ObjectLiteralExpressionNode node)
    {
        var properties = string.Join(", ", node.Properties.Select(VisitPropertyAssignment));
        var result = $"{{ {properties} }}";
        return node.AsConstAssertion ? $"{result} as const" : result;
    }

    private string VisitPropertyAssignment(PropertyAssignmentNode node)
    {
        var value = node.Value switch
        {
            LiteralExpressionNode lit => VisitLiteralExpression(lit),
            ObjectLiteralExpressionNode obj => VisitObjectLiteralExpression(obj),
            IdentifierExpressionNode id => VisitIdentifierExpression(id),
            MemberAccessExpressionNode ma => VisitMemberAccessExpression(ma),
            CallExpressionNode call => VisitCallExpression(call),
            _ => VisitExpression((ExpressionNode)node.Value)
        };
        return $"{node.Name.Name}: {value}";
    }
    
    /// <summary>
    /// Рендерит примитивную Zod схему (z.string(), z.number(), и т.д.)
    /// </summary>
    private string VisitZodPrimitiveSchema(ZodPrimitiveSchemaNode node)
    {
        if (!string.IsNullOrEmpty(node.SchemaReference))
        {
            if (node.IsLazy)
                return $"z.lazy(() => {node.SchemaReference})";
            return node.SchemaReference;
        }

        return node.SchemaKind switch
        {
            ZodSchemaKind.String => "z.string()",
            ZodSchemaKind.Number => "z.coerce.number()",
            ZodSchemaKind.Integer => "z.coerce.number().int()",
            ZodSchemaKind.BigInt => "z.coerce.bigint()",
            ZodSchemaKind.Boolean => "z.boolean()",
            ZodSchemaKind.Null => "z.null()",
            ZodSchemaKind.Any => "z.any()",
            _ => throw new NotSupportedException($"Unsupported primitive schema kind: {node.SchemaKind}")
        };
    }
    
    public override string VisitZodObject(ZodObjectNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        sb.Append("z.object({");
        CodeFormatter.AppendLine(sb, indentLevel, "");
        indentLevel++;
        
        for (int i = 0; i < node.Properties.Count; i++)
        {
            var prop = node.Properties[i];
            CodeFormatter.Append(sb, indentLevel);
            
            sb.Append(prop.Name.Name);
            sb.Append(": ");
            
            var schema = VisitZodProperty(prop);
            sb.Append(schema);
            
            if (i < node.Properties.Count - 1)
            {
                sb.Append(",");
            }
            CodeFormatter.AppendLine(sb, indentLevel, "");
        }
        
        indentLevel--;
        CodeFormatter.AppendLine(sb, indentLevel, "");
        CodeFormatter.Append(sb, indentLevel, "})");
        
        return sb.ToString();
    }
    
    public override string VisitZodProperty(ZodPropertyNode node)
    {
        var sb = new StringBuilder();
        
        // Рендерим схему
        var schema = Visit((AstNode)node.Schema);
        sb.Append(schema);
        
        // Применяем модификаторы: nullish для optional (API может вернуть null или не вернуть свойство)
        if (!node.IsRequired && node.IsOptional)
        {
            sb.Append(".nullish()");
            return sb.ToString();
        }
        
        if (node.IsNullable)
        {
            sb.Append(".nullable()");
        }
        
        return sb.ToString();
    }
    
    public override string VisitZodArray(ZodArrayNode node)
    {
        var elementSchema = Visit((AstNode)node.ElementSchema);
        return $"z.array({elementSchema})";
    }
    
    public override string VisitZodUnion(ZodUnionNode node)
    {
        var variants = node.Variants.Select(v => Visit((AstNode)v));
        return $"z.union([{string.Join(", ", variants)}])";
    }
    
    public override string VisitZodIntersection(ZodIntersectionNode node)
    {
        var schemas = node.Schemas.Select(s => Visit((AstNode)s));
        return $"z.intersection({string.Join(", ", schemas)})";
    }
    
    public override string VisitZodEnum(ZodEnumNode node)
    {
        var values = node.Values.Select(v => VisitLiteralExpression(v.Value));
        return $"z.enum([{string.Join(", ", values)}])";
    }
    
    public override string VisitZodEnumValue(ZodEnumValueNode node)
    {
        return VisitLiteralExpression(node.Value);
    }
    
    public override string VisitZodEffects(ZodEffectsNode node)
    {
        var sb = new StringBuilder();
        
        var innerSchema = Visit((AstNode)node.InnerSchema);
        sb.Append(innerSchema);
        
        // Применяем эффект в зависимости от типа
        switch (node.EffectKind)
        {
            case ZodEffectKind.Refine:
                if (node.EffectFunction != null)
                {
                    var effectFn = Visit(node.EffectFunction);
                    sb.Append($".refine({effectFn})");
                }
                break;
            case ZodEffectKind.Transform:
                if (node.EffectFunction != null)
                {
                    var effectFn = Visit(node.EffectFunction);
                    sb.Append($".transform({effectFn})");
                }
                break;
            case ZodEffectKind.SuperRefine:
                if (node.EffectFunction != null)
                {
                    var effectFn = Visit(node.EffectFunction);
                    sb.Append($".superRefine({effectFn})");
                }
                break;
            case ZodEffectKind.Optional:
                sb.Append(".nullish()");
                break;
            case ZodEffectKind.Nullable:
                sb.Append(".nullable()");
                break;
        }
        
        return sb.ToString();
    }
    
    // Вспомогательные методы для рендеринга примитивных типов
    
    private string RenderZodPrimitive(ZodSchemaKind kind)
    {
        return kind switch
        {
            ZodSchemaKind.String => "z.string()",
            ZodSchemaKind.Number => "z.coerce.number()",
            ZodSchemaKind.Integer => "z.coerce.number().int()",
            ZodSchemaKind.BigInt => "z.coerce.bigint()",
            ZodSchemaKind.Boolean => "z.boolean()",
            ZodSchemaKind.Null => "z.null()",
            ZodSchemaKind.Any => "z.any()",
            _ => "z.any()"
        };
    }
    
    // Переопределяем базовые методы для корректной работы с выражениями
    
    public override string VisitLiteralExpression(LiteralExpressionNode node)
    {
        return node.LiteralKind switch
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
    
    // Реализация всех абстрактных методов BaseAstVisitor через VisitDefault
    public override string VisitRawType(RawTypeNode node) => node.RawTypeString;
    public override string VisitSimpleType(SimpleTypeNode node) => VisitDefault(node);
    public override string VisitExpressionStatement(ExpressionStatementNode node) => VisitDefault(node);
    public override string VisitTypeDeclaration(TypeDeclarationNode node) => VisitDefault(node);
    public override string VisitNullableType(NullableTypeNode node) => VisitDefault(node);
    public override string VisitThrowStatement(ThrowStatementNode node) => VisitDefault(node);
    public override string VisitIfStatement(IfStatementNode node) => VisitDefault(node);
    public override string VisitDictionaryType(DictionaryTypeNode node) => VisitDefault(node);
    public override string VisitComment(CommentNode node) => VisitDefault(node);
    public override string VisitOptionalType(OptionalTypeNode node) => VisitDefault(node);
    public override string VisitEnumMemberDeclaration(EnumMemberDeclarationNode node) => VisitDefault(node);
    public override string VisitReturnStatement(ReturnStatementNode node) => VisitDefault(node);
    public override string VisitUnionType(UnionTypeNode node) => VisitDefault(node);
    public override string VisitGenericType(GenericTypeNode node) => VisitDefault(node);
    public override string VisitImport(ImportNode node) => VisitDefault(node);
    public override string VisitAttribute(AttributeNode node) => VisitDefault(node);
    public override string VisitCallExpression(CallExpressionNode node)
    {
        var newPrefix = node.IsNewExpression ? "new " : "";
        var target = VisitExpression(node.Target);
        var args = node.Arguments.Count > 0
            ? string.Join(", ", node.Arguments.Select(a => VisitExpression((AstNode)a)))
            : "";
        return $"{newPrefix}{target}({args})";
    }
    public override string VisitIdentifier(IdentifierNode node) => VisitDefault(node);
    public override string VisitConditionalExpression(ConditionalExpressionNode node) => VisitDefault(node);
    public override string VisitExport(ExportNode node) => VisitDefault(node);
    public override string VisitIntersectionType(IntersectionTypeNode node) => VisitDefault(node);
    public override string VisitUsingStatement(UsingStatementNode node) => VisitDefault(node);
    public override string VisitCastExpression(CastExpressionNode node) => VisitDefault(node);
    public override string VisitForEachStatement(ForEachStatementNode node) => VisitDefault(node);
    public override string VisitAwaitExpression(AwaitExpressionNode node) => VisitDefault(node);
    public override string VisitMemberAccessExpression(MemberAccessExpressionNode node)
    {
        var obj = VisitExpression(node.Object);
        return $"{obj}.{node.Member.Name}";
    }
    public override string VisitPropertyDeclaration(PropertyDeclarationNode node) => VisitDefault(node);
    public override string VisitArrayType(ArrayTypeNode node) => VisitDefault(node);
    public override string VisitCatchClause(CatchClauseNode node) => VisitDefault(node);
    public override string VisitParameterDeclaration(ParameterDeclarationNode node) => VisitDefault(node);
    public override string VisitVariableDeclarationStatement(VariableDeclarationStatementNode node) => VisitDefault(node);
    public override string VisitMethodDeclaration(MethodDeclarationNode node) => VisitDefault(node);
    public override string VisitBlock(BlockNode node) => VisitDefault(node);
    public override string VisitBinaryExpression(BinaryExpressionNode node) => VisitDefault(node);
    public override string VisitTryCatchStatement(TryCatchStatementNode node) => VisitDefault(node);
}
