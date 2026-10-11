using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;
using ValIO.Tests.Fixtures;

namespace ValIO.Generators.TypeScript.Tests.Integration;

/// <summary>Type-checks complete generated clients and schemas against real transport and validator libraries.</summary>
/// <remarks>
/// Set VALIO_VALIDATOR_NODE_MODULES to an external dependency directory containing TypeScript, the selected
/// validators, and the transport packages. An absent variable skips this explicit integration fixture;
/// an invalid directory, missing dependency, or compiler error fails it. Failed output directories are
/// retained outside the repository so the exact generated sources and diagnostics remain inspectable.
/// </remarks>
[TestFixture]
[Category("NodeIntegration")]
[NonParallelizable]
public sealed class GeneratedTypeScriptCompileTests
{
    /// <summary>Supplies every HTTP transport and supported validator, including generation without validation.</summary>
    /// <returns>The independent strict-compilation cases.</returns>
    private static IEnumerable<TestCaseData> Cases()
    {
        foreach (var transport in new[]
        {
            "fetch", "axios", "nextjs", "ky", "got", "ofetch", "undici", "angular-rxjs", "angular-promise", "solid"
        })
        {
            foreach (var validation in new[] { "none", "zod3", "zod4", "valibot", "typebox" })
                yield return new TestCaseData(transport, validation).SetName($"GeneratedTypeScriptCompile [{transport}, {validation}]");
        }
    }

    /// <summary>Compiles actual pipeline output covering model composition, enums, request encodings, and empty responses.</summary>
    /// <param name="transport">The HTTP client or framework transport to generate.</param>
    /// <param name="validation">The selected validator, or <c>none</c> to omit runtime schemas.</param>
    /// <returns>The asynchronous generation and TypeScript compiler check.</returns>
    [TestCaseSource(nameof(Cases))]
    public async Task GeneratedClientAndSchemas_CompileWithStrictTypeChecking(string transport, string validation)
    {
        var modules = NodeModulesDirectory();
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentFromJsonAsync(OpenApiFixture.Read("TypeScriptCompile/compile-contracts-3.1.json"));
        var context = await harness.RunAsync(document, new TypeScriptGenerationOptions
        {
            Transports = [transport],
            ValidationLibrary = validation == "none" ? null : validation,
        });

        var files = context.GeneratedFiles.Where(file => file.RelativePath.EndsWith(".ts", StringComparison.Ordinal)).ToArray();
        files.Should().NotBeEmpty("the actual pipeline must emit TypeScript source files");
        files.Select(file => file.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count().Should().Be(files.Length,
            "a compilation must not overwrite generated paths that differ only by letter casing");
        var clientSuffix = transport is "angular-rxjs" or "angular-promise" ? ".service.ts" : $".{transport}.ts";
        files.Should().Contain(file => file.RelativePath.EndsWith(clientSuffix, StringComparison.Ordinal), "the requested transport must be generated");
        files.Should().Contain(file => file.RelativePath.EndsWith("state/state.ts", StringComparison.Ordinal), "named enums must participate in the compilation");
        if (validation != "none")
            files.Should().Contain(file => file.RelativePath.EndsWith(".schema.ts", StringComparison.Ordinal), "the selected validator must contribute real schemas");

        var workspace = Path.Combine(Path.GetTempPath(), $"valio-ts-compile-{transport}-{validation}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        var succeeded = false;
        try
        {
            var sourcePaths = await WriteSources(workspace, files);
            var scriptPath = Path.Combine(workspace, "compile.cjs");
            await File.WriteAllTextAsync(scriptPath, CompilerScript(workspace, modules, sourcePaths), Encoding.UTF8);
            await RunCompiler(workspace, scriptPath, transport, validation);
            succeeded = true;
        }
        finally
        {
            if (succeeded)
                Directory.Delete(workspace, recursive: true);
            else
                TestContext.Out.WriteLine($"Generated TypeScript compilation output retained at: {workspace}");
        }
    }

    /// <summary>Resolves the explicitly configured real-library environment without hiding setup errors.</summary>
    /// <returns>The absolute dependency directory.</returns>
    private static string NodeModulesDirectory()
    {
        var modules = Environment.GetEnvironmentVariable("VALIO_VALIDATOR_NODE_MODULES");
        if (string.IsNullOrWhiteSpace(modules))
        {
            Assert.Ignore("Set VALIO_VALIDATOR_NODE_MODULES to run generated TypeScript compilation against real libraries.");
            return string.Empty;
        }

        Directory.Exists(modules).Should().BeTrue("VALIO_VALIDATOR_NODE_MODULES is set and must identify an existing dependency directory: {0}", modules);
        File.Exists(Path.Combine(modules, "typescript", "lib", "typescript.js")).Should().BeTrue("the configured environment must contain the real TypeScript compiler");
        return Path.GetFullPath(modules);
    }

    /// <summary>Materializes only the generated TypeScript files in a unique temporary compilation directory.</summary>
    /// <param name="workspace">The external temporary directory owned by this test case.</param>
    /// <param name="files">The actual files returned by the generation pipeline.</param>
    /// <returns>The absolute source paths supplied to the compiler.</returns>
    private static async Task<IReadOnlyList<string>> WriteSources(string workspace, IReadOnlyList<GeneratedFile> files)
    {
        var sourcePaths = new List<string>();
        foreach (var file in files)
        {
            var path = Path.GetFullPath(Path.Combine(workspace, file.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            path.Should().StartWith(workspace + Path.DirectorySeparatorChar, "generated files must remain inside the test workspace");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, file.Content, Encoding.UTF8);
            sourcePaths.Add(path);
        }
        return sourcePaths;
    }

    /// <summary>Runs the real TypeScript semantic compiler and reports its complete diagnostics on failure.</summary>
    /// <param name="workspace">The generated sources and compiler script directory.</param>
    /// <param name="scriptPath">The Node script invoking the TypeScript compiler API.</param>
    /// <param name="transport">The transport included in diagnostic context.</param>
    /// <param name="validation">The validator included in diagnostic context.</param>
    /// <returns>The asynchronous compiler process check.</returns>
    private static async Task RunCompiler(string workspace, string scriptPath, string transport, string validation)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("node")
            {
                WorkingDirectory = workspace,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.StartInfo.ArgumentList.Add(scriptPath);
        process.Start().Should().BeTrue("Node must start the TypeScript compiler process");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"TypeScript compilation timed out for {transport}/{validation}. Sources: {workspace}\n{await outputTask}\n{await errorTask}");
        }

        var output = await outputTask + await errorTask;
        process.ExitCode.Should().Be(0,
            "all generated sources must compile under TypeScript strict mode for {0}/{1}. Sources: {2}\nCompiler diagnostics:\n{3}",
            transport, validation, workspace, output);
        output.Should().Contain("strict compilation passed", "the compiler must finish its semantic check");
    }

    /// <summary>Builds a compiler program that resolves imports from the actual external dependency installation.</summary>
    /// <param name="workspace">The temporary generated-source root.</param>
    /// <param name="modules">The external node_modules directory containing real package declarations.</param>
    /// <param name="sourcePaths">Every emitted TypeScript source path, including clients, models, schemas, and exports.</param>
    /// <returns>A CommonJS Node script that fails on any compiler diagnostic.</returns>
    private static string CompilerScript(string workspace, string modules, IReadOnlyList<string> sourcePaths) => $$"""
        const path = require('node:path');
        const modules = {{JsonSerializer.Serialize(modules)}};
        const ts = require(path.join(modules, 'typescript', 'lib', 'typescript.js'));
        const root = {{JsonSerializer.Serialize(workspace)}};
        const files = {{JsonSerializer.Serialize(sourcePaths)}};
        const options = {
          strict: true,
          noEmit: true,
          target: ts.ScriptTarget.ES2022,
          module: ts.ModuleKind.ESNext,
          moduleResolution: ts.ModuleResolutionKind.Bundler,
          esModuleInterop: true,
          allowSyntheticDefaultImports: true,
          experimentalDecorators: true,
          forceConsistentCasingInFileNames: true,
          skipLibCheck: true,
          lib: ['lib.es2022.d.ts', 'lib.dom.d.ts', 'lib.dom.iterable.d.ts'],
          types: ['node'],
          typeRoots: [path.join(modules, '@types')]
        };
        const host = ts.createCompilerHost(options);
        const cache = ts.createModuleResolutionCache(root, file => file, options);
        const dependencyEntry = path.join(path.dirname(modules), '__valio_dependency_lookup__.ts');
        host.resolveModuleNames = (names, containingFile) => names.map(name => {
          let resolved = ts.resolveModuleName(name, containingFile, options, host, cache).resolvedModule;
          if (!resolved && !name.startsWith('.') && !path.isAbsolute(name))
            resolved = ts.resolveModuleName(name, dependencyEntry, options, host, cache).resolvedModule;
          return resolved;
        });
        const program = ts.createProgram(files, options, host);
        const diagnostics = ts.getPreEmitDiagnostics(program);
        if (diagnostics.length > 0) {
          process.stderr.write(ts.formatDiagnosticsWithColorAndContext(diagnostics, {
            getCanonicalFileName: file => file,
            getCurrentDirectory: () => root,
            getNewLine: () => '\n'
          }));
          process.exitCode = 1;
        } else {
          console.log(`strict compilation passed: ${files.length} generated files, TypeScript ${ts.version}`);
        }
        """;
}
