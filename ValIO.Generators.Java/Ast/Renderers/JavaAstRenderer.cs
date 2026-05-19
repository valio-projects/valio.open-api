using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Java.Ast.Nodes;
using ValIO.Generators.Java.Ast.Visitors;

namespace ValIO.Generators.Java.Ast.Renderers;

/// <summary>
/// Renderer for the Java AST. Targets Java 21: records for DTOs, regular classes with explicit
/// constructors for HTTP clients, plain enums (with optional backing string value field).
/// </summary>
public sealed class JavaAstRenderer : JavaAstVisitorBase<string>, IJavaAstRenderer
{
    private const string Indent = "    ";

    public string Render(JavaSourceFileNode node)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(node.Package))
        {
            sb.Append($"package {node.Package};\n\n");
        }

        if (node.Imports.Count > 0)
        {
            foreach (var import in node.Imports.OrderBy(i => i.FullyQualifiedName, StringComparer.Ordinal))
            {
                sb.Append(RenderImportLine(import));
            }
            sb.Append('\n');
        }

        for (int i = 0; i < node.Declarations.Count; i++)
        {
            sb.Append(RenderDeclaration(node.Declarations[i]));
            if (i < node.Declarations.Count - 1)
                sb.Append('\n');
        }

        return sb.ToString();
    }

    public override string VisitJavaSourceFile(JavaSourceFileNode node) => Render(node);

    public override string VisitJavaImport(JavaImportNode node) => RenderImportLine(node);

    public override string VisitJavaRecord(JavaRecordDeclarationNode node) => RenderRecord(node);

    public override string VisitJavaClass(JavaClassDeclarationNode node) => RenderClass(node);

    public override string VisitEnumDeclaration(EnumDeclarationNode node) => RenderEnum(node);

    public override string VisitInterfaceDeclaration(InterfaceDeclarationNode node) => VisitDefault(node);

    public override string VisitMethodDeclaration(MethodDeclarationNode node) => RenderMethod(node, 1);

    public override string VisitParameterDeclaration(ParameterDeclarationNode node) => RenderParameter(node);

    public override string VisitPropertyDeclaration(PropertyDeclarationNode node) => RenderField(node, 1);

    public override string VisitEnumMemberDeclaration(EnumMemberDeclarationNode node) => RenderEnumConstant(node, 1, withBackingValue: node.Value != null);

    public override string VisitTypeDeclaration(TypeDeclarationNode node) => VisitDefault(node);

    public override string VisitIdentifier(IdentifierNode node) => node.Name;

    public override string VisitAttribute(AttributeNode node) => $"@{node.Name.Name}";

    public override string VisitComment(CommentNode node) => $"// {node.Text}";

    public override string VisitImport(ImportNode node) => "";

    public override string VisitExport(ExportNode node) => Visit(node.Declaration);

    // Types -------------------------------------------------------------------------

    public override string VisitSimpleType(SimpleTypeNode node) => node.Name.Name;

    public override string VisitGenericType(GenericTypeNode node)
    {
        var args = string.Join(", ", node.TypeArguments.Select(Visit));
        return $"{node.Name.Name}<{args}>";
    }

    public override string VisitNullableType(NullableTypeNode node) => Visit(node.InnerType);

    public override string VisitOptionalType(OptionalTypeNode node) => Visit(node.InnerType);

    public override string VisitArrayType(ArrayTypeNode node) => $"{Visit(node.ElementType)}[]";

    public override string VisitDictionaryType(DictionaryTypeNode node) =>
        $"Map<{Visit(node.KeyType)}, {Visit(node.ValueType)}>";

    public override string VisitUnionType(UnionTypeNode node) => "Object";

    public override string VisitIntersectionType(IntersectionTypeNode node) => "Object";

    // Expressions -------------------------------------------------------------------

    public override string VisitLiteralExpression(LiteralExpressionNode node) => node.LiteralKind switch
    {
        LiteralKind.String    => $"\"{EscapeString(node.Value?.ToString() ?? string.Empty)}\"",
        LiteralKind.Number    => node.Value?.ToString() ?? "0",
        LiteralKind.Boolean   => (node.Value is bool b && b) ? "true" : "false",
        LiteralKind.Null      => "null",
        LiteralKind.Character => $"'{EscapeChar(node.Value?.ToString() ?? string.Empty)}'",
        _                     => node.Value?.ToString() ?? "null",
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
        return $"{target}.{node.Member.Name}";
    }

    public override string VisitBinaryExpression(BinaryExpressionNode node)
    {
        var op = node.Operator switch
        {
            BinaryOperator.Add                 => "+",
            BinaryOperator.Subtract            => "-",
            BinaryOperator.Multiply            => "*",
            BinaryOperator.Divide              => "/",
            BinaryOperator.Modulo              => "%",
            BinaryOperator.Equal               => "==",
            BinaryOperator.NotEqual            => "!=",
            BinaryOperator.LessThan            => "<",
            BinaryOperator.LessThanOrEqual     => "<=",
            BinaryOperator.GreaterThan         => ">",
            BinaryOperator.GreaterThanOrEqual  => ">=",
            BinaryOperator.LogicalAnd          => "&&",
            BinaryOperator.LogicalOr           => "||",
            BinaryOperator.Assign              => "=",
            _ => throw new NotSupportedException($"Binary operator {node.Operator} not supported for Java"),
        };
        // Assignments stand alone — don't wrap in parens (used in constructor bodies).
        if (node.Operator == BinaryOperator.Assign)
            return $"{Visit(node.Left)} {op} {Visit(node.Right)}";
        return $"({Visit(node.Left)} {op} {Visit(node.Right)})";
    }

    public override string VisitConditionalExpression(ConditionalExpressionNode node) =>
        $"({Visit(node.Condition)} ? {Visit(node.TrueExpression)} : {Visit(node.FalseExpression)})";

    public override string VisitAwaitExpression(AwaitExpressionNode node) => Visit(node.Expression);

    public override string VisitCastExpression(CastExpressionNode node)
    {
        var type = Visit(node.TargetType);
        return $"(({type}) {Visit(node.Expression)})";
    }

    // Statements --------------------------------------------------------------------

    public override string VisitBlock(BlockNode node)
    {
        var sb = new StringBuilder();
        sb.Append("{\n");
        foreach (var stmt in node.Statements)
        {
            sb.Append(Indent).Append(Visit(stmt)).Append('\n');
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
        sb.Append($"for (var {node.Variable.Name} : {Visit(node.Collection)}) {{\n");
        foreach (var stmt in node.Body.Statements)
            sb.Append(Indent).Append(Visit(stmt)).Append('\n');
        sb.Append("}");
        return sb.ToString();
    }

    public override string VisitVariableDeclarationStatement(VariableDeclarationStatementNode node)
    {
        var typeStr = node.Type != null ? Visit(node.Type) : "var";
        var init = node.Initializer != null ? $" = {Visit(node.Initializer)}" : "";
        return $"{typeStr} {node.Name.Name}{init};";
    }

    public override string VisitThrowStatement(ThrowStatementNode node)
    {
        var expr = node.Expression != null ? Visit(node.Expression) : "";
        return $"throw {expr};";
    }

    public override string VisitUsingStatement(UsingStatementNode node)
    {
        // Java try-with-resources approximation: only used by client step which uses TryCatchStatement instead.
        var sb = new StringBuilder();
        sb.Append($"try ({Visit(node.Resource)}) {{\n");
        foreach (var stmt in node.Body.Statements)
            sb.Append(Indent).Append(Visit(stmt)).Append('\n');
        sb.Append("}");
        return sb.ToString();
    }

    public override string VisitTryCatchStatement(TryCatchStatementNode node)
    {
        var sb = new StringBuilder();
        // Detect try-with-resources via metadata key "withResources" (list of variable decl strings).
        if (node.Metadata.TryGetValue("withResources", out var resourcesObj) && resourcesObj is IReadOnlyList<string> resources && resources.Count > 0)
        {
            sb.Append("try (").Append(string.Join("; ", resources)).Append(") {\n");
        }
        else
        {
            sb.Append("try {\n");
        }
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
        var typeStr = node.ExceptionType != null ? Visit(node.ExceptionType) : "Throwable";
        var varStr = node.ExceptionVariable != null ? " " + node.ExceptionVariable.Name : " e";
        var sb = new StringBuilder();
        sb.Append($"catch ({typeStr}{varStr}) {{\n");
        foreach (var stmt in node.Body.Statements)
            sb.Append(Indent).Append(Visit(stmt)).Append('\n');
        sb.Append("}");
        return sb.ToString();
    }

    // --- Internal rendering --------------------------------------------------------

    private string RenderImportLine(JavaImportNode import)
    {
        var staticKw = import.IsStatic ? "static " : "";
        var wildcard = import.IsWildcard ? ".*" : "";
        return $"import {staticKw}{import.FullyQualifiedName}{wildcard};\n";
    }

    private string RenderDeclaration(DeclarationNode decl) => decl switch
    {
        JavaRecordDeclarationNode r => RenderRecord(r),
        JavaClassDeclarationNode c  => RenderClass(c),
        EnumDeclarationNode e       => RenderEnum(e),
        InterfaceDeclarationNode i  => VisitDefault(i),
        _                           => Visit(decl),
    };

    private string RenderRecord(JavaRecordDeclarationNode node)
    {
        var sb = new StringBuilder();
        if (node.Documentation != null)
            sb.Append(RenderJavadoc(node.Documentation.Text, 0));
        foreach (var attr in node.Attributes)
            sb.Append($"@{attr.Name.Name}\n");

        var implementsStr = node.Implements.Count > 0
            ? " implements " + string.Join(", ", node.Implements)
            : "";

        sb.Append($"public record {node.Name.Name}(");
        if (node.Components.Count == 0)
        {
            sb.Append(')');
        }
        else
        {
            sb.Append('\n');
            for (int i = 0; i < node.Components.Count; i++)
            {
                var p = node.Components[i];
                sb.Append(Indent);
                sb.Append(RenderRecordComponent(p));
                if (i < node.Components.Count - 1) sb.Append(',');
                sb.Append('\n');
            }
            sb.Append(')');
        }
        sb.Append(implementsStr);
        if (node.Members.Count > 0)
        {
            sb.Append(" {\n");
            for (int i = 0; i < node.Members.Count; i++)
            {
                sb.Append(RenderMember(node.Members[i], 1));
                if (i < node.Members.Count - 1) sb.Append('\n');
            }
            sb.Append("}\n");
        }
        else
        {
            sb.Append(" {}\n");
        }
        return sb.ToString();
    }

    private string RenderRecordComponent(ParameterDeclarationNode node)
    {
        var typeStr = node.Type != null ? Visit(node.Type) : "Object";
        return $"{typeStr} {node.Name.Name}";
    }

    private string RenderClass(JavaClassDeclarationNode node)
    {
        var sb = new StringBuilder();
        if (node.Documentation != null)
            sb.Append(RenderJavadoc(node.Documentation.Text, 0));
        foreach (var attr in node.Attributes)
            sb.Append($"@{attr.Name.Name}\n");

        var modifiers = new List<string> { "public" };
        if (node.IsAbstract) modifiers.Add("abstract");
        else if (node.IsFinal) modifiers.Add("final");

        var head = string.Join(' ', modifiers) + $" class {node.Name.Name}";
        if (!string.IsNullOrEmpty(node.Extends))
            head += $" extends {node.Extends}";
        if (node.Implements.Count > 0)
            head += " implements " + string.Join(", ", node.Implements);

        sb.Append(head).Append(" {\n");

        bool prevWasBlank = true;
        foreach (var field in node.Fields)
        {
            sb.Append(RenderField(field, 1));
            prevWasBlank = false;
        }

        if (node.Constructor != null)
        {
            if (!prevWasBlank) sb.Append('\n');
            sb.Append(RenderConstructor(node.Name.Name, node.Constructor, 1));
            prevWasBlank = false;
        }

        for (int i = 0; i < node.Members.Count; i++)
        {
            if (!prevWasBlank) sb.Append('\n');
            sb.Append(RenderMember(node.Members[i], 1));
            prevWasBlank = false;
        }

        sb.Append("}\n");
        return sb.ToString();
    }

    private string RenderMember(DeclarationNode member, int indent) => member switch
    {
        MethodDeclarationNode m   => RenderMethod(m, indent),
        PropertyDeclarationNode p => RenderField(p, indent),
        EnumDeclarationNode e     => RenderEnum(e),
        _                         => Visit(member),
    };

    private string RenderField(PropertyDeclarationNode node, int indent)
    {
        var pad = Repeat(Indent, indent);
        var sb = new StringBuilder();
        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            sb.Append(RenderJavadoc(node.Documentation.Text, indent));
        foreach (var attr in node.Attributes)
            sb.Append(pad).Append($"@{attr.Name.Name}\n");

        var visibility = "private";
        var finalKw = node.IsReadOnly ? "final " : "";
        var typeStr = Visit(node.Type);
        var def = node.DefaultValue != null ? $" = {Visit(node.DefaultValue)}" : "";
        sb.Append(pad).Append($"{visibility} {finalKw}{typeStr} {node.Name.Name}{def};\n");
        return sb.ToString();
    }

    private string RenderConstructor(string typeName, MethodDeclarationNode ctor, int indent)
    {
        var pad = Repeat(Indent, indent);
        var sb = new StringBuilder();
        if (ctor.Documentation != null && !string.IsNullOrWhiteSpace(ctor.Documentation.Text))
            sb.Append(RenderJavadoc(ctor.Documentation.Text, indent));
        var visibility = ctor.AccessModifier switch
        {
            AccessModifier.Private   => "private",
            AccessModifier.Protected => "protected",
            _                        => "public",
        };
        var paramsStr = string.Join(", ", ctor.Parameters.Select(RenderParameter));
        sb.Append(pad).Append($"{visibility} {typeName}({paramsStr}) {{\n");
        if (ctor.Body != null)
        {
            foreach (var stmt in ctor.Body.Statements)
                sb.Append(IndentStatement(Visit(stmt), indent + 1)).Append('\n');
        }
        sb.Append(pad).Append("}\n");
        return sb.ToString();
    }

    private string RenderMethod(MethodDeclarationNode node, int indent)
    {
        var pad = Repeat(Indent, indent);
        var sb = new StringBuilder();
        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            sb.Append(RenderJavadoc(node.Documentation.Text, indent));
        foreach (var attr in node.Attributes)
            sb.Append(pad).Append($"@{attr.Name.Name}\n");
        var visibility = node.AccessModifier switch
        {
            AccessModifier.Private   => "private",
            AccessModifier.Protected => "protected",
            _                        => "public",
        };
        var returnStr = node.ReturnType != null ? Visit(node.ReturnType) : "void";
        var paramsStr = string.Join(", ", node.Parameters.Select(RenderParameter));

        // Throws clause stored in metadata as IReadOnlyList<string>.
        var throwsStr = "";
        if (node.Metadata.TryGetValue("throws", out var throwsObj) && throwsObj is IReadOnlyList<string> throws && throws.Count > 0)
            throwsStr = " throws " + string.Join(", ", throws);

        sb.Append(pad).Append($"{visibility} {returnStr} {node.Name.Name}({paramsStr}){throwsStr}");
        if (node.Body != null)
        {
            sb.Append(" {\n");
            foreach (var stmt in node.Body.Statements)
                sb.Append(IndentStatement(Visit(stmt), indent + 1)).Append('\n');
            sb.Append(pad).Append("}\n");
        }
        else
        {
            sb.Append(";\n");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Prefix every line of <paramref name="rendered"/> with <paramref name="indent"/> levels of indent.
    /// </summary>
    private static string IndentStatement(string rendered, int indent)
    {
        var pad = Repeat(Indent, indent);
        var lines = rendered.Split('\n');
        var sb = new StringBuilder(rendered.Length + lines.Length * pad.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            // Skip indent on completely blank lines.
            if (lines[i].Length > 0) sb.Append(pad);
            sb.Append(lines[i]);
            if (i < lines.Length - 1) sb.Append('\n');
        }
        return sb.ToString();
    }

    private string RenderParameter(ParameterDeclarationNode node)
    {
        var typeStr = node.Type != null ? Visit(node.Type) : "Object";
        return $"{typeStr} {node.Name.Name}";
    }

    private string RenderEnum(EnumDeclarationNode node)
    {
        var sb = new StringBuilder();
        if (node.Documentation != null)
            sb.Append(RenderJavadoc(node.Documentation.Text, 0));
        sb.Append($"public enum {node.Name.Name} {{\n");

        var members = node.Members.OfType<EnumMemberDeclarationNode>().ToList();
        bool hasBackingValue = members.Any(m => m.Value != null);
        var backingType = node.UnderlyingType != null ? Visit(node.UnderlyingType) : "String";

        for (int i = 0; i < members.Count; i++)
        {
            sb.Append(RenderEnumConstant(members[i], 1, hasBackingValue));
            if (i < members.Count - 1) sb.Append(",\n");
            else sb.Append(";\n");
        }

        if (hasBackingValue)
        {
            sb.Append('\n');
            sb.Append(Indent).Append($"private final {backingType} value;\n\n");
            sb.Append(Indent).Append($"{node.Name.Name}({backingType} value) {{\n");
            sb.Append(Repeat(Indent, 2)).Append("this.value = value;\n");
            sb.Append(Indent).Append("}\n\n");
            sb.Append(Indent).Append($"public {backingType} value() {{\n");
            sb.Append(Repeat(Indent, 2)).Append("return value;\n");
            sb.Append(Indent).Append("}\n");
        }

        sb.Append("}\n");
        return sb.ToString();
    }

    private string RenderEnumConstant(EnumMemberDeclarationNode node, int indent, bool withBackingValue)
    {
        var pad = Repeat(Indent, indent);
        var sb = new StringBuilder();
        if (node.Documentation != null && !string.IsNullOrWhiteSpace(node.Documentation.Text))
            sb.Append(RenderJavadoc(node.Documentation.Text, indent));
        sb.Append(pad).Append(node.Name.Name);
        if (withBackingValue && node.Value != null)
            sb.Append('(').Append(Visit(node.Value)).Append(')');
        return sb.ToString();
    }

    private static string RenderJavadoc(string text, int indent)
    {
        var pad = Repeat(Indent, indent);
        var sb = new StringBuilder();
        sb.Append(pad).Append("/**\n");
        foreach (var line in text.Split('\n', StringSplitOptions.None))
            sb.Append(pad).Append(" * ").Append(line.TrimEnd()).Append('\n');
        sb.Append(pad).Append(" */\n");
        return sb.ToString();
    }

    private static string EscapeString(string value)
    {
        var sb = new StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"':  sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:   sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }

    private static string EscapeChar(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var ch = value[0];
        return ch switch
        {
            '\\' => "\\\\",
            '\'' => "\\'",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            _    => ch.ToString(),
        };
    }

    private static string Repeat(string s, int count)
    {
        if (count <= 0) return "";
        var sb = new StringBuilder(s.Length * count);
        for (int i = 0; i < count; i++) sb.Append(s);
        return sb.ToString();
    }
}
