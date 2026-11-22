using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR;
using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.Cpp.Ast.Builders.Std;
using ValIO.Generators.Cpp.Ast.Renderers;
using ValIO.Generators.Cpp.Services;
using ValIO.Generators.Cpp.Tests.TestHelpers;

namespace ValIO.Generators.Cpp.Tests.Builders.Std;

/// <summary>Guards deterministic ordinary default construction of required enum fields.</summary>
[TestFixture]
public sealed class StdEnumModelInitializationTests
{
    /// <summary>Checks member names cannot hide enum or model types in direct, optional, or nested field declarations.</summary>
    [Test]
    public void FieldsSharingDeclarationNames_QualifyEveryAffectedReference()
    {
        var (header, _) = Build([
            new IrProperty(Property("EarlierStates", "State"))
            {
                Type = new IrArrayType { ElementType = new IrModelReference { ModelName = "State" } },
            },
            new IrProperty(Property("State", "State")) { IsRequired = false },
            Property("LaterState", "State"),
            Property("EarlierChild", "Child"),
            Property("Child", "Child"),
            new IrProperty(Property("LaterChildren", "Child"))
            {
                IsRequired = false,
                Type = new IrArrayType { ElementType = new IrModelReference { ModelName = "Child" } },
            },
        ]);

        header.Should().Contain("std::vector<::Api::State> EarlierStates;");
        header.Should().Contain("std::optional<::Api::State> State;");
        header.Should().Contain("::Api::State LaterState = Api::State();");
        header.Should().Contain("::Api::Child EarlierChild;");
        header.Should().Contain("::Api::Child Child;");
        header.Should().Contain("std::optional<std::vector<::Api::Child>> LaterChildren;");
        header.Should().Contain("#include \"Enums/State.h\"");
        header.Should().Contain("#include \"Models/Child.h\"");
    }

    /// <summary>Checks required string and numeric enum references use structured value initialization.</summary>
    [Test]
    public void RequiredEnumWithoutSchemaDefault_IsValueInitializedWithTypedAst()
    {
        var (header, fields) = Build([
            Property("Status", "State"), Property("NumericLevel", "Level"),
            new IrProperty(Property("OptionalState", "State")) { IsRequired = false },
            new IrProperty(Property("NullableState", "State")) { Type = new IrNullableType { InnerType = new IrModelReference { ModelName = "State" } } },
            Property("Child", "Child"),
            new IrProperty(Property("States", "State")) { Type = new IrArrayType { ElementType = new IrModelReference { ModelName = "State" } } },
        ]);
        header.Should().Contain("State Status = Api::State();");
        header.Should().Contain("Level NumericLevel = Api::Level();");
        foreach (var name in new[] { "Status", "NumericLevel" })
        {
            var initializer = fields.Single(field => field.Name.Name == name).DefaultValue.Should().BeOfType<CallExpressionNode>().Which;
            initializer.Arguments.Should().BeEmpty();
            initializer.Type.Should().NotBeNull();
            initializer.Target.Should().BeOfType<MemberAccessExpressionNode>()
                .Which.UseScopeOperator.Should().BeTrue();
        }
        foreach (var name in new[] { "OptionalState", "NullableState", "Child", "States" })
            fields.Single(field => field.Name.Name == name).DefaultValue.Should().BeNull();
    }

    /// <summary>Checks real parsed JSON defaults select the named member rather than being overwritten by zero initialization.</summary>
    /// <param name="isInteger">Whether the enum uses numeric wire values.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void ExplicitSchemaDefault_InitializesDeclaredMember(bool isInteger)
    {
        var defaultValue = JsonSerializer.Deserialize<JsonElement>(isInteger ? "2e0" : "\"approved\"");
        var (header, fields) = Build([new IrProperty(Property("Value", isInteger ? "Level" : "State"))
        {
            Constraints = new IrPropertyConstraints { DefaultValue = defaultValue },
        }]);
        header.Should().Contain(isInteger ? "Level Value = Api::Level::High;" : "State Value = Api::State::Approved;");
        fields.Single().DefaultValue.Should().BeOfType<MemberAccessExpressionNode>()
            .Which.UseScopeOperator.Should().BeTrue();
    }

    /// <summary>Checks a schema default cannot accidentally match a string enum through numeric text conversion.</summary>
    [Test]
    public void InvalidEnumSchemaDefault_FailsGenerationExplicitly()
    {
        var build = () => Build([new IrProperty(Property("Value", "State"))
        {
            Constraints = new IrPropertyConstraints { DefaultValue = 2 },
        }]);
        build.Should().Throw<NotSupportedException>().WithMessage("*Value*schema default outside*State*");
    }

    /// <summary>Creates a required declaration reference used by each focused field scenario.</summary>
    /// <param name="name">The generated property name.</param>
    /// <param name="type">The referenced declaration name.</param>
    /// <returns>The required model property.</returns>
    private static IrProperty Property(string name, string type) => new()
    {
        Name = name, OriginalName = name, IsRequired = true, Type = new IrModelReference { ModelName = type },
    };

    /// <summary>Builds and renders actual model fields using finalized enum and model declarations.</summary>
    /// <param name="properties">The fields to generate.</param>
    /// <returns>The generated header and its structured field nodes.</returns>
    private static (string Header, IReadOnlyList<FieldDeclarationNode> Fields) Build(IReadOnlyList<IrProperty> properties)
    {
        var model = new IrModel { Name = "Probe", Properties = properties };
        var state = new IrEnum
        {
            Name = "State", BaseType = IrEnumBaseType.String,
            Values =
            [
                new IrEnumValue { Name = "placed", Value = "placed" },
                new IrEnumValue { Name = "approved", Value = "approved" },
                new IrEnumValue { Name = "digit", Value = "2" },
            ],
        };
        var level = new IrEnum
        {
            Name = "Level", BaseType = IrEnumBaseType.Integer,
            Values = [new IrEnumValue { Name = "high", Value = 2 }],
        };
        var context = CppDocumentContextFactory.Create([model, new IrModel { Name = "Child", Properties = [] }], [state, level]);
        var naming = new StdNamingConvention();
        var header = new StdModelAstBuilder(new StdCppTypeMapper(naming, context), naming, context).Build(model, "Api");
        var fields = ((StructDeclarationNode)((NamespaceDeclarationNode)header.Declarations.Single()).Declarations.Single()).Members
            .Cast<FieldDeclarationNode>().ToList();
        return (new CppAstRenderer().Render(header), fields);
    }
}
