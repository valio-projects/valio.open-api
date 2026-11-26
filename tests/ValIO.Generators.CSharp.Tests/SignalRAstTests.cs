using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.CSharp.Ast.Builders;
using ValIO.Generators.CSharp.Ast.Nodes;

namespace ValIO.Generators.CSharp.Tests;

/// <summary>Examines the C# server and client contracts before source is printed.</summary>
[TestFixture]
public sealed class SignalRAstTests
{
    /// <summary>Clones a hub and its client and verifies their members and ordered invocation arguments.</summary>
    [Test]
    public void BuildsAndClonesOrderedInvocationAndHubDeclarations()
    {
        var hub = new IrSignalRHub("Support", "/hubs/support",
            [new("SubscribeTicket",
                [new("ticketId", new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Uuid }),
                    new("cancellationToken", new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Integer })],
                new IrModelReference { ModelName = "RealtimeResult" })],
            [new("WalletChanged", [], null)]);
        var builder = new CSharpSignalRAstBuilder();

        var server = (CompilationUnitNode)builder.BuildHub(hub, "Sdk", "SupportHubBase").Clone();
        var declaration = server.Namespaces.Single().Declarations.OfType<ClassDeclarationNode>().Single();
        declaration.IsAbstract.Should().BeTrue();
        declaration.BaseTypes.Single().Should().BeOfType<GenericTypeNode>()
            .Which.TypeArguments.Single().Should().BeOfType<SimpleTypeNode>().Which.Name.Name.Should().Be("ISupportClientEvents");

        var client = (CompilationUnitNode)builder.BuildClient(hub, "Sdk", "SupportSignalRClient").Clone();
        var method = client.Namespaces.Single().Declarations.OfType<ClassDeclarationNode>().Single().Members
            .OfType<MethodDeclarationNode>().Single(member => member.Name.Name == "SubscribeTicket");
        var statement = method.Body!.Statements.Single().Should().BeOfType<ReturnStatementNode>().Which;
        var call = statement.Value.Should().BeOfType<CallExpressionNode>().Which;
        call.Target.Should().BeOfType<MemberAccessExpressionNode>().Which.Member.Name.Should().Be("InvokeCoreAsync");
        call.GenericTypeArgument.Should().BeOfType<SimpleTypeNode>().Which.Name.Name.Should().Be("RealtimeResult");
        call.Arguments[0].Should().BeOfType<LiteralExpressionNode>().Which.Value.Should().Be("SubscribeTicket");
        var arguments = call.Arguments[1].Should().BeOfType<CollectionExpressionNode>().Which;
        arguments.Children.OfType<IdentifierExpressionNode>().Select(arg => arg.Identifier.Name)
            .Should().Equal(method.Parameters.Take(2).Select(parameter => parameter.Name.Name));
        call.Arguments[2].Should().BeOfType<IdentifierExpressionNode>().Which.Identifier.Name.Should().Be("cancellationToken_");
        client.EnableNullable.Should().BeTrue();
    }
}
