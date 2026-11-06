using System.Text.Json;
using ValIO.Core.Plugins;

namespace ValIO.Generators.TypeScript.Tests.TestHelpers;

/// <summary>Runs generated TypeScript modules using external Node dependencies without modifying repository packages.</summary>
internal static class GeneratedTypeScriptRuntime
{
    /// <summary>Transpiles generated modules and executes assertions against their runtime behavior.</summary>
    /// <param name="context">The completed generation pipeline result.</param>
    /// <param name="runtimeScript">JavaScript assertions with root, path, assert, and require available.</param>
    /// <param name="useEsModules">Whether to load generated modules as native ESM to exercise initialization order.</param>
    /// <returns>The process output after successful execution.</returns>
    public static async Task<string> Run(GenerationContext context, string runtimeScript, bool useEsModules = false)
    {
        using var workspace = new GeneratedTypeScriptTestWorkspace();
        var files = context.GeneratedFiles.Where(file => file.RelativePath.EndsWith(".ts", StringComparison.Ordinal)).ToArray();
        var sourcePaths = await workspace.WriteSources(files);
        var script = Bootstrap(workspace.Root, workspace.Modules, sourcePaths, useEsModules) + Environment.NewLine + runtimeScript;
        var output = await workspace.RunNode("runtime.cjs", script, "generated module runtime contracts");
        workspace.MarkSuccessful();
        return output;
    }

    /// <summary>Creates transpilation infrastructure using real dependency packages and independently pinned Zod major versions.</summary>
    /// <param name="workspace">The generated module directory.</param>
    /// <param name="modules">The external node_modules directory.</param>
    /// <param name="sourcePaths">The validated generated TypeScript files to transpile.</param>
    /// <param name="useEsModules">Whether transpiled modules retain native ESM imports.</param>
    /// <returns>The JavaScript bootstrap, defining root, path, and assert for callers.</returns>
    private static string Bootstrap(string workspace, string modules, IReadOnlyList<string> sourcePaths, bool useEsModules) => $$"""
        const fs = require('node:fs');
        const path = require('node:path');
        const assert = require('node:assert/strict');
        const nativeModuleLoader = require('node:module');
        const loadNativeDependency = nativeModuleLoader._load;
        nativeModuleLoader._load = function(request, ...args) {
          const dependency = request === 'zod' ? 'zod3' : request === 'zod/v4' ? 'zod4' : request;
          return loadNativeDependency.call(this, dependency, ...args);
        };
        const ts = require(path.join({{JsonSerializer.Serialize(modules)}}, 'typescript', 'lib', 'typescript.js'));
        const root = {{JsonSerializer.Serialize(workspace)}};
        const files = {{JsonSerializer.Serialize(sourcePaths)}};
        const useEsModules = {{JsonSerializer.Serialize(useEsModules)}};
        if (useEsModules) {
          const { pathToFileURL } = require('node:url');
          const dependencyEntry = pathToFileURL(path.join(path.dirname({{JsonSerializer.Serialize(modules)}}), '__valio_dependency_lookup__.mjs')).href;
          fs.writeFileSync(path.join(root, 'package.json'), '{"type":"module"}');
          nativeModuleLoader.registerHooks({
            resolve(specifier, context, nextResolve) {
              if (specifier === 'zod' || specifier === 'zod/v4')
                return nextResolve(specifier === 'zod' ? 'zod3' : 'zod4', { ...context, parentURL: dependencyEntry });
              if (specifier.startsWith('.') && !path.extname(specifier).endsWith('.js'))
                return nextResolve(specifier + '.js', context);
              return nextResolve(specifier, context);
            }
          });
        }
        for (const file of files) {
          const result = ts.transpileModule(fs.readFileSync(file, 'utf8'), {
            compilerOptions: {
              module: useEsModules ? ts.ModuleKind.ESNext : ts.ModuleKind.CommonJS,
              target: ts.ScriptTarget.ES2022,
              experimentalDecorators: true
            },
            reportDiagnostics: true,
            fileName: file
          });
          if (result.diagnostics?.length) {
            throw new Error(ts.formatDiagnosticsWithColorAndContext(result.diagnostics, {
              getCanonicalFileName: file => file,
              getCurrentDirectory: () => root,
              getNewLine: () => '\n'
            }));
          }
          fs.writeFileSync(file.slice(0, -3) + '.js', result.outputText);
        }
        """;
}
