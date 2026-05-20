using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Tests.Services;

/// <summary>
/// Tests for <see cref="UENamingConvention"/>: F-prefix for structs,
/// U-prefix for classes, E-prefix for enums.
/// </summary>
[TestFixture]
public class UENamingConventionTests
{
    private UENamingConvention _sut = null!;

    [SetUp]
    public void Setup() => _sut = new UENamingConvention();

    [Test]
    public void StructName_AddsFPrefix()
    {
        _sut.StructName("Pet").Should().Be("FPet");
    }

    [Test]
    public void StructName_AlreadyPrefixed_DoesNotDoubleF()
    {
        _sut.StructName("FCustom").Should().Be("FCustom");
    }

    [Test]
    public void StructName_LowercaseInput_CamelCased()
    {
        _sut.StructName("my_model").Should().Be("FMyModel");
    }

    [Test]
    public void ClassName_AddsUPrefix()
    {
        _sut.ClassName("AsyncAction").Should().Be("UAsyncAction");
    }

    [Test]
    public void ClassName_AlreadyPrefixed_DoesNotDoubleU()
    {
        _sut.ClassName("UCustom").Should().Be("UCustom");
    }

    [Test]
    public void EnumName_AddsEPrefix()
    {
        _sut.EnumName("Status").Should().Be("EStatus");
    }

    [Test]
    public void EnumName_AlreadyPrefixed_DoesNotDoubleE()
    {
        _sut.EnumName("ECustom").Should().Be("ECustom");
    }

    [Test]
    public void EnumName_SnakeCaseInput_PascalCased()
    {
        _sut.EnumName("user_role").Should().Be("EUserRole");
    }
}
