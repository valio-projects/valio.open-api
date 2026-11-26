using ValIO.Tests.Fixtures;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR.Types;
using ValIO.Core.Tests.TestHelpers;

namespace ValIO.Core.Tests.IR;

/// <summary>Проверяет, что допустимость null для контейнера не смешивается с необязательностью элементов и свойств.</summary>
[TestFixture]
public sealed class NullableContainerTests
{
    /// <summary>Сохраняет допустимость null для массивов и словарей в обеих версиях OpenAPI.</summary>
    /// <param name="version">Проверяемая версия OpenAPI.</param>
    /// <returns>The asynchronous container type assertions.</returns>
    [TestCase("3.0.3")]
    [TestCase("3.1.0")]
    public async Task Nullable_containers_keep_their_inner_type(string version)
    {
        var ir = await IrTestHarness.BuildIrAsync(OpenApiFixture.Read($"CoreIR/nullable-containers-{version}.json"));
        var model = ir.Models.Single(model => model.Name == "Holder");
        var items = model.Properties.Single(p => p.OriginalName == "items");
        items.IsRequired.Should().BeTrue();
        items.Type.Should().BeOfType<IrNullableType>().Which.InnerType.Should().BeOfType<IrArrayType>();
        var lookup = model.Properties.Single(p => p.OriginalName == "lookup");
        lookup.IsRequired.Should().BeFalse();
        lookup.Type.Should().BeOfType<IrNullableType>().Which.InnerType.Should().BeOfType<IrDictionaryType>();
    }
}
