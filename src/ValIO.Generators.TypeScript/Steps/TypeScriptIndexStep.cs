using ValIO.Core.Ast.Nodes;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Ast.JsDoc;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Renderers;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>Builds the root barrel from the exports recorded by TypeScript module trees.</summary>
public sealed class TypeScriptIndexStep : ICodeGenerationStep
{
    /// <inheritdoc />
    public string Name => "typescript-index";

    /// <inheritdoc />
    public int Order => 70;

    /// <inheritdoc />
    public bool ShouldExecute(GenerationContext context) => TypeScriptModuleFiles.Exports(context).Count > 0;

    /// <inheritdoc />
    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var root = context.Configuration.FileOrganization.ModelsPath ?? "models";
        var modelsPath = root.Replace('\\', '/').TrimEnd('/') + "/";
        var paths = TypeScriptModuleFiles.Exports(context)
            .Where(module => module.Key != "index.ts" && !module.Key.EndsWith("/index.ts", StringComparison.Ordinal))
            .OrderBy(module => !module.Key.StartsWith(modelsPath, StringComparison.Ordinal))
            .ThenBy(module => module.Key, StringComparer.Ordinal);
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        var declarations = new SortedDictionary<string, IReadOnlyList<ModuleReExportNode>>(StringComparer.Ordinal);
        foreach (var (path, exports) in paths)
        {
            ct.ThrowIfCancellationRequested();
            var module = "./" + path[..^3];
            var fresh = exports.Where(symbol => claimed.Add(symbol.Name)).ToArray();
            if (fresh.Length == exports.Count)
            {
                declarations.Add(path, [new ModuleReExportNode(module)]);
                continue;
            }

            var values = BuildNamedExports(fresh, isType: false);
            var types = BuildNamedExports(fresh, isType: true);
            var selected = new List<ModuleReExportNode>(2);
            if (values.Length > 0)
                selected.Add(new ModuleReExportNode(module, values));
            if (types.Length > 0)
                selected.Add(new ModuleReExportNode(module, types, typeOnly: true));
            declarations.Add(path, selected);
        }

        var reExports = declarations.Values.SelectMany(items => items).ToArray();
        if (reExports.Length > 0)
        {
            var header = TypeScriptDoc.FileHeader("index.ts", "index", "Barrel file re-exporting all generated TypeScript modules.");
            var module = new SourceFileNode(declarations: reExports, fileDocumentation: header);
            var renderer = new TypeScriptAstRenderer();
            context.GeneratedFiles.Add(new GeneratedFile("index.ts", renderer.Render(module)));
        }
        return Task.CompletedTask;
    }

    /// <summary>Sorts the named exports for one TypeScript namespace.</summary>
    /// <param name="symbols">The names still available in the barrel.</param>
    /// <param name="isType">The namespace to collect.</param>
    /// <returns>The identifiers for the named export clause.</returns>
    private static IdentifierNode[] BuildNamedExports(IEnumerable<TypeScriptModuleFiles.Symbol> symbols, bool isType) =>
        symbols.Where(symbol => symbol.IsType == isType).OrderBy(symbol => symbol.Name, StringComparer.Ordinal)
            .Select(symbol => new IdentifierNode(symbol.Name)).ToArray();
}
