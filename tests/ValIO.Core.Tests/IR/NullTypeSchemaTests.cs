using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Tests.TestHelpers;
using ValIO.Tests.Fixtures;

namespace ValIO.Core.Tests.IR;

/// <summary>
/// Покрывает OpenAPI 3.1 «только null» ({"type":"null"}) как самостоятельный тип свойства
/// (конверт success/error) и диагностический контекст в сообщениях об ошибках построения IR.
/// </summary>
[TestFixture]
public class NullTypeSchemaTests
{
    /// <summary>Maps a property that accepts only null to a null primitive.</summary>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    public async Task StandaloneNullTypeProperty_MapsToNullPrimitive()
    {
        var ir = await IrTestHarness.BuildIrAsync(OpenApiFixture.Read("CoreIR/standalone-null-3.1.json"));
        var model = ir.Models.Single(model => model.Name == "Envelope");

        var error = model.Properties.Single(p => p.OriginalName == "error");
        error.Type.Should().BeOfType<IrPrimitiveType>()
            .Which.PrimitiveKind.Should().Be(IrPrimitiveKind.Null);
    }

    /// <summary>Preserves nullable normalization when an anyOf contains an explicit null alternative.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task NullInsideAnyOf_StaysNullable()
    {
        var ir = await IrTestHarness.BuildIrAsync(OpenApiFixture.Read("CoreIR/null-anyof-3.1.json"));
        var model = ir.Models.Single(candidate => candidate.Name == "M");

        var capacity = model.Properties.Single(p => p.OriginalName == "capacity");
        capacity.Type.Should().BeOfType<IrNullableType>();
    }

    /// <summary>Reports the endpoint, operation, response and property for a malformed schema.</summary>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    public async Task MalformedSchema_ErrorMessageIncludesLocationContext()
    {
        var doc = OpenApiFixture.Read("CoreIR/malformed-response-array-3.1.json");

        var act = async () => await IrTestHarness.BuildIrAsync(doc);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should()
                .Contain("getApartments").And
                .Contain("/api/apartments").And
                .Contain("response 200").And
                .Contain("data");
    }
}
