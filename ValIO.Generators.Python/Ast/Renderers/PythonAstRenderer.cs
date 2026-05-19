using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Python.Ast.Nodes;
using ValIO.Generators.Python.Ast.Visitors;

namespace ValIO.Generators.Python.Ast.Renderers;

/// <summary>
/// Рендерер Python AST в исходный код.
/// Единственное место в Python-плагине, где разрешён StringBuilder (AST-first контракт).
/// </summary>
public sealed class PythonAstRenderer : PythonAstVisitorBase<string>, IPythonAstRenderer
{
    private const int IndentSize = 4;

    public string Render(PythonModuleNode module) => VisitPythonModule(module);

    public override string VisitPythonModule(PythonModuleNode node)
    {
        var sb = new StringBuilder();

        if (node.ModuleDoc != null && !string.IsNullOrWhiteSpace(node.ModuleDoc.Text))
        {
            AppendDocstring(sb, indent: 0, node.ModuleDoc.Text);
            sb.AppendLine();
        }

        if (node.FutureImports.Count > 0)
        {
            foreach (var fi in node.FutureImports)
                sb.AppendLine(VisitPythonImport(fi));
            sb.AppendLine();
        }

        if (node.Imports.Count > 0)
        {
            foreach (var imp in node.Imports)
                sb.AppendLine(VisitPythonImport(imp));
            sb.AppendLine();
        }

        if (node.TypeCheckingImports.Count > 0)
        {
            sb.AppendLine("if TYPE_CHECKING:");
            foreach (var imp in node.TypeCheckingImports)
            {
                sb.Append(Indent(1));
                sb.AppendLine(VisitPythonImport(imp));
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

        if (node.TrailingStatements.Count > 0)
        {
            sb.AppendLine();
            foreach (var stmt in node.TrailingStatements)
                sb.AppendLine(stmt);
        }

        return sb.ToString();
    }

    public override string VisitPythonImport(PythonImportNode node)
    {
        var hasFrom = !string.IsNullOrEmpty(node.Module);
        if (!hasFrom)
            return node.Alias != null
                ? throw new InvalidOperationException("Bare 'import' cannot have alias without a module")
                : "import " + node.Module;

        if (node.Names.Count == 0)
        {
            return node.Alias != null
                ? $"import {node.Module} as {node.Alias}"
                : $"import {node.Module}";
        }

        var names = string.Join(", ", node.Names);
        return node.Alias != null
            ? $"from {node.Module} import {names} as {node.Alias}"
            : $"from {node.Module} import {names}";
    }

    public override string VisitPythonClassDeclaration(PythonClassDeclarationNode node) =>
        RenderClass(node, indent: 0);

    private string RenderClass(PythonClassDeclarationNode node, int indent)
    {
        var sb = new StringBuilder();

        foreach (var decorator in node.Decorators)
            sb.AppendLine($"{Indent(indent)}@{decorator}");

        var bases = node.BaseClasses.Count > 0
            ? "(" + string.Join(", ", node.BaseClasses.Select(RenderType)) + ")"
            : "";
        sb.AppendLine($"{Indent(indent)}class {node.Name.Name}{bases}:");

        var bodyIndent = indent + 1;
        var hasBody = false;

        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
        {
            AppendDocstring(sb, bodyIndent, node.Documentation.Text);
            hasBody = true;
        }

        if (node.Members.Count == 0)
        {
            if (!hasBody)
                sb.AppendLine($"{Indent(bodyIndent)}pass");
            return sb.ToString();
        }

        if (hasBody) sb.AppendLine();

        // Эвристика: вставляем пустую строку после model_config = ConfigDict(...) перед остальными полями.
        for (var i = 0; i < node.Members.Count; i++)
        {
            var member = node.Members[i];
            var renderedMember = RenderDeclaration(member, bodyIndent);
            sb.Append(renderedMember);
            if (!renderedMember.EndsWith("\n", StringComparison.Ordinal))
                sb.AppendLine();

            var isConfigLine = member is PythonAttributeAssignmentNode pa && pa.Name.Name == "model_config";
            var nextIsField = i + 1 < node.Members.Count && node.Members[i + 1] is PythonAttributeAssignmentNode;
            if (isConfigLine && nextIsField)
                sb.AppendLine();

            if (member is PythonFunctionDeclarationNode && i < node.Members.Count - 1)
                sb.AppendLine();
        }

        return sb.ToString();
    }

    public override string VisitPythonAttributeAssignment(PythonAttributeAssignmentNode node) =>
        RenderAttributeAssignment(node, indent: 0);

    private string RenderAttributeAssignment(PythonAttributeAssignmentNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);

        var lhs = node.Name.Name;
        string line;
        if (node.Annotation != null)
        {
            var annotation = RenderType(node.Annotation);
            if (node.QuoteAnnotation)
                annotation = "\"" + annotation + "\"";
            line = node.DefaultValueLiteral != null
                ? $"{lhs}: {annotation} = {node.DefaultValueLiteral}"
                : $"{lhs}: {annotation}";
        }
        else
        {
            line = node.DefaultValueLiteral != null
                ? $"{lhs} = {node.DefaultValueLiteral}"
                : lhs;
        }

        sb.AppendLine($"{prefix}{line}");

        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocstring(sb, indent, node.Documentation.Text);

        return sb.ToString();
    }

    public override string VisitPythonFunctionDeclaration(PythonFunctionDeclarationNode node) =>
        RenderFunction(node, indent: 0);

    private string RenderFunction(PythonFunctionDeclarationNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);

        foreach (var decorator in node.Decorators)
            sb.AppendLine($"{prefix}@{decorator}");

        var asyncPrefix = node.IsAsync ? "async " : "";
        var paramText = string.Join(", ", node.Parameters.Select(RenderParameter));
        var returnAnnotation = node.ReturnType != null ? $" -> {RenderType(node.ReturnType)}" : "";
        sb.AppendLine($"{prefix}{asyncPrefix}def {node.Name.Name}({paramText}){returnAnnotation}:");

        var bodyIndent = indent + 1;
        var hasBody = false;

        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
        {
            AppendDocstring(sb, bodyIndent, node.Documentation.Text);
            hasBody = true;
        }

        if (node.Body.Count == 0)
        {
            if (!hasBody)
                sb.AppendLine($"{Indent(bodyIndent)}pass");
            return sb.ToString();
        }

        foreach (var line in node.Body)
        {
            if (string.IsNullOrEmpty(line))
            {
                sb.AppendLine();
                continue;
            }
            sb.AppendLine($"{Indent(bodyIndent)}{line}");
        }

        return sb.ToString();
    }

    public override string VisitPythonParameter(PythonParameterNode node) => RenderParameter(node);

    private string RenderParameter(PythonParameterNode node)
    {
        var annotation = node.Annotation != null ? $": {RenderType(node.Annotation)}" : "";
        var defaultValue = node.DefaultValueLiteral != null ? $" = {node.DefaultValueLiteral}" : "";
        // PEP 8 рекомендует "name: type = default" с пробелами вокруг "=" только для annotated defaults.
        if (node.Annotation == null && node.DefaultValueLiteral != null)
            defaultValue = $"={node.DefaultValueLiteral}";
        return $"{node.Name.Name}{annotation}{defaultValue}";
    }

    public override string VisitPythonEnumDeclaration(PythonEnumDeclarationNode node) =>
        RenderEnum(node, indent: 0);

    private string RenderEnum(PythonEnumDeclarationNode node, int indent)
    {
        var sb = new StringBuilder();
        var prefix = Indent(indent);
        var baseClass = node.BaseClass == PythonEnumBase.IntEnum ? "IntEnum" : "StrEnum";

        sb.AppendLine($"{prefix}class {node.Name.Name}({baseClass}):");

        var bodyIndent = indent + 1;
        var hasBody = false;

        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
        {
            AppendDocstring(sb, bodyIndent, node.Documentation.Text);
            hasBody = true;
        }

        if (node.Members.Count == 0)
        {
            if (!hasBody)
                sb.AppendLine($"{Indent(bodyIndent)}pass");
            return sb.ToString();
        }

        if (hasBody) sb.AppendLine();

        foreach (var m in node.Members)
        {
            sb.Append(RenderEnumMember(m, bodyIndent));
        }

        return sb.ToString();
    }

    public override string VisitPythonEnumMember(PythonEnumMemberNode node) =>
        RenderEnumMember(node, indent: 0);

    private string RenderEnumMember(PythonEnumMemberNode node, int indent)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Indent(indent)}{node.Name} = {node.ValueLiteral}");
        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            AppendDocstring(sb, indent, node.Documentation.Text);
        return sb.ToString();
    }

    private string RenderDeclaration(DeclarationNode declaration, int indent) =>
        declaration switch
        {
            PythonClassDeclarationNode cls => RenderClass(cls, indent),
            PythonAttributeAssignmentNode attr => RenderAttributeAssignment(attr, indent),
            PythonFunctionDeclarationNode fn => RenderFunction(fn, indent),
            PythonEnumDeclarationNode en => RenderEnum(en, indent),
            _ => throw new NotSupportedException(
                $"Cannot render declaration of type {declaration.GetType().Name} in Python module")
        };

    // ------------- Type rendering -------------

    public override string VisitSimpleType(SimpleTypeNode node)
    {
        if (node.LiteralValue != null)
            return $"Literal[{RenderLiteralValue(node.LiteralValue)}]";
        return node.Name.Name;
    }

    public override string VisitGenericType(GenericTypeNode node)
    {
        var args = string.Join(", ", node.TypeArguments.Select(RenderType));
        return $"{node.Name.Name}[{args}]";
    }

    public override string VisitNullableType(NullableTypeNode node) =>
        $"{RenderType(node.InnerType)} | None";

    public override string VisitOptionalType(OptionalTypeNode node) =>
        $"{RenderType(node.InnerType)} | None";

    public override string VisitArrayType(ArrayTypeNode node) =>
        $"list[{RenderType(node.ElementType)}]";

    public override string VisitUnionType(UnionTypeNode node) =>
        string.Join(" | ", node.Types.Select(RenderType));

    public override string VisitIntersectionType(IntersectionTypeNode node) =>
        // Python не имеет intersection; берём первый тип.
        node.Types.Count > 0 ? RenderType(node.Types[0]) : "Any";

    public override string VisitDictionaryType(DictionaryTypeNode node) =>
        $"dict[{RenderType(node.KeyType)}, {RenderType(node.ValueType)}]";

    public override string VisitRawType(RawTypeNode node) => node.RawTypeString;

    private string RenderType(TypeNode type) => type.Accept(this);

    // ------------- Expression / Statement / Misc visitors -------------

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
        return node.IsNewExpression ? $"{target}({args})" : $"{target}({args})";
    }

    public override string VisitMemberAccessExpression(MemberAccessExpressionNode node) =>
        $"{Visit(node.Object)}.{node.Member.Name}";

    public override string VisitUnaryExpression(UnaryExpressionNode node)
    {
        var op = node.Operator switch
        {
            UnaryOperator.LogicalNot => "not ",
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
            BinaryOperator.LogicalAnd => "and",
            BinaryOperator.LogicalOr => "or",
            _ => throw new NotSupportedException($"Operator {node.Operator} not supported in Python renderer")
        };
        return $"{left} {op} {right}";
    }

    public override string VisitConditionalExpression(ConditionalExpressionNode node) =>
        $"{Visit(node.TrueExpression)} if {Visit(node.Condition)} else {Visit(node.FalseExpression)}";

    public override string VisitAwaitExpression(AwaitExpressionNode node) =>
        $"await {Visit(node.Expression)}";

    public override string VisitCastExpression(CastExpressionNode node) =>
        // Python не имеет cast; возвращаем выражение как есть.
        Visit(node.Expression);

    public override string VisitReturnStatement(ReturnStatementNode node) =>
        node.Value == null ? "return" : $"return {Visit(node.Value)}";

    public override string VisitIfStatement(IfStatementNode node)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"if {Visit(node.Condition)}:");
        sb.AppendLine(Visit(node.ThenBlock));
        if (node.ElseBlock != null)
        {
            sb.AppendLine("else:");
            sb.AppendLine(Visit(node.ElseBlock));
        }
        return sb.ToString();
    }

    public override string VisitForEachStatement(ForEachStatementNode node) =>
        $"for {node.Variable.Name} in {Visit(node.Collection)}:\n{Visit(node.Body)}";

    public override string VisitExpressionStatement(ExpressionStatementNode node) =>
        Visit(node.Expression);

    public override string VisitVariableDeclarationStatement(VariableDeclarationStatementNode node)
    {
        var initializer = node.Initializer != null ? $" = {Visit(node.Initializer)}" : "";
        return $"{node.Name.Name}{initializer}";
    }

    public override string VisitThrowStatement(ThrowStatementNode node) =>
        node.Expression == null ? "raise" : $"raise {Visit(node.Expression)}";

    public override string VisitUsingStatement(UsingStatementNode node) =>
        $"with {Visit(node.Resource)}:\n{Visit(node.Body)}";

    public override string VisitTryCatchStatement(TryCatchStatementNode node)
    {
        var sb = new StringBuilder();
        sb.AppendLine("try:");
        sb.AppendLine(Visit(node.TryBlock));
        foreach (var c in node.CatchClauses)
            sb.AppendLine(Visit(c));
        if (node.FinallyBlock != null)
        {
            sb.AppendLine("finally:");
            sb.AppendLine(Visit(node.FinallyBlock));
        }
        return sb.ToString();
    }

    public override string VisitCatchClause(CatchClauseNode node)
    {
        var exType = node.ExceptionType != null ? " " + RenderType(node.ExceptionType) : "";
        var exVar = node.ExceptionVariable != null ? $" as {node.ExceptionVariable.Name}" : "";
        return $"except{exType}{exVar}:\n{Visit(node.Body)}";
    }

    public override string VisitTypeDeclaration(TypeDeclarationNode node) =>
        throw new NotSupportedException("TypeDeclarationNode not supported by Python renderer");

    public override string VisitPropertyDeclaration(PropertyDeclarationNode node) => VisitDefault(node);
    public override string VisitMethodDeclaration(MethodDeclarationNode node) => VisitDefault(node);
    public override string VisitParameterDeclaration(ParameterDeclarationNode node) => VisitDefault(node);
    public override string VisitEnumMemberDeclaration(EnumMemberDeclarationNode node) => VisitDefault(node);
    public override string VisitEnumDeclaration(EnumDeclarationNode node) => VisitDefault(node);
    public override string VisitInterfaceDeclaration(InterfaceDeclarationNode node) => VisitDefault(node);

    // ------------- Helpers -------------

    private static string Indent(int level) =>
        level <= 0 ? string.Empty : new string(' ', level * IndentSize);

    private static void AppendDocstring(StringBuilder sb, int indent, string text)
    {
        var prefix = Indent(indent);
        var trimmed = text.Trim('\r', '\n');
        if (!trimmed.Contains('\n'))
        {
            sb.AppendLine($"{prefix}\"\"\"{EscapeForDocstring(trimmed)}\"\"\"");
            return;
        }

        sb.AppendLine($"{prefix}\"\"\"");
        foreach (var rawLine in trimmed.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
                sb.AppendLine();
            else
                sb.AppendLine($"{prefix}{line}");
        }
        sb.AppendLine($"{prefix}\"\"\"");
    }

    private static string EscapeForDocstring(string text) =>
        text.Replace("\"\"\"", "\\\"\\\"\\\"", StringComparison.Ordinal);

    private static string RenderLiteralValue(object? value, LiteralKind? kind = null)
    {
        if (value == null) return "None";
        return value switch
        {
            string s when kind == LiteralKind.String => $"\"{EscapeStringLiteral(s)}\"",
            bool b => b ? "True" : "False",
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
