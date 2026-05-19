using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.Java.Ast.Nodes;

namespace ValIO.Generators.Java.Ast.Builders;

/// <summary>
/// Строит <see cref="JavaSourceFileNode"/> с record-декларацией для одной IR-модели.
/// </summary>
public sealed class JavaRecordModelAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly JavaIrTypeMapper _typeMapper;

    public JavaRecordModelAstBuilder(INamingConverter namingConverter, JavaIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
    }

    public JavaSourceFileNode BuildSourceFile(IrModel model, string rootPackage)
    {
        var pkg = NormalizePackage($"{rootPackage}.models");

        var components = new List<ParameterDeclarationNode>();
        var importTargets = new HashSet<string>(StringComparer.Ordinal);

        foreach (var prop in model.Properties)
        {
            var isOptional = _typeMapper.IsOptionalInLanguage(prop);
            // Required + primitive → unboxed (int/long/double/boolean). Optional → boxed.
            var typeNode = _typeMapper.MapType(prop.Type, preferUnboxed: !isOptional);
            foreach (var im in _typeMapper.CollectImports(prop.Type)) importTargets.Add(im);

            var name = SanitizeComponentName(_namingConverter.ToCamelCase(prop.Name));
            components.Add(new ParameterDeclarationNode(
                name: new IdentifierNode(name),
                type: typeNode,
                isRequired: prop.IsRequired));
        }

        var docLines = new List<string>();
        if (!string.IsNullOrWhiteSpace(model.Description))
            docLines.Add(model.Description!);
        var doc = docLines.Count > 0 ? new CommentNode(string.Join("\n", docLines), CommentKind.MultiLine) : null;

        var record = new JavaRecordDeclarationNode(
            name: new IdentifierNode(model.Name),
            components: components,
            implements: null,
            members: ImmutableList<DeclarationNode>.Empty,
            documentation: doc);

        var imports = importTargets
            .Select(t => new JavaImportNode(t))
            .ToImmutableList();

        return new JavaSourceFileNode(
            package: pkg,
            imports: imports,
            declarations: new DeclarationNode[] { record }.ToImmutableList());
    }

    private static string NormalizePackage(string pkg) => pkg.Trim('.').Replace('/', '.');

    private static string SanitizeComponentName(string name) =>
        JavaReservedWords.Reserved.Contains(name) ? name + "_" : name;
}
