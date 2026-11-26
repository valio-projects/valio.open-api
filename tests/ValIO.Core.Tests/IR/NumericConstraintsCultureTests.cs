using ValIO.Tests.Fixtures;
using System.Globalization;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Tests.TestHelpers;

namespace ValIO.Core.Tests.IR;

/// <summary>Проверяет независимость числовых границ OpenAPI от региональных настроек процесса.</summary>
[TestFixture]
public sealed class NumericConstraintsCultureTests
{
    /// <summary>Разбирает все дробные границы при региональных настройках с разными десятичными разделителями.</summary>
    /// <param name="culture">Региональные настройки, используемые при построении промежуточного представления.</param>
    /// <returns>Задача, завершающаяся после проверки всех границ.</returns>
    [TestCase("en-US")]
    [TestCase("ru-RU")]
    public async Task FractionalBoundsUseInvariantOpenApiSyntax(string culture)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var ir = await IrTestHarness.BuildIrAsync(OpenApiFixture.Read("CoreIR/numeric-constraints.json"));
            var model = ir.Models.Single(model => model.Name == "Item");
            var range = model.Properties.Single(p => p.OriginalName == "amount").Constraints;
            range.Should().NotBeNull();
            range!.Min.Should().Be(-1.5m);
            range.Max.Should().Be(125m);
            var exclusive = model.Properties.Single(p => p.OriginalName == "score").Constraints;
            exclusive.Should().NotBeNull();
            exclusive!.ExclusiveMin.Should().Be(1.25m);
            exclusive.ExclusiveMax.Should().Be(250m);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }
}
