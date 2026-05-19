using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Utilities;
using ValIO.Generators.Php.Ast.Nodes;
using ValIO.Generators.Php.Ast.Visitors;

namespace ValIO.Generators.Php.Ast.Renderers;

/// <summary>
/// Рендерер AST для генерации PHP-кода. PHP 8.2+: <c>final class</c>, <c>readonly</c>-промоция, backed enums.
/// </summary>
public sealed class PhpAstRenderer : PhpAstVisitorBase<string>, IPhpAstRenderer
{
    private const string Indent = "    ";

    public string Render(PhpSourceFileNode node)
    {
        var sb = new StringBuilder();
        sb.Append("<?php\n\n");
        sb.Append("declare(strict_types=1);\n\n");

        if (!string.IsNullOrWhiteSpace(node.Namespace))
        {
            sb.Append($"namespace {node.Namespace};\n\n");
        }

        if (node.Uses.Count > 0)
        {
            foreach (var use in node.Uses.OrderBy(u => u.FullyQualifiedName, StringComparer.Ordinal))
            {
                sb.Append(RenderUseLine(use));
            }
            sb.Append('\n');
        }

        for (int i = 0; i < node.Declarations.Count; i++)
        {
            var decl = node.Declarations[i];
            sb.Append(RenderDeclaration(decl));
            if (i < node.Declarations.Count - 1)
                sb.Append('\n');
        }

        return sb.ToString();
    }

    public override string VisitPhpSourceFile(PhpSourceFileNode node) => Render(node);

    public override string VisitPhpUseStatement(PhpUseStatementNode node) => RenderUseLine(node);

    public override string VisitPhpClassDeclaration(PhpClassDeclarationNode node) => RenderClass(node);

    public override string VisitPhpConstructor(PhpConstructorDeclarationNode node) => RenderConstructor(node, 1);

    public override string VisitEnumDeclaration(EnumDeclarationNode node) => RenderEnum(node);

    public override string VisitInterfaceDeclaration(InterfaceDeclarationNode node) => RenderInterface(node);

    public override string VisitMethodDeclaration(MethodDeclarationNode node) => RenderMethod(node, 1);

    public override string VisitParameterDeclaration(ParameterDeclarationNode node) => RenderParameter(node);

    public override string VisitPropertyDeclaration(PropertyDeclarationNode node) => RenderClassProperty(node, 1);

    public override string VisitEnumMemberDeclaration(EnumMemberDeclarationNode node) => RenderEnumCase(node, 1);

    public override string VisitTypeDeclaration(TypeDeclarationNode node) => VisitDefault(node);

    public override string VisitIdentifier(IdentifierNode node) => node.Name;

    public override string VisitAttribute(AttributeNode node) => $"#[{node.Name.Name}]";

    public override string VisitComment(CommentNode node) => $"// {node.Text}";

    public override string VisitImport(ImportNode node) => "";
    public override string VisitExport(ExportNode node) => Visit(node.Declaration);

    // Типы --------------------------------------------------------------------------

    public override string VisitSimpleType(SimpleTypeNode node) => node.Name.Name;

    public override string VisitGenericType(GenericTypeNode node)
    {
        // PHP не имеет рантайм-generics, отдаём имя без аргументов; PHPDoc печатается отдельно.
        return node.Name.Name;
    }

    public override string VisitNullableType(NullableTypeNode node)
    {
        var inner = Visit(node.InnerType);
        return inner.StartsWith('?') ? inner : $"?{inner}";
    }

    public override string VisitOptionalType(OptionalTypeNode node) => Visit(node.InnerType);

    public override string VisitArrayType(ArrayTypeNode node) => "array";

    public override string VisitDictionaryType(DictionaryTypeNode node) => "array";

    public override string VisitUnionType(UnionTypeNode node) =>
        string.Join("|", node.Types.Select(Visit));

    public override string VisitIntersectionType(IntersectionTypeNode node) =>
        string.Join("&", node.Types.Select(Visit));

    // Выражения ---------------------------------------------------------------------

    public override string VisitLiteralExpression(LiteralExpressionNode node) => node.LiteralKind switch
    {
        LiteralKind.String => $"'{EscapeSingleQuoted(node.Value?.ToString() ?? string.Empty)}'",
        LiteralKind.Number => node.Value?.ToString() ?? "0",
        LiteralKind.Boolean => (node.Value is bool b && b) ? "true" : "false",
        LiteralKind.Null => "null",
        LiteralKind.Character => $"'{EscapeSingleQuoted(node.Value?.ToString() ?? string.Empty)}'",
        _ => node.Value?.ToString() ?? "null",
    };

    public override string VisitIdentifierExpression(IdentifierExpressionNode node) => node.Identifier.Name;

    public override string VisitCallExpression(CallExpressionNode node)
    {
        var prefix = node.IsNewExpression ? "new " : "";
        var target = Visit(node.Target);
        var args = string.Join(", ", node.Arguments.Select(Visit));
        return $"{prefix}{target}({args})";
    }

    public override string VisitMemberAccessExpression(MemberAccessExpressionNode node)
    {
        var target = Visit(node.Object);
        return $"{target}->{node.Member.Name}";
    }

    public override string VisitBinaryExpression(BinaryExpressionNode node)
    {
        var op = node.Operator switch
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
            BinaryOperator.Assign => "=",
            BinaryOperator.NullishCoalescing => "??",
            _ => throw new NotSupportedException($"Binary operator {node.Operator} not supported for PHP"),
        };
        return $"({Visit(node.Left)} {op} {Visit(node.Right)})";
    }

    public override string VisitConditionalExpression(ConditionalExpressionNode node) =>
        $"({Visit(node.Condition)} ? {Visit(node.TrueExpression)} : {Visit(node.FalseExpression)})";

    public override string VisitAwaitExpression(AwaitExpressionNode node) => Visit(node.Expression);

    public override string VisitCastExpression(CastExpressionNode node)
    {
        var type = Visit(node.TargetType);
        return $"({type}) {Visit(node.Expression)}";
    }

    // Statements --------------------------------------------------------------------

    public override string VisitBlock(BlockNode node)
    {
        var sb = new StringBuilder();
        sb.Append("{\n");
        foreach (var stmt in node.Statements)
        {
            sb.Append(Indent);
            sb.Append(Visit(stmt));
            sb.Append('\n');
        }
        sb.Append("}");
        return sb.ToString();
    }

    public override string VisitReturnStatement(ReturnStatementNode node) =>
        node.Value == null ? "return;" : $"return {Visit(node.Value)};";

    public override string VisitExpressionStatement(ExpressionStatementNode node) =>
        $"{Visit(node.Expression)};";

    public override string VisitIfStatement(IfStatementNode node)
    {
        var sb = new StringBuilder();
        sb.Append($"if ({Visit(node.Condition)}) {{\n");
        foreach (var stmt in node.ThenBlock.Statements)
            sb.Append(Indent).Append(Visit(stmt)).Append('\n');
        sb.Append("}");
        return sb.ToString();
    }

    public override string VisitForEachStatement(ForEachStatementNode node)
    {
        var sb = new StringBuilder();
        sb.Append($"foreach ({Visit(node.Collection)} as ${node.Variable.Name}) {{\n");
        foreach (var stmt in node.Body.Statements)
            sb.Append(Indent).Append(Visit(stmt)).Append('\n');
        sb.Append("}");
        return sb.ToString();
    }

    public override string VisitVariableDeclarationStatement(VariableDeclarationStatementNode node)
    {
        var init = node.Initializer != null ? $" = {Visit(node.Initializer)}" : "";
        return $"${node.Name.Name}{init};";
    }

    public override string VisitThrowStatement(ThrowStatementNode node)
    {
        var expr = node.Expression != null ? Visit(node.Expression) : "";
        return $"throw {expr};";
    }

    public override string VisitUsingStatement(UsingStatementNode node) => VisitDefault(node);

    public override string VisitTryCatchStatement(TryCatchStatementNode node)
    {
        var sb = new StringBuilder();
        sb.Append("try {\n");
        foreach (var stmt in node.TryBlock.Statements)
            sb.Append(Indent).Append(Visit(stmt)).Append('\n');
        sb.Append("}");
        foreach (var c in node.CatchClauses)
            sb.Append(' ').Append(VisitCatchClause(c));
        if (node.FinallyBlock != null)
        {
            sb.Append(" finally {\n");
            foreach (var stmt in node.FinallyBlock.Statements)
                sb.Append(Indent).Append(Visit(stmt)).Append('\n');
            sb.Append("}");
        }
        return sb.ToString();
    }

    public override string VisitCatchClause(CatchClauseNode node)
    {
        var typeStr = node.ExceptionType != null ? Visit(node.ExceptionType) : "\\Throwable";
        var varStr = node.ExceptionVariable != null ? $" ${node.ExceptionVariable.Name}" : "";
        var sb = new StringBuilder();
        sb.Append($"catch ({typeStr}{varStr}) {{\n");
        foreach (var stmt in node.Body.Statements)
            sb.Append(Indent).Append(Visit(stmt)).Append('\n');
        sb.Append("}");
        return sb.ToString();
    }

    // --- Внутренний рендеринг --------------------------------------------------------

    private string RenderUseLine(PhpUseStatementNode use)
    {
        var alias = string.IsNullOrEmpty(use.Alias) ? "" : $" as {use.Alias}";
        return $"use {use.FullyQualifiedName}{alias};\n";
    }

    private string RenderDeclaration(DeclarationNode decl) => decl switch
    {
        PhpClassDeclarationNode cls => RenderClass(cls),
        EnumDeclarationNode en      => RenderEnum(en),
        InterfaceDeclarationNode it => RenderInterface(it),
        _                           => Visit(decl),
    };

    private string RenderClass(PhpClassDeclarationNode node)
    {
        var sb = new StringBuilder();
        if (node.Documentation != null)
            sb.Append(RenderDocBlock(node.Documentation.Text, 0));
        foreach (var attr in node.Attributes)
            sb.Append($"#[{attr.Name.Name}]\n");

        var modifiers = new List<string>();
        if (node.IsAbstract) modifiers.Add("abstract");
        if (node.IsFinal && !node.IsAbstract) modifiers.Add("final");
        if (node.IsReadonly) modifiers.Add("readonly");

        var head = string.Join(' ', modifiers);
        if (head.Length > 0) head += ' ';
        head += $"class {node.Name.Name}";
        if (node.Implements.Count > 0)
            head += " implements " + string.Join(", ", node.Implements);

        sb.Append(head).Append('\n');
        sb.Append("{\n");

        if (node.Constructor != null)
        {
            sb.Append(RenderConstructor(node.Constructor, 1));
            if (node.Members.Count > 0)
                sb.Append('\n');
        }

        for (int i = 0; i < node.Members.Count; i++)
        {
            var m = node.Members[i];
            sb.Append(RenderMember(m, 1));
            if (i < node.Members.Count - 1)
                sb.Append('\n');
        }

        sb.Append("}\n");
        return sb.ToString();
    }

    private string RenderMember(DeclarationNode member, int indent) => member switch
    {
        PhpConstructorDeclarationNode ctor => RenderConstructor(ctor, indent),
        MethodDeclarationNode m            => RenderMethod(m, indent),
        PropertyDeclarationNode p          => RenderClassProperty(p, indent),
        _                                  => Visit(member),
    };

    private string RenderConstructor(PhpConstructorDeclarationNode node, int indent)
    {
        var sb = new StringBuilder();
        var pad = Repeat(Indent, indent);

        var docLines = new List<string>();
        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            docLines.Add(node.Documentation.Text);
        foreach (var p in node.PromotedParameters)
        {
            if (!string.IsNullOrEmpty(p.PhpDocType))
                docLines.Add($"@param {p.PhpDocType} ${p.Parameter.Name.Name}");
        }
        if (docLines.Count > 0)
            sb.Append(RenderDocBlock(docLines, indent));

        sb.Append(pad).Append("public function __construct(");
        if (node.PromotedParameters.Count == 0)
        {
            sb.Append(")");
        }
        else
        {
            sb.Append('\n');
            for (int i = 0; i < node.PromotedParameters.Count; i++)
            {
                var p = node.PromotedParameters[i];
                sb.Append(Repeat(Indent, indent + 1));
                sb.Append(RenderPromotedParameter(p));
                sb.Append(",\n");
            }
            sb.Append(pad).Append(')');
        }
        sb.Append(" {\n");
        if (node.Body != null)
        {
            foreach (var stmt in node.Body.Statements)
            {
                sb.Append(Repeat(Indent, indent + 1));
                sb.Append(Visit(stmt));
                sb.Append('\n');
            }
        }
        sb.Append(pad).Append("}\n");
        return sb.ToString();
    }

    private string RenderPromotedParameter(PhpPromotedParameter p)
    {
        var readonlyKw = p.IsReadonly ? "readonly " : "";
        var type = p.Parameter.Type != null ? Visit(p.Parameter.Type) + " " : "";
        var def = p.Parameter.DefaultValue != null ? $" = {Visit(p.Parameter.DefaultValue)}" : "";
        return $"{p.Visibility} {readonlyKw}{type}${p.Parameter.Name.Name}{def}";
    }

    private string RenderClassProperty(PropertyDeclarationNode node, int indent)
    {
        var sb = new StringBuilder();
        var pad = Repeat(Indent, indent);
        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            sb.Append(RenderDocBlock(node.Documentation.Text, indent));
        var typeStr = Visit(node.Type);
        var readonlyKw = node.IsReadOnly ? "readonly " : "";
        var def = node.DefaultValue != null ? $" = {Visit(node.DefaultValue)}" : "";
        sb.Append(pad).Append($"public {readonlyKw}{typeStr} ${node.Name.Name}{def};\n");
        return sb.ToString();
    }

    private string RenderMethod(MethodDeclarationNode node, int indent)
    {
        var sb = new StringBuilder();
        var pad = Repeat(Indent, indent);
        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            sb.Append(RenderDocBlock(node.Documentation.Text, indent));
        var visibility = node.AccessModifier switch
        {
            AccessModifier.Private   => "private",
            AccessModifier.Protected => "protected",
            _                        => "public",
        };
        var paramsStr = string.Join(", ", node.Parameters.Select(RenderParameter));
        var returnStr = node.ReturnType != null ? ": " + Visit(node.ReturnType) : "";
        sb.Append(pad).Append($"{visibility} function {node.Name.Name}({paramsStr}){returnStr}");
        if (node.Body != null)
        {
            sb.Append("\n").Append(pad).Append("{\n");
            foreach (var stmt in node.Body.Statements)
            {
                sb.Append(Repeat(Indent, indent + 1));
                sb.Append(Visit(stmt));
                sb.Append('\n');
            }
            sb.Append(pad).Append("}\n");
        }
        else
        {
            sb.Append(";\n");
        }
        return sb.ToString();
    }

    private string RenderParameter(ParameterDeclarationNode node)
    {
        var type = node.Type != null ? Visit(node.Type) + " " : "";
        var def = node.DefaultValue != null ? $" = {Visit(node.DefaultValue)}" : "";
        return $"{type}${node.Name.Name}{def}";
    }

    private string RenderInterface(InterfaceDeclarationNode node)
    {
        var sb = new StringBuilder();
        if (node.Documentation != null)
            sb.Append(RenderDocBlock(node.Documentation.Text, 0));
        sb.Append($"interface {node.Name.Name}");
        if (node.BaseTypes.Count > 0)
            sb.Append(" extends ").Append(string.Join(", ", node.BaseTypes.Select(Visit)));
        sb.Append("\n{\n");
        foreach (var member in node.Members)
        {
            sb.Append(RenderMember(member, 1));
        }
        sb.Append("}\n");
        return sb.ToString();
    }

    private string RenderEnum(EnumDeclarationNode node)
    {
        var sb = new StringBuilder();
        if (node.Documentation != null)
            sb.Append(RenderDocBlock(node.Documentation.Text, 0));
        var backingType = node.UnderlyingType != null ? ": " + Visit(node.UnderlyingType) : "";
        sb.Append($"enum {node.Name.Name}{backingType}\n");
        sb.Append("{\n");
        foreach (var member in node.Members.OfType<EnumMemberDeclarationNode>())
            sb.Append(RenderEnumCase(member, 1));
        sb.Append("}\n");
        return sb.ToString();
    }

    private string RenderEnumCase(EnumMemberDeclarationNode node, int indent)
    {
        var pad = Repeat(Indent, indent);
        var sb = new StringBuilder();
        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            sb.Append(RenderDocBlock(node.Documentation.Text, indent));
        var value = node.Value != null ? " = " + Visit(node.Value) : "";
        sb.Append(pad).Append($"case {node.Name.Name}{value};\n");
        return sb.ToString();
    }

    private static string RenderDocBlock(string text, int indent)
    {
        var pad = Repeat(Indent, indent);
        var sb = new StringBuilder();
        sb.Append(pad).Append("/**\n");
        foreach (var line in text.Split('\n', StringSplitOptions.None))
            sb.Append(pad).Append(" * ").Append(line.TrimEnd()).Append('\n');
        sb.Append(pad).Append(" */\n");
        return sb.ToString();
    }

    private static string RenderDocBlock(IReadOnlyList<string> lines, int indent)
    {
        var pad = Repeat(Indent, indent);
        var sb = new StringBuilder();
        sb.Append(pad).Append("/**\n");
        foreach (var line in lines)
            sb.Append(pad).Append(" * ").Append(line).Append('\n');
        sb.Append(pad).Append(" */\n");
        return sb.ToString();
    }

    private static string EscapeSingleQuoted(string value) =>
        value.Replace("\\", "\\\\").Replace("'", "\\'");

    private static string Repeat(string s, int count)
    {
        if (count <= 0) return "";
        var sb = new StringBuilder(s.Length * count);
        for (int i = 0; i < count; i++) sb.Append(s);
        return sb.ToString();
    }
}
