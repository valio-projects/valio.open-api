using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Core.Utilities;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Renderers;

/// <summary>
/// Рендерер AST для генерации TypeScript кода
/// Реализует ITypeScriptAstVisitor и IHttpAstVisitor для поддержки HTTP клиентов
/// </summary>
public class TypeScriptAstRenderer : TypeScriptAstVisitorBase<string>, ITypeScriptAstRenderer, IHttpAstVisitor<string>
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
        
        // Рендерим declarations (export уже добавлен в RenderInterface, RenderEnum, RenderTypeAlias)
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
        // Проверяем metadata для литеральных строк
        var isLiteralString = node.Metadata.TryGetValue("IsLiteralString", out var literalStrObj) 
            && literalStrObj is bool isLiteral && isLiteral;
        
        if (isLiteralString && node.Metadata.TryGetValue("LiteralValue", out var literalValue))
        {
            // Это литеральная строка - рендерим значение в кавычках с экранированием
            // Экранирование и форматирование делается ТОЛЬКО здесь, в renderer
            var valueStr = literalValue?.ToString() ?? "";
            var escapedValue = CodeEscapeHelper.EscapeTypeScriptString(valueStr);
            
            var nullableStr = node.IsNullable ? " | null" : "";
            var optionalStr = node.IsOptional ? "?" : "";
            return $"\"{escapedValue}\"{optionalStr}{nullableStr}";
        }
        
        // Проверяем metadata для литеральных чисел
        var isLiteralNumber = node.Metadata.TryGetValue("IsLiteralNumber", out var literalNumObj) 
            && literalNumObj is bool isLiteralNum && isLiteralNum;
        
        if (isLiteralNumber && node.Metadata.TryGetValue("LiteralValue", out var literalNumValue))
        {
            // Это литеральное число - рендерим значение как есть
            var nullableStr = node.IsNullable ? " | null" : "";
            var optionalStr = node.IsOptional ? "?" : "";
            return $"{literalNumValue}{optionalStr}{nullableStr}";
        }
        
        // Обычный тип
        var typeName = node.Name.Name;
        var nullableStr2 = node.IsNullable ? " | null" : "";
        var optionalStr2 = node.IsOptional ? "?" : "";
        return $"{typeName}{optionalStr2}{nullableStr2}";
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
        var readonlyStr = node.IsReadOnly ? "readonly " : "";
        return $"{readonlyStr}{RenderType(node.ElementType)}[]";
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
    
    public override string VisitArrayLiteralExpression(ArrayLiteralExpressionNode node)
    {
        var elements = string.Join(", ", node.Elements.Select(Visit));
        return $"[{elements}]";
    }

    public override string VisitTemplateLiteralExpression(TemplateLiteralExpressionNode node)
    {
        var parts = string.Join("", node.Parts.Select(p => p.IsExpression ? $"${{{Visit(p.Expression!)}}}" : p.Text));
        return $"`{parts}`";
    }

    public override string VisitReturnStatement(ReturnStatementNode node)
    {
        if (node.Value == null)
        {
            return "return;";
        }
        var expr = Visit(node.Value);
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
        var savedIndent = _indentLevel;
        var condition = Visit(node.Condition);
        CodeFormatter.AppendLine(sb, _indentLevel, $"if ({condition}) {{");
        _indentLevel++;
        foreach (var stmt in node.ThenBlock.Statements)
        {
            var rendered = Visit(stmt);
            CodeFormatter.AppendLine(sb, _indentLevel, rendered);
        }
        _indentLevel = savedIndent;
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
            BinaryOperator.Assign => "=",
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
        
        if (import.ImportKind == ImportKind.Default && import.Items.Count > 0)
        {
            // Default import: import name from "module"
            CodeFormatter.AppendLine(_sb, 0, $"import {import.Items[0].Name}{from};");
        }
        else if (import.ImportKind == ImportKind.Namespace)
        {
            // Namespace import: import * as name from "module"
            var name = import.Items.Count > 0 ? import.Items[0].Name : "namespace";
            CodeFormatter.AppendLine(_sb, 0, $"import * as {name}{from};");
        }
        else
        {
            // Named imports: import type { a, b } from "module" (TypeScript type imports)
            var namedImports = string.Join(", ", import.Items.Select(i => i.Name));
            CodeFormatter.AppendLine(_sb, 0, $"import type {{ {namedImports} }}{from};");
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
        // Для TypeScript interface всегда export
        var exportKeyword = "export "; // Всегда экспортируем интерфейсы
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
        // Для TypeScript type alias всегда export
        var exportKeyword = "export "; // Всегда экспортируем type aliases
        var type = RenderType(node.Type);
        CodeFormatter.AppendLine(sb, indentLevel, $"{exportKeyword}type {node.Name.Name} = {type};");
    }

    private void RenderFunction(StringBuilder sb, FunctionDeclarationNode node, int indentLevel)
    {
        // Формируем расширенный JSDoc комментарий
        var jsDocLines = new List<string>();
        
        // Добавляем описание (@description)
        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
        {
            jsDocLines.Add($"@description {node.Documentation.Text}");
        }
        
        // Добавляем @function для явного указания, что это функция
        jsDocLines.Add("@function");
        
        // Добавляем @async для асинхронных функций
        if (node.IsAsync)
        {
            jsDocLines.Add("@async");
        }
        
        // Добавляем @deprecated если функция deprecated
        if (node.IsDeprecated)
        {
            jsDocLines.Add("@deprecated");
        }
        
        // Добавляем параметры (@param)
        foreach (var param in node.Parameters)
        {
            var paramType = RenderType(param.Type);
            var paramDescription = param.Documentation?.Text ?? "";
            var paramName = param.Name.Name;
            var optionalMarker = !param.IsRequired ? "?" : "";
            
            if (!string.IsNullOrWhiteSpace(paramDescription))
            {
                jsDocLines.Add($"@param {{{paramType}}} {optionalMarker}{paramName} {paramDescription}");
            }
            else
            {
                jsDocLines.Add($"@param {{{paramType}}} {optionalMarker}{paramName}");
            }
        }
        
        // Добавляем тип возвращаемого значения (@returns)
        // Проверяем, является ли это type predicate (value is Type)
        if (node.ReturnType != null)
        {
            if (node.IsTypePredicate && !string.IsNullOrEmpty(node.TypePredicateParameterName))
            {
                // Type predicate: value is Type
                var returnTypeStr = RenderType(node.ReturnType);
                jsDocLines.Add($"@returns {{{node.TypePredicateParameterName} is {returnTypeStr}}}");
            }
            else
            {
                var returnTypeStr = RenderType(node.ReturnType);
                jsDocLines.Add($"@returns {{{returnTypeStr}}}");
            }
        }
        
        // Добавляем @example если есть в metadata
        if (node.Metadata.TryGetValue("Example", out var exampleObj) && exampleObj != null)
        {
            var exampleStr = exampleObj.ToString() ?? "";
            jsDocLines.Add($"@example {exampleStr}");
        }
        
        // Добавляем @todo если есть в metadata
        if (node.Metadata.TryGetValue("Todo", out var todoObj) && todoObj != null)
        {
            var todoStr = todoObj.ToString() ?? "";
            jsDocLines.Add($"@todo {todoStr}");
        }
        
        // Генерируем JSDoc комментарий
        if (jsDocLines.Count > 0)
        {
            CodeFormatter.AppendLine(sb, indentLevel, "/**");
            foreach (var line in jsDocLines)
            {
                CodeFormatter.AppendLine(sb, indentLevel, $" * {line}");
            }
            CodeFormatter.AppendLine(sb, indentLevel, " */");
        }
        
        // Модификаторы и объявление функции
        var exportKeyword = node.IsExported ? "export " : "";
        var asyncKeyword = node.IsAsync ? "async " : "";
        var functionKeyword = node.IsArrowFunction ? "" : "function ";
        var parameters = string.Join(", ", node.Parameters.Select(p => RenderParameterDeclaration(p)));
        
        // Проверяем, является ли это type predicate (value is Type)
        string returnType;
        if (node.ReturnType != null)
        {
            if (node.IsTypePredicate && !string.IsNullOrEmpty(node.TypePredicateParameterName))
            {
                // Type predicate: value is Type
                var returnTypeStr = RenderType(node.ReturnType);
                returnType = $": {node.TypePredicateParameterName} is {returnTypeStr}";
            }
            else
            {
                returnType = $": {RenderType(node.ReturnType)}";
            }
        }
        else
        {
            returnType = "";
        }
        
        if (node.IsArrowFunction)
        {
            // Arrow function: const fn = (params) => body
            // Для arrow function тело может быть выражением или блоком
            if (node.Body != null && node.Body.Statements.Count == 1 && node.Body.Statements[0] is ReturnStatementNode returnStmt && returnStmt.Value != null)
            {
                // Если тело состоит из одного return statement, рендерим как выражение
                var bodyExpression = Visit(returnStmt.Value);
                CodeFormatter.AppendLine(sb, indentLevel, $"{exportKeyword}const {node.Name.Name} = {asyncKeyword}({parameters}){returnType} => {bodyExpression};");
            }
            else
            {
                // Arrow function с блоком
                CodeFormatter.AppendLine(sb, indentLevel, $"{exportKeyword}const {node.Name.Name} = {asyncKeyword}({parameters}){returnType} => {{");
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
                CodeFormatter.AppendLine(sb, indentLevel, "};");
            }
        }
        else
        {
            // Обычная функция: function fn(params): returnType { body }
            CodeFormatter.AppendLine(sb, indentLevel, $"{exportKeyword}{asyncKeyword}{functionKeyword}{node.Name.Name}({parameters}){returnType} {{");
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
                // Type-only функция (без тела)
                CodeFormatter.AppendLine(sb, indentLevel + 1, "// Empty function body");
            }
            CodeFormatter.AppendLine(sb, indentLevel, "}");
        }
    }

    private void RenderVariable(StringBuilder sb, VariableDeclarationNode node, int indentLevel)
    {
        // JSDoc комментарий
        if (node.Documentation != null)
        {
            RenderComment(sb, node.Documentation, indentLevel);
        }
        
        // Модификаторы и объявление переменной
        var exportKeyword = node.IsExported ? "export " : "";
        
        var constKeyword = node.DeclarationKind switch
        {
            VariableDeclarationKind.Const => "const",
            VariableDeclarationKind.Let => "let",
            VariableDeclarationKind.Var => "var",
            _ => "const"
        };
        var type = node.Type != null ? $": {RenderType(node.Type)}" : "";
        var initializer = node.Initializer != null ? $" = {Visit(node.Initializer)}" : "";
        
        // Проверяем, нужно ли добавить "as const satisfies" assertion для строгой типизации
        var asSatisfies = "";
        if (node.AsConstSatisfies && node.SatisfiesType != null)
        {
            // Используем "as const satisfies" для максимально строгой типизации
            var satisfiesTypeStr = RenderType(node.SatisfiesType);
            asSatisfies = $" as const satisfies {satisfiesTypeStr}";
        }
        // Если нет "as const satisfies", проверяем "as const" (для обратной совместимости)
        else if (node.AsConstAssertion)
        {
            asSatisfies = " as const";
        }
        
        CodeFormatter.AppendLine(sb, indentLevel, $"{exportKeyword}{constKeyword} {node.Name.Name}{type}{initializer}{asSatisfies};");
    }

    private void RenderEnum(StringBuilder sb, EnumDeclarationNode node, int indentLevel)
    {
        // JSDoc комментарий
        if (node.Documentation != null)
        {
            RenderComment(sb, node.Documentation, indentLevel);
        }
        
        // Модификаторы и объявление enum
        // Для TypeScript enum всегда export
        var exportKeyword = "export "; // Всегда экспортируем enum'ы
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
        // Проверяем, является ли тип nullable или optional
        var isNullable = IsTypeNullable(node.Type);
        var isOptional = !node.IsRequired || IsTypeOptional(node.Type);
        
        // Получаем constraints из metadata
        var constraints = node.Constraints;
        
        // Формируем JSDoc комментарий
        var jsDocLines = new List<string>();
        
        // Добавляем описание из документации (@description)
        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
        {
            jsDocLines.Add($"@description {node.Documentation.Text}");
        }
        
        // Добавляем @type для явного указания типа
        var typeString = RenderType(node.Type);
        jsDocLines.Add($"@type {{{typeString}}}");
        
        // Добавляем @nullable для nullable типов
        if (isNullable)
        {
            jsDocLines.Add("@nullable");
        }
        
        // Добавляем @optional для optional свойств
        if (isOptional && !node.IsRequired)
        {
            jsDocLines.Add("@optional");
        }
        
        // Добавляем @readonly если свойство readonly (можно добавить через metadata)
        if (node.Metadata.TryGetValue("IsReadOnly", out var isReadOnlyObj) && isReadOnlyObj is bool isReadOnly && isReadOnly)
        {
            jsDocLines.Add("@readonly");
        }
        
        // Добавляем @deprecated если свойство deprecated (можно добавить через metadata)
        if (node.Metadata.TryGetValue("IsDeprecated", out var isDeprecatedObj) && isDeprecatedObj is bool isDeprecated && isDeprecated)
        {
            jsDocLines.Add("@deprecated");
        }
        
        // Добавляем constraints из IrPropertyConstraints
        if (constraints != null)
        {
            // Минимальная длина для строк
            if (constraints.MinLength.HasValue)
            {
                jsDocLines.Add($"@minLength {constraints.MinLength.Value}");
            }
            
            // Максимальная длина для строк
            if (constraints.MaxLength.HasValue)
            {
                jsDocLines.Add($"@maxLength {constraints.MaxLength.Value}");
            }
            
            // Минимальное значение для чисел
            if (constraints.Min.HasValue)
            {
                jsDocLines.Add($"@min {constraints.Min.Value}");
            }
            
            // Максимальное значение для чисел
            if (constraints.Max.HasValue)
            {
                jsDocLines.Add($"@max {constraints.Max.Value}");
            }
            
            // Исключающее минимальное значение
            if (constraints.ExclusiveMin.HasValue)
            {
                jsDocLines.Add($"@exclusiveMin {constraints.ExclusiveMin.Value}");
            }
            
            // Исключающее максимальное значение
            if (constraints.ExclusiveMax.HasValue)
            {
                jsDocLines.Add($"@exclusiveMax {constraints.ExclusiveMax.Value}");
            }
            
            // Регулярное выражение
            if (!string.IsNullOrWhiteSpace(constraints.Pattern))
            {
                jsDocLines.Add($"@pattern {constraints.Pattern}");
            }
            
            // Значение по умолчанию
            if (constraints.DefaultValue != null)
            {
                var defaultValueStr = RenderDefaultValue(constraints.DefaultValue);
                jsDocLines.Add($"@default {defaultValueStr}");
            }
        }
        
        // Добавляем @example если есть в metadata
        if (node.Metadata.TryGetValue("Example", out var exampleObj) && exampleObj != null)
        {
            var exampleStr = RenderDefaultValue(exampleObj);
            jsDocLines.Add($"@example {exampleStr}");
        }
        
        // Добавляем @enum если тип является enum (можно определить через metadata)
        if (node.Metadata.TryGetValue("IsEnum", out var isEnumObj) && isEnumObj is bool isEnum && isEnum)
        {
            jsDocLines.Add("@enum");
        }
        
        // Добавляем @constant если свойство является константой (можно добавить через metadata)
        if (node.Metadata.TryGetValue("IsConstant", out var isConstantObj) && isConstantObj is bool isConstant && isConstant)
        {
            jsDocLines.Add("@constant");
        }
        
        // Генерируем JSDoc комментарий, если есть что добавить
        if (jsDocLines.Count > 0)
        {
            CodeFormatter.AppendLine(sb, indentLevel, "/**");
            foreach (var line in jsDocLines)
            {
                CodeFormatter.AppendLine(sb, indentLevel, $" * {line}");
            }
            CodeFormatter.AppendLine(sb, indentLevel, " */");
        }
        
        // Объявление свойства
        var optional = node.IsRequired ? "" : "?";
        // PropertyDeclarationNode не имеет IsReadOnly для TypeScript, используем пустую строку
        var isReadonly = ""; // Можно добавить через metadata позже
        var type = RenderType(node.Type);
        CodeFormatter.AppendLine(sb, indentLevel, $"{isReadonly}{node.Name.Name}{optional}: {type};");
    }
    
    /// <summary>
    /// Рендерит значение по умолчанию для JSDoc
    /// </summary>
    private string RenderDefaultValue(object? value)
    {
        if (value == null)
            return "null";
        
        return value switch
        {
            string str => $"\"{str.Replace("\"", "\\\"")}\"",
            bool b => b ? "true" : "false",
            int or long or short or byte => value.ToString() ?? "0",
            float or double or decimal => value.ToString() ?? "0",
            _ => value.ToString() ?? "null"
        };
    }
    
    /// <summary>
    /// Проверяет, является ли тип nullable
    /// </summary>
    private bool IsTypeNullable(TypeNode type)
    {
        return type switch
        {
            NullableTypeNode => true,
            UnionTypeNode union when union.Types.Any(t => t is SimpleTypeNode st && st.Name.Name == "null") => true,
            SimpleTypeNode simple when simple.IsNullable => true,
            ArrayTypeNode array when array.IsNullable => true,
            GenericTypeNode generic when generic.IsNullable => true,
            DictionaryTypeNode dict when dict.IsNullable => true,
            IntersectionTypeNode intersection when intersection.IsNullable => true,
            _ => false
        };
    }
    
    /// <summary>
    /// Проверяет, является ли тип optional
    /// </summary>
    private bool IsTypeOptional(TypeNode type)
    {
        return type switch
        {
            OptionalTypeNode => true,
            SimpleTypeNode simple when simple.IsOptional => true,
            ArrayTypeNode array when array.IsOptional => true,
            GenericTypeNode generic when generic.IsOptional => true,
            DictionaryTypeNode dict when dict.IsOptional => true,
            IntersectionTypeNode intersection when intersection.IsOptional => true,
            _ => false
        };
    }

    private void RenderParameter(StringBuilder sb, ParameterDeclarationNode node)
    {
        var optional = node.IsRequired ? "" : "?";
        var type = RenderType(node.Type);
        var defaultValue = node.DefaultValue != null ? $" = {Visit(node.DefaultValue)}" : "";
        sb.Append($"{node.Name.Name}{optional}: {type}{defaultValue}");
    }

    private string RenderParameterDeclaration(ParameterDeclarationNode node)
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
        if (comment.CommentKind == CommentKind.JSDoc || comment.CommentKind == CommentKind.Xml)
        {
            CommentGenerator.GenerateJSDocComment(sb, indentLevel, comment.Text);
        }
        else if (comment.CommentKind == CommentKind.SingleLine)
        {
            CodeFormatter.AppendLine(sb, indentLevel, $"// {comment.Text}");
        }
        else if (comment.CommentKind == CommentKind.MultiLine)
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
            LiteralKind.String => $"\"{CodeEscapeHelper.EscapeTypeScriptString(node.Value?.ToString() ?? "")}\"",
            LiteralKind.Number => node.Value?.ToString() ?? "0",
            LiteralKind.Boolean => node.Value?.ToString()?.ToLowerInvariant() ?? "false",
            LiteralKind.Null => "null",
            LiteralKind.Character => $"'{node.Value}'",
            _ => node.Value?.ToString() ?? "null"
        };
    }

    // Реализация недостающих абстрактных методов BaseAstVisitor
    public override string VisitUsingStatement(UsingStatementNode node)
    {
        // TypeScript не имеет using statement в том же смысле, что C#
        // Можно использовать try-finally или просто игнорировать
        return VisitDefault(node);
    }
    
    public override string VisitMethodDeclaration(MethodDeclarationNode node)
    {
        // TypeScript использует FunctionDeclaration, а не MethodDeclaration
        // Преобразуем в функцию
        return VisitDefault(node);
    }
    
    public override string VisitThrowStatement(ThrowStatementNode node)
    {
        var exception = node.Expression != null ? Visit(node.Expression) : "";
        return $"throw {exception};";
    }
    
    public override string VisitVariableDeclarationStatement(VariableDeclarationStatementNode node)
    {
        var name = node.Name.Name;
        var type = node.Type != null ? $": {RenderType(node.Type)}" : "";
        var initializer = node.Initializer != null ? $" = {Visit(node.Initializer)}" : "";
        return $"const {name}{type}{initializer};";
    }
    
    public override string VisitTryCatchStatement(TryCatchStatementNode node)
    {
        var sb = new StringBuilder();
        var savedIndent = _indentLevel;
        CodeFormatter.AppendLine(sb, _indentLevel, "try {");
        _indentLevel++;
        foreach (var stmt in node.TryBlock.Statements)
        {
            var rendered = Visit(stmt);
            CodeFormatter.AppendLine(sb, _indentLevel, rendered);
        }
        _indentLevel = savedIndent;
        if (node.CatchClauses.Count > 0)
        {
            foreach (var catchClause in node.CatchClauses)
            {
                var catchCode = VisitCatchClause(catchClause);
                sb.Append(catchCode);
            }
        }
        if (node.FinallyBlock != null)
        {
            CodeFormatter.AppendLine(sb, _indentLevel, "finally {");
            _indentLevel++;
            foreach (var stmt in node.FinallyBlock.Statements)
            {
                var rendered = Visit(stmt);
                CodeFormatter.AppendLine(sb, _indentLevel, rendered);
            }
            _indentLevel = savedIndent;
            CodeFormatter.AppendLine(sb, _indentLevel, "}");
        }
        return sb.ToString();
    }
    
    public override string VisitAwaitExpression(AwaitExpressionNode node)
    {
        var expression = Visit(node.Expression);
        return $"await {expression}";
    }
    
    public override string VisitCatchClause(CatchClauseNode node)
    {
        var sb = new StringBuilder();
        var savedIndent = _indentLevel;
        var exceptionVar = node.ExceptionVariable != null ? node.ExceptionVariable.Name : "";
        var exceptionType = node.ExceptionType != null ? $": {RenderType(node.ExceptionType)}" : "";
        var exception = !string.IsNullOrEmpty(exceptionVar) ? $"({exceptionVar}{exceptionType})" : "";
        CodeFormatter.AppendLine(sb, _indentLevel, $"catch {exception} {{");
        _indentLevel++;
        foreach (var stmt in node.Body.Statements)
        {
            var rendered = Visit(stmt);
            CodeFormatter.AppendLine(sb, _indentLevel, rendered);
        }
        _indentLevel = savedIndent;
        CodeFormatter.AppendLine(sb, _indentLevel, "}");
        return sb.ToString();
    }
    
    public override string VisitCastExpression(CastExpressionNode node)
    {
        var expression = Visit(node.Expression);
        var targetType = RenderType(node.TargetType);
        // TypeScript использует as для type assertion
        return $"({expression} as {targetType})";
    }

    #endregion
}
