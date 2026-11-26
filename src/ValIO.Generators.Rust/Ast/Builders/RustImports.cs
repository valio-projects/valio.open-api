using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Builders;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.Rust.Ast.Builders;

/// <summary>Builds Rust use declarations for generated models and serialization traits.</summary>
internal static class RustImports
{
    /// <summary>Imports the traits used by the generated Serde derives.</summary>
    internal static ImportNode Serde() => new([new("Deserialize"), new("Serialize")], "serde");

    /// <summary>Finds referenced declarations using their stable module filenames.</summary>
    /// <param name="context">The document's model and enum indexes.</param>
    /// <param name="naming">The naming converter shared with the file producers.</param>
    /// <param name="types">The schema types referenced by this file.</param>
    /// <param name="self">The declaration already defined in this module, if any.</param>
    internal static IEnumerable<ImportNode> Models(IrDocumentContext context, INamingConverter naming,
        IEnumerable<IrType> types, string? self = null)
    {
        var dependencies = new IrTypeDependencyGraphBuilder();
        foreach (var name in types.SelectMany(dependencies.ExtractDependenciesFromType).Distinct(StringComparer.Ordinal))
        {
            if (name == self)
                continue;

            var original = context.ModelsByName.GetValueOrDefault(name)?.OriginalName
                ?? context.EnumsByName.GetValueOrDefault(name)?.OriginalName;
            if (original is null)
                continue;

            var module = naming.ToSnakeCase(original);
            yield return new ImportNode([new(naming.ToPascalCase(name))], $"crate::models::{module}");
        }
    }
}
