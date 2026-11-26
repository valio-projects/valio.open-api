# SignalR round-trip probe

The same OpenAPI fixture generates an ASP.NET Core hub, a .NET client and a TypeScript client. Run these commands from this directory with .NET 10 and Node.js installed.

```console
dotnet run --project ../../src/ValIO.Cli -- generate --config valio.config.json
npm --prefix client ci
node client/node_modules/typescript/bin/tsc --project client/tsconfig.json
dotnet run --project server
```

The probe starts Kestrel on a free loopback port. Each client calls a method with a UUID, checks its typed result, receives an event with an exact numeric string and a null argument, then removes one of two handlers and verifies that the other handler receives the next event. A resultless call triggers a notification without arguments.
