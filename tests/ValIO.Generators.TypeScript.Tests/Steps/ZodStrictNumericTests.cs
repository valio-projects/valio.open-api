using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR.Enums;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;
using ValIO.Generators.TypeScript.Zod.Ast.Builders;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>Checks strict numeric validation and culture-independent generation.</summary>
[TestFixture]
[Category("NodeIntegration")]
public sealed class ZodStrictNumericTests
{
    /// <summary>Checks identical numeric output across cultures and executes each Zod version once.</summary>
    /// <param name="version">The selected validation library.</param>
    /// <returns>The asynchronous numeric compilation and runtime check.</returns>
    [TestCase("zod3")]
    [TestCase("zod4")]
    public async Task Model_numbers_reject_coercion_and_enforce_constraints(string version)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        GenerationContext? context = null;
        string? baseline = null;
        try
        {
            foreach (var culture in new[] { "en-US", "ru-RU" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                using var harness = new PipelineTestHarness();
                var document = await PipelineTestHarness.LoadDocumentAsync("strict-numeric.json");
                context = await harness.RunAsync(document, new TypeScriptGenerationOptions
                { Transports = ["fetch"], ValidationLibrary = version });
                var schema = context.GeneratedFiles.Single(file => file.RelativePath.EndsWith("numericModel.schema.ts", StringComparison.Ordinal)).Content;
                if (baseline is not null) schema.Should().Be(baseline);
                baseline = schema;
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }

        await GeneratedTypeScriptCompiler.Compile(context!.GeneratedFiles.Where(file => file.RelativePath.EndsWith(".ts", StringComparison.Ordinal)).ToArray(),
            "strict numeric schemas with " + version);
        var bigint = new TypeScriptAstRenderer().Visit(
            ZodExpressionBuilder.BuildPrimitiveExpression(IrPrimitiveKind.Long, useBigInt: true).Expression);
        await GeneratedTypeScriptRuntime.Run(context, $$"""
            const z = require({{JsonSerializer.Serialize(version)}}).z;
            const schema = require(path.join(root, 'models/numericModel/numericModel.schema.js')).NumericModelSchema;
            const ok = {count:7,price:1.5,nullableCount:null,longValue:12};
            assert.equal(schema.safeParse(ok).success, true);
            assert.equal(schema.safeParse({...ok,nullableCount:0}).success, true);
            const bad = [...[-1,1.5,'7',null].map(count => ({...ok,count})),
              {...ok,price:'1.5'},{...ok,price:0.4},{...ok,nullableCount:'2'},
              {...ok,longValue:'12'},{...ok,longValue:null},{...ok,longValue:1.5}];
            for (const value of bad) assert.equal(schema.safeParse(value).success, false, JSON.stringify(value));
            const big = {{bigint}};
            assert.equal(big.safeParse(12n).success, true);
            assert.equal(big.safeParse('12').success, false);
            assert.equal(big.safeParse(null).success, false);
            """);
    }
}
