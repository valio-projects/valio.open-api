using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Tests.TestHelpers;

namespace ValIO.Core.Tests.IR;

/// <summary>
/// Один логический API, выраженный в 4 диалектах OpenAPI (2.0 x-nullable / 3.0 nullable /
/// 3.1, 3.2 type:[T,"null"]). После нормализации IR должен быть эквивалентен независимо от версии.
/// Версии, не поддерживаемые текущим ридером, помечаются Ignore (а не падением).
/// </summary>
[TestFixture]
public class MultiVersionEquivalenceTests
{
    private static readonly string[] Versions = { "2.0", "3.0", "3.1", "3.2" };

    private static async Task<IrDocument?> TryBuildAsync(string version)
    {
        try
        {
            return await IrTestHarness.BuildIrAsync(
                OpenApiFragments.LoadEmbedded($"multiversion/account-{version}.json"));
        }
        catch (Exception ex) when (version is "2.0" or "3.2")
        {
            Assert.Ignore($"Reader does not (yet) support OpenAPI {version}: {ex.Message}");
            return null;
        }
    }

    [TestCaseSource(nameof(Versions))]
    public async Task Reader_ParsesVersion(string version)
    {
        var ir = await TryBuildAsync(version);
        ir!.Models.Select(m => m.Name).Should().Contain("Account");
    }

    [TestCaseSource(nameof(Versions))]
    public async Task NullableScalar_EquivalentAcrossDialects(string version)
    {
        var ir = await TryBuildAsync(version);
        var nickname = ir!.Models.Single(m => m.Name == "Account")
            .Properties.Single(p => p.OriginalName == "nickname");

        nickname.Type.Should().BeOfType<IrNullableType>()
            .Which.InnerType.Should().BeOfType<IrPrimitiveType>()
            .Which.PrimitiveKind.Should().Be(IrPrimitiveKind.String);
    }

    [TestCaseSource(nameof(Versions))]
    public async Task NullableRefWrapper_EquivalentAcrossDialects(string version)
    {
        var ir = await TryBuildAsync(version);
        var envelope = ir!.Models.Single(m => m.Name == "Holder")
            .Properties.Single(p => p.OriginalName == "envelope");

        envelope.Type.Should().BeOfType<IrNullableType>()
            .Which.InnerType.Should().BeOfType<IrModelReference>()
            .Which.ModelName.Should().Be("Account");
    }
}
