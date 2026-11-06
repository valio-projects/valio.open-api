using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>Checks that allOf validation retains inherited and locally narrowed constraints.</summary>
[TestFixture]
[Category("NodeIntegration")]
public sealed class ZodAllOfNarrowingTests
{
    /// <summary>Compiles and executes generated schemas against both Zod major versions.</summary>
    /// <param name="version">The selected validation library.</param>
    /// <returns>The asynchronous inherited constraint check.</returns>
    [TestCase("zod3")]
    [TestCase("zod4")]
    public async Task Child_requires_nonnull_id_and_preserves_both_length_constraints(string version)
    {
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentAsync("allof-narrowing.json");
        var context = await harness.RunAsync(document, new TypeScriptGenerationOptions
        { Transports = ["fetch"], ValidationLibrary = version });
        await GeneratedTypeScriptCompiler.Compile(context.GeneratedFiles.Where(file => file.RelativePath.EndsWith(".ts", StringComparison.Ordinal)).ToArray(),
            "allOf narrowing with " + version);
        await GeneratedTypeScriptRuntime.Run(context, """
            const schema = require(path.join(root, 'models/child/child.schema.js')).ChildSchema;
            assert.equal(schema.safeParse({name:'n',id:'abc'}).success, true);
            for (const value of [{name:'n'},{name:'n',id:null},{name:'n',id:'ab'},{name:'n',id:'abcdefg'},{id:'abc'}])
              assert.equal(schema.safeParse(value).success, false, JSON.stringify(value));
            """);
    }
}
