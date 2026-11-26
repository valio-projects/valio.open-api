using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Builders;
using ValIO.Core.IR.Types;
using ValIO.Core.IR.Utilities;
using ValIO.Generators.Dart.Ast.Nodes;

namespace ValIO.Generators.Dart.Ast.Builders;

/// <summary>Finds the model and enum sources named by a Dart declaration.</summary>
internal static class DartImports
{
    /// <summary>Creates import nodes for the referenced types, using the same filenames as their producer.</summary>
    /// <param name="context">The source document indexes.</param>
    /// <param name="naming">The output naming convention.</param>
    /// <param name="types">The referenced wire types.</param>
    /// <param name="prefix">The location of model files relative to the generated file.</param>
    /// <param name="self">The declaration in the current file, if any.</param>
    internal static IReadOnlyList<DartImportNode> Build(IrDocumentContext context, INamingConverter naming,
        IEnumerable<IrType> types, string prefix, string? self = null)
    {
        var imports = new SortedSet<string>(StringComparer.Ordinal);
        var dependencies = new IrTypeDependencyGraphBuilder();
        foreach (var type in types)
        {
            if (IrTypeQueries.IsFileOrBinary(type))
                imports.Add("dart:typed_data");
            foreach (var name in dependencies.ExtractDependenciesFromType(type))
            {
                if (name == self)
                    continue;

                var original = context.ModelsByName.GetValueOrDefault(name)?.OriginalName
                    ?? context.EnumsByName.GetValueOrDefault(name)?.OriginalName;
                if (original is not null)
                    imports.Add($"{prefix}{naming.ToSnakeCase(original)}.dart");
            }
        }

        return imports.Select(import => new DartImportNode(import)).ToArray();
    }
}
