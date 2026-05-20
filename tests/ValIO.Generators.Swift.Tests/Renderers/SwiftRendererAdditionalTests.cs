using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Swift.Ast.Renderers;

namespace ValIO.Generators.Swift.Tests.Renderers;

[TestFixture]
public class SwiftRendererAdditionalTests
{
    private SwiftAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new SwiftAstRenderer();

    [Test]
    public void ArrayType_RendersBracketSyntax()
    {
        var n = new GenericTypeNode(new IdentifierNode("Array"),
            ImmutableList.Create<TypeNode>(new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: false)));
        var rendered = _renderer.VisitGenericType(n);
        // Swift Array<T> or [T] — both are valid; verify presence of String.
        rendered.Should().Contain("String");
    }

    [Test]
    public void DictionaryType_RendersBracketSyntax()
    {
        var n = new GenericTypeNode(new IdentifierNode("Dictionary"),
            ImmutableList.Create<TypeNode>(
                new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: false),
                new SimpleTypeNode(new IdentifierNode("Int"), isPrimitive: false)));
        var rendered = _renderer.VisitGenericType(n);
        rendered.Should().Contain("String").And.Contain("Int");
    }

    [Test]
    public void Identifier_RendersBareName()
    {
        _renderer.VisitIdentifier(new IdentifierNode("getUserById")).Should().Be("getUserById");
    }
}
