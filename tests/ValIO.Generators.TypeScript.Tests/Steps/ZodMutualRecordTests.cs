using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>Checks cyclic typed records in generated ES modules.</summary>
[TestFixture]
[Category("NodeIntegration")]
public sealed class ZodMutualRecordTests
{
    /// <summary>Compiles and imports mutually recursive record schemas against both Zod versions.</summary>
    /// <param name="version">The selected validation library.</param>
    /// <returns>The asynchronous native ESM initialization and nested validation check.</returns>
    [TestCase("zod3")]
    [TestCase("zod4")]
    public async Task Mutual_records_validate_nested_values_in_esm(string version)
    {
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentAsync("mutual-record-cycles.json");
        var context = await harness.RunAsync(document, new TypeScriptGenerationOptions
        { Transports = ["fetch"], ValidationLibrary = version });
        await GeneratedTypeScriptCompiler.Compile(context.GeneratedFiles.Where(file => file.RelativePath.EndsWith(".ts", StringComparison.Ordinal)).ToArray(),
            "mutually recursive records with " + version);
        await GeneratedTypeScriptRuntime.Run(context, """
            (async () => {
              const { pathToFileURL } = require('node:url');
              const { AlphaSchema: schema } = await import(pathToFileURL(path.join(root, 'models/alpha/alpha.schema.js')).href);
              assert.equal(schema.safeParse({name:'root',children:{entry:[{rank:'v',children:{back:{name:'inner'}}}]}}).success, true);
              for (const value of [{name:'root',children:{entry:[{rank:42}]}},
                {name:'root',children:{entry:[{rank:'v',children:{back:{name:123}}}]}}])
                assert.equal(schema.safeParse(value).success, false, JSON.stringify(value));
            })().catch(error => { console.error(error); process.exitCode = 1; });
            """, useEsModules: true);
    }
}
