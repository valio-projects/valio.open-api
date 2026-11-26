using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Plugins;

namespace ValIO.Tests.Fixtures;

/// <summary>Verifies composed model fields through a target-language pipeline across OpenAPI dialects.</summary>
public abstract class AllOfModelsContractTests
{
    /// <summary>Checks direct and transitive inherited fields while keeping the source IR own-only.</summary>
    /// <param name="version">The OpenAPI or Swagger dialect parsed by the actual reader.</param>
    /// <param name="modelName">The generated model selected from the inheritance chain.</param>
    /// <param name="ownProperty">The property declared directly by the selected model.</param>
    /// <returns>A task that completes after generation and all assertions.</returns>
    [TestCase("2.0", "Child", "name")]
    [TestCase("2.0", "GrandChild", "label")]
    [TestCase("3.0.4", "Child", "name")]
    [TestCase("3.0.4", "GrandChild", "label")]
    [TestCase("3.1.2", "Child", "name")]
    [TestCase("3.1.2", "GrandChild", "label")]
    [TestCase("3.2.0", "Child", "name")]
    [TestCase("3.2.0", "GrandChild", "label")]
    public async Task AllOf_EmitsInheritedPropertiesWithoutChangingOwnProperties(string version, string modelName, string ownProperty)
    {
        var document = await OpenApiFixture.LoadAsync($"Inheritance/chain-{version}.json");
        var context = await GenerateAsync(document);

        var file = ModelFile(context, modelName);
        var expectedProperties = modelName == "Child" ? new[] { "id", "name" } : new[] { "id", "name", "label" };
        foreach (var propertyName in expectedProperties)
        {
            var declaration = StringField(propertyName);
            Regex.Matches(file.Content, Regex.Escape(declaration)).Count.Should().Be(1,
                $"{modelName} must declare {propertyName} exactly once for OpenAPI {version}");
        }

        var sourceModel = context.IrDocument.Models.Single(model => model.Name == modelName);
        sourceModel.Properties.Select(property => property.OriginalName).Should().Equal(ownProperty);
        sourceModel.Composition.Should().NotBeNull();
        sourceModel.Composition!.Kind.Should().Be(IrCompositionKind.AllOf);
        sourceModel.OriginalSchema.Should().BeSameAs(document.Components!.Schemas![modelName]);
    }

    /// <summary>Checks that inherited nested unions survive parsing, IR conversion and model generation.</summary>
    /// <param name="version">The OpenAPI 3 dialect supporting oneOf and anyOf.</param>
    /// <param name="compositionKind">The property composition keyword, oneOf or anyOf.</param>
    /// <returns>A task that completes after the pipeline and preservation assertions.</returns>
    [TestCase("3.0.4", "oneOf")]
    [TestCase("3.0.4", "anyOf")]
    [TestCase("3.1.2", "oneOf")]
    [TestCase("3.1.2", "anyOf")]
    [TestCase("3.2.0", "oneOf")]
    [TestCase("3.2.0", "anyOf")]
    public async Task AllOf_InheritedNestedUnionRetainsFieldAndDocumentReferences(string version, string compositionKind)
    {
        var document = await OpenApiFixture.LoadAsync($"Inheritance/nested-{compositionKind}-{version}.json");
        var context = await GenerateAsync(document);

        var baseModel = context.IrDocument.Models.Single(model => model.Name == "Base");
        var property = baseModel.Properties.Single(property => property.OriginalName == "payload");
        property.IsRequired.Should().BeTrue();
        var array = property.Type.Should().BeOfType<IrArrayType>().Subject;
        var dictionary = array.ElementType.Should().BeOfType<IrDictionaryType>().Subject;
        var union = dictionary.ValueType.Should().BeOfType<IrUnionType>().Subject;
        union.Kind.Should().Be(compositionKind == "oneOf" ? IrUnionKind.OneOf : IrUnionKind.AnyOf);
        union.Types.Should().AllBeOfType<IrModelReference>();
        union.Types.Cast<IrModelReference>().Select(reference => reference.ModelName).Should().Equal("First", "Second");

        var file = ModelFile(context, "Child");
        foreach (var expected in NestedUnionFragments)
            file.Content.Should().Contain(expected);
        ModelFile(context, "First").Content.Should().NotBeNullOrWhiteSpace();
        ModelFile(context, "Second").Content.Should().NotBeNullOrWhiteSpace();
        var child = context.IrDocument.Models.Single(model => model.Name == "Child");
        child.Properties.Select(property => property.OriginalName).Should().Equal("name");
    }

    /// <summary>Preserves nested inclusive and exclusive alternatives inside inherited array and dictionary fields.</summary>
    /// <param name="version">The OpenAPI 3 dialect parsed by the actual reader.</param>
    /// <param name="scenario">The explicit nested composition fixture.</param>
    /// <returns>A task that completes after generation and IR boundary assertions.</returns>
    [TestCase("3.0.4", "anyOf-in-allOf")]
    [TestCase("3.1.2", "anyOf-in-allOf")]
    [TestCase("3.2.0", "anyOf-in-allOf")]
    [TestCase("3.0.4", "allOf-in-oneOf")]
    [TestCase("3.1.2", "allOf-in-oneOf")]
    [TestCase("3.2.0", "allOf-in-oneOf")]
    public async Task NestedCompositions_PreserveBoundariesAcrossTheModelPipeline(string version, string scenario)
    {
        var document = await OpenApiFixture.LoadAsync($"Inheritance/{scenario}-{version}.json");
        var context = await GenerateAsync(document);

        var property = context.IrDocument.Models.Single(model => model.Name == "Base").Properties
            .Single(property => property.OriginalName == "payload");
        var array = property.Type.Should().BeOfType<IrArrayType>().Subject;
        var dictionary = array.ElementType.Should().BeOfType<IrDictionaryType>().Subject;
        if (scenario == "anyOf-in-allOf")
        {
            var intersection = dictionary.ValueType.Should().BeOfType<IrIntersectionType>().Subject;
            intersection.Types.Should().HaveCount(2);
            var union = intersection.Types[0].Should().BeOfType<IrUnionType>().Subject;
            union.Kind.Should().Be(IrUnionKind.AnyOf);
            union.Types.Cast<IrModelReference>().Select(reference => reference.ModelName).Should().Equal("First", "Second");
            intersection.Types[1].Should().BeOfType<IrModelReference>().Which.ModelName.Should().Be("Third");
        }
        else
        {
            var union = dictionary.ValueType.Should().BeOfType<IrUnionType>().Subject;
            union.Kind.Should().Be(IrUnionKind.OneOf);
            union.Types.Should().HaveCount(2);
            var intersection = union.Types[0].Should().BeOfType<IrIntersectionType>().Subject;
            intersection.Types.Cast<IrModelReference>().Select(reference => reference.ModelName).Should().Equal("First", "Second");
            union.Types[1].Should().BeOfType<IrModelReference>().Which.ModelName.Should().Be("Third");
        }

        ModelFile(context, "Child").Content.Should().MatchRegex(@"(?i)\bpayload\b");
        foreach (var modelName in new[] { "First", "Second", "Third" })
        {
            ModelFile(context, modelName).Content.Should().NotBeNullOrWhiteSpace();
            context.IrDocument.Models.Count(model => model.Name == modelName).Should().Be(1);
        }
        context.IrDocument.Models.Single(model => model.Name == "Child").Properties
            .Select(property => property.OriginalName).Should().Equal("name");
    }

    /// <summary>Runs the target generator using its normal pipeline setup.</summary>
    /// <param name="document">The fixture document produced by the OpenAPI reader.</param>
    /// <returns>The output files and final IR.</returns>
    protected abstract Task<GenerationContext> GenerateAsync(OpenApiDocument document);

    /// <summary>Describes the target's string field syntax.</summary>
    /// <param name="propertyName">The original name of the string property.</param>
    /// <returns>The expected declaration in the generated output.</returns>
    protected abstract string StringField(string propertyName);

    /// <summary>Identifies the output file for one generated model.</summary>
    /// <param name="modelName">The original model name.</param>
    /// <returns>The file name including its target-language extension.</returns>
    protected abstract string ModelFileName(string modelName);

    /// <summary>Gets the declarations and imports expected for the nested union.</summary>
    protected abstract IReadOnlyList<string> NestedUnionFragments { get; }

    /// <summary>Selects the model output from the generated file set.</summary>
    /// <param name="context">The completed pipeline state.</param>
    /// <param name="modelName">The model declared by the fixture.</param>
    /// <returns>The generated file for the selected model.</returns>
    private GeneratedFile ModelFile(GenerationContext context, string modelName)
    {
        var fileName = ModelFileName(modelName);
        return context.GeneratedFiles.Single(file =>
            file.RelativePath.Replace('\\', '/').EndsWith($"/{fileName}", StringComparison.Ordinal));
    }
}
