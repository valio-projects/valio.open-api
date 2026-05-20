using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Dart.Ast.Nodes;
using ValIO.Generators.Dart.Ast.Visitors;

namespace ValIO.Generators.Dart.Ast.Renderers;

/// <summary>
/// Renders a Dart AST to .dart source text. The only place StringBuilder
/// is allowed in the Dart pipeline (everywhere else is AST nodes).
/// </summary>
public sealed class DartAstRenderer : DartAstVisitorBase<string>, IDartAstRenderer
{
    private const string Indent = "  "; // 2 spaces, Dart standard

    public string Render(DartFileNode file) => VisitDartFile(file);

    // ------------- File / imports -------------

    public override string VisitDartFile(DartFileNode node)
    {
        var sb = new StringBuilder();

        if (node.FileDoc != null && !string.IsNullOrWhiteSpace(node.FileDoc.Text))
        {
            foreach (var line in node.FileDoc.Text.Split('\n'))
                sb.AppendLine("// " + line.TrimEnd('\r'));
            sb.AppendLine();
        }

        foreach (var import in node.Imports)
            sb.AppendLine(VisitDartImport(import));

        if (node.Imports.Count > 0 && node.Declarations.Count > 0)
            sb.AppendLine();

        for (var i = 0; i < node.Declarations.Count; i++)
        {
            sb.Append(RenderDeclaration(node.Declarations[i]));
            if (i < node.Declarations.Count - 1) sb.AppendLine();
        }

        return sb.ToString();
    }

    public override string VisitDartImport(DartImportNode node) =>
        node.Alias == null
            ? $"import '{node.Uri}';"
            : $"import '{node.Uri}' as {node.Alias};";

    // ------------- Classes / enums -------------

    public override string VisitDartClass(DartClassDeclarationNode node) =>
        RenderClass(node, indent: 0);

    private string RenderClass(DartClassDeclarationNode node, int indent)
    {
        var sb = new StringBuilder();
        var pad = Pad(indent);

        AppendDoc(sb, node.Documentation, indent);

        sb.Append(pad);
        if (node.IsAbstract) sb.Append("abstract ");
        sb.Append("class ");
        sb.Append(node.Name.Name);
        if (node.BaseType != null) sb.Append(" extends " + RenderType(node.BaseType));
        if (node.Mixins.Count > 0)
            sb.Append(" with " + string.Join(", ", node.Mixins.Select(RenderType)));
        sb.AppendLine(" {");

        var body = indent + 1;
        foreach (var f in node.Fields)
            sb.Append(RenderField(f, body));
        if (node.Fields.Count > 0 && node.Methods.Count > 0)
            sb.AppendLine();
        for (var i = 0; i < node.Methods.Count; i++)
        {
            sb.Append(RenderMethod(node.Methods[i], body));
            if (i < node.Methods.Count - 1) sb.AppendLine();
        }

        sb.AppendLine(pad + "}");
        return sb.ToString();
    }

    public override string VisitDartEnum(DartEnumDeclarationNode node) =>
        RenderEnum(node, indent: 0);

    private string RenderEnum(DartEnumDeclarationNode node, int indent)
    {
        var sb = new StringBuilder();
        var pad = Pad(indent);

        AppendDoc(sb, node.Documentation, indent);

        sb.Append(pad);
        sb.Append("enum ");
        sb.Append(node.Name.Name);
        sb.AppendLine(" {");

        for (var i = 0; i < node.Members.Count; i++)
        {
            var m = node.Members[i];
            sb.Append(Pad(indent + 1));
            sb.Append(m.Name.Name);
            if (m.ValueLiteral != null)
                sb.Append("(" + m.ValueLiteral + ")");
            sb.AppendLine(i == node.Members.Count - 1 ? ";" : ",");
        }

        if (node.BackingType != null)
        {
            sb.AppendLine();
            sb.AppendLine($"{Pad(indent + 1)}const {node.Name.Name}([this.value]);");
            sb.AppendLine();
            sb.AppendLine($"{Pad(indent + 1)}final {RenderType(node.BackingType)}? value;");
        }

        sb.AppendLine(pad + "}");
        return sb.ToString();
    }

    public override string VisitDartEnumMember(DartEnumMemberNode node) =>
        node.ValueLiteral == null ? node.Name.Name : $"{node.Name.Name}({node.ValueLiteral})";

    // ------------- Fields / methods / parameters -------------

    public override string VisitDartField(DartFieldNode node) => RenderField(node, 1);

    private string RenderField(DartFieldNode node, int indent)
    {
        var sb = new StringBuilder();
        AppendDoc(sb, node.Documentation, indent);
        sb.Append(Pad(indent));
        if (!string.IsNullOrEmpty(node.JsonKey))
        {
            // Plain comment marker — projects that consume this can add a
            // proper @JsonKey annotation later. Keeps the renderer simple.
            sb.AppendLine($"// json: '{node.JsonKey}'");
            sb.Append(Pad(indent));
        }
        if (node.IsFinal) sb.Append("final ");
        sb.Append(RenderType(node.Type));
        if (node.IsNullable) sb.Append("?");
        sb.AppendLine(" " + node.Name.Name + ";");
        return sb.ToString();
    }

    public override string VisitDartMethod(DartMethodNode node) => RenderMethod(node, 1);

    private string RenderMethod(DartMethodNode node, int indent)
    {
        var sb = new StringBuilder();
        AppendDoc(sb, node.Documentation, indent);

        sb.Append(Pad(indent));
        if (node.IsStatic) sb.Append("static ");

        if (node.IsConstructor)
        {
            // Either default or named constructor; convention: name = class name when default.
            sb.Append(node.Name.Name);
        }
        else
        {
            sb.Append(node.ReturnType != null ? RenderType(node.ReturnType) : "void");
            if (node.IsAsync && node.ReturnType != null
                && !RenderType(node.ReturnType).StartsWith("Future<", StringComparison.Ordinal)
                && RenderType(node.ReturnType) != "void")
            {
                // No-op: builders are responsible for wrapping returns in Future<>.
            }
            sb.Append(" " + node.Name.Name);
        }

        sb.Append(RenderParameterList(node.Parameters));

        if (node.IsAsync) sb.Append(" async");

        if (node.BodyLines.Count == 0)
        {
            sb.AppendLine(";");
            return sb.ToString();
        }

        sb.AppendLine(" {");
        foreach (var line in node.BodyLines)
        {
            // Preserve internal indentation tokens supplied by the builder.
            sb.AppendLine(Pad(indent + 1) + line);
        }
        sb.AppendLine(Pad(indent) + "}");
        return sb.ToString();
    }

    public override string VisitDartParameter(DartParameterNode node) => RenderParameter(node);

    private string RenderParameter(DartParameterNode node)
    {
        var sb = new StringBuilder();
        if (node.IsNamed && node.IsRequired) sb.Append("required ");
        sb.Append(RenderType(node.Type));
        sb.Append(" ");
        sb.Append(node.Name.Name);
        if (node.DefaultValueLiteral != null) sb.Append(" = " + node.DefaultValueLiteral);
        return sb.ToString();
    }

    private string RenderParameterList(IReadOnlyList<DartParameterNode> parameters)
    {
        if (parameters.Count == 0) return "()";

        var positional = parameters.Where(p => !p.IsNamed).ToList();
        var named = parameters.Where(p => p.IsNamed).ToList();

        var sb = new StringBuilder("(");
        var first = true;
        foreach (var p in positional)
        {
            if (!first) sb.Append(", ");
            sb.Append(RenderParameter(p));
            first = false;
        }
        if (named.Count > 0)
        {
            if (!first) sb.Append(", ");
            sb.Append("{");
            for (var i = 0; i < named.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(RenderParameter(named[i]));
            }
            sb.Append("}");
        }
        sb.Append(")");
        return sb.ToString();
    }

    // ------------- Types -------------

    public override string VisitSimpleType(SimpleTypeNode node) => node.Name.Name;

    public override string VisitGenericType(GenericTypeNode node)
    {
        var args = string.Join(", ", node.TypeArguments.Select(RenderType));
        return $"{node.Name.Name}<{args}>";
    }

    public override string VisitNullableType(NullableTypeNode node)
    {
        var inner = RenderType(node.InnerType);
        return inner.EndsWith("?", StringComparison.Ordinal) ? inner : inner + "?";
    }

    public override string VisitOptionalType(OptionalTypeNode node) => RenderType(node.InnerType);

    public override string VisitArrayType(ArrayTypeNode node) =>
        $"List<{RenderType(node.ElementType)}>";

    public override string VisitUnionType(UnionTypeNode node) => "dynamic";
    public override string VisitIntersectionType(IntersectionTypeNode node) => "dynamic";
    public override string VisitDictionaryType(DictionaryTypeNode node) =>
        $"Map<{RenderType(node.KeyType)}, {RenderType(node.ValueType)}>";
    public override string VisitRawType(RawTypeNode node) => node.RawTypeString;

    private string RenderType(TypeNode type) => type.Accept(this);

    // ------------- Misc visitors -------------

    public override string VisitIdentifier(IdentifierNode node) => node.Name;
    public override string VisitComment(CommentNode node) => $"// {node.Text}";
    public override string VisitAttribute(AttributeNode node) => $"@{node.Name.Name}";
    public override string VisitImport(ImportNode node) => string.Empty;
    public override string VisitExport(ExportNode node) => RenderDeclaration(node.Declaration);
    public override string VisitTypeDeclaration(TypeDeclarationNode node) => VisitDefault(node);
    public override string VisitEnumMemberDeclaration(EnumMemberDeclarationNode node) =>
        node.Name.Name;
    public override string VisitEnumDeclaration(EnumDeclarationNode node) => VisitDefault(node);
    public override string VisitInterfaceDeclaration(InterfaceDeclarationNode node) => VisitDefault(node);

    // ------------- Helpers -------------

    private string RenderDeclaration(DeclarationNode declaration) => declaration switch
    {
        DartClassDeclarationNode cls => RenderClass(cls, indent: 0),
        DartEnumDeclarationNode en   => RenderEnum(en, indent: 0),
        DartMethodNode m             => RenderMethod(m, indent: 0),
        DartFieldNode f              => RenderField(f, indent: 0),
        _ => throw new NotSupportedException(
            $"Cannot render declaration of type {declaration.GetType().Name} in Dart file")
    };

    private static string Pad(int level) => string.Concat(Enumerable.Repeat(Indent, level));

    private static void AppendDoc(StringBuilder sb, CommentNode? doc, int indent)
    {
        if (doc == null || string.IsNullOrWhiteSpace(doc.Text)) return;
        var pad = Pad(indent);
        foreach (var line in doc.Text.Split('\n'))
            sb.AppendLine($"{pad}/// {line.TrimEnd('\r')}");
    }
}
