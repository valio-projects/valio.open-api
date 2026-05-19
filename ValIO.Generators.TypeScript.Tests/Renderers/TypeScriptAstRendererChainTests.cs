using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.TypeScript.Ast.Renderers;

namespace ValIO.Generators.TypeScript.Tests.Renderers;

/// <summary>
/// Тесты на Chain of Responsibility в <see cref="TypeScriptAstRenderer"/>:
/// проверяем, что звенья опрашиваются, что приоритет соблюдён, и что пустая
/// цепочка не меняет поведение базового рендерера.
/// </summary>
[TestFixture]
public class TypeScriptAstRendererChainTests
{
    /// <summary>
    /// Тестовое звено, которое умеет рендерить только указанный целевой узел.
    /// </summary>
    private sealed class TaggedRenderer : ITypeScriptNodeRenderer
    {
        private readonly AstNode _target;
        private readonly string _output;
        public int CallCount { get; private set; }

        public TaggedRenderer(int priority, AstNode target, string output)
        {
            Priority = priority;
            _target = target;
            _output = output;
        }

        public int Priority { get; }
        public bool CanRender(AstNode node) => ReferenceEquals(node, _target);

        public string Render(AstNode node, ITypeScriptAstRenderer host)
        {
            CallCount++;
            return _output;
        }
    }

    private static SimpleTypeNode MakeStringType() => new(new IdentifierNode("string"), isPrimitive: true);

    [Test]
    public void EmptyChain_FallsBackToBaseDispatch()
    {
        // Arrange: рендерер без звеньев должен работать как раньше.
        var sut = new TypeScriptAstRenderer();
        var node = MakeStringType();

        // Act
        var result = sut.Visit(node);

        // Assert: базовая диспетчеризация формирует имя типа.
        result.Should().Be("string");
    }

    [Test]
    public void NullChain_FallsBackToBaseDispatch()
    {
        // Arrange: ctor с null цепочкой эквивалентен пустой.
        var sut = new TypeScriptAstRenderer(chain: null);
        var node = MakeStringType();

        // Act
        var result = sut.Visit(node);

        // Assert
        result.Should().Be("string");
    }

    [Test]
    public void Chain_HighPriorityRenderer_HandlesMatchingNode()
    {
        // Arrange: одно звено, которое перехватывает конкретный узел.
        var marker = MakeStringType();
        var renderer = new TaggedRenderer(priority: 200, target: marker, output: "<TAGGED>");
        var sut = new TypeScriptAstRenderer(new[] { renderer });

        // Act
        var result = sut.Visit(marker);

        // Assert: звено вернуло свою строку, минуя стандартный рендеринг.
        result.Should().Be("<TAGGED>");
        renderer.CallCount.Should().Be(1);
    }

    [Test]
    public void Chain_PriorityOrdering_HigherPriorityWins()
    {
        // Arrange: две записи могут отрендерить один и тот же узел; берётся
        // та, у которой выше Priority.
        var marker = MakeStringType();
        var low = new TaggedRenderer(priority: 10, target: marker, output: "<LOW>");
        var high = new TaggedRenderer(priority: 200, target: marker, output: "<HIGH>");

        // Передаём в произвольном порядке, проверяем что сортировка работает.
        var sut = new TypeScriptAstRenderer(new[] { low, high });

        // Act
        var result = sut.Visit(marker);

        // Assert
        result.Should().Be("<HIGH>");
        high.CallCount.Should().Be(1);
        low.CallCount.Should().Be(0);
    }

    [Test]
    public void Chain_NonMatchingNode_FallsBackToBase()
    {
        // Arrange: звено привязано к одному узлу, второй проходит мимо.
        var bound = MakeStringType();
        var unrelated = new SimpleTypeNode(new IdentifierNode("number"), isPrimitive: true);
        var renderer = new TaggedRenderer(priority: 200, target: bound, output: "<TAGGED>");
        var sut = new TypeScriptAstRenderer(new[] { renderer });

        // Act
        var result = sut.Visit(unrelated);

        // Assert: для неподходящего узла отрабатывает базовый рендеринг.
        result.Should().Be("number");
        renderer.CallCount.Should().Be(0);
    }

    [Test]
    public void Chain_FirstMatchingWinsForEqualPriorities()
    {
        // Arrange: при равных приоритетах OrderByDescending — стабильна, поэтому
        // первая запись в исходной коллекции должна выиграть.
        var marker = MakeStringType();
        var first = new TaggedRenderer(priority: 100, target: marker, output: "<FIRST>");
        var second = new TaggedRenderer(priority: 100, target: marker, output: "<SECOND>");

        var sut = new TypeScriptAstRenderer(new[] { first, second });

        // Act
        var result = sut.Visit(marker);

        // Assert
        result.Should().Be("<FIRST>");
        first.CallCount.Should().Be(1);
        second.CallCount.Should().Be(0);
    }
}
