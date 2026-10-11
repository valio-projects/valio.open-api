using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.Cpp.Ast.Builders.Std;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Ast.Renderers;
using ValIO.Generators.Cpp.Models;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Tests.Builders.Std;

/// <summary>Guards C++ contract semantics previously missed by the single-enum smoke document.</summary>
[TestFixture]
public sealed class StdContractRegressionTests
{
    /// <summary>Checks integer enums preserve the complete signed 64-bit range in declarations and codec signatures.</summary>
    [Test]
    public void Enum_WideIntegerValuesUsePortableInt64Constants()
    {
        var irEnum = new IrEnum
        {
            Name = "Wide", BaseType = IrEnumBaseType.Integer,
            Values = [new IrEnumValue { Name = "minimum", Value = long.MinValue }, new IrEnumValue { Name = "maximum", Value = long.MaxValue }],
        };
        var builder = new StdEnumAstBuilder(new StdNamingConvention());
        var renderer = new CppAstRenderer();
        var definition = renderer.Render(builder.Build(irEnum, "Api"));
        var (header, _) = builder.BuildJson(irEnum, "Api", new CppGenerationOptions());

        definition.Should().Contain("enum class Wide : std::int64_t");
        definition.Should().Contain("Minimum = -9223372036854775807LL - 1");
        definition.Should().Contain("Maximum = 9223372036854775807LL");
        renderer.Render(header).Should().Contain("WideFromWire(std::int64_t w)");
    }

    /// <summary>Checks unsupported integer values produce diagnostics rather than fabricated ordinal wire values.</summary>
    [Test]
    public void Enum_InvalidIntegerWireValueFailsExplicitly()
    {
        var irEnum = new IrEnum { Name = "Bad", BaseType = IrEnumBaseType.Integer, Values = [new IrEnumValue { Name = "bad", Value = "9223372036854775808" }] };
        var build = () => new StdEnumAstBuilder(new StdNamingConvention()).Build(irEnum, "Api");
        build.Should().Throw<NotSupportedException>().WithMessage("*9223372036854775808*signed 64-bit*");
    }

    /// <summary>Rejects composed model alternatives whose constraints cannot be represented by an ordinary C++ struct.</summary>
    /// <param name="kind">The unsupported composition kind.</param>
    [TestCase(IrCompositionKind.OneOf)]
    [TestCase(IrCompositionKind.AnyOf)]
    public void Model_AlternativeCompositionFailsExplicitly(IrCompositionKind kind)
    {
        var model = new IrModel
        {
            Name = "Choice", Properties = [],
            Composition = new IrComposition { Kind = kind, Types = [new IrModelReference { ModelName = "Variant" }] },
        };
        var naming = new StdNamingConvention();
        var builder = new StdModelAstBuilder(new StdCppTypeMapper(naming), naming);
        var build = () => builder.Build(model, new HashSet<string>(), new Dictionary<string, IrModel>(), "Api");
        build.Should().Throw<NotSupportedException>().WithMessage("*Choice*typed variant codec*");
    }

    /// <summary>Rejects nested allOf alternatives instead of flattening only the reference members.</summary>
    [Test]
    public void Model_NestedAlternativeInsideAllOfFailsExplicitly()
    {
        var model = new IrModel
        {
            Name = "Mixed", Properties = [],
            Composition = new IrComposition
            {
                Kind = IrCompositionKind.AllOf,
                Types = [new IrUnionType { Types = [new IrModelReference { ModelName = "First" }, new IrModelReference { ModelName = "Second" }] }],
            },
        };
        var naming = new StdNamingConvention();
        var builder = new StdModelAstBuilder(new StdCppTypeMapper(naming), naming);
        var build = () => builder.Build(model, new HashSet<string>(), new Dictionary<string, IrModel>(), "Api");
        build.Should().Throw<NotSupportedException>().WithMessage("*Mixed*nested non-reference allOf*codec*");
    }

    /// <summary>Checks optional presence and nullable values retain separate storage and codec operations.</summary>
    [Test]
    public void Model_PresenceAndNullabilityRemainIndependent()
    {
        var nullable = new IrNullableType { InnerType = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.String } };
        var model = new IrModel
        {
            Name = "Probe",
            Properties =
            [
                new IrProperty { Name = "RequiredNullable", OriginalName = "required_null", IsRequired = true, Type = nullable },
                new IrProperty { Name = "OptionalNullable", OriginalName = "optional_null", IsRequired = false, Type = nullable },
            ],
        };
        var naming = new StdNamingConvention();
        var mapper = new StdCppTypeMapper(naming);
        var renderer = new CppAstRenderer();
        var lookup = new Dictionary<string, IrModel>();
        var enums = new HashSet<string>();

        var header = renderer.Render(new StdModelAstBuilder(mapper, naming).Build(model, enums, lookup, "Api"));
        var (_, codec) = new StdModelJsonAstBuilder(mapper, naming).Build(model, enums, lookup, "Api", new CppGenerationOptions());
        var source = renderer.Render(codec);

        header.Should().Contain("std::optional<std::string> RequiredNullable;");
        header.Should().Contain("std::optional<std::optional<std::string>> OptionalNullable;");
        source.Should().Contain("j.at(\"required_null\").get_to(value.RequiredNullable);");
        source.Should().NotContain("j.contains(\"required_null\")");
        source.Should().Contain("j[\"required_null\"] = value.RequiredNullable;");
        source.Should().Contain("value.OptionalNullable.emplace(j.at(\"optional_null\").get<std::optional<std::string>>());");
        source.Should().Contain("value.OptionalNullable.reset();");
        source.Should().Contain("j = nlohmann::json::object();");
    }

    /// <summary>Checks path placeholders bind wire names even when parameters are reversed or repeated.</summary>
    [Test]
    public void Client_PathParametersResolveByWireName()
    {
        var operation = new IrOperation
        {
            MethodName = "GetPair", HttpMethod = "GET", Path = "/{second-wire}/{first-wire}/{second-wire}",
            PathParameters =
            [
                new IrParameter { Name = "First", OriginalName = "first-wire", IsRequired = true, Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Integer } },
                new IrParameter { Name = "Second", OriginalName = "second-wire", IsRequired = true, Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Integer } },
            ],
            QueryParameters = [], HeaderParameters = [], Responses = [],
        };

        var source = RenderClient(operation, CppApiStyle.Sync).Source;
        source.Should().Contain("\"/\" + ValIO::UrlEncode(std::to_string(Second)) + \"/\" + ValIO::UrlEncode(std::to_string(First)) + \"/\" + ValIO::UrlEncode(std::to_string(Second))");
    }

    /// <summary>Checks async-only clients retain a private implementation and own async transport state.</summary>
    [Test]
    public void Client_AsyncOnlyHasOwnedWorkerAndPrivateSyncImplementation()
    {
        var operation = new IrOperation
        {
            MethodName = "Fetch", HttpMethod = "GET", Path = "/probe",
            PathParameters = [], QueryParameters = [], HeaderParameters = [], Responses = [],
        };
        var result = RenderClient(operation, CppApiStyle.Async);

        result.Header.Should().Contain("private:");
        result.Header.Should().Contain("Fetch() const;");
        result.Header.Should().Contain("FetchAsync() const;");
        result.Source.Should().Contain("ProbeClient::Fetch() const");
        result.Source.Should().Contain("[httpConfig = _config]");
        result.Source.Should().Contain("ProbeClient worker = ProbeClient(httpConfig);");
        result.Source.Should().Contain("return worker.Fetch();");
        result.Source.Should().NotContain("[this");
    }

    /// <summary>Checks cloning retains the linkage needed for generated header-defined converters.</summary>
    [Test]
    public void FreeFunction_ClonePreservesInlineLinkage()
    {
        var node = new CppFreeFunctionNode(new IdentifierNode("Wire"), new RawTypeNode("void"), body: new BlockNode([]), isInline: true);
        var clone = (CppFreeFunctionNode)node.Clone();
        clone.IsInline.Should().BeTrue();
        new CppAstRenderer().Render(clone).Should().Contain("inline void Wire()");
    }

    /// <summary>Renders a minimal client for a focused operation regression.</summary>
    /// <param name="operation">The operation under test.</param>
    /// <param name="style">The client execution style.</param>
    /// <returns>The client header and implementation text.</returns>
    private static (string Header, string Source) RenderClient(IrOperation operation, CppApiStyle style)
    {
        var naming = new StdNamingConvention();
        var builder = new StdClientAstBuilder(new StdCppTypeMapper(naming), naming);
        var result = builder.Build(new IrClient { Name = "Probe", Operations = [operation] }, new HashSet<string>(),
            new Dictionary<string, IrModel>(), "Api", new CppGenerationOptions { ApiStyle = style });
        var renderer = new CppAstRenderer();
        return (renderer.Render(result.ClientHeader), renderer.Render(result.ClientSource));
    }
}
