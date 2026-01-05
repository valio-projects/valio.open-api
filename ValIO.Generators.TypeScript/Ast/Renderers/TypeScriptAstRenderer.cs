using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Core.Utilities;

namespace ValIO.Generators.TypeScript.Ast.Renderers;

/// <summary>
/// Рендерер AST для генерации TypeScript кода
/// </summary>
public class TypeScriptAstRenderer : AstVisitorBase<string>, ITypeScriptAstRenderer
{
    private readonly StringBuilder _sb = new();
    private int _indentLevel = 0;

    /// <summary>
    /// Рендерит SourceFile
    /// </summary>
    public string Render(SourceFileNode node)
    {
        _sb.Clear();
        _indentLevel = 0;
        
        // Рендерим imports
        if (node.Imports.Count > 0)
        {
            foreach (var import in node.Imports)
            {
                RenderImport(import);
            }
            CodeFormatter.AppendEmptyLine(_sb);
        }
        
        // Рендерим declarations
        foreach (var declaration in node.Declarations)
        {
            var rendered = RenderDeclaration(declaration);
            _sb.Append(rendered);
            CodeFormatter.AppendEmptyLine(_sb);
        }
        
        return _sb.ToString();
    }

    /// <summary>
    /// Рендерит Interface
    /// </summary>
    public string Render(InterfaceDeclarationNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        RenderInterface(sb, node, indentLevel);
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит TypeAlias
    /// </summary>
    public string Render(TypeAliasDeclarationNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        RenderTypeAlias(sb, node, indentLevel);
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит Function
    /// </summary>
    public string Render(FunctionDeclarationNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        RenderFunction(sb, node, indentLevel);
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит Variable
    /// </summary>
    public string Render(VariableDeclarationNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        RenderVariable(sb, node, indentLevel);
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит Enum
    /// </summary>
    public string Render(EnumDeclarationNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        RenderEnum(sb, node, indentLevel);
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит Property
    /// </summary>
    public string Render(PropertyDeclarationNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        RenderProperty(sb, node, indentLevel);
        
        return sb.ToString();
    }

    #region Visitor Pattern Implementation

    public override string VisitSourceFile(SourceFileNode node)
    {
        return Render(node);
    }

    public override string VisitInterfaceDeclaration(InterfaceDeclarationNode node)
    {
        return Render(node);
    }

    public override string VisitTypeAliasDeclaration(TypeAliasDeclarationNode node)
    {
        return Render(node);
    }

    public override string VisitFunctionDeclaration(FunctionDeclarationNode node)
    {
        return Render(node);
    }

    public override string VisitVariableDeclaration(VariableDeclarationNode node)
    {
        return Render(node);
    }

    public override string VisitEnumDeclaration(EnumDeclarationNode node)
    {
        return Render(node);
    }

    public override string VisitPropertyDeclaration(PropertyDeclarationNode node)
    {
        return Render(node);
    }

    public override string VisitParameterDeclaration(ParameterDeclarationNode node)
    {
        var sb = new StringBuilder();
        RenderParameter(sb, node);
        return sb.ToString();
    }

    public override string VisitSimpleType(SimpleTypeNode node)
    {
        return node.Name.Name;
    }

    public override string VisitNullableType(NullableTypeNode node)
    {
        return $"{RenderType(node.InnerType)} | null";
    }

    public override string VisitOptionalType(OptionalTypeNode node)
    {
        return $"{RenderType(node.InnerType)}?";
    }

    public override string VisitArrayType(ArrayTypeNode node)
    {
        return $"{RenderType(node.ElementType)}[]";
    }

    public override string VisitGenericType(GenericTypeNode node)
    {
        var typeArgs = string.Join(", ", node.TypeArguments.Select(RenderType));
        return $"{node.Name.Name}<{typeArgs}>";
    }

    public override string VisitUnionType(UnionTypeNode node)
    {
        var types = string.Join(" | ", node.Types.Select(RenderType));
        return types;
    }

    public override string VisitIntersectionType(IntersectionTypeNode node)
    {
        var types = string.Join(" & ", node.Types.Select(RenderType));
        return types;
    }

    public override string VisitDictionaryType(DictionaryTypeNode node)
    {
        var keyType = RenderType(node.KeyType);
        var valueType = RenderType(node.ValueType);
        return $"Record<{keyType}, {valueType}>";
    }

    public override string VisitLiteralExpression(LiteralExpressionNode node)
    {
        return RenderLiteral(node);
    }

    public override string VisitIdentifierExpression(IdentifierExpressionNode node)
    {
        return node.Identifier.Name;
    }

    public override string VisitCallExpression(CallExpressionNode node)
    {
        var target = Visit(node.Target);
        var args = string.Join(", ", node.Arguments.Select(Visit));
        return $"{target}({args})";
    }

    public override string VisitMemberAccessExpression(MemberAccessExpressionNode node)
    {
        var target = Visit(node.Object);
        return $"{target}.{node.Member.Name}";
    }

    public override string VisitObjectLiteralExpression(ObjectLiteralExpressionNode node)
    {
        var properties = string.Join(", ", node.Properties.Select(p => VisitPropertyAssignment(p)));
        return $"{{ {properties} }}";
    }

    public override string VisitTemplateLiteralExpression(TemplateLiteralExpressionNode node)
    {
        var parts = string.Join("", node.Parts.Select(p => p.IsExpression ? $"${{{Visit(p.Expression!)}}}" : p.Text));
        return $"`{parts}`";
    }

    public override string VisitReturnStatement(ReturnStatementNode node)
    {
        if (node.Expression == null)
        {
            return "return;";
        }
        var expr = Visit(node.Expression);
        return $"return {expr};";
    }

    public override string VisitExpressionStatement(ExpressionStatementNode node)
    {
        var expr = Visit(node.Expression);
        return $"{expr};";
    }

    public override string VisitIfStatement(IfStatementNode node)
    {
        var sb = new StringBuilder();
        var condition = Visit(node.Condition);
        CodeFormatter.AppendLine(sb, _indentLevel, $"if ({condition}) {{");
        _indentLevel++;
        foreach (var stmt in node.ThenStatements)
        {
            var rendered = Visit(stmt);
            CodeFormatter.AppendLine(sb, _indentLevel, rendered);
        }
        _indentLevel--;
        CodeFormatter.AppendLine(sb, _indentLevel, "}");
        return sb.ToString();
    }

    public override string VisitForEachStatement(ForEachStatementNode node)
    {
        var sb = new StringBuilder();
        var item = node.Variable.Name;
        var collection = Visit(node.Collection);
        CodeFormatter.AppendLine(sb, _indentLevel, $"for (const {item} of {collection}) {{");
        _indentLevel++;
        foreach (var stmt in node.Body.Statements)
        {
            var rendered = Visit(stmt);
            CodeFormatter.AppendLine(sb, _indentLevel, rendered);
        }
        _indentLevel--;
        CodeFormatter.AppendLine(sb, _indentLevel, "}");
        return sb.ToString();
    }

    public override string VisitBinaryExpression(BinaryExpressionNode node)
    {
        var left = Visit(node.Left);
        var right = Visit(node.Right);
        var op = GetBinaryOperator(node.Operator);
        return $"({left} {op} {right})";
    }
    
    /// <summary>
    /// Преобразует BinaryOperator в строку оператора TypeScript
    /// </summary>
    private static string GetBinaryOperator(BinaryOperator op)
    {
        return op switch
        {
            BinaryOperator.Add => "+",
            BinaryOperator.Subtract => "-",
            BinaryOperator.Multiply => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.Modulo => "%",
            BinaryOperator.Equal => "===",
            BinaryOperator.NotEqual => "!==",
            BinaryOperator.LessThan => "<",
            BinaryOperator.LessThanOrEqual => "<=",
            BinaryOperator.GreaterThan => ">",
            BinaryOperator.GreaterThanOrEqual => ">=",
            BinaryOperator.LogicalAnd => "&&",
            BinaryOperator.LogicalOr => "||",
            BinaryOperator.BitwiseAnd => "&",
            BinaryOperator.BitwiseOr => "|",
            BinaryOperator.BitwiseXor => "^",
            BinaryOperator.LeftShift => "<<",
            BinaryOperator.RightShift => ">>",
            _ => throw new NotSupportedException($"Binary operator {op} is not supported")
        };
    }

    public override string VisitConditionalExpression(ConditionalExpressionNode node)
    {
        var condition = Visit(node.Condition);
        var trueExpr = Visit(node.TrueExpression);
        var falseExpr = Visit(node.FalseExpression);
        return $"({condition} ? {trueExpr} : {falseExpr})";
    }

    public override string VisitBlock(BlockNode node)
    {
        var sb = new StringBuilder();
        CodeFormatter.AppendLine(sb, _indentLevel, "{");
        _indentLevel++;
        foreach (var stmt in node.Statements)
        {
            var rendered = Visit(stmt);
            CodeFormatter.AppendLine(sb, _indentLevel, rendered);
        }
        _indentLevel--;
        CodeFormatter.AppendLine(sb, _indentLevel, "}");
        return sb.ToString();
    }

    public override string VisitImport(ImportNode node)
    {
        var sb = new StringBuilder();
        RenderImport(node);
        return sb.ToString();
    }

    public override string VisitExport(ExportNode node)
    {
        var declaration = Visit(node.Declaration);
        var exportKeyword = node.ExportKind switch
        {
            ExportKind.Default => "export default ",
            ExportKind.Named => "export ",
            _ => "export "
        };
        return $"{exportKeyword}{declaration}";
    }

    public override string VisitIdentifier(IdentifierNode node)
    {
        return node.Name;
    }

    public override string VisitComment(CommentNode node)
    {
        var sb = new StringBuilder();
        RenderComment(sb, node, 0);
        return sb.ToString();
    }

    public override string VisitAttribute(AttributeNode node)
    {
        // TypeScript не имеет атрибутов в том же смысле, что C#
        // Можно использовать декораторы, но для базовой реализации возвращаем пустую строку
        return "";
    }

    public override string VisitEnumMemberDeclaration(EnumMemberDeclarationNode node)
    {
        var sb = new StringBuilder();
        RenderEnumMember(sb, node, 0);
        return sb.ToString();
    }

    public override string VisitPropertyAssignment(PropertyAssignmentNode node)
    {
        var key = node.Name.Name;
        var value = Visit(node.Value);
        if (node.IsShorthand)
        {
            return key; // Shorthand: { name } вместо { name: name }
        }
        return $"{key}: {value}";
    }

    // C# специфичные методы (не используются для TypeScript, но требуются интерфейсом)
    public override string VisitTypeDeclaration(TypeDeclarationNode node)
    {
        throw new NotSupportedException("TypeDeclaration is not supported in TypeScript renderer. Use specific declaration types instead.");
    }

    public override string VisitMethodDeclaration(MethodDeclarationNode node)
    {
        throw new NotSupportedException("MethodDeclaration is not supported in TypeScript renderer. Use FunctionDeclaration instead.");
    }

    public override string VisitCompilationUnit(CompilationUnitNode node)
    {
        throw new NotSupportedException("CompilationUnit is not supported in TypeScript renderer. Use SourceFile instead.");
    }

    public override string VisitNamespaceDeclaration(NamespaceDeclarationNode node)
    {
        throw new NotSupportedException("NamespaceDeclaration is not supported in TypeScript renderer.");
    }

    public override string VisitUsingDirective(UsingDirectiveNode node)
    {
        throw new NotSupportedException("UsingDirective is not supported in TypeScript renderer. Use Import instead.");
    }

    public override string VisitClassDeclaration(ClassDeclarationNode node)
    {
        throw new NotSupportedException("ClassDeclaration is not supported in TypeScript renderer. Use InterfaceDeclaration or TypeAliasDeclaration instead.");
    }

    public override string VisitRecordDeclaration(RecordDeclarationNode node)
    {
        throw new NotSupportedException("RecordDeclaration is not supported in TypeScript renderer. Use InterfaceDeclaration or TypeAliasDeclaration instead.");
    }

    public override string VisitStructDeclaration(StructDeclarationNode node)
    {
        throw new NotSupportedException("StructDeclaration is not supported in TypeScript renderer. Use InterfaceDeclaration or TypeAliasDeclaration instead.");
    }

    public override string VisitFieldDeclaration(FieldDeclarationNode node)
    {
        throw new NotSupportedException("FieldDeclaration is not supported in TypeScript renderer. Use PropertyDeclaration or VariableDeclaration instead.");
    }

    public override string VisitConstructorDeclaration(ConstructorDeclarationNode node)
    {
        throw new NotSupportedException("ConstructorDeclaration is not supported in TypeScript renderer.");
    }

    public override string VisitGenericParameter(GenericParameterNode node)
    {
        var constraint = node.Constraint != null ? $" extends {RenderType(node.Constraint)}" : "";
        var defaultValue = node.Default != null ? $" = {RenderType(node.Default)}" : "";
        return $"{node.Name.Name}{constraint}{defaultValue}";
    }

    public override string VisitPropertySignature(PropertySignatureNode node)
    {
        var readonlyKeyword = node.IsReadonly ? "readonly " : "";
        var optional = node.IsRequired ? "" : "?";
        var type = RenderType(node.Type);
        return $"{readonlyKeyword}{node.Name.Name}{optional}: {type}";
    }

    // HTTP специфичные методы
    public override string VisitHttpMethod(HttpMethodNode node)
    {
        return node.Method switch
        {
            HttpMethodKind.Get => "GET",
            HttpMethodKind.Post => "POST",
            HttpMethodKind.Put => "PUT",
            HttpMethodKind.Delete => "DELETE",
            HttpMethodKind.Patch => "PATCH",
            HttpMethodKind.Head => "HEAD",
            HttpMethodKind.Options => "OPTIONS",
            _ => throw new NotSupportedException($"HTTP method {node.Method} is not supported")
        };
    }

    public override string VisitRoute(RouteNode node)
    {
        return $"\"{node.Path}\"";
    }

    public override string VisitPathParameter(PathParameterNode node)
    {
        return $"{node.Name.Name}: {RenderType(node.Type)}";
    }

    public override string VisitQueryParameter(QueryParameterNode node)
    {
        var optional = node.IsRequired ? "" : "?";
        return $"{node.Name.Name}{optional}: {RenderType(node.Type)}";
    }

    public override string VisitHeaderParameter(HeaderParameterNode node)
    {
        var optional = node.IsRequired ? "" : "?";
        return $"{node.Name.Name}{optional}: {RenderType(node.Type)}";
    }

    public override string VisitRequestBody(RequestBodyNode node)
    {
        return RenderType(node.Type);
    }

    public override string VisitResponseType(ResponseTypeNode node)
    {
        return RenderType(node.Type);
    }

    #endregion

    #region Private Helper Methods

    private void RenderImport(ImportNode import)
    {
        var from = !string.IsNullOrEmpty(import.From) ? $" from \"{import.From}\"" : "";
        var imports = import.Items.Count > 0 
            ? string.Join(", ", import.Items.Select(i => i.Name))
            : "*";
        
        if (import.Kind == ImportKind.Default && import.Items.Count > 0)
        {
            // Default import: import name from "module"
            CodeFormatter.AppendLine(_sb, 0, $"import {import.Items[0].Name}{from};");
        }
        else if (import.Kind == ImportKind.Namespace)
        {
            // Namespace import: import * as name from "module"
            var name = import.Items.Count > 0 ? import.Items[0].Name : "namespace";
            CodeFormatter.AppendLine(_sb, 0, $"import * as {name}{from};");
        }
        else
        {
            // Named imports: import { a, b } from "module"
            var namedImports = string.Join(", ", import.Items.Select(i => i.Name));
            CodeFormatter.AppendLine(_sb, 0, $"import {{ {namedImports} }}{from};");
        }
    }

    private string RenderDeclaration(DeclarationNode declaration)
    {
        return declaration.Kind switch
        {
            AstNodeKind.InterfaceDeclaration => Visit((InterfaceDeclarationNode)declaration),
            AstNodeKind.TypeAliasDeclaration => Visit((TypeAliasDeclarationNode)declaration),
            AstNodeKind.FunctionDeclaration => Visit((FunctionDeclarationNode)declaration),
            AstNodeKind.VariableDeclaration => Visit((VariableDeclarationNode)declaration),
            AstNodeKind.EnumDeclaration => Visit((EnumDeclarationNode)declaration),
            _ => throw new NotSupportedException($"Declaration kind {declaration.Kind} is not supported")
        };
    }

    private void RenderInterface(StringBuilder sb, InterfaceDeclarationNode node, int indentLevel)
    {
        // JSDoc комментарий
        if (node.Documentation != null)
        {
            RenderComment(sb, node.Documentation, indentLevel);
        }
        
        // Модификаторы и объявление interface
        // Для TypeScript interface всегда export (если нужно, можно добавить флаг позже)
        var exportKeyword = ""; // InterfaceDeclarationNode не имеет IsExported, используем пустую строку
        var baseTypes = node.BaseTypes.Count > 0 ? $" extends {string.Join(", ", node.BaseTypes.Select(RenderType))}" : "";
        CodeFormatter.AppendLine(sb, indentLevel, $"{exportKeyword}interface {node.Name.Name}{baseTypes} {{");
        
        // Члены interface
        foreach (var member in node.Members)
        {
            if (member is PropertyDeclarationNode property)
            {
                RenderProperty(sb, property, indentLevel + 1);
            }
        }
        
        CodeFormatter.AppendLine(sb, indentLevel, "}");
    }

    private void RenderTypeAlias(StringBuilder sb, TypeAliasDeclarationNode node, int indentLevel)
    {
        // JSDoc комментарий
        if (node.Documentation != null)
        {
            RenderComment(sb, node.Documentation, indentLevel);
        }
        
        // Модификаторы и объявление type alias
        // TypeAliasDeclarationNode не имеет IsExported, используем пустую строку
        var exportKeyword = "";
        var type = RenderType(node.Type);
        CodeFormatter.AppendLine(sb, indentLevel, $"{exportKeyword}type {node.Name.Name} = {type};");
    }

    private void RenderFunction(StringBuilder sb, FunctionDeclarationNode node, int indentLevel)
    {
        // JSDoc комментарий
        if (node.Documentation != null)
        {
            var parameters = node.Parameters.Select(p => (p.Name.Name, p.Documentation?.Text, RenderType(p.Type))).ToList();
            CommentGenerator.GenerateJSDocCommentWithParams(sb, indentLevel, node.Documentation.Text, parameters, node.ReturnType != null ? RenderType(node.ReturnType) : null);
        }
        
        // Модификаторы и объявление функции
        // FunctionDeclarationNode не имеет IsExported, используем пустую строку
        var exportKeyword = "";
        var asyncKeyword = node.IsAsync ? "async " : "";
        var functionKeyword = node.IsArrowFunction ? "" : "function ";
        var parameters = string.Join(", ", node.Parameters.Select(p => VisitParameterDeclaration(p)));
        var returnType = node.ReturnType != null ? $": {RenderType(node.ReturnType)}" : "";
        
        if (node.IsArrowFunction)
        {
            CodeFormatter.AppendLine(sb, indentLevel, $"{exportKeyword}const {node.Name.Name} = {asyncKeyword}({parameters}){returnType} => {{");
        }
        else
        {
            CodeFormatter.AppendLine(sb, indentLevel, $"{exportKeyword}{asyncKeyword}{functionKeyword}{node.Name.Name}({parameters}){returnType} {{");
        }
        
        // Тело функции
        if (node.Body != null && node.Body.Statements.Count > 0)
        {
            var savedIndent = _indentLevel;
            _indentLevel = indentLevel + 1;
            foreach (var stmt in node.Body.Statements)
            {
                var rendered = Visit(stmt);
                CodeFormatter.AppendLine(sb, _indentLevel, rendered);
            }
            _indentLevel = savedIndent;
        }
        else
        {
            CodeFormatter.AppendLine(sb, indentLevel + 1, "// Empty function body");
        }
        
        CodeFormatter.AppendLine(sb, indentLevel, "};");
    }

    private void RenderVariable(StringBuilder sb, VariableDeclarationNode node, int indentLevel)
    {
        // JSDoc комментарий
        if (node.Documentation != null)
        {
            RenderComment(sb, node.Documentation, indentLevel);
        }
        
        // Модификаторы и объявление переменной
        // VariableDeclarationNode не имеет IsExported, используем пустую строку
        var exportKeyword = "";
        var constKeyword = node.DeclarationKind switch
        {
            VariableDeclarationKind.Const => "const",
            VariableDeclarationKind.Let => "let",
            VariableDeclarationKind.Var => "var",
            _ => "const"
        };
        var type = node.Type != null ? $": {RenderType(node.Type)}" : "";
        var initializer = node.Initializer != null ? $" = {Visit(node.Initializer)}" : "";
        CodeFormatter.AppendLine(sb, indentLevel, $"{exportKeyword}{constKeyword} {node.Name.Name}{type}{initializer};");
    }

    private void RenderEnum(StringBuilder sb, EnumDeclarationNode node, int indentLevel)
    {
        // JSDoc комментарий
        if (node.Documentation != null)
        {
            RenderComment(sb, node.Documentation, indentLevel);
        }
        
        // Модификаторы и объявление enum
        // EnumDeclarationNode не имеет IsExported и IsConstEnum для TypeScript, используем пустые строки
        var exportKeyword = "";
        var constKeyword = ""; // TypeScript const enum можно добавить позже через metadata
        CodeFormatter.AppendLine(sb, indentLevel, $"{exportKeyword}{constKeyword}enum {node.Name.Name} {{");
        
        // Члены enum
        for (int i = 0; i < node.Members.Count; i++)
        {
            var member = node.Members[i];
            var comma = i < node.Members.Count - 1 ? "," : "";
            RenderEnumMember(sb, (EnumMemberDeclarationNode)member, indentLevel + 1, comma);
        }
        
        CodeFormatter.AppendLine(sb, indentLevel, "}");
    }

    private void RenderProperty(StringBuilder sb, PropertyDeclarationNode node, int indentLevel)
    {
        // JSDoc комментарий
        if (node.Documentation != null)
        {
            RenderComment(sb, node.Documentation, indentLevel);
        }
        
        // Объявление свойства
        var optional = node.IsRequired ? "" : "?";
        // PropertyDeclarationNode не имеет IsReadOnly для TypeScript, используем пустую строку
        var readonly = ""; // Можно добавить через metadata позже
        var type = RenderType(node.Type);
        CodeFormatter.AppendLine(sb, indentLevel, $"{readonly}{node.Name.Name}{optional}: {type};");
    }

    private void RenderParameter(StringBuilder sb, ParameterDeclarationNode node)
    {
        var optional = node.IsRequired ? "" : "?";
        var type = RenderType(node.Type);
        var defaultValue = node.DefaultValue != null ? $" = {Visit(node.DefaultValue)}" : "";
        sb.Append($"{node.Name.Name}{optional}: {type}{defaultValue}");
    }

    private string VisitParameterDeclaration(ParameterDeclarationNode node)
    {
        var sb = new StringBuilder();
        RenderParameter(sb, node);
        return sb.ToString();
    }

    private void RenderEnumMember(StringBuilder sb, EnumMemberDeclarationNode node, int indentLevel, string? comma = null)
    {
        // JSDoc комментарий
        if (node.Documentation != null)
        {
            RenderComment(sb, node.Documentation, indentLevel);
        }
        
        // Значение enum
        var value = node.Value != null ? $" = {Visit(node.Value)}" : "";
        CodeFormatter.AppendLine(sb, indentLevel, $"{node.Name.Name}{value}{comma ?? ""}");
    }


    private void RenderComment(StringBuilder sb, CommentNode comment, int indentLevel)
    {
        if (comment.Kind == CommentKind.JSDoc || comment.Kind == CommentKind.Xml)
        {
            CommentGenerator.GenerateJSDocComment(sb, indentLevel, comment.Text);
        }
        else if (comment.Kind == CommentKind.SingleLine)
        {
            CodeFormatter.AppendLine(sb, indentLevel, $"// {comment.Text}");
        }
        else if (comment.Kind == CommentKind.MultiLine)
        {
            var lines = comment.Text.Split('\n');
            CodeFormatter.AppendLine(sb, indentLevel, "/*");
            foreach (var line in lines)
            {
                CodeFormatter.AppendLine(sb, indentLevel, $" * {line.TrimEnd()}");
            }
            CodeFormatter.AppendLine(sb, indentLevel, " */");
        }
    }

    private string RenderType(TypeNode type)
    {
        return type.Kind switch
        {
            AstNodeKind.SimpleType => Visit((SimpleTypeNode)type),
            AstNodeKind.NullableType => Visit((NullableTypeNode)type),
            AstNodeKind.OptionalType => Visit((OptionalTypeNode)type),
            AstNodeKind.ArrayType => Visit((ArrayTypeNode)type),
            AstNodeKind.GenericType => Visit((GenericTypeNode)type),
            AstNodeKind.UnionType => Visit((UnionTypeNode)type),
            AstNodeKind.IntersectionType => Visit((IntersectionTypeNode)type),
            AstNodeKind.DictionaryType => Visit((DictionaryTypeNode)type),
            _ => throw new NotSupportedException($"Type kind {type.Kind} is not supported")
        };
    }

    private string RenderLiteral(LiteralExpressionNode node)
    {
        return node.LiteralKind switch
        {
            LiteralKind.String => $"\"{CodeEscapeHelper.EscapeTypeScriptString(node.Value)}\"",
            LiteralKind.Integer => node.Value?.ToString() ?? "0",
            LiteralKind.Float => node.Value?.ToString() ?? "0.0",
            LiteralKind.Double => node.Value?.ToString() ?? "0.0",
            LiteralKind.Boolean => node.Value?.ToString()?.ToLowerInvariant() ?? "false",
            LiteralKind.Null => "null",
            LiteralKind.Undefined => "undefined",
            _ => node.Value?.ToString() ?? "null"
        };
    }

    #endregion
}
