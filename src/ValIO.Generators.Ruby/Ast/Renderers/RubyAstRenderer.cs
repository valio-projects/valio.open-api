using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Ruby.Ast.Nodes;
using ValIO.Generators.Ruby.Ast.Visitors;

namespace ValIO.Generators.Ruby.Ast.Renderers;

/// <summary>
/// Renders Ruby AST nodes to source text. This is the only place in the Ruby
/// plugin allowed to use a <see cref="StringBuilder"/> (AST-first rule).
/// </summary>
public sealed class RubyAstRenderer : RubyAstVisitorBase<string>, IRubyAstRenderer
{
    private const int IndentSize = 2;

    public string Render(RubyFileNode file) => VisitRubyFile(file);

    public override string VisitRubyFile(RubyFileNode node)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# frozen_string_literal: true");
        sb.AppendLine();

        if (node.FileDoc != null && !string.IsNullOrWhiteSpace(node.FileDoc.Text))
        {
            AppendDocComment(sb, indent: 0, node.FileDoc.Text);
            sb.AppendLine();
        }

        if (node.Requires.Count > 0)
        {
            var requires = node.Requires
                .Distinct(StringComparer.Ordinal)
                .OrderBy(r => r, StringComparer.Ordinal)
                .ToList();
            foreach (var r in requires)
                sb.AppendLine($"require '{EscapeSingleQuoted(r)}'");
            sb.AppendLine();
        }

        for (var i = 0; i < node.Classes.Count; i++)
        {
            var rendered = RenderClass(node.Classes[i], indent: 0);
            sb.Append(rendered);
            if (!rendered.EndsWith("\n", StringComparison.Ordinal))
                sb.AppendLine();
            if (i < node.Classes.Count - 1)
                sb.AppendLine();
        }

        return sb.ToString();
    }

    public override string VisitRubyClass(RubyClassNode node) => RenderClass(node, indent: 0);

    private string RenderClass(RubyClassNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);

        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent, node.Documentation.Text);

        var parent = string.IsNullOrEmpty(node.ParentClass) ? "" : $" < {node.ParentClass}";
        sb.AppendLine($"{prefix}class {node.Name.Name}{parent}");

        var bodyIndent = indent + 1;

        if (node.Members.Count == 0)
        {
            sb.AppendLine($"{prefix}end");
            return sb.ToString();
        }

        for (var i = 0; i < node.Members.Count; i++)
        {
            var rendered = RenderDeclaration(node.Members[i], bodyIndent);
            sb.Append(rendered);
            if (!rendered.EndsWith("\n", StringComparison.Ordinal))
                sb.AppendLine();
            if (i < node.Members.Count - 1)
                sb.AppendLine();
        }

        sb.AppendLine($"{prefix}end");
        return sb.ToString();
    }

    public override string VisitRubyAttribute(RubyAttributeNode node) => RenderAttribute(node, indent: 0);

    private string RenderAttribute(RubyAttributeNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);

        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent, node.Documentation.Text);

        if (node.Type != null)
        {
            var typeText = RenderType(node.Type);
            sb.AppendLine($"{prefix}# @return [{typeText}]");
        }

        sb.AppendLine($"{prefix}attr_accessor :{node.Name.Name}");
        return sb.ToString();
    }

    public override string VisitRubyMethod(RubyMethodNode node) => RenderMethod(node, indent: 0);

    private string RenderMethod(RubyMethodNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);

        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent, node.Documentation.Text);

        var paramText = node.Parameters.Count == 0 ? "" : "(" + string.Join(", ", node.Parameters) + ")";
        sb.AppendLine($"{prefix}def {node.Name.Name}{paramText}");

        var bodyIndent = indent + 1;
        if (node.Body.Count == 0)
        {
            // Ruby methods with empty bodies are syntactically valid but render
            // a comment placeholder so the result is greppable.
            sb.AppendLine($"{Indent(bodyIndent)}# (no body)");
        }
        else
        {
            foreach (var line in node.Body)
            {
                if (string.IsNullOrEmpty(line))
                {
                    sb.AppendLine();
                    continue;
                }
                sb.AppendLine($"{Indent(bodyIndent)}{line}");
            }
        }

        sb.AppendLine($"{prefix}end");
        return sb.ToString();
    }

    private string RenderDeclaration(DeclarationNode declaration, int indent) =>
        declaration switch
        {
            RubyClassNode c     => RenderClass(c, indent),
            RubyAttributeNode a => RenderAttribute(a, indent),
            RubyMethodNode m    => RenderMethod(m, indent),
            RubyConstantNode k  => RenderConstant(k, indent),
            _ => throw new NotSupportedException(
                $"Cannot render declaration of type {declaration.GetType().Name} in Ruby file")
        };

    private string RenderConstant(RubyConstantNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);
        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent, node.Documentation.Text);
        sb.AppendLine($"{prefix}{node.Name.Name} = {node.ValueLiteral}");
        return sb.ToString();
    }

    // ------------- Type rendering (used only by YARD comments) -------------

    public override string VisitSimpleType(SimpleTypeNode node) => node.Name.Name;

    public override string VisitGenericType(GenericTypeNode node)
    {
        var args = string.Join(", ", node.TypeArguments.Select(RenderType));
        return $"{node.Name.Name}<{args}>";
    }

    public override string VisitNullableType(NullableTypeNode node) => RenderType(node.InnerType);

    public override string VisitOptionalType(OptionalTypeNode node) => RenderType(node.InnerType);

    public override string VisitArrayType(ArrayTypeNode node) => $"Array<{RenderType(node.ElementType)}>";

    public override string VisitUnionType(UnionTypeNode node) => "Object";

    public override string VisitIntersectionType(IntersectionTypeNode node) => "Object";

    public override string VisitDictionaryType(DictionaryTypeNode node) =>
        $"Hash<{RenderType(node.KeyType)}, {RenderType(node.ValueType)}>";

    public override string VisitRawType(RawTypeNode node) => node.RawTypeString;

    private string RenderType(TypeNode type) => type.Accept(this);

    // ------------- Misc visitors (mostly default; not used by the Ruby builders) -------------

    public override string VisitIdentifier(IdentifierNode node) => node.Name;
    public override string VisitComment(CommentNode node) => node.Text;
    public override string VisitAttribute(AttributeNode node) => node.Name.Name;
    public override string VisitBlock(BlockNode node) => string.Join("\n", node.Statements.Select(Visit));
    public override string VisitImport(ImportNode node) => string.Empty;
    public override string VisitExport(ExportNode node) => string.Empty;

    public override string VisitLiteralExpression(LiteralExpressionNode node) =>
        RenderLiteralValue(node.Value, node.LiteralKind);

    public override string VisitIdentifierExpression(IdentifierExpressionNode node) => node.Identifier.Name;

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
            _ => throw new NotSupportedException($"Operator {node.Operator} not supported in Ruby renderer")
        };
        return $"{left} {op} {right}";
    }

    public override string VisitConditionalExpression(ConditionalExpressionNode node) =>
        $"{Visit(node.Condition)} ? {Visit(node.TrueExpression)} : {Visit(node.FalseExpression)}";

    public override string VisitAwaitExpression(AwaitExpressionNode node) => Visit(node.Expression);
    public override string VisitCastExpression(CastExpressionNode node) => Visit(node.Expression);

    public override string VisitReturnStatement(ReturnStatementNode node) =>
        node.Value == null ? "return" : $"return {Visit(node.Value)}";

    public override string VisitIfStatement(IfStatementNode node)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"if {Visit(node.Condition)}");
        sb.AppendLine(Visit(node.ThenBlock));
        if (node.ElseBlock != null)
        {
            sb.AppendLine("else");
            sb.AppendLine(Visit(node.ElseBlock));
        }
        sb.Append("end");
        return sb.ToString();
    }

    public override string VisitForEachStatement(ForEachStatementNode node) =>
        $"{Visit(node.Collection)}.each do |{node.Variable.Name}|\n{Visit(node.Body)}\nend";

    public override string VisitExpressionStatement(ExpressionStatementNode node) => Visit(node.Expression);

    public override string VisitVariableDeclarationStatement(VariableDeclarationStatementNode node)
    {
        var initializer = node.Initializer != null ? $" = {Visit(node.Initializer)}" : "";
        return $"{node.Name.Name}{initializer}";
    }

    public override string VisitThrowStatement(ThrowStatementNode node) =>
        node.Expression == null ? "raise" : $"raise {Visit(node.Expression)}";

    public override string VisitUsingStatement(UsingStatementNode node) => Visit(node.Body);

    public override string VisitTryCatchStatement(TryCatchStatementNode node)
    {
        var sb = new StringBuilder();
        sb.AppendLine("begin");
        sb.AppendLine(Visit(node.TryBlock));
        foreach (var c in node.CatchClauses)
        {
            sb.AppendLine(Visit(c));
        }
        sb.Append("end");
        return sb.ToString();
    }

    public override string VisitCatchClause(CatchClauseNode node)
    {
        var exVar = node.ExceptionVariable?.Name ?? "e";
        return $"rescue => {exVar}\n{Visit(node.Body)}";
    }

    public override string VisitTypeDeclaration(TypeDeclarationNode node) =>
        throw new NotSupportedException("TypeDeclarationNode not supported by Ruby renderer");

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
            sb.AppendLine($"{prefix}# {trimmed}");
            return;
        }

        foreach (var rawLine in trimmed.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
                sb.AppendLine($"{prefix}#");
            else
                sb.AppendLine($"{prefix}# {line}");
        }
    }

    private static string RenderLiteralValue(object? value, LiteralKind? kind = null)
    {
        if (value == null) return "nil";
        return value switch
        {
            string s when kind == LiteralKind.String => $"'{EscapeSingleQuoted(s)}'",
            bool b => b ? "true" : "false",
            string s => $"'{EscapeSingleQuoted(s)}'",
            _ => value.ToString() ?? "nil"
        };
    }

    private static string EscapeSingleQuoted(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
         .Replace("'", "\\'", StringComparison.Ordinal);
}
