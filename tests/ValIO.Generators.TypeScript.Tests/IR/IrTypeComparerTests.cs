using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.IR.Utilities;

namespace ValIO.Generators.TypeScript.Tests.IR;

/// <summary>
/// Tests for <see cref="IrTypeComparer.AreSemanticallyEqual"/>.
/// </summary>
[TestFixture]
public class IrTypeComparerTests
{
    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    [Test]
    public void BothNull_Equal()
    {
        IrTypeComparer.AreSemanticallyEqual(null, null).Should().BeTrue();
    }

    [Test]
    public void OneNull_NotEqual()
    {
        IrTypeComparer.AreSemanticallyEqual(null, Prim(IrPrimitiveKind.String)).Should().BeFalse();
        IrTypeComparer.AreSemanticallyEqual(Prim(IrPrimitiveKind.String), null).Should().BeFalse();
    }

    [Test]
    public void SamePrimitive_Equal()
    {
        IrTypeComparer.AreSemanticallyEqual(
            Prim(IrPrimitiveKind.String),
            Prim(IrPrimitiveKind.String)).Should().BeTrue();
    }

    [Test]
    public void DifferentPrimitives_NotEqual()
    {
        IrTypeComparer.AreSemanticallyEqual(
            Prim(IrPrimitiveKind.String),
            Prim(IrPrimitiveKind.Integer)).Should().BeFalse();
    }

    [Test]
    public void SameModelRef_Equal()
    {
        IrTypeComparer.AreSemanticallyEqual(
            new IrModelReference { ModelName = "Pet" },
            new IrModelReference { ModelName = "Pet" }).Should().BeTrue();
    }

    [Test]
    public void DifferentModelRefs_NotEqual()
    {
        IrTypeComparer.AreSemanticallyEqual(
            new IrModelReference { ModelName = "Pet" },
            new IrModelReference { ModelName = "Owner" }).Should().BeFalse();
    }

    [Test]
    public void ArrayOfSamePrimitive_Equal()
    {
        IrTypeComparer.AreSemanticallyEqual(
            new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) },
            new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) }).Should().BeTrue();
    }

    [Test]
    public void ArrayOfDifferentPrimitive_NotEqual()
    {
        IrTypeComparer.AreSemanticallyEqual(
            new IrArrayType { ElementType = Prim(IrPrimitiveKind.String) },
            new IrArrayType { ElementType = Prim(IrPrimitiveKind.Integer) }).Should().BeFalse();
    }

    [Test]
    public void NullableUnwraps_StringEqualsNullableString()
    {
        IrTypeComparer.AreSemanticallyEqual(
            Prim(IrPrimitiveKind.String),
            new IrNullableType { InnerType = Prim(IrPrimitiveKind.String) }).Should().BeTrue();
    }

    [Test]
    public void OptionalUnwraps_StringEqualsOptionalString()
    {
        IrTypeComparer.AreSemanticallyEqual(
            Prim(IrPrimitiveKind.String),
            new IrOptionalType { InnerType = Prim(IrPrimitiveKind.String) }).Should().BeTrue();
    }

    [Test]
    public void NestedNullableOptional_StillUnwraps()
    {
        // Nullable around Optional around String — all should compare equal to String.
        var nested = new IrNullableType
        {
            InnerType = new IrOptionalType { InnerType = Prim(IrPrimitiveKind.Integer) }
        };
        IrTypeComparer.AreSemanticallyEqual(Prim(IrPrimitiveKind.Integer), nested).Should().BeTrue();
    }

    [Test]
    public void ArrayOfModelRef_VsArrayOfDifferentRef_NotEqual()
    {
        IrTypeComparer.AreSemanticallyEqual(
            new IrArrayType { ElementType = new IrModelReference { ModelName = "Pet" } },
            new IrArrayType { ElementType = new IrModelReference { ModelName = "Owner" } })
            .Should().BeFalse();
    }
}
