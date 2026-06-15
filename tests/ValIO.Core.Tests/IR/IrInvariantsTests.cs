using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR.Types;
using ValIO.Core.Tests.TestHelpers;

namespace ValIO.Core.Tests.IR;

/// <summary>
/// Структурные инварианты IR, защищающие сразу все 12 генераторов:
/// в IR не должно быть вырожденных intersection/union (&lt;2 членов) и осиротевших inline-моделей.
/// </summary>
[TestFixture]
public class IrInvariantsTests
{
    private const string CompositionHeavyDoc = """
    {
      "openapi":"3.0.3",
      "info":{"title":"T","version":"1.0.0"},
      "paths":{},
      "components":{"schemas":{
        "Mode":{"type":"string","enum":["a","b"]},
        "Base":{"type":"object","properties":{"id":{"type":"string"}}},
        "Extra":{"type":"object","properties":{"x":{"type":"string"}}},
        "Wrapper":{"type":"object","properties":{
          "mode":{"allOf":[{"$ref":"#/components/schemas/Mode"}],"nullable":true},
          "single":{"allOf":[{"$ref":"#/components/schemas/Base"}]},
          "nested":{"allOf":[{"allOf":[{"$ref":"#/components/schemas/Base"}]}]},
          "intersection":{"allOf":[{"$ref":"#/components/schemas/Base"},{"$ref":"#/components/schemas/Extra"}]}
        }},
        "Derived":{"allOf":[{"$ref":"#/components/schemas/Base"},{"type":"object","properties":{"y":{"type":"string"}}}]}
      }}
    }
    """;

    [Test]
    public async Task BuiltIr_HasNoSingleArmIntersectionOrUnion()
    {
        var ir = await IrTestHarness.BuildIrAsync(CompositionHeavyDoc);

        foreach (var type in IrTypeWalker.AllTypes(ir))
        {
            if (type is IrIntersectionType i)
                i.Types.Count.Should().BeGreaterThan(1, "an IrIntersectionType must never have <2 members");
            if (type is IrUnionType u)
                u.Types.Count.Should().BeGreaterThan(1, "an IrUnionType must never have <2 members");
        }
    }

    [Test]
    public async Task BuiltIr_HasNoOrphanModels()
    {
        var ir = await IrTestHarness.BuildIrAsync(CompositionHeavyDoc);

        // Every model is either reachable from another model (dependency edge) or a top-level
        // component schema. Phase C must not leave behind inline models with no incoming reference.
        var referenced = new HashSet<string>();
        foreach (var kvp in ir.TypeDependencies.Dependencies)
            foreach (var dep in kvp.Value)
                referenced.Add(dep);

        // Inline models are named like "<Prop>InlineModel"/"InlineModelN"; assert none are orphaned.
        var inlineModels = ir.Models.Where(m => m.Name.Contains("InlineModel", StringComparison.Ordinal));
        foreach (var inline in inlineModels)
            referenced.Should().Contain(inline.Name,
                $"inline model '{inline.Name}' must be referenced by something (no orphans after Phase C)");
    }
}
