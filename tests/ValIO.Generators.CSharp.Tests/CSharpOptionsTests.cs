using ValIO.Tests.Fixtures;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR;
using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
using ValIO.Generators.CSharp.Ast.Builders;
using ValIO.Generators.CSharp.Ast.Renderers;
using ValIO.Generators.CSharp.Models;
using ValIO.Generators.CSharp.Options;
using ValIO.Generators.CSharp.Services;
using ValIO.Generators.CSharp.Steps;

namespace ValIO.Generators.CSharp.Tests;

/// <summary>Checks that options have one typed representation and affect generated output.</summary>
[TestFixture]
public sealed class CSharpOptionsTests
{
    /// <summary>Preserves advanced options from JSON parsing through the generation step.</summary>
    [Test]
    public async Task ParsedOptions_AreConsumedByModelsStep()
    {
        var descriptor = new CSharpOptionsDescriptor();
        var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            FixtureFile.ReadJsonCase("Configuration/csharp-options-tests.json", "model-generation"))!;
        var options = descriptor.ParseOptions(raw, out var errors);
        errors.Should().BeEmpty();
        var typed = options.Should().BeOfType<CSharpGenerationOptions>().Subject;
        typed.PropertyAccessor.Should().Be(PropertyAccessorType.Init);
        var context = new GenerationContext
        {
            PluginOptions = typed,
            Configuration = new GenerationConfiguration { Namespace = "Shared.Namespace" },
            IrDocument = new IrDocument
            {
                Metadata = new IrDocumentMetadata(),
                Models = [new IrModel
                {
                    Name = "Item",
                    Properties = [new IrProperty
                    {
                        Name = "Value", OriginalName = "value", IsRequired = true,
                        Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.String }
                    }]
                }],
                Enums = [], Clients = [],
                TypeDependencies = new IrTypeDependencyGraph
                {
                    Dependencies = new Dictionary<string, IReadOnlyList<string>>(), TopologicalOrder = ["Item"]
                }
            }
        };

        await new CSharpModelsStep(new CSharpNamingConverter(), new CSharpAstRenderer()).ExecuteAsync(context);

        var code = context.GeneratedFiles.Single().Content;
        code.Should().Contain("namespace Shared.Namespace");
        code.Should().Contain("sealed class Item").And.Contain("required string Value { get; init; }");
        code.Should().NotContain("IEquatable");
    }

    /// <summary>Rejects unsupported and malformed options instead of silently ignoring them.</summary>
    /// <param name="caseName">The name of the invalid option fixture.</param>
    [TestCase("unsupported-model-type")]
    [TestCase("unsupported-query-style")]
    [TestCase("unsupported-nullable-references")]
    [TestCase("unsupported-nullable-activity")]
    [TestCase("unsupported-access-modifier")]
    [TestCase("invalid-equality-flag")]
    [TestCase("invalid-property-accessor")]
    [TestCase("unknown-option")]
    public void UnsupportedOptions_ProduceActionableErrors(string caseName)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(FixtureFile.ReadJsonCase("Configuration/csharp-options-tests.json", caseName))!;

        var options = new CSharpOptionsDescriptor().ParseOptions(raw, out var errors);

        options.Should().BeNull();
        errors.Should().NotBeEmpty();
    }

    /// <summary>Checks independent XML settings from profile parsing to attributes on string enums.</summary>
    /// <param name="xmlEnum">Whether XmlSerializer uses the enum's declared literals.</param>
    /// <param name="dataContract">Whether DataContractSerializer uses the enum's declared literals.</param>
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void EnumAttributeOptions_AffectOnlyTheirSerializer(bool xmlEnum, bool dataContract)
    {
        var raw = new Dictionary<string, JsonElement>
        {
            ["generateXmlEnumAttributes"] = JsonSerializer.SerializeToElement(xmlEnum),
            ["generateDataContractAttributes"] = JsonSerializer.SerializeToElement(dataContract)
        };
        var parsed = new CSharpOptionsDescriptor().ParseOptions(raw, out var errors);
        errors.Should().BeEmpty();
        var builder = new CSharpAstBuilder(new CSharpNamingConverter(), CSharpDocumentFixture.Context([], []),
            parsed.Should().BeOfType<CSharpGenerationOptions>().Subject);
        var renderer = new CSharpAstRenderer();
        var state = new IrEnum
        {
            Name = "State", BaseType = IrEnumBaseType.String,
            Values = [new IrEnumValue { Name = "InProgress", Value = "in-progress" }]
        };
        var code = renderer.Render(builder.BuildCompilationUnit(state));
        code.Contains("[XmlEnum(\"in-progress\")]", StringComparison.Ordinal).Should().Be(xmlEnum);
        code.Contains("[DataContract]", StringComparison.Ordinal).Should().Be(dataContract);
        code.Should().Contain("[EnumMember(Value = \"in-progress\")]").And.Contain("[JsonStringEnumMemberName(\"in-progress\")]");

        var numeric = new IrEnum(state)
        {
            BaseType = IrEnumBaseType.Integer,
            Values = [new IrEnumValue { Name = "Started", Value = 7 }]
        };
        renderer.Render(builder.BuildCompilationUnit(numeric)).Should().Contain("Started = 7")
            .And.NotContain("XmlEnum").And.NotContain("DataContract").And.NotContain("JsonStringEnumMemberName");
    }

    /// <summary>Enforces the same unsupported-mode contract for direct AST builder consumers.</summary>
    [Test]
    public void DirectBuilder_RejectsUnsupportedModelKinds()
    {
        var construct = () => new CSharpAstBuilder(new CSharpNamingConverter(), CSharpDocumentFixture.Context([], []),
            new CSharpGenerationOptions { ModelType = ModelType.Record });

        construct.Should().Throw<ArgumentException>().WithMessage("*modelType=Class*");
    }

    /// <summary>Uses the generated PascalCase member for a schema default and throws when two wire values map to the same CLR name.</summary>
    [Test]
    public void PascalCaseEnumMembers_UseTheSameNameForDefaultsAndDetectCollisions()
    {
        var raw = new Dictionary<string, JsonElement> { ["pascalCaseEnumMembers"] = JsonSerializer.SerializeToElement(true) };
        var parsed = new CSharpOptionsDescriptor().ParseOptions(raw, out var errors);
        errors.Should().BeEmpty();
        var state = new IrEnum
        {
            Name = "State", BaseType = IrEnumBaseType.String,
            Values = [new IrEnumValue { Name = "player", Value = "player" }]
        };
        var model = new IrModel
        {
            Name = "Session",
            Properties = [new IrProperty
            {
                Name = "state", OriginalName = "state", IsRequired = false, Type = new IrModelReference { ModelName = "State" },
                Constraints = new IrPropertyConstraints { DefaultValue = "player" }
            }]
        };
        var builder = new CSharpAstBuilder(new CSharpNamingConverter(), CSharpDocumentFixture.Context([model], [state]),
            parsed.Should().BeOfType<CSharpGenerationOptions>().Subject);
        var renderer = new CSharpAstRenderer();
        renderer.Render(builder.BuildCompilationUnit(model, "Example")).Should().Contain("global::Example.State.Player");
        renderer.Render(builder.BuildCompilationUnit(state, "Example")).Should().Contain("[JsonStringEnumMemberName(\"player\")]")
            .And.Contain("    Player");
        var collision = new IrEnum(state) { Values = [.. state.Values, new IrEnumValue { Name = "Player", Value = "Player" }] };
        var generate = () => builder.BuildCompilationUnit(collision);
        generate.Should().Throw<InvalidOperationException>().WithMessage("*'State'*'player'*'Player'*");
    }
}
