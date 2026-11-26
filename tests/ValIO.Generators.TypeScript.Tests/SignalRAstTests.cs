using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
using ValIO.Core.Services;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.Tests;

/// <summary>Examines the callback and invocation trees before the TypeScript renderer runs.</summary>
[TestFixture]
public sealed class SignalRAstTests
{
    /// <summary>Verifies that cloning preserves argument order and the handler reference used for removal.</summary>
    [Test]
    public void BuildsAndClonesTypedInvocationsAndIndividualUnsubscribe()
    {
        var uuid = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Uuid };
        var count = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Integer };
        var hub = new IrSignalRHub("Support", "/hubs/support",
            [new("SubscribeTicket", [new("ticketId", uuid), new("attempt", count)], count)],
            [new("SupportChanged", [new("attempt", new IrNullableType { InnerType = count })], null)]);
        var context = new GenerationContext
        {
            Configuration = new GenerationConfiguration { LanguageId = "typescript" },
            IrDocument = new IrDocument
            {
                Metadata = new IrDocumentMetadata(), Clients = [], Models = [], Enums = [],
                TypeDependencies = new IrTypeDependencyGraph
                {
                    Dependencies = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                    TopologicalOrder = [],
                },
            },
        };
        var builder = new TypeScriptSignalRAstBuilder(new NamingConverter());
        var file = (SourceFileNode)builder.Build(context, hub).Clone();

        var factory = file.Declarations.OfType<FunctionDeclarationNode>().Single();
        var connection = factory.Parameters.Single().Type.Should().BeOfType<GenericTypeNode>().Which;
        connection.TypeArguments[0].Should().BeOfType<ImportTypeNode>();
        var returned = factory.Body!.Statements.Single().Should().BeOfType<ReturnStatementNode>().Which;
        var adapter = returned.Value.Should().BeOfType<ObjectLiteralExpressionNode>().Which;

        var methods = adapter.Properties[0].Value.Should().BeOfType<ObjectLiteralExpressionNode>().Which;
        var invokeFunction = methods.Properties.Single().Value.Should().BeOfType<ArrowFunctionExpressionNode>().Which;
        var invoke = invokeFunction.BodyExpression.Should().BeOfType<CallExpressionNode>().Which;
        invoke.GenericTypeArgument.Should().BeOfType<SimpleTypeNode>().Which.Name.Name.Should().Be("number");
        invoke.Arguments[0].Should().BeOfType<LiteralExpressionNode>().Which.Value.Should().Be("SubscribeTicket");
        invoke.Arguments.Skip(1).OfType<IdentifierExpressionNode>().Select(argument => argument.Identifier.Name)
            .Should().Equal("_ticketId", "_attempt");

        var events = adapter.Properties[1].Value.Should().BeOfType<ObjectLiteralExpressionNode>().Which;
        var subscribe = events.Properties.Single().Value.Should().BeOfType<ArrowFunctionExpressionNode>().Which;
        subscribe.Parameters.Single().Type.Should().BeOfType<FunctionTypeNode>();
        var on = (CallExpressionNode)((ExpressionStatementNode)subscribe.BodyBlock!.Statements[0]).Expression;
        var unsubscribe = (ArrowFunctionExpressionNode)((ReturnStatementNode)subscribe.BodyBlock.Statements[1]).Value!;
        var off = (CallExpressionNode)unsubscribe.BodyExpression!;
        ((MemberAccessExpressionNode)on.Target).Member.Name.Should().Be("on");
        ((MemberAccessExpressionNode)off.Target).Member.Name.Should().Be("off");
        ((IdentifierExpressionNode)on.Arguments[1]).Identifier.Name.Should().Be(subscribe.Parameters.Single().Name.Name);
        ((IdentifierExpressionNode)off.Arguments[1]).Identifier.Name.Should().Be(subscribe.Parameters.Single().Name.Name);
    }
}
