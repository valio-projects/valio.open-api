using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.FSharp.Ast.Nodes;
using ValIO.Generators.FSharp.Ast.Visitors;

namespace ValIO.Generators.FSharp.Ast.Renderers;

/// <summary>
/// Renders F# AST nodes to source text. This is the only place in the
/// F# plugin allowed to use a <see cref="StringBuilder"/> (AST-first rule).
/// </summary>
public sealed class FSharpAstRenderer : FSharpAstVisitorBase<string>, IFSharpAstRenderer
{
    private const int IndentSize = 4;

    public string Render(FSharpFileNode file) => VisitFSharpFile(file);

    public override string VisitFSharpFile(FSharpFileNode node)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"module {node.ModuleName}");

        if (node.Opens.Count > 0)
        {
            sb.AppendLine();
            foreach (var openName in node.Opens
                .OrderBy(o => o, StringComparer.Ordinal))
            {
                sb.AppendLine($"open {openName}");
            }
        }

        for (var i = 0; i < node.Declarations.Count; i++)
        {
            sb.AppendLine();
            var rendered = RenderDeclaration(node.Declarations[i]);
            sb.Append(rendered);
            if (!rendered.EndsWith("\n", StringComparison.Ordinal))
                sb.AppendLine();
        }

        return sb.ToString();
    }

    public override string VisitFSharpRecord(FSharpRecordNode node)
    {
        var sb = new StringBuilder();
        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent: 0, node.Documentation.Text);

        if (node.Fields.Count == 0)
        {
            sb.AppendLine($"type {node.Name.Name} = {{ }}");
            return sb.ToString();
        }

        sb.AppendLine($"type {node.Name.Name} = {{");
        foreach (var field in node.Fields)
        {
            sb.AppendLine($"{Indent(1)}{field.Name}: {RenderType(field.Type)}");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    public override string VisitFSharpDU(FSharpDuNode node)
    {
        var sb = new StringBuilder();
        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent: 0, node.Documentation.Text);

        if (node.Cases.Count == 0)
        {
            sb.AppendLine($"type {node.Name.Name} = unit");
            return sb.ToString();
        }

        sb.AppendLine($"type {node.Name.Name} =");
        foreach (var c in node.Cases)
        {
            if (c.Payload != null)
                sb.AppendLine($"{Indent(1)}| {c.Name} of {RenderType(c.Payload)}");
            else
                sb.AppendLine($"{Indent(1)}| {c.Name}");
        }
        return sb.ToString();
    }

    public override string VisitFSharpFunction(FSharpFunctionNode node)
    {
        var sb = new StringBuilder();
        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocComment(sb, indent: 0, node.Documentation.Text);

        var paramText = node.Parameters.Count == 0
            ? "()"
            : string.Join(" ", node.Parameters.Select(p => $"({p.Name}: {RenderType(p.Type)})"));

        var returnAnnotation = node.ReturnType != null
            ? $" : {RenderType(node.ReturnType)}"
            : string.Empty;

        if (node.BodyLines.Count == 0)
        {
            sb.AppendLine($"let {node.Name.Name} {paramText}{returnAnnotation} = ()");
            return sb.ToString();
        }

        sb.AppendLine($"let {node.Name.Name} {paramText}{returnAnnotation} =");
        foreach (var line in node.BodyLines)
        {
            if (string.IsNullOrEmpty(line))
            {
                sb.AppendLine();
                continue;
            }
            sb.AppendLine($"{Indent(1)}{line}");
        }
        return sb.ToString();
    }

    private string RenderDeclaration(DeclarationNode declaration) =>
        declaration switch
        {
            FSharpRecordNode r   => VisitFSharpRecord(r),
            FSharpDuNode d       => VisitFSharpDU(d),
            FSharpFunctionNode f => VisitFSharpFunction(f),
            _ => throw new NotSupportedException(
                $"Cannot render declaration of type {declaration.GetType().Name} in F# file"),
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
        // Special-case: list[T] renders post-fix as "T list" (F# idiom).
        if (string.Equals(node.Name.Name, "list", StringComparison.Ordinal) && node.TypeArguments.Count == 1)
            return $"{RenderType(node.TypeArguments[0])} list";
        // Map<K,V> uses angle brackets (matches the IR-mapper output for dictionaries).
        var args = string.Join(", ", node.TypeArguments.Select(RenderType));
        return $"{node.Name.Name}<{args}>";
    }

    public override string VisitNullableType(NullableTypeNode node) =>
        $"{RenderType(node.InnerType)} option";

    public override string VisitOptionalType(OptionalTypeNode node) =>
        $"{RenderType(node.InnerType)} option";

    public override string VisitArrayType(ArrayTypeNode node) =>
        $"{RenderType(node.ElementType)} array";

    public override string VisitUnionType(UnionTypeNode node) => "obj";

    public override string VisitIntersectionType(IntersectionTypeNode node) => "obj";

    public override string VisitDictionaryType(DictionaryTypeNode node) =>
        $"Map<{RenderType(node.KeyType)}, {RenderType(node.ValueType)}>";

    public override string VisitRawType(RawTypeNode node) => node.RawTypeString;

    private string RenderType(TypeNode type) => type.Accept(this);

    // ------------- Misc visitors -------------

    public override string VisitIdentifier(IdentifierNode node) => node.Name;

    public override string VisitComment(CommentNode node) => node.Text;

    public override string VisitAttribute(AttributeNode node) =>
        node.Arguments.Count > 0
            ? $"[<{node.Name.Name}({string.Join(", ", node.Arguments.Select(Visit))})>]"
            : $"[<{node.Name.Name}>]";

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
            UnaryOperator.LogicalNot => "not ",
            UnaryOperator.Negate     => "-",
            _ => string.Empty,
        };
        return $"{op}{Visit(node.Operand)}";
    }

    public override string VisitBinaryExpression(BinaryExpressionNode node)
    {
        var left = Visit(node.Left);
        var right = Visit(node.Right);
        if (node.Operator == BinaryOperator.Assign || node.IsAssignment)
            return $"{left} <- {right}";
        var op = node.Operator switch
        {
            BinaryOperator.Add                => "+",
            BinaryOperator.Subtract           => "-",
            BinaryOperator.Multiply           => "*",
            BinaryOperator.Divide             => "/",
            BinaryOperator.Equal              => "=",
            BinaryOperator.NotEqual           => "<>",
            BinaryOperator.LessThan           => "<",
            BinaryOperator.LessThanOrEqual    => "<=",
            BinaryOperator.GreaterThan        => ">",
            BinaryOperator.GreaterThanOrEqual => ">=",
            BinaryOperator.LogicalAnd         => "&&",
            BinaryOperator.LogicalOr          => "||",
            _ => throw new NotSupportedException($"Operator {node.Operator} not supported in F# renderer"),
        };
        return $"{left} {op} {right}";
    }

    public override string VisitConditionalExpression(ConditionalExpressionNode node) =>
        $"(if {Visit(node.Condition)} then {Visit(node.TrueExpression)} else {Visit(node.FalseExpression)})";

    public override string VisitAwaitExpression(AwaitExpressionNode node) =>
        $"let! _ = {Visit(node.Expression)}";

    public override string VisitCastExpression(CastExpressionNode node) =>
        $"({Visit(node.Expression)} :?> {RenderType(node.TargetType)})";

    public override string VisitReturnStatement(ReturnStatementNode node) =>
        node.Value == null ? "()" : Visit(node.Value);

    public override string VisitIfStatement(IfStatementNode node)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"if {Visit(node.Condition)} then");
        sb.AppendLine(Visit(node.ThenBlock));
        if (node.ElseBlock != null)
        {
            sb.AppendLine("else");
            sb.AppendLine(Visit(node.ElseBlock));
        }
        return sb.ToString();
    }

    public override string VisitForEachStatement(ForEachStatementNode node) =>
        $"for {node.Variable.Name} in {Visit(node.Collection)} do\n{Visit(node.Body)}";

    public override string VisitExpressionStatement(ExpressionStatementNode node) =>
        Visit(node.Expression);

    public override string VisitVariableDeclarationStatement(VariableDeclarationStatementNode node)
    {
        var initializer = node.Initializer != null ? $" = {Visit(node.Initializer)}" : "";
        return $"let {node.Name.Name}{initializer}";
    }

    public override string VisitThrowStatement(ThrowStatementNode node) =>
        node.Expression == null
            ? "raise (System.Exception())"
            : $"raise {Visit(node.Expression)}";

    public override string VisitUsingStatement(UsingStatementNode node) =>
        $"use _ = {Visit(node.Resource)}\n{Visit(node.Body)}";

    public override string VisitTryCatchStatement(TryCatchStatementNode node)
    {
        var sb = new StringBuilder();
        sb.AppendLine("try");
        sb.AppendLine(Visit(node.TryBlock));
        foreach (var c in node.CatchClauses)
            sb.AppendLine(Visit(c));
        if (node.FinallyBlock != null)
        {
            sb.AppendLine("finally");
            sb.AppendLine(Visit(node.FinallyBlock));
        }
        return sb.ToString();
    }

    public override string VisitCatchClause(CatchClauseNode node)
    {
        var exVar = node.ExceptionVariable?.Name ?? "ex";
        return $"with\n| {exVar} ->\n{Visit(node.Body)}";
    }

    public override string VisitTypeDeclaration(TypeDeclarationNode node) =>
        throw new NotSupportedException("TypeDeclarationNode not supported by F# renderer");

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
        if (value == null) return "null";
        return value switch
        {
            string s when kind == LiteralKind.String => $"\"{EscapeStringLiteral(s)}\"",
            bool b => b ? "true" : "false",
            string s => $"\"{EscapeStringLiteral(s)}\"",
            _ => value.ToString() ?? "null",
        };
    }

    private static string EscapeStringLiteral(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
         .Replace("\"", "\\\"", StringComparison.Ordinal)
         .Replace("\n", "\\n", StringComparison.Ordinal)
         .Replace("\r", "\\r", StringComparison.Ordinal);
}
