using System.Collections.Immutable;
using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Ast.Nodes.UE;
using ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Renderers;

/// <summary>
/// Рендерер C++/UE AST в строковый код.
/// Visitor Pattern — каждый узел рендерится через свойства, без Metadata.
/// </summary>
public sealed class CppAstRenderer : CppAstVisitorBase<string>
{
    private int _indentLevel;
    private const string IndentChar = "\t";

    public string Render(AstNode node)
    {
        _indentLevel = 0;
        return Visit(node);
    }

    // ===================================================================
    // File-level nodes
    // ===================================================================

    public override string VisitCppHeaderFile(CppHeaderFileNode node)
    {
        var sb = new StringBuilder();
        if (node.HeaderComment != null)
        {
            sb.AppendLine(RenderBlockComment(node.HeaderComment.Text));
            sb.AppendLine();
        }
        if (node.PragmaOnce)
        {
            sb.AppendLine("#pragma once");
            sb.AppendLine();
        }
        foreach (var include in node.Includes)
            sb.AppendLine(Visit(include));
        if (node.Includes.Count > 0) sb.AppendLine();

        if (node.ForwardDeclarations.Count > 0)
        {
            sb.AppendLine(RenderSeparator("Forward declarations"));
            sb.AppendLine();
            foreach (var fwd in node.ForwardDeclarations)
                sb.AppendLine(Visit(fwd));
            sb.AppendLine();
        }
        foreach (var alias in node.UsingAliases)
            sb.AppendLine(Visit(alias));
        if (node.UsingAliases.Count > 0) sb.AppendLine();

        foreach (var decl in node.Declarations)
            sb.Append(Visit(decl));
        return sb.ToString();
    }

    public override string VisitCppSourceFile(CppSourceFileNode node)
    {
        var sb = new StringBuilder();
        if (node.HeaderComment != null)
        {
            sb.AppendLine(RenderBlockComment(node.HeaderComment.Text));
            sb.AppendLine();
        }
        foreach (var include in node.Includes)
            sb.AppendLine(Visit(include));
        if (node.Includes.Count > 0) sb.AppendLine();

        foreach (var section in node.Sections)
        {
            if (section.Members.Count == 0) continue;

            if (section.Title != null)
            {
                sb.AppendLine(RenderSeparator(section.Title));
                sb.AppendLine();
            }

            if (section.WrapInAnonymousNamespace)
            {
                sb.AppendLine("namespace");
                sb.AppendLine("{");
                _indentLevel++;
            }

            foreach (var member in section.Members)
            {
                sb.Append(Visit(member));
                sb.AppendLine();
            }

            if (section.WrapInAnonymousNamespace)
            {
                _indentLevel--;
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    public override string VisitCppIncludeDirective(CppIncludeDirectiveNode node)
    {
        return node.IsSystemInclude
            ? $"#include <{node.Path}>"
            : $"#include \"{node.Path}\"";
    }

    public override string VisitCppForwardDeclaration(CppForwardDeclarationNode node)
    {
        var keyword = node.DeclarationKind switch
        {
            CppForwardDeclarationKind.Class => "class",
            CppForwardDeclarationKind.Struct => "struct",
            CppForwardDeclarationKind.Enum => "enum class",
            _ => "class"
        };
        return $"{keyword} {node.Name.Name};";
    }

    public override string VisitCppUsingAlias(CppUsingAliasNode node)
    {
        return $"using {node.Alias.Name} = {RenderType(node.TargetType)};";
    }

    // ===================================================================
    // UE Declaration nodes
    // ===================================================================

    public override string VisitUStruct(UStructDeclarationNode node)
    {
        var sb = new StringBuilder();
        if (node.Documentation != null)
            sb.AppendLine(RenderDoxygenComment(node.Documentation.Text));
        sb.AppendLine(RenderUStructMacro(node.Specifiers));
        var baseStr = node.BaseStruct != null ? $" : public {node.BaseStruct.Name}" : "";
        sb.AppendLine($"struct {node.Name.Name}{baseStr}");
        sb.AppendLine("{");
        _indentLevel++;
        sb.AppendLine($"{Indent()}GENERATED_BODY()");
        if (node.SkippedProperties.Count > 0)
        {
            sb.AppendLine();
            foreach (var skipped in node.SkippedProperties)
            {
                var typeStr = RenderType(skipped.Type);
                var text = $"{typeStr} {skipped.PropertyName} — not supported: {skipped.Reason}";
                sb.AppendLine($"{Indent()}{RenderDoxygenComment(text)}");
            }
        }
        if (node.Fields.Count > 0) sb.AppendLine();
        foreach (var field in node.Fields)
            sb.Append(Visit(field));
        foreach (var method in node.InlineMethods)
        {
            if (method.Name.Name == "IsSuccess")
                sb.AppendLine($"{Indent()}bool IsSuccess() const {{ return StatusCode >= 200 && StatusCode < 300; }}");
        }
        _indentLevel--;
        sb.AppendLine("};");
        sb.AppendLine();
        return sb.ToString();
    }

    public override string VisitUPropertyField(UPropertyFieldNode node)
    {
        var sb = new StringBuilder();
        if (node.Documentation != null)
            sb.AppendLine($"{Indent()}{RenderDoxygenComment(node.Documentation.Text)}");
        if (!node.SkipUProperty)
            sb.AppendLine($"{Indent()}{RenderUPropertyMacro(node.Specifiers)}");
        var typeStr = RenderType(node.Type);
        var defaultStr = node.DefaultValue != null ? $" = {RenderDefaultValue(node.DefaultValue)}" : "";
        sb.AppendLine($"{Indent()}{typeStr} {node.Name.Name}{defaultStr};");
        sb.AppendLine();
        return sb.ToString();
    }

    public override string VisitUClass(UClassDeclarationNode node)
    {
        var sb = new StringBuilder();
        if (node.Documentation != null)
            sb.AppendLine(RenderDoxygenComment(node.Documentation.Text));
        sb.AppendLine(RenderUClassMacro(node.Specifiers));
        var apiStr = !string.IsNullOrEmpty(node.ApiMacro) ? $"{node.ApiMacro} " : "";
        var basesStr = node.BaseTypes.Count > 0
            ? $" : public {string.Join(", public ", node.BaseTypes.Select(RenderType))}"
            : "";
        sb.AppendLine($"class {apiStr}{node.Name.Name}{basesStr}");
        sb.AppendLine("{");
        _indentLevel++;
        sb.AppendLine($"{Indent()}GENERATED_BODY()");
        _indentLevel--;
        if (node.PublicMembers.Count > 0)
        {
            sb.AppendLine("public:");
            _indentLevel++;
            foreach (var member in node.PublicMembers)
                sb.Append(Visit(member));
            _indentLevel--;
        }
        if (node.ProtectedMembers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("protected:");
            _indentLevel++;
            foreach (var member in node.ProtectedMembers)
                sb.Append(Visit(member));
            _indentLevel--;
        }
        sb.AppendLine("};");
        sb.AppendLine();
        return sb.ToString();
    }

    public override string VisitUFunction(UFunctionDeclarationNode node)
    {
        var sb = new StringBuilder();
        if (node.Documentation != null)
            sb.AppendLine($"{Indent()}{RenderDoxygenComment(node.Documentation.Text)}");
        if (node.Specifiers.BlueprintCallable || node.Specifiers.BlueprintPure)
            sb.AppendLine($"{Indent()}{RenderUFunctionMacro(node.Specifiers)}");
        var modifiers = new List<string>();
        if (node.IsVirtual) modifiers.Add("virtual");
        if (node.IsStatic) modifiers.Add("static");
        var returnStr = node.ReturnType != null ? RenderType(node.ReturnType) : "void";
        var paramsStr = string.Join(", ", node.Parameters.Select(RenderParameterInline));
        var suffix = (node.IsConst ? " const" : "") + (node.IsOverride ? " override" : "");
        var mods = modifiers.Count > 0 ? string.Join(" ", modifiers) + " " : "";
        if (node.Body != null)
        {
            sb.Append($"{Indent()}{mods}{returnStr} {node.Name.Name}({paramsStr}){suffix} ");
            sb.Append(Visit(node.Body));
        }
        else
        {
            sb.AppendLine($"{Indent()}{mods}{returnStr} {node.Name.Name}({paramsStr}){suffix};");
        }
        sb.AppendLine();
        return sb.ToString();
    }

    public override string VisitUDelegate(UDelegateDeclarationNode node)
    {
        var macroSuffix = node.GetMacroParamSuffix();
        var paramsStr = string.Join(", ", node.Parameters.Select(p => $"{RenderType(p.Type)}, {p.Name.Name}"));
        var args = node.Parameters.Count > 0 ? $"{node.DelegateName.Name}, {paramsStr}" : node.DelegateName.Name;
        return $"DECLARE_DYNAMIC_MULTICAST_DELEGATE{macroSuffix}({args});\n";
    }

    public override string VisitUEnum(UEnumDeclarationNode node)
    {
        var sb = new StringBuilder();
        if (node.Documentation != null)
            sb.AppendLine(RenderDoxygenComment(node.Documentation.Text));
        var enumParts = new List<string> { "BlueprintType" };
        if (!string.IsNullOrEmpty(node.Category)) enumParts.Add($"Category = \"{node.Category}\"");
        sb.AppendLine($"UENUM({string.Join(", ", enumParts)})");
        sb.AppendLine($"enum class {node.Name.Name} : {RenderType(node.UnderlyingType)}");
        sb.AppendLine("{");
        _indentLevel++;
        for (var i = 0; i < node.Values.Count; i++)
        {
            var val = node.Values[i];
            var displayMeta = !string.IsNullOrEmpty(val.DisplayName)
                ? $" UMETA(DisplayName = \"{val.DisplayName}\")"
                : "";
            var comma = i < node.Values.Count - 1 ? "," : "";
            sb.AppendLine($"{Indent()}{val.Name.Name}{displayMeta}{comma}");
        }
        _indentLevel--;
        sb.AppendLine("};");
        sb.AppendLine();
        return sb.ToString();
    }

    // ===================================================================
    // C++ Function / Method nodes
    // ===================================================================

    public override string VisitCppFreeFunction(CppFreeFunctionNode node)
    {
        var sb = new StringBuilder();
        if (node.Documentation != null)
            sb.AppendLine($"{Indent()}{RenderDoxygenComment(node.Documentation.Text)}");
        var returnStr = RenderType(node.ReturnType);
        var paramsStr = string.Join(",\n" + Indent() + "\t", node.Parameters.Select(RenderParameterInline));
        if (node.IsDeclarationOnly)
        {
            var inlineParams = string.Join(", ", node.Parameters.Select(RenderParameterInline));
            sb.AppendLine($"{Indent()}{returnStr} {node.Name.Name}({inlineParams});");
        }
        else if (node.Body != null)
        {
            sb.AppendLine($"{Indent()}{returnStr} {node.Name.Name}({string.Join(", ", node.Parameters.Select(RenderParameterInline))})");
            sb.AppendLine($"{Indent()}{{");
            sb.AppendLine($"{Indent()}\t// TODO: implement");
            sb.AppendLine($"{Indent()}}}");
        }
        sb.AppendLine();
        return sb.ToString();
    }

    public override string VisitCppParameter(CppParameterNode node)
    {
        return RenderParameterInline(node);
    }

    public override string VisitCppMethodImplementation(CppMethodImplementationNode node)
    {
        var sb = new StringBuilder();
        var returnStr = node.ReturnType != null ? RenderType(node.ReturnType) + " " : "";
        var paramsStr = node.Parameters.Count > 0 ? string.Join(", ", node.Parameters.Select(RenderParameterInline)) : "";
        var constStr = node.IsConst ? " const" : "";
        sb.AppendLine($"{returnStr}{node.ClassName.Name}::{node.MethodName.Name}({paramsStr}){constStr}");
        sb.AppendLine("{");
        sb.AppendLine("\t// TODO: implement");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ===================================================================
    // Specialized implementation nodes (AST Body, no string building)
    // ===================================================================

    public override string VisitCppPathBuilderImplementation(CppPathBuilderImplementationNode node)
    {
        var sb = new StringBuilder();
        var paramsStr = string.Join(", ", node.Parameters.Select(RenderParameterInline));
        sb.AppendLine($"FString {node.ClassName.Name}::{node.MethodName.Name}({paramsStr})");
        sb.Append(Visit(node.Body));
        return sb.ToString();
    }

    public override string VisitCppParseFunction(CppParseFunctionNode node)
    {
        var sb = new StringBuilder();
        var paramsStr = string.Join(", ", node.Parameters.Select(RenderParameterInline));
        sb.AppendLine($"{Indent()}{RenderType(node.ReturnType)} {node.Name.Name}({paramsStr})");
        sb.Append(Visit(node.Body));
        return sb.ToString();
    }

    public override string VisitCppNativeApiFunction(CppNativeApiFunctionNode node)
    {
        var sb = new StringBuilder();
        var returnStr = RenderType(node.ReturnType);
        var paramsStr = string.Join(",\n\t", node.Parameters.Select(RenderParameterInline));
        sb.AppendLine($"{returnStr} {node.Name.Name}(");
        sb.AppendLine($"\t{paramsStr})");
        sb.Append(Visit(node.Body));
        return sb.ToString();
    }

    public override string VisitCppAsyncFactoryImplementation(CppAsyncFactoryImplementationNode node)
    {
        var sb = new StringBuilder();
        var returnStr = RenderType(node.ReturnType) + " ";
        var paramsStr = string.Join(", ", node.Parameters.Select(RenderParameterInline));
        sb.AppendLine($"{returnStr}{node.ClassName.Name}::{node.MethodName.Name}({paramsStr})");
        sb.Append(Visit(node.Body));
        return sb.ToString();
    }

    public override string VisitCppAsyncCompleteImplementation(CppAsyncCompleteImplementationNode node)
    {
        var sb = new StringBuilder();
        var returnStr = node.ReturnType != null ? RenderType(node.ReturnType) + " " : "";
        var paramsStr = string.Join(", ", node.Parameters.Select(RenderParameterInline));
        sb.AppendLine($"{returnStr}{node.ClassName.Name}::{node.MethodName.Name}({paramsStr})");
        sb.Append(Visit(node.Body));
        return sb.ToString();
    }

    public override string VisitCppAsyncActivateImplementation(CppAsyncActivateImplementationNode node)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"void {node.ClassName.Name}::Activate()");
        sb.Append(Visit(node.Body));
        return sb.ToString();
    }

    public override string VisitCppLambdaExpression(CppLambdaExpressionNode node)
    {
        var sb = new StringBuilder();
        var captures = node.Captures.Count > 0 ? string.Join(", ", node.Captures) : "";
        sb.Append($"[{captures}]");
        var pars = string.Join(", ", node.Parameters.Select(RenderParameterInline));
        sb.Append($"({pars}) ");
        sb.Append(Visit(node.Body));
        return sb.ToString();
    }

    // ===================================================================
    // Block & Statements (C++ rendering)
    // ===================================================================

    public override string VisitBlock(BlockNode node)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Indent()}{{");
        _indentLevel++;
        foreach (var stmt in node.Statements)
        {
            var rendered = Visit(stmt);
            if (!string.IsNullOrEmpty(rendered))
            {
                if (rendered.Contains('\n'))
                    sb.Append(rendered);
                else
                    sb.AppendLine($"{Indent()}{rendered}");
            }
        }
        _indentLevel--;
        sb.AppendLine($"{Indent()}}}");
        return sb.ToString();
    }

    public override string VisitVariableDeclarationStatement(VariableDeclarationStatementNode node)
    {
        var type = node.Type != null ? RenderType(node.Type) + " " : "";
        if (node.Initializer == null)
            return $"{type}{node.Name.Name};";
        var initRendered = TryRenderChainedExpression(node.Initializer) ?? Visit(node.Initializer);
        var init = $" = {initRendered}";
        var result = $"{type}{node.Name.Name}{init};";
        // VisitBlock добавляет Indent() только для однострочных; для многострочных — добавляем сами
        if (result.Contains('\n'))
        {
            result = Indent() + result;
            if (!result.EndsWith('\n'))
                result += Environment.NewLine;
        }
        return result;
    }

    /// <summary>
    /// Рендерит цепочку вызовов (Client->NewRequest(...).AddQueryParam(...).Build()) с переносами строк.
    /// Возвращает null если выражение не является цепочкой.
    /// </summary>
    private string? TryRenderChainedExpression(ExpressionNode expr)
    {
        var chain = new List<(string Member, IReadOnlyList<ExpressionNode> Args)>();
        if (!TryCollectChain(expr, chain, out var head) || head == null)
            return null;
        if (chain.Count == 0)
            return null;

        var sb = new StringBuilder();
        sb.Append(Visit(head));
        var continuationIndent = Indent() + IndentChar;
        foreach (var (member, args) in chain)
        {
            var argsStr = string.Join(", ", args.Select(Visit));
            sb.AppendLine();
            sb.Append($"{continuationIndent}.{member}({argsStr})");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Собирает цепочку вызовов: Call(MemberAccess(Call(...), Member), Args).
    /// </summary>
    private static bool TryCollectChain(
        ExpressionNode expr,
        List<(string Member, IReadOnlyList<ExpressionNode> Args)> chain,
        out ExpressionNode? head)
    {
        head = null;
        if (expr is not CallExpressionNode call || call.Target is not MemberAccessExpressionNode mac)
            return false;

        if (mac.Object is CallExpressionNode inner)
        {
            if (!TryCollectChain(inner, chain, out head))
                return false;
            chain.Add((mac.Member.Name, call.Arguments));
            return true;
        }

        head = call;
        return true;
    }

    public override string VisitReturnStatement(ReturnStatementNode node)
    {
        if (node.Value == null) return "return;";
        return $"return {Visit(node.Value)};";
    }

    public override string VisitExpressionStatement(ExpressionStatementNode node)
    {
        return $"{Visit(node.Expression)};";
    }

    public override string VisitIfStatement(IfStatementNode node)
    {
        var sb = new StringBuilder();
        var cond = Visit(node.Condition);
        sb.AppendLine($"{Indent()}if ({cond})");
        sb.Append(Visit(node.ThenBlock));
        if (node.ElseBlock != null && node.ElseBlock.Statements.Count > 0)
        {
            sb.AppendLine($"{Indent()}else");
            sb.Append(Visit(node.ElseBlock));
        }
        return sb.ToString();
    }

    // ===================================================================
    // Expressions (C++ rendering)
    // ===================================================================

    public override string VisitCallExpression(CallExpressionNode node)
    {
        var target = Visit(node.Target);
        var genericStr = !string.IsNullOrEmpty(node.GenericTypeName) ? $"<{node.GenericTypeName}>" : "";
        var args = string.Join(", ", node.Arguments.Select(Visit));
        return $"{target}{genericStr}({args})";
    }

    public override string VisitUnaryExpression(UnaryExpressionNode node)
    {
        var op = node.Operator switch
        {
            UnaryOperator.Dereference => "*",
            UnaryOperator.AddressOf => "&",
            UnaryOperator.LogicalNot => "!",
            UnaryOperator.Negate => "-",
            _ => ""
        };
        return $"{op}{Visit(node.Operand)}";
    }

    public override string VisitMemberAccessExpression(MemberAccessExpressionNode node)
    {
        var obj = Visit(node.Object);
        var op = node.UseScopeOperator ? "::" : (node.UseArrowOperator ? "->" : ".");
        return $"{obj}{op}{node.Member.Name}";
    }

    public override string VisitBinaryExpression(BinaryExpressionNode node)
    {
        var left = Visit(node.Left);
        var right = Visit(node.Right);
        if (node.Operator == BinaryOperator.Assign || node.IsAssignment)
            return $"{left} = {right}";
        var op = node.Operator switch
        {
            BinaryOperator.Add => "+",
            BinaryOperator.Subtract => "-",
            BinaryOperator.Multiply => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.Modulo => "%",
            BinaryOperator.Equal => "==",
            BinaryOperator.NotEqual => "!=",
            BinaryOperator.LessThan => "<",
            BinaryOperator.LessThanOrEqual => "<=",
            BinaryOperator.GreaterThan => ">",
            BinaryOperator.GreaterThanOrEqual => ">=",
            BinaryOperator.LogicalAnd => "&&",
            BinaryOperator.LogicalOr => "||",
            _ => "="
        };
        return $"{left} {op} {right}";
    }

    public override string VisitLiteralExpression(LiteralExpressionNode node)
    {
        return node.LiteralKind switch
        {
            LiteralKind.String => $"TEXT(\"{EscapeCppString(node.Value?.ToString() ?? "")}\")",
            LiteralKind.Number => node.Value?.ToString() ?? "0",
            LiteralKind.Boolean => (node.Value?.ToString() ?? "false").ToLowerInvariant(),
            LiteralKind.Null => "{}",
            _ => node.Value?.ToString() ?? "0"
        };
    }

    public override string VisitIdentifierExpression(IdentifierExpressionNode node) => node.Identifier.Name;

    // ===================================================================
    // Base AST visitor stubs / simple
    // ===================================================================

    public override string VisitIdentifier(IdentifierNode node) => node.Name;
    public override string VisitComment(CommentNode node) => RenderDoxygenComment(node.Text);
    public override string VisitAttribute(AttributeNode node) => "";
    public override string VisitImport(ImportNode node) => "";
    public override string VisitExport(ExportNode node) => "";

    public override string VisitSimpleType(SimpleTypeNode node) => node.Name.Name;
    public override string VisitGenericType(GenericTypeNode node)
    {
        var args = string.Join(", ", node.TypeArguments.Select(RenderType));
        return $"{node.Name.Name}<{args}>";
    }
    public override string VisitNullableType(NullableTypeNode node) => RenderType(node.InnerType);
    public override string VisitOptionalType(OptionalTypeNode node) => RenderType(node.InnerType);
    public override string VisitArrayType(ArrayTypeNode node) => $"TArray<{RenderType(node.ElementType)}>";
    public override string VisitUnionType(UnionTypeNode node) =>
        node.Types.Count > 0 ? RenderType(node.Types[0]) : "FString";
    public override string VisitIntersectionType(IntersectionTypeNode node) =>
        node.Types.Count > 0 ? RenderType(node.Types[0]) : "FString";
    public override string VisitDictionaryType(DictionaryTypeNode node) =>
        $"TMap<{RenderType(node.KeyType)}, {RenderType(node.ValueType)}>";
    public override string VisitConditionalExpression(ConditionalExpressionNode node) => "";
    public override string VisitAwaitExpression(AwaitExpressionNode node) => "";
    public override string VisitCastExpression(CastExpressionNode node) => "";

    public override string VisitForEachStatement(ForEachStatementNode node) => "";
    public override string VisitThrowStatement(ThrowStatementNode node) => "";
    public override string VisitUsingStatement(UsingStatementNode node) => "";
    public override string VisitTryCatchStatement(TryCatchStatementNode node) => "";
    public override string VisitCatchClause(CatchClauseNode node) => "";

    public override string VisitTypeDeclaration(TypeDeclarationNode node) => "";
    public override string VisitPropertyDeclaration(PropertyDeclarationNode node) => "";
    public override string VisitMethodDeclaration(MethodDeclarationNode node) => "";
    public override string VisitParameterDeclaration(ParameterDeclarationNode node) => "";
    public override string VisitEnumMemberDeclaration(EnumMemberDeclarationNode node) => "";
    public override string VisitEnumDeclaration(EnumDeclarationNode node) => "";
    public override string VisitInterfaceDeclaration(InterfaceDeclarationNode node) => "";

    public override string VisitHttpMethod(HttpMethodNode node) => "";
    public override string VisitRoute(RouteNode node) => "";
    public override string VisitPathParameter(PathParameterNode node) => "";
    public override string VisitQueryParameter(QueryParameterNode node) => "";
    public override string VisitHeaderParameter(HeaderParameterNode node) => "";
    public override string VisitRequestBody(RequestBodyNode node) => "";
    public override string VisitResponseType(ResponseTypeNode node) => "";

    // ===================================================================
    // Helpers
    // ===================================================================

    private string RenderType(TypeNode type) => Visit(type);

    private string RenderParameterInline(CppParameterNode param)
    {
        var constStr = param.IsConst ? "const " : "";
        var typeStr = RenderType(param.Type);
        var refStr = param.IsReference ? "&" : param.IsPointer ? "*" : "";
        var defaultStr = param.DefaultValue != null ? $" = {RenderDefaultValue(param.DefaultValue)}" : "";
        return $"{constStr}{typeStr}{refStr} {param.Name.Name}{defaultStr}";
    }

    private static string RenderDefaultValue(ExpressionNode expr)
    {
        if (expr is LiteralExpressionNode lit)
        {
            return lit.LiteralKind switch
            {
                LiteralKind.String => lit.Value?.ToString() ?? "",
                LiteralKind.Number => lit.Value?.ToString() ?? "0",
                LiteralKind.Boolean => lit.Value?.ToString()?.ToLowerInvariant() ?? "false",
                LiteralKind.Null => "{}",
                _ => lit.Value?.ToString() ?? ""
            };
        }
        return expr.ToString() ?? "";
    }

    private string RenderDoxygenComment(string text)
    {
        if (text.Contains('\n'))
        {
            var lines = text.Split('\n');
            var sb = new StringBuilder();
            sb.AppendLine($"{Indent()}/**");
            foreach (var line in lines)
                sb.AppendLine($"{Indent()} * {line.TrimEnd()}");
            sb.Append($"{Indent()} */");
            return sb.ToString();
        }
        return $"/** {text} */";
    }

    private static string RenderBlockComment(string text) => $"// {text}";

    private static string RenderUStructMacro(UStructSpecifiers spec)
    {
        var parts = new List<string> { "BlueprintType" };
        if (!string.IsNullOrEmpty(spec.Category)) parts.Add($"Category = \"{spec.Category}\"");
        foreach (var meta in spec.Meta)
            parts.Add($"meta = ({meta.Key} = \"{meta.Value}\")");
        return $"USTRUCT({string.Join(", ", parts)})";
    }

    private static string RenderUPropertyMacro(UPropertySpecifiers spec)
    {
        var parts = new List<string>();
        if (spec.EditAnywhere) parts.Add("EditAnywhere");
        if (spec.BlueprintReadWrite) parts.Add("BlueprintReadWrite");
        if (spec.BlueprintReadOnly) parts.Add("BlueprintReadOnly");
        if (spec.BlueprintAssignable) parts.Add("BlueprintAssignable");
        if (!string.IsNullOrEmpty(spec.Category)) parts.Add($"Category = \"{spec.Category}\"");
        if (spec.Meta.Count > 0)
        {
            var metaParts = spec.Meta.Select(kv => $"{kv.Key} = \"{kv.Value}\"");
            parts.Add($"meta = ({string.Join(", ", metaParts)})");
        }
        return $"UPROPERTY({string.Join(", ", parts)})";
    }

    private static string RenderUClassMacro(UClassSpecifiers spec)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(spec.Category)) parts.Add($"Category = \"{spec.Category}\"");
        if (spec.Meta.Count > 0)
        {
            var metaParts = spec.Meta.Select(kv => $"{kv.Key} = \"{kv.Value}\"");
            parts.Add($"meta = ({string.Join(", ", metaParts)})");
        }
        return $"UCLASS({string.Join(", ", parts)})";
    }

    private static string RenderUFunctionMacro(UFunctionSpecifiers spec)
    {
        var parts = new List<string>();
        if (spec.BlueprintCallable) parts.Add("BlueprintCallable");
        if (spec.BlueprintPure) parts.Add("BlueprintPure");
        if (!string.IsNullOrEmpty(spec.Category)) parts.Add($"Category = \"{spec.Category}\"");
        if (spec.Meta.Count > 0)
        {
            var metaParts = spec.Meta.Select(kv => $"{kv.Key} = \"{kv.Value}\"");
            parts.Add($"meta = ({string.Join(", ", metaParts)})");
        }
        return $"UFUNCTION({string.Join(", ", parts)})";
    }

    private static string RenderSeparator(string title) =>
        "// ---------------------------------------------------------------------------\n" +
        $"// {title}\n" +
        "// ---------------------------------------------------------------------------";

    private static string EscapeCppString(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    private string Indent() => string.Concat(Enumerable.Repeat(IndentChar, _indentLevel));
}
