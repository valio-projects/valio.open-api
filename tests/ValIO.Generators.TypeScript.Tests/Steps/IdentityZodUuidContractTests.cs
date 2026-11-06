using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>Exercises the Identity contract for Zod optionality, typed records and shared UUID formats.</summary>
[TestFixture]
public sealed class IdentityZodUuidContractTests
{
    /// <summary>Builds a generated pipeline for a single isolated test.</summary>
    /// <param name="fixture">The embedded OpenAPI document name.</param>
    /// <param name="validation">The Zod version to use.</param>
    /// <param name="transport">The native HTTP transport to generate.</param>
    /// <returns>The generated files.</returns>
    private static async Task<GenerationContext> Generate(string fixture, string validation, string transport = "axios")
    {
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentAsync(fixture);
        return await harness.RunAsync(document, new TypeScriptGenerationOptions
        { Transports = [transport], ValidationLibrary = validation, UuidFormat = "regex" });
    }

    /// <summary>Checks platform line endings in generated Zod schemas and multiline TypeScript clients.</summary>
    /// <param name="version">The selected Zod version.</param>
    /// <returns>A task that completes after generated files have consistent line endings.</returns>
    [TestCase("zod3")]
    [TestCase("zod4")]
    public async Task GeneratedSchemasAndClientsUseConsistentLineEndings(string version)
    {
        var context = await Generate("zod-contract.json", version);
        var schema = context.GeneratedFiles.First(f => f.RelativePath.Replace('\\', '/').EndsWith("models/contract/contract.schema.ts", StringComparison.Ordinal));
        var client = context.GeneratedFiles.First(f => f.RelativePath.EndsWith(".axios.ts", StringComparison.Ordinal));
        foreach (var file in new[] { schema, client })
        {
            var remainder = file.Content.Replace(Environment.NewLine, "", StringComparison.Ordinal);
            remainder.Should().NotContain("\n", file.RelativePath).And.NotContain("\r", file.RelativePath);
        }
        foreach (var file in context.GeneratedFiles.Where(f => f.RelativePath.EndsWith(".ts", StringComparison.Ordinal)))
        {
            var withoutCrLf = file.Content.Replace("\r\n", "", StringComparison.Ordinal);
            withoutCrLf.Should().NotContain("\r", file.RelativePath);
            if (file.Content.Contains("\r\n", StringComparison.Ordinal))
                withoutCrLf.Should().NotContain("\n", file.RelativePath);
        }
    }

    /// <summary>Loads emitted model and endpoint modules to check presence and typed dictionaries at runtime.</summary>
    /// <param name="version">The selected Zod version.</param>
    /// <returns>The asynchronous generated-module contract check.</returns>
    [TestCase("zod3")]
    [TestCase("zod4")]
    [Category("NodeIntegration")]
    public async Task Generated_model_preserves_presence_and_dictionary_values(string version)
    {
        var context = await Generate("zod-contract.json", version);
        await GeneratedTypeScriptCompiler.Compile(context.GeneratedFiles
            .Where(file => file.RelativePath.EndsWith(".ts", StringComparison.Ordinal)).ToArray(),
            "model presence and dictionary values with " + version);
        await GeneratedTypeScriptRuntime.Run(context, """
            const schema = require(path.join(root, 'models/contract/contract.schema.js')).ContractSchema;
            const ok = {requiredValue:'a',requiredNullable:null,requiredNullableDictionary:null,nullableValues:{a:null}};
            assert.equal(schema.safeParse(ok).success, true);
            assert.equal(schema.safeParse({...ok,optionalNullable:null,optionalNullableArray:null}).success, true);
            assert.equal(schema.safeParse({...ok,optionalNullable:'a',optionalNullableArray:[1],children:{child:ok}}).success, true);
            const bad = [{...ok,optionalValue:null},{...ok,errors:{a:[42]}},
              {...ok,requiredNullable:undefined},{...ok,requiredValue:undefined},{...ok,requiredValue:null},
              {...ok,requiredNullableDictionary:undefined},{...ok,requiredNullableDictionary:{a:5}},
              {...ok,optionalNullableArray:['1']},{...ok,children:{child:{...ok,requiredValue:42}}}];
            for (const value of bad) assert.equal(schema.safeParse(value).success, false, JSON.stringify(value));
            const axios = require('axios');
            let calls = 0, responseBody = {entry:['valid']};
            const client = axios.create({adapter: async config => {
              calls++;
              return {status:200,data:responseBody,statusText:'',headers:{},config};
            }});
            const submit = require(path.join(root, 'api/postDictionaries/postDictionaries.axios.js')).postDictionaries;
            (async () => {
              const result = await submit({body:{entry:['valid']}},undefined,undefined,client);
              assert.deepEqual(result.body, {entry:['valid']});
              assert.equal(calls, 1);
              await assert.rejects(() => submit({body:{entry:[42]}},undefined,undefined,client));
              assert.equal(calls, 1, 'Invalid dictionary values must fail before HTTP');
              responseBody = {entry:[42]};
              await assert.rejects(() => submit({body:{entry:['valid']}},undefined,undefined,client));
            })().catch(error => { console.error(error); process.exitCode = 1; });
            """);
    }

    /// <summary>Checks shared UUID discovery even when no model contains UUIDs.</summary>
    /// <param name="fixture">The document with one UUID usage.</param>
    /// <returns>A task that completes after UUID discovery and shared imports are checked.</returns>
    [TestCase("uuid-only-response.json")]
    [TestCase("uuid-only-body.json")]
    [TestCase("uuid-only-header.json")]
    public async Task Inline_uuid_uses_generate_shared_formats(string fixture)
    {
        var context = await Generate(fixture, "zod4");
        context.GeneratedFiles.Should().Contain(f => f.RelativePath.Replace('\\', '/').EndsWith("models/formats.ts", StringComparison.Ordinal));
        if (fixture == "uuid-only-response.json")
        {
            var result = context.GeneratedFiles.First(f => f.RelativePath.EndsWith("getResponseOnly.results.schemas.ts", StringComparison.Ordinal)).Content;
            result.Should().Contain("import { UUID_REGEX } from \"../../models/formats\"");
            result.Should().Contain("z.string().regex(UUID_REGEX)");
        }
    }

    /// <summary>Executes the emitted UUID module against trailing-character boundary cases.</summary>
    /// <returns>The asynchronous JavaScript regular-expression check.</returns>
    [Test]
    [Category("NodeIntegration")]
    public async Task Generated_uuid_regex_rejects_every_trailing_character()
    {
        var context = await Generate("uuid-only-response.json", "zod4");
        await GeneratedTypeScriptRuntime.Run(context, """
            const { UUID_REGEX } = require(path.join(root, 'models/formats.js'));
            const uuid = '78d459e0-1269-40cf-b520-d31c21b13178';
            for (const value of [uuid,uuid.toUpperCase(),'00000000-0000-0000-0000-000000000000'])
              assert.equal(UUID_REGEX.test(value), true, value);
            const bad = [...Array.from({length:128},(_,index) => uuid + String.fromCharCode(index)),
              uuid+'\u2028',uuid+'\u2029',uuid+'\r\n',uuid+'💡','',uuid.slice(1),'x'+uuid,uuid.replace('-','_')];
            for (const value of bad) assert.equal(UUID_REGEX.test(value), false, JSON.stringify(value));
            """);
    }
}
