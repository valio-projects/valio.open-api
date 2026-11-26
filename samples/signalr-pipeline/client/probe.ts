import { deepEqual, equal } from "node:assert/strict";
import { HubConnectionBuilder, HttpTransportType } from "@microsoft/signalr";
import { createSupportSignalRClient, supportHubPath } from "./generated";

async function main() {
    const url = new URL(supportHubPath, process.argv[2]).href;
    const connection = new HubConnectionBuilder().withUrl(url, { transport: HttpTransportType.LongPolling }).build();
    const client = createSupportSignalRClient(connection);
    const retained: [string, number | null][] = [];
    const removed: (number | null)[] = [];
    const unsubscribe = client.events.SupportChanged((_message, attempt) => removed.push(attempt));
    const unsubscribeRetained = client.events.SupportChanged((message, attempt) => retained.push([message.visibleSequence, attempt]));
    const heartbeat = new Promise<void>(resolve => { client.events.WalletChanged(resolve); });
    const ticket = "23d74627-be7d-408e-9bf5-77a21e9f6b28";
    try {
        await connection.start();
        deepEqual(await client.methods.SubscribeTicket(ticket), { succeeded: true });
        deepEqual(retained, [["9007199254740993", null]]);
        deepEqual(removed, [null]);

        unsubscribe();
        deepEqual(await client.methods.SubscribeTicket(ticket), { succeeded: true });
        await client.methods.Heartbeat();
        await heartbeat;
        equal(retained.length, 2);
        equal(removed.length, 1);
        console.log("SignalR TypeScript exchange passed.");
    } finally {
        unsubscribeRetained();
        await connection.stop();
    }
}

void main().catch(error => { console.error(error); process.exitCode = 1; });
