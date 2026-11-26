using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.CSharp.Ast.Nodes;
using ValIO.Generators.CSharp.Services;

namespace ValIO.Generators.CSharp.Ast.Builders;

/// <summary>Lowers the SignalR contract to C# declarations that use the official runtime.</summary>
internal sealed class CSharpSignalRAstBuilder
{
    /// <summary>The runtime connection name, qualified to avoid model-name collisions.</summary>
    private const string ConnectionType = "global::Microsoft.AspNetCore.SignalR.Client.HubConnection";

    /// <summary>The common completion type for invocations and event handlers.</summary>
    private const string TaskName = "global::System.Threading.Tasks.Task";

    /// <summary>Applies the same wire type rules as the model generator.</summary>
    private readonly CSharpIrTypeMapper _types = new();

    /// <summary>Builds the server implementation surface and its typed event contract.</summary>
    /// <param name="hub">The validated SignalR hub.</param>
    /// <param name="ns">The output namespace.</param>
    /// <param name="name">The generated hub base name.</param>
    /// <returns>The compilation unit containing both declarations.</returns>
    internal CompilationUnitNode BuildHub(IrSignalRHub hub, string ns, string name)
    {
        var eventsName = "I" + hub.Name + "ClientEvents";
        var events = new InterfaceDeclarationNode(new(eventsName),
            members: hub.Events.Select(BuildEventDeclaration).ToArray(),
            documentation: Doc("Events the hub may send to connected clients."));
        var baseType = new GenericTypeNode(new("global::Microsoft.AspNetCore.SignalR.Hub"), [Type(eventsName)]);
        var server = new ClassDeclarationNode(new(name), baseTypes: [baseType],
            members: [BuildPath(hub.Path), .. hub.Methods.Select(BuildServerMethod)],
            documentation: Doc("Defines the callable surface of the hub for an application-owned implementation."))
        {
            IsAbstract = true,
        };

        return BuildFile(ns, [events, server], client: false);
    }

    /// <summary>Builds a typed client that uses an existing connection.</summary>
    /// <param name="hub">The validated SignalR hub.</param>
    /// <param name="ns">The output namespace.</param>
    /// <param name="name">The generated client name.</param>
    /// <returns>The compilation unit containing the adapter.</returns>
    /// <exception cref="InvalidOperationException">An event exceeds the runtime delegate arity.</exception>
    internal CompilationUnitNode BuildClient(IrSignalRHub hub, string ns, string name)
    {
        var field = new FieldDeclarationNode(new("_connection"), Type(ConnectionType), isReadOnly: true,
            documentation: Doc("The connection supplied by the application's composition."));
        var assignConnection = new BinaryExpressionNode(ConnectionMember(), BinaryOperator.Assign, Ref("connection"));
        var constructor = new ConstructorDeclarationNode(new(name), new BlockNode([new ExpressionStatementNode(assignConnection)]),
            parameters: [new(new("connection"), Type(ConnectionType), documentation: Doc("The configured SignalR connection."))],
            documentation: Doc("Creates a client over a configured SignalR connection."));

        var members = new List<DeclarationNode> { field, constructor, BuildPath(hub.Path) };
        members.AddRange(hub.Methods.Select(BuildInvocation));
        foreach (var notification in hub.Events)
        {
            if (notification.Parameters.Count > 16)
                throw new InvalidOperationException($"SignalR event '{hub.Name}.{notification.Name}' exceeds the C# delegate arity of 16.");

            members.Add(BuildSubscription(notification));
        }

        if (hub.Events.Any(notification => notification.Parameters.Count > 0))
        {
            members.Add(new MethodDeclarationNode(new("ParameterType"), Type("global::System.Type"),
                body: Return(new TypeOfExpressionNode(Type("T"))), accessModifier: AccessModifier.Private,
                documentation: Doc("Gets the SignalR binder type including nullable value types."),
                genericTypeParameters: ["T"], isStatic: true)
            {
                ReturnsDocumentation = "The type used to bind the incoming argument.",
            });
        }

        var client = new ClassDeclarationNode(new(name), isSealed: true, members: members,
            documentation: Doc("Invokes hub methods and subscribes to notifications using an existing connection."));
        return BuildFile(ns, [client], client: true);
    }

    /// <summary>Creates a file with the nullable context and the imports required by the mapped types.</summary>
    /// <param name="ns">The output namespace.</param>
    /// <param name="declarations">The declarations inside the namespace.</param>
    /// <param name="client">Whether SignalR client extensions are used.</param>
    /// <returns>The complete C# syntax tree.</returns>
    private static CompilationUnitNode BuildFile(string ns, IReadOnlyList<DeclarationNode> declarations, bool client)
    {
        var imports = new List<UsingDirectiveNode> { new(new("System")), new(new("System.Collections.Generic")) };
        if (client)
            imports.Add(new(new("Microsoft.AspNetCore.SignalR.Client")));

        return new CompilationUnitNode(imports, [new(new(ns), declarations)])
        {
            EnableNullable = true,
            FileHeader = new CommentNode("<auto-generated />"),
        };
    }

    /// <summary>Declares an event method implemented by the server's client proxy.</summary>
    /// <param name="notification">The outgoing event signature.</param>
    /// <returns>A bodyless client proxy method.</returns>
    private MethodDeclarationNode BuildEventDeclaration(IrSignalRCall notification) =>
        new(new(CSharpIdentifier.Escape(notification.Name)), Type(TaskName), Parameters(notification),
            documentation: Doc($"Delivers the {notification.Name} notification."),
            isInterfaceMethod: true)
        {
            ReturnsDocumentation = "The asynchronous send.",
        };

    /// <summary>Declares the override required from the application-owned hub.</summary>
    /// <param name="call">The incoming method signature.</param>
    /// <returns>An abstract method using the declared result type.</returns>
    private MethodDeclarationNode BuildServerMethod(IrSignalRCall call) =>
        new(new(CSharpIdentifier.Escape(call.Name)), TaskType(call.Result), Parameters(call),
            documentation: Doc($"Handles the {call.Name} invocation."), isAbstract: true)
        {
            ReturnsDocumentation = "The completion of the hub method.",
        };

    /// <summary>Builds a typed invocation with the original argument order and a local cancellation token.</summary>
    /// <param name="call">The incoming method signature.</param>
    /// <returns>The client method calling the existing connection.</returns>
    private MethodDeclarationNode BuildInvocation(IrSignalRCall call)
    {
        var cancellation = "cancellationToken";
        while (call.Parameters.Any(parameter => parameter.Name == cancellation))
            cancellation += "_";

        var parameters = Parameters(call);
        var arguments = new CollectionExpressionNode(parameters.Select(parameter =>
            (ExpressionNode)new IdentifierExpressionNode(parameter.Name)).ToArray());
        var invoke = new CallExpressionNode(ConnectionMember("InvokeCoreAsync"),
            [Literal(call.Name), arguments, Ref(cancellation)],
            genericTypeArgument: call.Result is null ? null : _types.MapType(call.Result));

        return new MethodDeclarationNode(new(CSharpIdentifier.Escape(call.Name)), TaskType(call.Result),
            [.. parameters, new(new(cancellation), Type("global::System.Threading.CancellationToken"),
                defaultValue: new DefaultExpressionNode(), documentation: Doc("Cancellation of the local invocation wait."))],
            Return(invoke), documentation: Doc($"Invokes {call.Name} on the connected hub."))
        {
            ReturnsDocumentation = "The asynchronous hub result.",
        };
    }

    /// <summary>Builds the event binder and handler invocation using the declared positional argument types.</summary>
    /// <param name="notification">The outgoing event signature.</param>
    /// <returns>The client method returning the individual subscription handle.</returns>
    private MethodDeclarationNode BuildSubscription(IrSignalRCall notification)
    {
        var parameterTypes = notification.Parameters.Select(parameter => _types.MapType(parameter.Type)).ToArray();
        var handlerType = new GenericTypeNode(new("global::System.Func"), [.. parameterTypes, Type(TaskName)]);
        var binders = parameterTypes.Select(type =>
            (ExpressionNode)new CallExpressionNode(Ref("ParameterType"), genericTypeArgument: type)).ToArray();
        var arguments = parameterTypes.Select((type, index) =>
        {
            var slot = new IndexAccessExpressionNode(Ref("arguments"), new LiteralExpressionNode(index, LiteralKind.Number));
            return (ExpressionNode)new CastExpressionNode(type, new NullForgivingExpressionNode(slot));
        }).ToArray();
        var handler = new LambdaExpressionNode(new("arguments"), new CallExpressionNode(Ref("handler"), arguments));
        var subscribe = new CallExpressionNode(ConnectionMember("On"),
            [Literal(notification.Name), new CollectionExpressionNode(binders), handler]);

        return new MethodDeclarationNode(new("On" + notification.Name), Type("global::System.IDisposable"),
            [new(new("handler"), handlerType, documentation: Doc("The callback invoked with the notification arguments."))],
            Return(subscribe), documentation: Doc($"Subscribes to the {notification.Name} notification."))
        {
            ReturnsDocumentation = "A subscription handle which removes this handler when disposed.",
        };
    }

    /// <summary>Infers the non-generic or typed completion type of a hub method.</summary>
    /// <param name="result">The declared payload, if present.</param>
    /// <returns>The mapped task type.</returns>
    private TypeNode TaskType(IrType? result) => result is null
        ? Type(TaskName)
        : new GenericTypeNode(new(TaskName), [_types.MapType(result)]);

    /// <summary>Maps the positional arguments shared by the server and client signatures.</summary>
    /// <param name="call">The method or event owning the arguments.</param>
    /// <returns>The ordered parameter declarations.</returns>
    private ParameterDeclarationNode[] Parameters(IrSignalRCall call) => call.Parameters.Select(parameter =>
        new ParameterDeclarationNode(new("@" + parameter.Name), _types.MapType(parameter.Type),
            documentation: Doc($"The {parameter.Name} argument."))).ToArray();

    /// <summary>Declares the endpoint path as a public constant.</summary>
    /// <param name="path">The owner-declared route.</param>
    /// <returns>The generated path constant.</returns>
    private static FieldDeclarationNode BuildPath(string path) =>
        new(new("HubPath"), Type("string"), AccessModifier.Public, isStatic: true,
            defaultValue: Literal(path), documentation: Doc("The path declared for this hub."));

    /// <summary>References the stored connection without ambiguity with user arguments.</summary>
    /// <param name="member">An optional method on the connection.</param>
    /// <returns>The connection or one of its members.</returns>
    private static MemberAccessExpressionNode ConnectionMember(string? member = null)
    {
        var connection = new MemberAccessExpressionNode(Ref("this"), new("_connection"));
        return member is null ? connection : new MemberAccessExpressionNode(connection, new(member));
    }

    /// <summary>Builds a body containing the returned expression.</summary>
    /// <param name="value">The value returned by the adapter.</param>
    /// <returns>A method body with a return statement.</returns>
    private static BlockNode Return(ExpressionNode value) => new([new ReturnStatementNode(value)]);

    /// <summary>References a symbol in scope.</summary>
    /// <param name="name">The C# symbol name.</param>
    /// <returns>The reference expression.</returns>
    private static IdentifierExpressionNode Ref(string name) => new(new IdentifierNode(name));

    /// <summary>Declares a simple type name.</summary>
    /// <param name="name">The qualified name or keyword of the type.</param>
    /// <returns>The type reference.</returns>
    private static SimpleTypeNode Type(string name) => new(new(name));

    /// <summary>Preserves the contract text for C# literal escaping.</summary>
    /// <param name="value">The route or wire name.</param>
    /// <returns>A string literal expression.</returns>
    private static LiteralExpressionNode Literal(string value) => new(value, LiteralKind.String);

    /// <summary>Captures documentation for the C# printer.</summary>
    /// <param name="text">The summary or parameter description.</param>
    /// <returns>The documentation node.</returns>
    private static CommentNode Doc(string text) => new(text, CommentKind.Xml);
}
