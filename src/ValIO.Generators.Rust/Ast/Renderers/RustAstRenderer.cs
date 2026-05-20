using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Rust.Ast.Nodes;
using ValIO.Generators.Rust.Ast.Visitors;

namespace ValIO.Generators.Rust.Ast.Renderers;

/// <summary>
/// Renders Rust AST nodes to source text. This is the only place in the
/// Rust plugin allowed to use a <see cref="StringBuilder"/> (AST-first rule).
/// </summary>
public sealed class RustAstRenderer : RustAstVisitorBase<string>, IRustAstRenderer
{
    private const int IndentSize = 4;

    public string Render(RustFileNode file) => VisitRustFile(file);

    public override string VisitRustFile(RustFileNode node)
    {
        var sb = new StringBuilder();

        if (node.ModuleDoc != null && !string.IsNullOrWhiteSpace(node.ModuleDoc.Text))
        {
            AppendModuleDoc(sb, node.ModuleDoc.Text);
            sb.AppendLine();
        }

        if (node.Uses.Count > 0)
        {
            foreach (var use in node.Uses
                .Distinct(StringComparer.Ordinal)
                .OrderBy(u => u, StringComparer.Ordinal))
            {
                sb.AppendLine($"use {use};");
            }
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

    public override string VisitRustStruct(RustStructNode node) =>
        RenderStruct(node, indent: 0);

    private string RenderStruct(RustStructNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);

        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent, node.Documentation.Text);

        if (node.Derives.Count > 0)
            sb.AppendLine($"{prefix}#[derive({string.Join(", ", node.Derives)})]");

        var visibility = string.IsNullOrEmpty(node.Visibility) ? string.Empty : node.Visibility + " ";

        if (node.Fields.Count == 0)
        {
            sb.AppendLine($"{prefix}{visibility}struct {node.Name.Name} {{}}");
            return sb.ToString();
        }

        sb.AppendLine($"{prefix}{visibility}struct {node.Name.Name} {{");

        var fieldIndent = indent + 1;
        for (var i = 0; i < node.Fields.Count; i++)
        {
            sb.Append(RenderField(node.Fields[i], fieldIndent));
        }

        sb.AppendLine($"{prefix}}}");
        return sb.ToString();
    }

    public override string VisitRustField(RustFieldNode node) =>
        RenderField(node, indent: 0);

    private string RenderField(RustFieldNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);

        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent, node.Documentation.Text);

        if (!string.IsNullOrEmpty(node.SerdeRename))
            sb.AppendLine($"{prefix}#[serde(rename = \"{EscapeStringLiteral(node.SerdeRename)}\")]");

        var visibility = string.IsNullOrEmpty(node.Visibility) ? string.Empty : node.Visibility + " ";
        var typeText = RenderType(node.Type);

        sb.AppendLine($"{prefix}{visibility}{node.Name.Name}: {typeText},");
        return sb.ToString();
    }

    public override string VisitRustEnum(RustEnumNode node) =>
        RenderEnum(node, indent: 0);

    private string RenderEnum(RustEnumNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);

        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent, node.Documentation.Text);

        if (node.Derives.Count > 0)
            sb.AppendLine($"{prefix}#[derive({string.Join(", ", node.Derives)})]");

        if (!string.IsNullOrEmpty(node.SerdeRenameAll))
            sb.AppendLine($"{prefix}#[serde(rename_all = \"{EscapeStringLiteral(node.SerdeRenameAll)}\")]");

        var visibility = string.IsNullOrEmpty(node.Visibility) ? string.Empty : node.Visibility + " ";

        if (node.Variants.Count == 0)
        {
            sb.AppendLine($"{prefix}{visibility}enum {node.Name.Name} {{}}");
            return sb.ToString();
        }

        sb.AppendLine($"{prefix}{visibility}enum {node.Name.Name} {{");

        var variantIndent = indent + 1;
        for (var i = 0; i < node.Variants.Count; i++)
        {
            sb.Append(RenderEnumVariant(node.Variants[i], variantIndent));
        }

        sb.AppendLine($"{prefix}}}");
        return sb.ToString();
    }

    public override string VisitRustEnumVariant(RustEnumVariantNode node) =>
        RenderEnumVariant(node, indent: 0);

    private string RenderEnumVariant(RustEnumVariantNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);
        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent, node.Documentation.Text);

        if (node.Payload != null)
            sb.AppendLine($"{prefix}{node.Name.Name}({RenderType(node.Payload)}),");
        else
            sb.AppendLine($"{prefix}{node.Name.Name},");
        return sb.ToString();
    }

    public override string VisitRustFunction(RustFunctionNode node) =>
        RenderFunction(node, indent: 0);

    private string RenderFunction(RustFunctionNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);

        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent, node.Documentation.Text);

        var visibility = string.IsNullOrEmpty(node.Visibility) ? string.Empty : node.Visibility + " ";
        var asyncMod = node.IsAsync ? "async " : string.Empty;
        var paramText = string.Join(", ", node.Parameters.Select(p => $"{p.Name}: {RenderType(p.Type)}"));
        var returnSuffix = node.ReturnType != null
            ? $" -> {RenderType(node.ReturnType)}"
            : string.Empty;

        var header = $"{prefix}{visibility}{asyncMod}fn {node.Name.Name}({paramText}){returnSuffix}";

        if (node.Body.Count == 0)
        {
            sb.AppendLine(header + " {}");
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

    private string RenderDeclaration(DeclarationNode declaration, int indent) =>
        declaration switch
        {
            RustStructNode s   => RenderStruct(s, indent),
            RustEnumNode e     => RenderEnum(e, indent),
            RustFunctionNode f => RenderFunction(f, indent),
            RustFieldNode fld  => RenderField(fld, indent),
            _ => throw new NotSupportedException(
                $"Cannot render declaration of type {declaration.GetType().Name} in Rust file")
        };

    // ------------- Type rendering -------------

    public override string VisitSimpleType(SimpleTypeNode node)
    {
        if (node.LiteralValue != null)
            return RenderLiteralValue(node.LiteralValue, LiteralKind.String);
        return node.Name.Name;
    }

    public override string VisitGenericType(GenericTypeNode node)
    {
        var args = string.Join(", ", node.TypeArguments.Select(RenderType));
        return $"{node.Name.Name}<{args}>";
    }

    public override string VisitNullableType(NullableTypeNode node)
    {
        var inner = RenderType(node.InnerType);
        return $"Option<{inner}>";
    }

    public override string VisitOptionalType(OptionalTypeNode node)
    {
        var inner = RenderType(node.InnerType);
        return $"Option<{inner}>";
    }

    public override string VisitArrayType(ArrayTypeNode node)
    {
        var element = RenderType(node.ElementType);
        return $"Vec<{element}>";
    }

    public override string VisitUnionType(UnionTypeNode node) =>
        // Rust has no native unions — fall back to serde_json::Value.
        "serde_json::Value";

    public override string VisitIntersectionType(IntersectionTypeNode node) =>
        "serde_json::Value";

    public override string VisitDictionaryType(DictionaryTypeNode node)
    {
        var key = RenderType(node.KeyType);
        var value = RenderType(node.ValueType);
        return $"std::collections::HashMap<{key}, {value}>";
    }

    public override string VisitRawType(RawTypeNode node) => node.RawTypeString;

    private string RenderType(TypeNode type) => type.Accept(this);

    // ------------- Misc visitors -------------

    public override string VisitIdentifier(IdentifierNode node) => node.Name;

    public override string VisitComment(CommentNode node) => node.Text;

    public override string VisitAttribute(AttributeNode node) =>
        node.Arguments.Count > 0
            ? $"#[{node.Name.Name}({string.Join(", ", node.Arguments.Select(Visit))})]"
            : $"#[{node.Name.Name}]";

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
            _ => throw new NotSupportedException($"Operator {node.Operator} not supported in Rust renderer")
        };
        return $"{left} {op} {right}";
    }

    public override string VisitConditionalExpression(ConditionalExpressionNode node) =>
        $"if {Visit(node.Condition)} {{ {Visit(node.TrueExpression)} }} else {{ {Visit(node.FalseExpression)} }}";

    public override string VisitAwaitExpression(AwaitExpressionNode node) =>
        $"{Visit(node.Expression)}.await";

    public override string VisitCastExpression(CastExpressionNode node) =>
        $"{Visit(node.Expression)} as {RenderType(node.TargetType)}";

    public override string VisitReturnStatement(ReturnStatementNode node) =>
        node.Value == null ? "return;" : $"return {Visit(node.Value)};";

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
        Visit(node.Expression) + ";";

    public override string VisitVariableDeclarationStatement(VariableDeclarationStatementNode node)
    {
        var initializer = node.Initializer != null ? $" = {Visit(node.Initializer)}" : "";
        return $"let {node.Name.Name}{initializer};";
    }

    public override string VisitThrowStatement(ThrowStatementNode node) =>
        node.Expression == null ? "panic!();" : $"panic!(\"{Visit(node.Expression)}\");";

    public override string VisitUsingStatement(UsingStatementNode node) =>
        // Rust has no using; rely on RAII / Drop traits.
        Visit(node.Body);

    public override string VisitTryCatchStatement(TryCatchStatementNode node)
    {
        // Rust uses Result<T, E> + ? operator; emit the try block as-is.
        return Visit(node.TryBlock);
    }

    public override string VisitCatchClause(CatchClauseNode node) => string.Empty;

    public override string VisitTypeDeclaration(TypeDeclarationNode node) =>
        throw new NotSupportedException("TypeDeclarationNode not supported by Rust renderer");

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

    private static void AppendModuleDoc(StringBuilder sb, string text)
    {
        var trimmed = text.Trim('\r', '\n');
        if (!trimmed.Contains('\n'))
        {
            sb.AppendLine($"//! {trimmed}");
            return;
        }
        foreach (var rawLine in trimmed.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
                sb.AppendLine("//!");
            else
                sb.AppendLine($"//! {line}");
        }
    }

    private static string RenderLiteralValue(object? value, LiteralKind? kind = null)
    {
        if (value == null) return "None";
        return value switch
        {
            string s when kind == LiteralKind.String => $"\"{EscapeStringLiteral(s)}\"",
            bool b => b ? "true" : "false",
            string s => $"\"{EscapeStringLiteral(s)}\"",
            _ => value.ToString() ?? "None"
        };
    }

    private static string EscapeStringLiteral(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
         .Replace("\"", "\\\"", StringComparison.Ordinal)
         .Replace("\n", "\\n", StringComparison.Ordinal)
         .Replace("\r", "\\r", StringComparison.Ordinal);
}
