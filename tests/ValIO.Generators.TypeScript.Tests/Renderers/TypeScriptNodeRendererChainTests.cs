using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Renderers;

namespace ValIO.Generators.TypeScript.Tests.Renderers;

/// <summary>
/// Unit tests for the <see cref="ITypeScriptNodeRenderer"/> Chain-of-Responsibility
/// wired into <see cref="TypeScriptAstRenderer"/>. The chain is consulted by
/// <c>VisitDefault</c> when a node's <c>Accept</c> falls through to
/// <see cref="IBaseAstVisitor{TResult}.DefaultVisit(AstNode)"/>.
/// </summary>
[TestFixture]
public class TypeScriptNodeRendererChainTests
{
    /// <summary>
    /// A test-only marker node that always routes through
    /// <see cref="IBaseAstVisitor{TResult}.DefaultVisit(AstNode)"/>, simulating a
    /// plugin-supplied node type the host renderer cannot dispatch on its own.
    /// </summary>
    private sealed class MarkerNode : AstNode
    {
        public string Tag { get; }

        public MarkerNode(string tag) : base(metadata: null) => Tag = tag;

        public override IReadOnlyList<AstNode> Children => ImmutableList<AstNode>.Empty;

        public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
            visitor.DefaultVisit(this);

        public override AstNode Clone() => new MarkerNode(Tag);
    }

    private sealed class MarkerRenderer : ITypeScriptNodeRenderer
    {
        private readonly Func<AstNode, bool> _canRender;
        private readonly Func<AstNode, ITypeScriptAstRenderer, string> _render;

        public MarkerRenderer(int priority, Func<AstNode, bool> canRender, Func<AstNode, ITypeScriptAstRenderer, string> render)
        {
            Priority = priority;
            _canRender = canRender;
            _render = render;
        }

        public int Priority { get; }
        public bool CanRender(AstNode node) => _canRender(node);
        public string Render(AstNode node, ITypeScriptAstRenderer host) => _render(node, host);
    }

    [Test]
    public void Chain_HighPriorityRenderer_HandlesMarkerNode()
    {
        var chain = new ITypeScriptNodeRenderer[]
        {
            new MarkerRenderer(
                priority: 100,
                canRender: n => n is MarkerNode,
                render: (n, _) => $"/* rendered: {((MarkerNode)n).Tag} */"),
        };
        var renderer = new TypeScriptAstRenderer(chain);

        var output = renderer.Visit(new MarkerNode("alpha"));

        output.Should().Be("/* rendered: alpha */");
    }

    [Test]
    public void Chain_HigherPriority_WinsWhenBothCanRender()
    {
        var chain = new ITypeScriptNodeRenderer[]
        {
            new MarkerRenderer(priority: 10, canRender: _ => true, render: (_, _) => "low"),
            new MarkerRenderer(priority: 50, canRender: _ => true, render: (_, _) => "high"),
        };
        var renderer = new TypeScriptAstRenderer(chain);

        var output = renderer.Visit(new MarkerNode("ignored"));

        output.Should().Be("high",
            "TypeScriptAstRenderer sorts the chain by descending Priority before consulting it");
    }

    [Test]
    public void Chain_Empty_FallsBackToBaseException()
    {
        var renderer = new TypeScriptAstRenderer(Array.Empty<ITypeScriptNodeRenderer>());

        var act = () => renderer.Visit(new MarkerNode("orphan"));

        act.Should().Throw<NotSupportedException>(
            "the base VisitDefault throws NotSupportedException when no chain link claims the node");
    }

    [Test]
    public void Chain_ParameterlessCtor_BehavesLikeEmptyChain()
    {
        // The legacy parameterless ctor is preserved for direct callers (tests,
        // out-of-DI usage). It must behave exactly as before — no chain → exception.
        var renderer = new TypeScriptAstRenderer();

        var act = () => renderer.Visit(new MarkerNode("orphan"));

        act.Should().Throw<NotSupportedException>();
    }

    [Test]
    public void Chain_NonMatchingLinks_FallThroughToBaseException()
    {
        // A renderer that claims a different node type must NOT be consulted for the
        // marker node — the chain continues past it.
        var chain = new ITypeScriptNodeRenderer[]
        {
            new MarkerRenderer(
                priority: 1000,
                canRender: _ => false,
                render: (_, _) => throw new InvalidOperationException("must not be called")),
        };
        var renderer = new TypeScriptAstRenderer(chain);

        var act = () => renderer.Visit(new MarkerNode("orphan"));

        act.Should().Throw<NotSupportedException>();
    }

    [Test]
    public void Chain_RenderReceivesHostInstance_ForRecursiveVisits()
    {
        // The host parameter lets a chain link recurse into the owning renderer for
        // children — verify the host passed in is the same instance.
        ITypeScriptAstRenderer? captured = null;
        var chain = new ITypeScriptNodeRenderer[]
        {
            new MarkerRenderer(
                priority: 1,
                canRender: _ => true,
                render: (_, host) => { captured = host; return string.Empty; }),
        };
        var renderer = new TypeScriptAstRenderer(chain);

        _ = renderer.Visit(new MarkerNode("x"));

        captured.Should().BeSameAs(renderer);
    }
}
