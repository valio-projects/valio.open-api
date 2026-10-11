using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Plugins;

namespace ValIO.Generators.TypeScript.Tests.TestHelpers;

/// <summary>Runs generated TypeScript modules using external Node dependencies without modifying repository packages.</summary>
internal static class GeneratedTypeScriptRuntime
{
    /// <summary>Transpiles generated modules and executes assertions against their runtime behavior.</summary>
    /// <param name="context">The completed generation pipeline result.</param>
    /// <param name="runtimeScript">JavaScript assertions with root, path, assert, and require available.</param>
    /// <returns>The process output after successful execution.</returns>
    /// <exception cref="OperationCanceledException">The Node process exceeds the execution timeout.</exception>
    public static async Task<string> Run(GenerationContext context, string runtimeScript)
    {
        var modules = Environment.GetEnvironmentVariable("VALIO_VALIDATOR_NODE_MODULES");
        if (string.IsNullOrWhiteSpace(modules) || !Directory.Exists(modules))
            Assert.Ignore("Set VALIO_VALIDATOR_NODE_MODULES to run real-library Node integration tests.");

        var workspace = Path.Combine(Path.GetTempPath(), $"valio-runtime-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        try
        {
            foreach (var file in context.GeneratedFiles.Where(file => file.RelativePath.EndsWith(".ts", StringComparison.Ordinal)))
            {
                var path = Path.Combine(workspace, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, file.Content, Encoding.UTF8);
            }
            var script = Path.Combine(workspace, "validate.cjs");
            await File.WriteAllTextAsync(script, Bootstrap(workspace, modules!) + Environment.NewLine + runtimeScript, Encoding.UTF8);
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("node")
                {
                    WorkingDirectory = workspace,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.StartInfo.ArgumentList.Add(script);
            process.StartInfo.Environment["NODE_PATH"] = modules!;
            process.Start();
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }
            var output = await standardOutput + await standardError;
            process.ExitCode.Should().Be(0, "generated modules must satisfy runtime contracts: {0}", output);
            return output;
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    /// <summary>Creates shared transpilation infrastructure for generated modules.</summary>
    /// <param name="workspace">The generated module directory.</param>
    /// <param name="modules">The external node_modules directory.</param>
    /// <returns>The JavaScript bootstrap, defining root, path, and assert for callers.</returns>
    private static string Bootstrap(string workspace, string modules) => $$"""
        const fs = require('node:fs');
        const path = require('node:path');
        const assert = require('node:assert/strict');
        const ts = require(path.join({{JsonSerializer.Serialize(modules)}}, 'typescript'));
        const root = {{JsonSerializer.Serialize(workspace)}};
        function compile(dir) {
          for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
            const file = path.join(dir, entry.name);
            if (entry.isDirectory()) compile(file);
            else if (file.endsWith('.ts')) {
              const result = ts.transpileModule(fs.readFileSync(file, 'utf8'), {
                compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
              });
              fs.writeFileSync(file.slice(0, -3) + '.js', result.outputText);
            }
          }
        }
        compile(root);
        """;
}
