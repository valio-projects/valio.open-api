using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Swift.Ast.Renderers;

namespace ValIO.Generators.Swift.Tests.Renderers;

/// <summary>
/// Direct unit tests for <see cref="SwiftAstRenderer"/> — type-rendering
/// surface (Array as <c>[T]</c>, Dictionary as <c>[K: V]</c>, optional
/// trailing <c>?</c>).
/// </summary>
[TestFixture]
public class SwiftAstRendererDirectTests
{
    private SwiftAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new SwiftAstRenderer();

    private static SimpleTypeNode Simple(string n, bool isNullable = false) =>
        new(new IdentifierNode(n), isNullable: isNullable);

    [Test]
    public void SimpleType_NotNullable_NoQuestionMark()
    {
        _renderer.VisitSimpleType(Simple("String")).Should().Be("String");
    }

    [Test]
    public void SimpleType_Nullable_AppendsQuestionMark()
    {
        _renderer.VisitSimpleType(Simple("Int", isNullable: true)).Should().Be("Int?");
    }

    [Test]
    public void GenericType_RendersAngleBrackets()
    {
        var t = new GenericTypeNode(new IdentifierNode("Result"),
            ImmutableList.Create<TypeNode>(Simple("Pet"), Simple("Error")));
        _renderer.VisitGenericType(t).Should().Be("Result<Pet, Error>");
    }

    [Test]
    public void NullableType_AppendsQuestionMark()
    {
        var t = new NullableTypeNode(Simple("Pet"));
        _renderer.VisitNullableType(t).Should().Be("Pet?");
    }

    [Test]
    public void OptionalType_StripsWrapper()
    {
        var t = new OptionalTypeNode(Simple("Int"));
        _renderer.VisitOptionalType(t).Should().Be("Int");
    }

    [Test]
    public void ArrayType_RendersInBrackets()
    {
        var t = new ArrayTypeNode(Simple("Pet"));
        _renderer.VisitArrayType(t).Should().Be("[Pet]");
    }

    [Test]
    public void ArrayType_NullableArray_AppendsQuestionMark()
    {
        var t = new ArrayTypeNode(Simple("Pet"), isNullable: true);
        _renderer.VisitArrayType(t).Should().Be("[Pet]?");
    }

    [Test]
    public void DictionaryType_RendersKeyColonValueInBrackets()
    {
        var t = new DictionaryTypeNode(Simple("String"), Simple("Pet"));
        _renderer.VisitDictionaryType(t).Should().Be("[String: Pet]");
    }

    [Test]
    public void DictionaryType_NullableDict_AppendsQuestionMark()
    {
        var t = new DictionaryTypeNode(Simple("String"), Simple("Pet"), isNullable: true);
        _renderer.VisitDictionaryType(t).Should().Be("[String: Pet]?");
    }

    [Test]
    public void UnionType_DegradesToAnyCodable()
    {
        var t = new UnionTypeNode(ImmutableList.Create<TypeNode>(Simple("A"), Simple("B")));
        _renderer.VisitUnionType(t).Should().Be("AnyCodable");
    }

    [Test]
    public void IntersectionType_DegradesToAnyCodable()
    {
        var t = new IntersectionTypeNode(ImmutableList.Create<TypeNode>(Simple("A"), Simple("B")));
        _renderer.VisitIntersectionType(t).Should().Be("AnyCodable");
    }

    [Test]
    public void RawType_PassesThroughUnchanged()
    {
        var t = new RawTypeNode("(Int) -> Void");
        _renderer.VisitRawType(t).Should().Be("(Int) -> Void");
    }
}
