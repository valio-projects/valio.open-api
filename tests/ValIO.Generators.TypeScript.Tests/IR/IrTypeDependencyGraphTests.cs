using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR;
using ValIO.Core.IR.Builders;
using ValIO.Core.Services;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.IR;

/// <summary>
/// Tests for <see cref="IrTypeDependencyGraph"/> — the topological ordering
/// drives both model and schema emission order across every language.
/// </summary>
[TestFixture]
public class IrTypeDependencyGraphTests
{
    private static async Task<IrDocument> BuildIrAsync(string resourceName)
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync(resourceName);
        var converter = new NamingConverter();
        var resolver = new SchemaResolver(doc);
        return new IrBuilder(resolver, converter).BuildFromOpenApi(doc);
    }

    [Test]
    public async Task SimpleSchema_GraphContainsAllModels()
    {
        var ir = await BuildIrAsync("simple-schema.json");
        ir.TypeDependencies.TopologicalOrder.Should().Contain("User");
        ir.TypeDependencies.Dependencies.Should().ContainKey("User");
    }

    [Test]
    public async Task Petstore_EveryModelInDependenciesIsInTopologicalOrder()
    {
        var ir = await BuildIrAsync("petstore.json");
        // Invariant: every key in Dependencies must appear in TopologicalOrder.
        ir.TypeDependencies.Dependencies.Keys.Should().BeSubsetOf(ir.TypeDependencies.TopologicalOrder);
    }

    [Test]
    public async Task Petstore_TopologicalOrder_HasNoDuplicates()
    {
        var ir = await BuildIrAsync("petstore.json");
        var order = ir.TypeDependencies.TopologicalOrder;
        order.Should().HaveCount(order.Distinct().Count(),
            "topological order must not contain duplicates");
    }

    [Test]
    public async Task ComplexSchema_DogDependsOnAnimalBase()
    {
        var ir = await BuildIrAsync("complex-schema.json");
        if (ir.TypeDependencies.Dependencies.TryGetValue("Dog", out var deps))
        {
            deps.Should().Contain("AnimalBase");
        }
    }

    [Test]
    public async Task ComplexSchema_AnimalBaseAppearsBeforeDog()
    {
        var ir = await BuildIrAsync("complex-schema.json");
        var order = ir.TypeDependencies.TopologicalOrder.ToList();
        var baseIdx = order.IndexOf("AnimalBase");
        var dogIdx = order.IndexOf("Dog");
        if (baseIdx >= 0 && dogIdx >= 0)
        {
            baseIdx.Should().BeLessThan(dogIdx);
        }
    }

    [Test]
    public async Task ComplexSchema_RecursiveTypeStillInOrder()
    {
        var ir = await BuildIrAsync("complex-schema.json");
        // TreeNode references itself — must still appear exactly once.
        var occurrences = ir.TypeDependencies.TopologicalOrder.Count(t => t == "TreeNode");
        occurrences.Should().Be(1);
    }
}
