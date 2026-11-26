using ValIO.Core.Ast.Nodes;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Angular.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>Records module declarations for the barrel before the source is handed to an output sink.</summary>
internal static class TypeScriptModuleFiles
{
    /// <summary>The request-local slot shared by TypeScript producer steps and the barrel.</summary>
    private const string Key = "typescript-module-exports";

    /// <summary>Associates an exported name with its TypeScript namespace.</summary>
    /// <param name="Name">The emitted identifier.</param>
    /// <param name="IsType">Whether the identifier is only available as a type.</param>
    internal readonly record struct Symbol(string Name, bool IsType);

    /// <summary>Renders one module and records the names produced by its declarations.</summary>
    /// <param name="context">The current generator request.</param>
    /// <param name="path">The module path relative to the output root.</param>
    /// <param name="module">The module tree before rendering.</param>
    /// <param name="renderer">The printer configured for the module's syntax.</param>
    internal static void Emit(GenerationContext context, string path, SourceFileNode module, Func<SourceFileNode, string> renderer)
    {
        var exports = module.Declarations.Select(declaration => declaration switch
        {
            FunctionDeclarationNode { IsExported: true } or VariableDeclarationNode { IsExported: true } or EnumDeclarationNode
                or AngularServiceClassNode =>
                new Symbol(declaration.Name.Name, IsType: false),
            InterfaceDeclarationNode or TypeAliasDeclarationNode => new Symbol(declaration.Name.Name, IsType: true),
            _ => (Symbol?)null,
        }).OfType<Symbol>().DistinctBy(symbol => symbol.Name, StringComparer.Ordinal).ToArray();
        var source = renderer(module);
        if (!context.StepData.TryGetValue(Key, out var value))
            context.StepData[Key] = value = new Dictionary<string, IReadOnlyList<Symbol>>(StringComparer.Ordinal);

        var modules = (Dictionary<string, IReadOnlyList<Symbol>>)value;
        modules[path.Replace('\\', '/')] = exports;
        context.GeneratedFiles.Add(new GeneratedFile(path, source));
    }

    /// <summary>Gets the export metadata collected from all emitted module trees.</summary>
    /// <param name="context">The current generator request.</param>
    /// <returns>Exports keyed by the normalized output path.</returns>
    internal static IReadOnlyDictionary<string, IReadOnlyList<Symbol>> Exports(GenerationContext context) =>
        context.StepData.TryGetValue(Key, out var value)
            ? (Dictionary<string, IReadOnlyList<Symbol>>)value
            : new Dictionary<string, IReadOnlyList<Symbol>>(StringComparer.Ordinal);
}
