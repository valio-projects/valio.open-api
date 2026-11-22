using ValIO.Core.IR;
using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Core.Tests.IR;

/// <summary>Covers value equality and nonmutating overrides used by the IR builder and generators.</summary>
public sealed class IrValueSemanticsTests
{
    /// <summary>Tests that exact constraints and concrete shape participate in dictionary keys.</summary>
    [Test]
    public void EquivalentScalarTypesShareDictionaryKeys()
    {
        IrType original = new IrPrimitiveType
        {
            PrimitiveKind = IrPrimitiveKind.Number,
            Format = "decimal",
            Constraints = new IrPropertyConstraints { Max = 123456789123456789.1234567891m },
        };
        var copy = original.WithConstraints(new IrPropertyConstraints { Max = 123456789123456789.1234567891m });
        var changed = original.WithConstraints(new IrPropertyConstraints { Max = 123456789123456789.1234567892m });
        var values = new Dictionary<IrType, string> { [original] = "money" };

        Assert.Multiple(() =>
        {
            Assert.That(ReferenceEquals(original, copy), Is.False);
            Assert.That(original == copy, Is.True);
            Assert.That(values[copy], Is.EqualTo("money"));
            Assert.That(values.ContainsKey(changed), Is.False);
            Assert.That(values.ContainsKey(new IrNullableType { InnerType = original }), Is.False);
        });
    }

    /// <summary>Tests that copying distinguishes generated fallback names from explicit source names.</summary>
    [Test]
    public void CopyPreservesOriginalWireNameFallback()
    {
        var fallback = new IrModelReference { ModelName = "Original" };
        var explicitName = new IrModelReference { ModelName = "Generated", OriginalModelName = "Original" };

        Assert.Multiple(() =>
        {
            Assert.That(new IrModelReference(fallback) { ModelName = "Renamed" }.OriginalModelName, Is.EqualTo("Renamed"));
            Assert.That(new IrModelReference(explicitName) { ModelName = "Renamed" }.OriginalModelName, Is.EqualTo("Original"));
            Assert.That(fallback.ModelName, Is.EqualTo("Original"));
        });
    }

    /// <summary>Tests that response copies retain header and alternate media declarations.</summary>
    [Test]
    public void ResponseCopyPreservesAlternateContentAndHeaders()
    {
        var payload = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.String };
        var response = new IrResponse
        {
            StatusCode = "200",
            Type = payload,
            ContentTypes = ["text/plain", "application/json"],
            MediaTypes = [new IrResponseMediaType { Name = "text/plain", Type = payload }],
            Headers = [new IrResponseHeader { Name = "ETag", IsRequired = true }],
        };

        var empty = new IrResponse(response) { StatusCode = "204", Type = null, ContentTypes = [] };
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo("200"));
            Assert.That(response.Type, Is.SameAs(payload));
            Assert.That(empty.Type, Is.Null);
            Assert.That(empty.Headers, Is.SameAs(response.Headers));
            Assert.That(empty.MediaTypes, Is.SameAs(response.MediaTypes));
        });
    }
}
