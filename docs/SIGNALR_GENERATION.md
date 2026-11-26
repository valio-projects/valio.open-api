# Generate ASP.NET Core SignalR contracts

Put the hub surface in an OpenAPI 3.1 document under the `x-valio-signalr` extension. Reference its
`components.schemas` for DTOs and enums. A SignalR-only document can use `"paths": {}`. An existing
OpenAPI document can describe its HTTP operations and hubs together.

```json
{
  "openapi": "3.1.0",
  "info": { "title": "Support", "version": "1.0.0" },
  "paths": {},
  "components": {
    "schemas": {
      "RealtimeResult": {
        "type": "object",
        "required": ["succeeded"],
        "properties": { "succeeded": { "type": "boolean" } }
      },
      "SupportChanged": {
        "type": "object",
        "required": ["visibleSequence"],
        "properties": { "visibleSequence": { "type": "string" } }
      }
    }
  },
  "x-valio-signalr": {
    "hubs": [
      {
        "name": "Support",
        "path": "/hubs/support",
        "methods": [
          {
            "name": "SubscribeTicket",
            "parameters": [{ "name": "ticketId", "schema": { "type": "string", "format": "uuid" } }],
            "result": { "$ref": "#/components/schemas/RealtimeResult" }
          }
        ],
        "events": [
          {
            "name": "SupportChanged",
            "parameters": [{ "name": "message", "schema": { "$ref": "#/components/schemas/SupportChanged" } }]
          },
          { "name": "WalletChanged" }
        ]
      }
    ]
  }
}
```

Use unique PascalCase alphanumeric hub names and method/event names. Use a literal rooted endpoint
path and preserve argument order. Omitting `result` declares a method that can be awaited without a
return value. Omitting `parameters` declares a zero-argument method or signal. Nullable arguments
are still positional and receive an explicit JSON null.

The TypeScript generator uses the same model and enum file settings as the HTTP generator. To preserve
values such as sequence numbers beyond JavaScript's safe integer range, declare them as JSON strings.
The JavaScript SignalR JSON protocol decodes numbers before a generated handler sees them. ValIO
rejects declared exact decimal and 64-bit integer numbers in a TypeScript hub signature, including
values inside referenced models.

## Generate

Example `valio.config.json` with one profile for the server, one for the .NET client and one for the browser.

```json
{
  "projects": {
    "supportServer": {
      "language": "CSharp",
      "schema": "support.json",
      "namespace": "Generated.Support",
      "outputPath": "Server/Generated",
      "generateClients": false,
      "options": { "generateSignalRHubs": true },
      "namingConvention": { "properties": "PascalCase" }
    },
    "supportDotNetClient": {
      "language": "CSharp",
      "schema": "support.json",
      "namespace": "Generated.Support",
      "outputPath": "Client/Generated",
      "namingConvention": { "properties": "PascalCase" }
    },
    "supportBrowser": {
      "language": "TypeScript",
      "schema": "support.json",
      "outputPath": "web/generated"
    }
  }
}
```

```console
valio generate --config valio.config.json --projects supportServer supportDotNetClient supportBrowser
```

A models-only profile sets `generateClients: false` and leaves `generateSignalRHubs` at its default
`false`. The .NET client needs `Microsoft.AspNetCore.SignalR.Client`. The browser SDK uses the
types from `@microsoft/signalr` and accepts the application's existing connection.

## Use the generated hub and clients

The C# server profile generates `SupportHubBase : Hub<ISupportClientEvents>`. Implement the hub
methods on a concrete class and apply the service's access policy when mapping it.

```csharp
app.MapHub<SupportHub>(SupportHubBase.HubPath).RequireAuthorization("SupportRead");
```

In the application-owned implementation, `Clients.Caller.SupportChanged(message)` is a typed send.
The .NET SDK's `SupportSignalRClient` accepts a `HubConnection` and exposes the typed
`SubscribeTicket` call and the disposable `OnSupportChanged` registration.

```typescript
import { createSupportSignalRClient } from "./generated";

const support = createSupportSignalRClient(connection);
const unsubscribe = support.events.SupportChanged(message => refresh(message));
await support.methods.SubscribeTicket(ticketId);
unsubscribe();
```

The service owns connection setup, URL selection (including BFF routes), authorization, reconnect,
and application behavior. The extension describes unary calls and server notifications using the
standard SignalR JSON protocol. Streaming, client return values, and reflection-based export from
existing hub classes are outside this profile.

See the [two-hub contract fixture](../tests/Fixtures/OpenApi/SignalR/support.json) for a typed RPC,
a multi-argument event and independent Inbox, Wallets, and Payments signals.
