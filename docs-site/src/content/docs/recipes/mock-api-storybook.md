---
title: MSW handlers in Storybook stories
description: Drive Storybook off the same OpenAPI as your real client.
---

The TypeScript plugin can emit [MSW](https://mswjs.io) handlers
alongside (or instead of) real HTTP clients. Wire them into Storybook
and your stories speak the same response shapes as production — no
hand-written fakes.

## Layout

```
my-app/
├── openapi.json
├── valio.config.json
├── .storybook/
│   ├── main.ts
│   └── preview.ts
└── src/
    ├── api/              ← Real fetch + Zod client
    └── mocks/            ← MSW handlers + factories from ValIO
```

## `valio.config.json`

```json
{
  "projects": {
    "api": {
      "language":   "typescript",
      "schema":     "./openapi.json",
      "outputPath": "./src/api",
      "options": {
        "transports":        ["fetch"],
        "validationLibrary": "zod",
        "queryWrapper":      "tanstack",
        "tanStackFramework": "react"
      }
    },
    "mocks": {
      "language":   "typescript",
      "schema":     "./openapi.json",
      "outputPath": "./src/mocks",
      "generateClients": false,
      "options": {
        "mockLibrary": "msw"
      }
    }
  }
}
```

Run both with `valio generate -c valio.config.json`.

## What ValIO emits

`src/mocks/` ends up with:

- `handlers.ts` — exports `handlers: HttpHandler[]` with one default
  handler per OpenAPI operation that responds with a Zod-schema-valid
  fake.
- `factories/*.ts` — one factory per model (e.g. `userFactory()`),
  useful for overriding individual fields in a single story.

## Storybook wiring

Install the MSW addon:

```bash
npm install --save-dev msw msw-storybook-addon
npx msw init public/
```

`.storybook/preview.ts`:

```ts
import { initialize, mswLoader } from 'msw-storybook-addon';
import { handlers } from '../src/mocks/handlers';

initialize();

export const loaders = [mswLoader];

export const parameters = {
  msw: {
    handlers,
  },
};
```

## Overriding in a single story

```ts
import { http, HttpResponse } from 'msw';
import { userFactory } from '../src/mocks/factories/user';
import { UserList } from './UserList';

export default { component: UserList };

export const Empty = {
  parameters: {
    msw: {
      handlers: [
        http.get('/api/users', () => HttpResponse.json([])),
      ],
    },
  },
};

export const ManyUsers = {
  parameters: {
    msw: {
      handlers: [
        http.get('/api/users', () =>
          HttpResponse.json([
            userFactory({ name: 'Ada' }),
            userFactory({ name: 'Linus' }),
          ]),
        ),
      ],
    },
  },
};
```

## Why it's nice

- Zod schemas validate the factory output, so a drift in the API
  shape fails the story load instead of producing a misleading
  screenshot.
- Re-running `valio generate -c valio.config.json` after an OpenAPI
  change updates both the real client *and* the mocks — they can never
  fall out of sync.
- The same `handlers` array can be reused in unit tests
  (`vitest` + `msw/node`) and E2E browser tests.
