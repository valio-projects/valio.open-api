using ValIO.Core.IR;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>Collects inherited declarations shared by TypeScript models and their validators.</summary>
internal static class TypeScriptModelProperties
{
    /// <summary>Groups reachable allOf base properties without discarding repeated wire names.</summary>
    /// <param name="model">The model whose inherited declarations are required.</param>
    /// <param name="modelsByName">The available models indexed by generated name.</param>
    /// <returns>The inherited declarations grouped by original wire name in base traversal order.</returns>
    public static Dictionary<string, List<IrProperty>> CollectInherited(
        IrModel model, IReadOnlyDictionary<string, IrModel> modelsByName)
    {
        var result = new Dictionary<string, List<IrProperty>>(StringComparer.Ordinal);
        CollectFromBases(model, modelsByName, new HashSet<string>(StringComparer.Ordinal) { model.Name }, result);
        return result;
    }

    /// <summary>Visits each reachable base once while retaining its declared property contracts.</summary>
    /// <param name="model">The current model whose direct bases are inspected.</param>
    /// <param name="modelsByName">The available models indexed by generated name.</param>
    /// <param name="visited">The model names already visited in this traversal.</param>
    /// <param name="properties">The inherited declarations grouped by wire name.</param>
    private static void CollectFromBases(IrModel model, IReadOnlyDictionary<string, IrModel> modelsByName,
        HashSet<string> visited, Dictionary<string, List<IrProperty>> properties)
    {
        if (model.Composition is not { Kind: IrCompositionKind.AllOf } composition)
            return;
        foreach (var reference in composition.Types.OfType<IrModelReference>())
        {
            if (!visited.Add(reference.ModelName) || !modelsByName.TryGetValue(reference.ModelName, out var parent))
                continue;
            foreach (var property in parent.Properties)
            {
                if (!properties.TryGetValue(property.OriginalName, out var declarations))
                    properties[property.OriginalName] = declarations = [];
                declarations.Add(property);
            }
            CollectFromBases(parent, modelsByName, visited, properties);
        }
    }
}
