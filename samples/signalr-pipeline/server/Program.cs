using System.Diagnostics;
using System.Threading.Channels;
using Generated.Support;
using Microsoft.AspNetCore.SignalR.Client;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Logging.ClearProviders();
builder.Services.AddSignalR();
await using var application = builder.Build();
application.MapHub<SupportHub>(SupportHubBase.HubPath);
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await application.StartAsync(deadline.Token);

var address = application.Urls.Single();
var url = new Uri(new Uri(address), SupportSignalRClient.HubPath);
await using var connection = new HubConnectionBuilder().WithUrl(url).Build();
var client = new SupportSignalRClient(connection);
var notices = Channel.CreateUnbounded<(string Sequence, int? Attempt)>();
var removedHandlerCalls = 0;
var removedSubscription = client.OnSupportChanged((_, _) =>
{
    Interlocked.Increment(ref removedHandlerCalls);
    return Task.CompletedTask;
});
using var subscription = client.OnSupportChanged((message, attempt) =>
{
    notices.Writer.TryWrite((message.VisibleSequence, attempt));
    return Task.CompletedTask;
});
var heartbeat = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
using var signal = client.OnWalletChanged(() =>
{
    heartbeat.TrySetResult();
    return Task.CompletedTask;
});

await connection.StartAsync(deadline.Token);
var ticket = Guid.NewGuid();
var first = await client.SubscribeTicket(ticket, deadline.Token);
var firstNotice = await notices.Reader.ReadAsync(deadline.Token);
if (!first.Succeeded || firstNotice != ("9007199254740993", null) || Volatile.Read(ref removedHandlerCalls) != 1)
    throw new InvalidOperationException("The .NET client did not receive the typed result and both event arguments.");

removedSubscription.Dispose();
var second = await client.SubscribeTicket(ticket, deadline.Token);
var secondNotice = await notices.Reader.ReadAsync(deadline.Token);
if (!second.Succeeded || secondNotice != firstNotice || Volatile.Read(ref removedHandlerCalls) != 1)
    throw new InvalidOperationException("Disposing one .NET subscription changed the other handler's delivery.");

await client.Heartbeat(deadline.Token);
await heartbeat.Task.WaitAsync(deadline.Token);
Console.WriteLine("SignalR .NET exchange passed.");

var probe = new ProcessStartInfo("node") { UseShellExecute = false };
probe.ArgumentList.Add(Path.GetFullPath("../../../../client/dist/probe.js", AppContext.BaseDirectory));
probe.ArgumentList.Add(address);
using var child = Process.Start(probe) ?? throw new InvalidOperationException("Node did not start.");
try
{
    await child.WaitForExitAsync(deadline.Token);
}
finally
{
    if (!child.HasExited)
        child.Kill(entireProcessTree: true);
}
if (child.ExitCode != 0)
    throw new InvalidOperationException("The TypeScript exchange failed.");

/// <summary>Implements the generated contract for the local round-trip probe.</summary>
public sealed class SupportHub : SupportHubBase
{
    /// <inheritdoc />
    public override async Task<RealtimeResult> SubscribeTicket(Guid ticketId)
    {
        await Clients.Caller.SupportChanged(new SupportChanged { VisibleSequence = "9007199254740993" }, null);
        return new RealtimeResult { Succeeded = ticketId != Guid.Empty };
    }

    /// <inheritdoc />
    public override Task Heartbeat() => Clients.Caller.WalletChanged();
}
