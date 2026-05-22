---
title: MSW handlers
description: Mock Service Worker handler generation for the TypeScript generator.
sidebar:
  order: 10
---

## Overview

When `mocks` is set to `msw`, the TypeScript generator emits a single
`mocks/handlers.ts` file that contains a typed
[`http.{verb}`](https://mswjs.io/docs/api/http) handler per OpenAPI
operation. Each handler returns an `HttpResponse.json(...)` body using the
example value(s) from the schema (or `{}` when no example is provided).

## When to choose

- You write component tests with [Vitest](https://vitest.dev) /
  [Jest](https://jestjs.io) and want network calls to resolve deterministically.
- You build Storybook stories that need realistic data without a backend.
- You run your frontend locally before the backend is ready.

## How to enable

```json
{
  "language": "typescript",
  "options": {
    "transports": ["fetch"],
    "mocks":      "msw"
  }
}
```

You can use either the `mocks` or `mockLibrary` key — both are normalised by
`TypeScriptOptionsDescriptor`.

## Generated snippet

```ts
// mocks/handlers.ts
import { http, HttpResponse } from "msw";

export const handlers = [
  http.get("/pets", () =>
    HttpResponse.json([
      { id: 1, name: "Sam", tag: "cat" },
      { id: 2, name: "Daisy", tag: "dog" },
    ]),
  ),

  http.post("/pets", async ({ request }) => {
    const body = (await request.json()) as { name: string; tag?: string };
    return HttpResponse.json({ id: 3, ...body }, { status: 201 });
  }),

  http.get("/pets/:id", ({ params }) =>
    HttpResponse.json({ id: Number(params.id), name: "Sam", tag: "cat" }),
  ),
];
```

## Wiring it up

Browser (Storybook, dev server):

```ts
// mocks/browser.ts
import { setupWorker } from "msw/browser";
import { handlers } from "./handlers";
export const worker = setupWorker(...handlers);
```

Node (Vitest, Jest):

```ts
// mocks/node.ts
import { setupServer } from "msw/node";
import { handlers } from "./handlers";
export const server = setupServer(...handlers);
```

## Notes

- Each handler honours the **path template** from the OpenAPI spec, so
  `/pets/{id}` becomes `/pets/:id` (MSW's path-to-regexp syntax).
- Operations with multiple documented responses (e.g. `200`, `404`) emit a
  single handler returning the `2xx` example by default; override individual
  handlers in tests with `server.use(...)` for failure paths.
- The generated file imports only `http` and `HttpResponse` from `msw` — no
  custom runtime is required.
