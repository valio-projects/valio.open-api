using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Kotlin.Ast.Renderers;

namespace ValIO.Generators.Kotlin.Tests.Renderers;

/// <summary>
/// Additional Kotlin AST renderer invariants.
/// </summary>
[TestFixture]
public class KotlinRendererAdditionalTests
{
    private KotlinAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new KotlinAstRenderer();

    [Test]
    public void ListGeneric_RendersWithAngleBrackets()
    {
        var n = new GenericTypeNode(new IdentifierNode("List"),
            ImmutableList.Create<TypeNode>(new SimpleTypeNode(new IdentifierNode("Pet"), isPrimitive: false)));
        _renderer.VisitGenericType(n).Should().Be("List<Pet>");
    }

    [Test]
    public void MapGeneric_RendersWithTwoTypeArguments()
    {
        var n = new GenericTypeNode(new IdentifierNode("Map"),
            ImmutableList.Create<TypeNode>(
                new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: false),
                new SimpleTypeNode(new IdentifierNode("Int"), isPrimitive: false)));
        _renderer.VisitGenericType(n).Should().Be("Map<String, Int>");
    }

    [Test]
    public void NestedGeneric_RendersRecursively()
    {
        var inner = new GenericTypeNode(new IdentifierNode("List"),
            ImmutableList.Create<TypeNode>(new SimpleTypeNode(new IdentifierNode("Pet"), isPrimitive: false)));
        var outer = new GenericTypeNode(new IdentifierNode("Map"),
            ImmutableList.Create<TypeNode>(
                new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: false),
                inner));
        _renderer.VisitGenericType(outer).Should().Be("Map<String, List<Pet>>");
    }

    [Test]
    public void Identifier_RendersBareName()
    {
        _renderer.VisitIdentifier(new IdentifierNode("getUserById")).Should().Be("getUserById");
    }
}
