using System.Text;
using System.Text.Json;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Core.Plugins;
using ValIO.Generators.CSharp.Ast.Renderers;
using ValIO.Generators.CSharp.Options;

namespace ValIO.Generators.CSharp.Steps;

/// <summary>Generates C# contracts and adapters around the official SignalR runtime.</summary>
/// <param name="renderer">The pipeline-owned C# type renderer.</param>
internal sealed class CSharpSignalRStep(ICSharpAstRenderer renderer) : ICodeGenerationStep
{
    /// <summary>Maps hub wire types using the same mapping as generated C# models.</summary>
    private readonly CSharpIrTypeMapper _types = new();

    /// <inheritdoc />
    public string Name => "csharp-signalr";

    /// <inheritdoc />
    public int Order => 40;

    /// <inheritdoc />
    public bool ShouldExecute(GenerationContext context) => context.IrDocument.SignalRHubs.Count > 0 &&
        (context.Configuration.GenerateClients || context.PluginOptions is CSharpGenerationOptions { GenerateSignalRHubs: true });

    /// <inheritdoc />
    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var options = context.PluginOptions as CSharpGenerationOptions;
        var ns = options?.Namespace ?? context.Configuration.Namespace ?? "Generated";
        foreach (var hub in context.IrDocument.SignalRHubs)
        {
            ct.ThrowIfCancellationRequested();
            if (options?.GenerateSignalRHubs == true)
            {
                var name = hub.Name + "HubBase";
                EnsureDeclarationNames(context, hub, name, "I" + hub.Name + "ClientEvents");
                context.GeneratedFiles.Add(new GeneratedFile($"SignalR/{name}.cs", RenderHub(hub, ns, name)));
            }

            if (context.Configuration.GenerateClients)
            {
                var name = hub.Name + "SignalRClient";
                EnsureDeclarationNames(context, hub, name);
                context.GeneratedFiles.Add(new GeneratedFile($"SignalR/{name}.cs", RenderClient(hub, ns, name)));
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Checks emitted names against models and generated members.</summary>
    /// <param name="context">The generation request with its model declarations.</param>
    /// <param name="hub">The hub being emitted.</param>
    /// <param name="names">The type names allocated to this hub artifact.</param>
    /// <exception cref="InvalidOperationException">A generated type or member name is already occupied.</exception>
    private static void EnsureDeclarationNames(GenerationContext context, IrSignalRHub hub, params string[] names)
    {
        foreach (var name in names)
        {
            if (context.DocumentContext.KnownTypeNames.Contains(name))
                throw new InvalidOperationException($"SignalR type '{name}' conflicts with a model or enum.");
        }

        var occupied = new HashSet<string>(names, StringComparer.Ordinal) { "HubPath", "ParameterType" };
        if (names.Length == 1)
        {
            foreach (var notification in hub.Events)
                occupied.Add("On" + notification.Name);
        }

        foreach (var call in hub.Methods)
        {
            if (!occupied.Add(call.Name))
                throw new InvalidOperationException($"SignalR method '{hub.Name}.{call.Name}' conflicts with a generated member.");
        }
    }

    /// <summary>Emits a typed server-to-client interface and a hub base for the service to implement.</summary>
    /// <param name="hub">The owner contract.</param>
    /// <param name="ns">The configured namespace.</param>
    /// <param name="name">The generated hub base type name.</param>
    /// <returns>The C# source file.</returns>
    private string RenderHub(IrSignalRHub hub, string ns, string name)
    {
        var clientEvents = "I" + hub.Name + "ClientEvents";
        var text = FileHeader(ns, client: false);
        text.AppendLine("/// <summary>Events the hub may send to connected clients.</summary>");
        text.AppendLine($"public interface {clientEvents}");
        text.AppendLine("{");
        foreach (var notification in hub.Events)
        {
            text.AppendLine($"    /// <summary>Delivers the {notification.Name} notification.</summary>");
            AppendParameterDocs(text, notification);
            text.AppendLine("    /// <returns>The asynchronous send.</returns>");
            text.AppendLine($"    global::System.Threading.Tasks.Task {notification.Name}({Parameters(notification)});");
        }

        text.AppendLine("}").AppendLine();
        text.AppendLine("/// <summary>Defines the callable surface of the hub for an application-owned implementation.</summary>");
        text.AppendLine($"public abstract class {name} : global::Microsoft.AspNetCore.SignalR.Hub<{clientEvents}>");
        text.AppendLine("{");
        AppendPath(text, hub.Path);
        foreach (var call in hub.Methods)
        {
            text.AppendLine();
            text.AppendLine($"    /// <summary>Handles the {call.Name} invocation.</summary>");
            AppendParameterDocs(text, call);
            text.AppendLine("    /// <returns>The completion of the hub method.</returns>");
            text.AppendLine($"    public abstract {TaskType(call.Result)} {call.Name}({Parameters(call)});");
        }

        return text.AppendLine("}").ToString();
    }

    /// <summary>Emits a typed client for an already-configured .NET SignalR connection.</summary>
    /// <param name="hub">The owner contract.</param>
    /// <param name="ns">The configured namespace.</param>
    /// <param name="name">The generated client type name.</param>
    /// <returns>The C# source file.</returns>
    private string RenderClient(IrSignalRHub hub, string ns, string name)
    {
        var text = FileHeader(ns, client: true);
        text.AppendLine("/// <summary>Invokes hub methods and subscribes to notifications using an existing connection.</summary>");
        text.AppendLine("/// <param name=\"connection\">The configured SignalR connection.</param>");
        text.AppendLine($"public sealed class {name}(global::Microsoft.AspNetCore.SignalR.Client.HubConnection connection)");
        text.AppendLine("{");
        text.AppendLine("    /// <summary>The connection supplied by the application's composition.</summary>");
        text.AppendLine("    private readonly global::Microsoft.AspNetCore.SignalR.Client.HubConnection _connection = connection;");
        text.AppendLine();
        AppendPath(text, hub.Path);
        foreach (var call in hub.Methods)
        {
            var cancellation = "cancellationToken";
            while (call.Parameters.Any(parameter => parameter.Name == cancellation))
                cancellation += "_";

            var parameters = Parameters(call);
            var generic = call.Result is null ? "" : $"<{RenderType(call.Result)}>";
            var arguments = string.Join(", ", call.Parameters.Select(parameter => "@" + parameter.Name));
            text.AppendLine();
            text.AppendLine($"    /// <summary>Invokes {call.Name} on the connected hub.</summary>");
            AppendParameterDocs(text, call);
            text.AppendLine($"    /// <param name=\"{cancellation}\">Cancellation of the local invocation wait.</param>");
            text.AppendLine("    /// <returns>The asynchronous hub result.</returns>");
            text.AppendLine($"    public {TaskType(call.Result)} {call.Name}({(parameters.Length == 0 ? "" : parameters + ", ")}global::System.Threading.CancellationToken {cancellation} = default) =>");
            text.AppendLine($"        this._connection.InvokeCoreAsync{generic}(\"{call.Name}\", [{arguments}], {cancellation});");
        }

        foreach (var notification in hub.Events)
        {
            var parameterTypes = notification.Parameters.Select(parameter => RenderType(parameter.Type)).ToArray();
            if (parameterTypes.Length > 16)
                throw new InvalidOperationException($"SignalR event '{hub.Name}.{notification.Name}' exceeds the C# delegate arity of 16.");

            var callbackTypes = parameterTypes.Append("global::System.Threading.Tasks.Task");
            var typeArguments = string.Join(", ", parameterTypes.Select(type => $"ParameterType<{type}>()"));
            var arguments = string.Join(", ", parameterTypes.Select((type, index) => $"({type})arguments[{index}]!"));
            text.AppendLine();
            text.AppendLine($"    /// <summary>Subscribes to the {notification.Name} notification.</summary>");
            text.AppendLine("    /// <param name=\"handler\">The callback invoked with the notification arguments.</param>");
            text.AppendLine("    /// <returns>A subscription handle which removes this handler when disposed.</returns>");
            text.AppendLine($"    public global::System.IDisposable On{notification.Name}(global::System.Func<{string.Join(", ", callbackTypes)}> handler) =>");
            text.AppendLine($"        this._connection.On(\"{notification.Name}\", [{typeArguments}], arguments => handler({arguments}));");
        }

        if (hub.Events.Any(notification => notification.Parameters.Count > 0))
        {
            text.AppendLine();
            text.AppendLine("    /// <summary>Gets the SignalR binder type including nullable value types.</summary>");
            text.AppendLine("    /// <typeparam name=\"T\">The declared argument type.</typeparam>");
            text.AppendLine("    /// <returns>The type used to bind the incoming argument.</returns>");
            text.AppendLine("    private static global::System.Type ParameterType<T>() => typeof(T);");
        }

        return text.AppendLine("}").ToString();
    }

    /// <summary>Creates the shared C# file prologue.</summary>
    /// <param name="ns">The configured namespace.</param>
    /// <param name="client">Whether to import SignalR client extensions.</param>
    /// <returns>A builder ready for declarations.</returns>
    private static StringBuilder FileHeader(string ns, bool client)
    {
        var text = new StringBuilder().AppendLine("// <auto-generated />").AppendLine("#nullable enable");
        text.AppendLine("using System;");
        text.AppendLine("using System.Collections.Generic;");
        if (client)
            text.AppendLine("using Microsoft.AspNetCore.SignalR.Client;");

        return text.AppendLine().AppendLine($"namespace {ns};").AppendLine();
    }

    /// <summary>Writes the path shared by the connection factory and endpoint mapping.</summary>
    /// <param name="text">The current generated type.</param>
    /// <param name="path">The declared hub path.</param>
    private static void AppendPath(StringBuilder text, string path)
    {
        text.AppendLine("    /// <summary>The path declared for this hub.</summary>");
        text.AppendLine($"    public const string HubPath = {JsonSerializer.Serialize(path)};");
    }

    /// <summary>Documents the ordered arguments of the generated method.</summary>
    /// <param name="text">The generated file receiving the documentation.</param>
    /// <param name="call">The method or notification.</param>
    private static void AppendParameterDocs(StringBuilder text, IrSignalRCall call)
    {
        foreach (var parameter in call.Parameters)
            text.AppendLine($"    /// <param name=\"{parameter.Name}\">The {parameter.Name} argument.</param>");
    }

    /// <summary>Renders a hub method's ordered C# parameters.</summary>
    /// <param name="call">The source operation.</param>
    /// <returns>The parameter list without enclosing parentheses.</returns>
    private string Parameters(IrSignalRCall call) =>
        string.Join(", ", call.Parameters.Select(parameter => $"{RenderType(parameter.Type)} @{parameter.Name}"));

    /// <summary>Renders the asynchronous return type of a hub invocation.</summary>
    /// <param name="result">The declared result, or null for a resultless operation.</param>
    /// <returns>The non-generic or typed task.</returns>
    private string TaskType(IrType? result) => result is null
        ? "global::System.Threading.Tasks.Task"
        : $"global::System.Threading.Tasks.Task<{RenderType(result)}>";

    /// <summary>Renders a normalized type using the existing C# type mapper.</summary>
    /// <param name="type">The wire type.</param>
    /// <returns>A C# type expression.</returns>
    private string RenderType(IrType type) => _types.MapType(type).Accept(renderer);
}
