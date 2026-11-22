using System.Text.Json.Nodes;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR;
using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.CSharp.Ast.Builders;
using ValIO.Generators.CSharp.Ast.Renderers;
using ValIO.Generators.CSharp.Options;
using ValIO.Generators.CSharp.Services;

namespace ValIO.Generators.CSharp.Tests.Builders;

/// <summary>
/// Regression tests for typing <c>property.Constraints.DefaultValue</c> by the DECLARED schema
/// type rather than the CLR runtime type of the parsed value. <c>IrBuilder.ExtractDefaultValue</c>
/// can hand back a numeric/boolean default as a plain boxed primitive, as a <c>JsonNode</c>, or
/// (as observed in a real generated client) as a bare string ("0") — typing the literal by
/// <c>value switch</c> made a numeric default render as a quoted string literal assigned to a
/// non-string property (<c>public int Offset { get; set; } = "0";</c> → CS0029).
/// </summary>
[TestFixture]
public class CSharpAstBuilderDefaultValueTests
{
    private CSharpAstBuilder _builder = null!;
    private CSharpAstRenderer _renderer = null!;

    /// <summary>Creates a renderer and a builder with the explicit fragment document context.</summary>
    [SetUp]
    public void Setup()
    {
        _builder = new CSharpAstBuilder(new CSharpNamingConverter(), CSharpDocumentFixture.Context([], []));
        _renderer = new CSharpAstRenderer();
    }

    private static IrProperty IntProperty(object defaultValue) => new()
    {
        OriginalName = "offset",
        Name = "Offset",
        Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Integer },
        IsRequired = false,
        Constraints = new IrPropertyConstraints { DefaultValue = defaultValue },
    };

    [Test]
    public void IntegerDefault_AsString_RendersUnquotedNumber()
    {
        // Real-world regression: ExtractDefaultValue handed back "0" (a string) for an integer schema default.
        var code = _renderer.Render(_builder.BuildProperty(IntProperty("0")));

        code.Should().Contain("public int Offset { get; set; } = 0;");
        code.Should().NotContain("\"0\"");
    }

    [Test]
    public void IntegerDefault_AsJsonElement_RendersUnquotedNumber()
    {
        // The ACTUAL real-world shape: IrBuilder.ExtractDefaultValue calls JsonValue.GetValue<object>()
        // on a JsonNode.Parse(...)-backed node, which returns a boxed System.Text.Json.JsonElement —
        // not a plain string or int. Verified empirically; this is what broke PagingQuery.offset/take
        // in production (talent.e2e Pin.E2E.ApiClient generation).
        var jsonElement = JsonNode.Parse("0")!.AsValue().GetValue<object>();

        var code = _renderer.Render(_builder.BuildProperty(IntProperty(jsonElement)));

        code.Should().Contain("public int Offset { get; set; } = 0;");
        code.Should().NotContain("\"0\"");
    }

    [Test]
    public void IntegerDefault_AsBoxedInt_RendersUnquotedNumber()
    {
        var code = _renderer.Render(_builder.BuildProperty(IntProperty(30)));

        code.Should().Contain("public int Offset { get; set; } = 30;");
    }

    [Test]
    public void LongDefault_GetsLSuffix()
    {
        var property = new IrProperty
        {
            OriginalName = "version",
            Name = "Version",
            Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Long },
            IsRequired = false,
            Constraints = new IrPropertyConstraints { DefaultValue = "9000000000" },
        };

        var code = _renderer.Render(_builder.BuildProperty(property));

        code.Should().Contain("public long Version { get; set; } = 9000000000L;");
    }

    [Test]
    public void NumberDefault_GetsDSuffix()
    {
        var property = new IrProperty
        {
            OriginalName = "rate",
            Name = "Rate",
            Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Number },
            IsRequired = false,
            Constraints = new IrPropertyConstraints { DefaultValue = "1.5" },
        };

        var code = _renderer.Render(_builder.BuildProperty(property));

        code.Should().Contain("public double Rate { get; set; } = 1.5d;");
    }

    /// <summary>Emits a decimal property with the exact decimal default and inclusive bound beyond JavaScript's safe integer range.</summary>
    [Test]
    public void DecimalDefault_UsesDecimalAndExactRange()
    {
        var property = new IrProperty
        {
            OriginalName = "amount",
            Name = "Amount",
            Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Number, Format = "decimal" },
            IsRequired = false,
            Constraints = new IrPropertyConstraints
            {
                DefaultValue = JsonNode.Parse("9007199254740993.1234567890")!.AsValue().GetValue<object>(),
                Min = 0.0000000001m,
                Max = 999999999999999999.9999999999m
            }
        };

        var model = new IrModel { Name = "Payment", Properties = [property] };
        var builder = new CSharpAstBuilder(new CSharpNamingConverter(), CSharpDocumentFixture.Context([model], []),
            new CSharpGenerationOptions { GenerateValidationAttributes = true });
        var code = _renderer.Render(builder.BuildProperty(property, model));

        code.Should().Contain("public decimal Amount { get; set; } = 9007199254740993.1234567890m;")
            .And.Contain("typeof(decimal), \"0.0000000001\", \"999999999999999999.9999999999\"");
    }

    /// <summary>Leaves the property without an initializer when the schema default exceeds the exact precision of CLR decimal.</summary>
    [Test]
    public void DecimalDefault_DoesNotEmitRoundedInitializer()
    {
        var property = new IrProperty
        {
            OriginalName = "amount",
            Name = "Amount",
            Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Number, Format = "decimal" },
            IsRequired = false,
            Constraints = new IrPropertyConstraints { DefaultValue = "1234567890123.123456789012345678" }
        };

        _builder.BuildProperty(property).DefaultValue.Should().BeNull();
    }

    /// <summary>Emits the unsigned suffix for the maximum unsigned 64-bit default so the generated literal keeps its full range.</summary>
    [Test]
    public void UInt64Default_UsesUnsignedLiteral()
    {
        var property = new IrProperty
        {
            OriginalName = "sequence",
            Name = "Sequence",
            Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Long, Format = "uint64" },
            IsRequired = false,
            Constraints = new IrPropertyConstraints { DefaultValue = "18446744073709551615" }
        };

        _renderer.Render(_builder.BuildProperty(property)).Should()
            .Contain("public ulong Sequence { get; set; } = 18446744073709551615UL;");
    }

    [Test]
    public void BooleanDefault_AsString_RendersLowercaseUnquoted()
    {
        var property = new IrProperty
        {
            OriginalName = "isActive",
            Name = "IsActive",
            Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Boolean },
            IsRequired = false,
            Constraints = new IrPropertyConstraints { DefaultValue = "true" },
        };

        var code = _renderer.Render(_builder.BuildProperty(property));

        code.Should().Contain("public bool IsActive { get; set; } = true;");
        code.Should().NotContain("\"true\"");
    }

    [Test]
    public void StringDefault_StaysQuoted()
    {
        var property = new IrProperty
        {
            OriginalName = "status",
            Name = "Status",
            Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.String },
            IsRequired = false,
            Constraints = new IrPropertyConstraints { DefaultValue = "pending" },
        };

        var code = _renderer.Render(_builder.BuildProperty(property));

        code.Should().Contain("public string Status { get; set; } = \"pending\";");
    }

    [Test]
    public void UnparseableNumericDefault_OmitsInitializer_DoesNotEmitBrokenLiteral()
    {
        var property = IntProperty("not-a-number");

        var code = _renderer.Render(_builder.BuildProperty(property));

        // Non-nullable value-type property with no parseable default and no "required" keyword —
        // safest output is simply no initializer (property defaults to 0), never a mistyped literal.
        code.Should().Contain("public int Offset { get; set; }");
        code.Should().NotContain("=");
    }

    [Test]
    public void IntegerEnumMember_ValueAsString_RendersUnquotedNumber()
    {
        // Mirrors IrBuilder's raw (un-normalized) enum value extraction.
        var enumIr = new IrEnum
        {
            Name = "Priority",
            BaseType = IrEnumBaseType.Integer,
            Values = [new IrEnumValue { Name = "High", Value = "2" }],
        };

        var code = _renderer.Render(_builder.BuildCompilationUnit(enumIr, "Test.Ns"));

        code.Should().Contain("High = 2");
        code.Should().NotContain("\"2\"");
    }

    [Test]
    public void IntegerEnumMember_ValueAsRawJsonNode_RendersUnquotedNumber()
    {
        // Real shape: IrBuilder.BuildEnum stores schema.Enum items directly (JsonNode), never
        // unwrapped via GetValue<object>() the way property defaults are.
        var enumIr = new IrEnum
        {
            Name = "Priority",
            BaseType = IrEnumBaseType.Integer,
            Values = [new IrEnumValue { Name = "High", Value = JsonNode.Parse("2")! }],
        };

        var code = _renderer.Render(_builder.BuildCompilationUnit(enumIr, "Test.Ns"));

        code.Should().Contain("High = 2");
        code.Should().NotContain("\"2\"");
    }

    [Test]
    public void StringDefault_AsJsonElement_StaysQuoted()
    {
        var jsonElement = JsonNode.Parse("\"pending\"")!.AsValue().GetValue<object>();
        var property = new IrProperty
        {
            OriginalName = "status",
            Name = "Status",
            Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.String },
            IsRequired = false,
            Constraints = new IrPropertyConstraints { DefaultValue = jsonElement },
        };

        var code = _renderer.Render(_builder.BuildProperty(property));

        code.Should().Contain("public string Status { get; set; } = \"pending\";");
    }
}
