using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Swift.Ast.Nodes;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Renderers;

/// <summary>
/// Renders Swift AST nodes to source text. This is the only place in the
/// Swift plugin allowed to use a <see cref="StringBuilder"/> (AST-first rule).
/// </summary>
public sealed class SwiftAstRenderer : SwiftAstVisitorBase<string>, ISwiftAstRenderer
{
    private const int IndentSize = 4;

    public string Render(SwiftFileNode file) => VisitSwiftFile(file);

    public override string VisitSwiftFile(SwiftFileNode node)
    {
        var sb = new StringBuilder();

        if (node.FileDoc != null && !string.IsNullOrWhiteSpace(node.FileDoc.Text))
        {
            AppendDocComment(sb, indent: 0, node.FileDoc.Text);
            sb.AppendLine();
        }

        if (node.Imports.Count > 0)
        {
            var imports = node.Imports
                .OrderBy(i => i.ModuleName, StringComparer.Ordinal)
                .ToList();
            foreach (var imp in imports)
                sb.AppendLine(VisitSwiftImport(imp));
            sb.AppendLine();
        }

        for (var i = 0; i < node.Declarations.Count; i++)
        {
            var rendered = RenderDeclaration(node.Declarations[i], indent: 0);
            sb.Append(rendered);
            if (!rendered.EndsWith("\n", StringComparison.Ordinal))
                sb.AppendLine();
            if (i < node.Declarations.Count - 1)
                sb.AppendLine();
        }

        return sb.ToString();
    }

    public override string VisitSwiftImport(SwiftImportNode node) =>
        $"import {node.ModuleName}";

    public override string VisitSwiftAttribute(SwiftAttributeNode node) =>
        node.Arguments.Count == 0
            ? $"@{node.Name}"
            : $"@{node.Name}({string.Join(", ", node.Arguments)})";

    public override string VisitSwiftStruct(SwiftStructDeclarationNode node) =>
        RenderStruct(node, indent: 0);

    private string RenderStruct(SwiftStructDeclarationNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);

        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent, node.Documentation.Text);

        foreach (var attr in node.SwiftAttributes)
            sb.AppendLine($"{prefix}{VisitSwiftAttribute(attr)}");

        var accessModifier = string.IsNullOrEmpty(node.AccessModifier) ? "" : node.AccessModifier + " ";
        var conformanceSuffix = node.Conformances.Count > 0
            ? ": " + string.Join(", ", node.Conformances)
            : "";

        if (node.Properties.Count == 0)
        {
            sb.AppendLine($"{prefix}{accessModifier}struct {node.Name.Name}{conformanceSuffix} {{");
            sb.AppendLine($"{prefix}}}");
            return sb.ToString();
        }

        sb.AppendLine($"{prefix}{accessModifier}struct {node.Name.Name}{conformanceSuffix} {{");

        var propIndent = indent + 1;
        for (var i = 0; i < node.Properties.Count; i++)
        {
            var prop = node.Properties[i];
            sb.Append(RenderProperty(prop, propIndent));
        }

        sb.AppendLine($"{prefix}}}");
        return sb.ToString();
    }

    public override string VisitSwiftClassDeclaration(SwiftClassDeclarationNode node) =>
        RenderClass(node, indent: 0);

    private string RenderClass(SwiftClassDeclarationNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);

        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent, node.Documentation.Text);

        foreach (var attr in node.SwiftAttributes)
            sb.AppendLine($"{prefix}{VisitSwiftAttribute(attr)}");

        var accessModifier = string.IsNullOrEmpty(node.AccessModifier) ? "" : node.AccessModifier + " ";
        var finalModifier = node.IsFinal ? "final " : "";
        var conformanceSuffix = node.Conformances.Count > 0
            ? ": " + string.Join(", ", node.Conformances)
            : "";

        sb.AppendLine($"{prefix}{accessModifier}{finalModifier}class {node.Name.Name}{conformanceSuffix} {{");

        var bodyIndent = indent + 1;

        // Render stored properties first.
        for (var i = 0; i < node.Properties.Count; i++)
        {
            var prop = node.Properties[i];
            sb.Append(RenderProperty(prop, bodyIndent));
        }

        // Then render members (initializers + functions), separating from properties with a blank line.
        if (node.Properties.Count > 0 && node.Members.Count > 0)
            sb.AppendLine();

        for (var i = 0; i < node.Members.Count; i++)
        {
            var rendered = RenderDeclaration(node.Members[i], bodyIndent);
            sb.Append(rendered);
            if (!rendered.EndsWith("\n", StringComparison.Ordinal))
                sb.AppendLine();
            if (i < node.Members.Count - 1)
                sb.AppendLine();
        }

        sb.AppendLine($"{prefix}}}");
        return sb.ToString();
    }

    public override string VisitSwiftProperty(SwiftPropertyNode node) =>
        RenderProperty(node, indent: 0);

    private string RenderProperty(SwiftPropertyNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);

        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent, node.Documentation.Text);

        foreach (var attr in node.SwiftAttributes)
            sb.AppendLine($"{prefix}{VisitSwiftAttribute(attr)}");

        var accessModifier = string.IsNullOrEmpty(node.AccessModifier) ? "" : node.AccessModifier + " ";
        var staticModifier = node.IsStatic ? "static " : "";
        var keyword = node.IsConstant ? "let" : "var";
        var typeText = RenderType(node.Type);
        var defaultClause = node.DefaultValueLiteral != null ? $" = {node.DefaultValueLiteral}" : "";

        sb.AppendLine($"{prefix}{accessModifier}{staticModifier}{keyword} {node.Name.Name}: {typeText}{defaultClause}");
        return sb.ToString();
    }

    public override string VisitSwiftEnumDeclaration(SwiftEnumDeclarationNode node) =>
        RenderEnum(node, indent: 0);

    private string RenderEnum(SwiftEnumDeclarationNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);

        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent, node.Documentation.Text);

        foreach (var attr in node.SwiftAttributes)
            sb.AppendLine($"{prefix}{VisitSwiftAttribute(attr)}");

        var accessModifier = string.IsNullOrEmpty(node.AccessModifier) ? "" : node.AccessModifier + " ";
        var inheritanceParts = new List<string>();
        if (!string.IsNullOrEmpty(node.RawType)) inheritanceParts.Add(node.RawType);
        inheritanceParts.AddRange(node.Conformances);
        var inheritanceSuffix = inheritanceParts.Count > 0 ? ": " + string.Join(", ", inheritanceParts) : "";

        sb.AppendLine($"{prefix}{accessModifier}enum {node.Name.Name}{inheritanceSuffix} {{");

        var memberIndent = indent + 1;
        for (var i = 0; i < node.Members.Count; i++)
        {
            sb.Append(RenderEnumMember(node.Members[i], memberIndent));
        }

        sb.AppendLine($"{prefix}}}");
        return sb.ToString();
    }

    public override string VisitSwiftEnumMember(SwiftEnumMemberNode node) =>
        RenderEnumMember(node, indent: 0);

    private string RenderEnumMember(SwiftEnumMemberNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);
        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent, node.Documentation.Text);
        var rawClause = node.RawValueLiteral != null ? $" = {node.RawValueLiteral}" : "";
        sb.AppendLine($"{prefix}case {node.Name}{rawClause}");
        return sb.ToString();
    }

    public override string VisitSwiftFunctionDeclaration(SwiftFunctionDeclarationNode node) =>
        RenderFunction(node, indent: 0);

    private string RenderFunction(SwiftFunctionDeclarationNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);

        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent, node.Documentation.Text);

        foreach (var attr in node.SwiftAttributes)
            sb.AppendLine($"{prefix}{VisitSwiftAttribute(attr)}");

        var modifiers = new List<string>();
        if (!string.IsNullOrEmpty(node.AccessModifier)) modifiers.Add(node.AccessModifier);
        if (node.IsStatic) modifiers.Add("static");
        var paramText = string.Join(", ", node.Parameters.Select(RenderParameter));

        var asyncThrowing = new List<string>();
        if (node.IsAsync) asyncThrowing.Add("async");
        if (node.IsThrowing) asyncThrowing.Add("throws");
        var asyncThrowingSuffix = asyncThrowing.Count == 0 ? "" : " " + string.Join(" ", asyncThrowing);

        var returnSuffix = node.ReturnType != null
            ? $" -> {RenderType(node.ReturnType)}"
            : "";

        string header;
        if (node.IsInitializer)
        {
            var modifierPrefix = modifiers.Count == 0 ? "" : string.Join(" ", modifiers) + " ";
            header = $"{prefix}{modifierPrefix}init({paramText}){asyncThrowingSuffix}{returnSuffix}";
        }
        else
        {
            modifiers.Add("func");
            var modifierPrefix = string.Join(" ", modifiers);
            header = $"{prefix}{modifierPrefix} {node.Name.Name}({paramText}){asyncThrowingSuffix}{returnSuffix}";
        }

        if (node.Body.Count == 0)
        {
            sb.AppendLine(header + " { }");
            return sb.ToString();
        }

        sb.AppendLine(header + " {");
        var blockIndent = indent + 1;
        foreach (var line in node.Body)
        {
            if (string.IsNullOrEmpty(line))
            {
                sb.AppendLine();
                continue;
            }
            sb.AppendLine($"{Indent(blockIndent)}{line}");
        }
        sb.AppendLine($"{prefix}}}");
        return sb.ToString();
    }

    public override string VisitSwiftParameter(SwiftParameterNode node) => RenderParameter(node);

    private string RenderParameter(SwiftParameterNode node)
    {
        var typeText = RenderType(node.Type);
        var defaultClause = node.DefaultValueLiteral != null ? $" = {node.DefaultValueLiteral}" : "";
        var labelPrefix = node.ExternalLabel != null && !string.Equals(node.ExternalLabel, node.Name.Name, StringComparison.Ordinal)
            ? node.ExternalLabel + " "
            : "";
        return $"{labelPrefix}{node.Name.Name}: {typeText}{defaultClause}";
    }

    private string RenderDeclaration(DeclarationNode declaration, int indent) =>
        declaration switch
        {
            SwiftStructDeclarationNode s   => RenderStruct(s, indent),
            SwiftClassDeclarationNode c    => RenderClass(c, indent),
            SwiftEnumDeclarationNode e     => RenderEnum(e, indent),
            SwiftFunctionDeclarationNode f => RenderFunction(f, indent),
            SwiftPropertyNode p            => RenderProperty(p, indent),
            _ => throw new NotSupportedException(
                $"Cannot render declaration of type {declaration.GetType().Name} in Swift file")
        };

    // ------------- Type rendering -------------

    public override string VisitSimpleType(SimpleTypeNode node)
    {
        if (node.LiteralValue != null)
            return RenderLiteralValue(node.LiteralValue, LiteralKind.String);
        return node.Name.Name + (node.IsNullable ? "?" : string.Empty);
    }

    public override string VisitGenericType(GenericTypeNode node)
    {
        var args = string.Join(", ", node.TypeArguments.Select(RenderType));
        var suffix = node.IsNullable ? "?" : string.Empty;
        return $"{node.Name.Name}<{args}>{suffix}";
    }

    public override string VisitNullableType(NullableTypeNode node)
    {
        var inner = RenderType(node.InnerType);
        return inner.EndsWith("?", StringComparison.Ordinal) ? inner : inner + "?";
    }

    public override string VisitOptionalType(OptionalTypeNode node) =>
        RenderType(node.InnerType);

    public override string VisitArrayType(ArrayTypeNode node)
    {
        var element = RenderType(node.ElementType);
        var suffix = node.IsNullable ? "?" : string.Empty;
        return $"[{element}]{suffix}";
    }

    public override string VisitUnionType(UnionTypeNode node) =>
        // Swift has no native unions — fall back to AnyCodable.
        "AnyCodable";

    public override string VisitIntersectionType(IntersectionTypeNode node) =>
        "AnyCodable";

    public override string VisitDictionaryType(DictionaryTypeNode node)
    {
        var key = RenderType(node.KeyType);
        var value = RenderType(node.ValueType);
        var suffix = node.IsNullable ? "?" : string.Empty;
        return $"[{key}: {value}]{suffix}";
    }

    public override string VisitRawType(RawTypeNode node) => node.RawTypeString;

    private string RenderType(TypeNode type) => type.Accept(this);

    // ------------- Misc visitors -------------

    public override string VisitIdentifier(IdentifierNode node) => node.Name;

    public override string VisitComment(CommentNode node) => node.Text;

    public override string VisitAttribute(AttributeNode node) =>
        node.Arguments.Count > 0
            ? $"@{node.Name.Name}({string.Join(", ", node.Arguments.Select(Visit))})"
            : $"@{node.Name.Name}";

    public override string VisitBlock(BlockNode node) =>
        string.Join("\n", node.Statements.Select(Visit));

    public override string VisitImport(ImportNode node) => string.Empty;
    public override string VisitExport(ExportNode node) => string.Empty;

    public override string VisitLiteralExpression(LiteralExpressionNode node) =>
        RenderLiteralValue(node.Value, node.LiteralKind);

    public override string VisitIdentifierExpression(IdentifierExpressionNode node) =>
        node.Identifier.Name;

    public override string VisitCallExpression(CallExpressionNode node)
    {
        var target = Visit(node.Target);
        var args = string.Join(", ", node.Arguments.Select(Visit));
        return $"{target}({args})";
    }

    public override string VisitMemberAccessExpression(MemberAccessExpressionNode node) =>
        $"{Visit(node.Object)}.{node.Member.Name}";

    public override string VisitUnaryExpression(UnaryExpressionNode node)
    {
        var op = node.Operator switch
        {
            UnaryOperator.LogicalNot => "!",
            UnaryOperator.Negate => "-",
            _ => ""
        };
        return $"{op}{Visit(node.Operand)}";
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
            BinaryOperator.Equal => "==",
            BinaryOperator.NotEqual => "!=",
            BinaryOperator.LessThan => "<",
            BinaryOperator.LessThanOrEqual => "<=",
            BinaryOperator.GreaterThan => ">",
            BinaryOperator.GreaterThanOrEqual => ">=",
            BinaryOperator.LogicalAnd => "&&",
            BinaryOperator.LogicalOr => "||",
            _ => throw new NotSupportedException($"Operator {node.Operator} not supported in Swift renderer")
        };
        return $"{left} {op} {right}";
    }

    public override string VisitConditionalExpression(ConditionalExpressionNode node) =>
        $"{Visit(node.Condition)} ? {Visit(node.TrueExpression)} : {Visit(node.FalseExpression)}";

    public override string VisitAwaitExpression(AwaitExpressionNode node) =>
        $"await {Visit(node.Expression)}";

    public override string VisitCastExpression(CastExpressionNode node) =>
        $"{Visit(node.Expression)} as! {RenderType(node.TargetType)}";

    public override string VisitReturnStatement(ReturnStatementNode node) =>
        node.Value == null ? "return" : $"return {Visit(node.Value)}";

    public override string VisitIfStatement(IfStatementNode node)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"if {Visit(node.Condition)} {{");
        sb.AppendLine(Visit(node.ThenBlock));
        sb.Append("}");
        if (node.ElseBlock != null)
        {
            sb.AppendLine(" else {");
            sb.AppendLine(Visit(node.ElseBlock));
            sb.Append("}");
        }
        return sb.ToString();
    }

    public override string VisitForEachStatement(ForEachStatementNode node) =>
        $"for {node.Variable.Name} in {Visit(node.Collection)} {{\n{Visit(node.Body)}\n}}";

    public override string VisitExpressionStatement(ExpressionStatementNode node) =>
        Visit(node.Expression);

    public override string VisitVariableDeclarationStatement(VariableDeclarationStatementNode node)
    {
        var initializer = node.Initializer != null ? $" = {Visit(node.Initializer)}" : "";
        return $"let {node.Name.Name}{initializer}";
    }

    public override string VisitThrowStatement(ThrowStatementNode node) =>
        node.Expression == null ? "throw NSError()" : $"throw {Visit(node.Expression)}";

    public override string VisitUsingStatement(UsingStatementNode node) =>
        // Swift has no exact equivalent — emit a defer-block stand-in.
        $"defer {{ {Visit(node.Resource)} }}\n{Visit(node.Body)}";

    public override string VisitTryCatchStatement(TryCatchStatementNode node)
    {
        var sb = new StringBuilder();
        sb.AppendLine("do {");
        sb.AppendLine(Visit(node.TryBlock));
        sb.Append("}");
        foreach (var c in node.CatchClauses)
        {
            sb.AppendLine(" " + Visit(c));
        }
        if (node.FinallyBlock != null)
        {
            sb.AppendLine(" // finally — emitted as defer earlier in the block");
        }
        return sb.ToString();
    }

    public override string VisitCatchClause(CatchClauseNode node)
    {
        var exVar = node.ExceptionVariable?.Name ?? "error";
        return $"catch let {exVar} {{\n{Visit(node.Body)}\n}}";
    }

    public override string VisitTypeDeclaration(TypeDeclarationNode node) =>
        throw new NotSupportedException("TypeDeclarationNode not supported by Swift renderer");

    public override string VisitPropertyDeclaration(PropertyDeclarationNode node) => VisitDefault(node);
    public override string VisitMethodDeclaration(MethodDeclarationNode node) => VisitDefault(node);
    public override string VisitParameterDeclaration(ParameterDeclarationNode node) => VisitDefault(node);
    public override string VisitEnumMemberDeclaration(EnumMemberDeclarationNode node) => VisitDefault(node);
    public override string VisitEnumDeclaration(EnumDeclarationNode node) => VisitDefault(node);
    public override string VisitInterfaceDeclaration(InterfaceDeclarationNode node) => VisitDefault(node);

    // ------------- Helpers -------------

    private static string Indent(int level) =>
        level <= 0 ? string.Empty : new string(' ', level * IndentSize);

    private static void AppendDocComment(StringBuilder sb, int indent, string text)
    {
        var prefix = Indent(indent);
        var trimmed = text.Trim('\r', '\n');
        if (!trimmed.Contains('\n'))
        {
            sb.AppendLine($"{prefix}/// {trimmed}");
            return;
        }

        foreach (var rawLine in trimmed.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
                sb.AppendLine($"{prefix}///");
            else
                sb.AppendLine($"{prefix}/// {line}");
        }
    }

    private static string RenderLiteralValue(object? value, LiteralKind? kind = null)
    {
        if (value == null) return "nil";
        return value switch
        {
            string s when kind == LiteralKind.String => $"\"{EscapeStringLiteral(s)}\"",
            bool b => b ? "true" : "false",
            string s => $"\"{EscapeStringLiteral(s)}\"",
            _ => value.ToString() ?? "nil"
        };
    }

    private static string EscapeStringLiteral(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
         .Replace("\"", "\\\"", StringComparison.Ordinal)
         .Replace("\n", "\\n", StringComparison.Ordinal)
         .Replace("\r", "\\r", StringComparison.Ordinal);
}
