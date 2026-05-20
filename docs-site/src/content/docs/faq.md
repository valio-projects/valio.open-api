---
title: FAQ
description: Frequently asked questions.
---

## Why one CLI binary, not a NuGet generator?

NuGet source generators run at compile time inside the consumer's
project. ValIO is OpenAPI-first — most consumers want the same generator
output reproduced across multiple repos and languages, not just inside a
single C# project. A standalone tool fits that better and keeps the
generator independent of the consumer's runtime.

## Can I use ValIO offline?

Yes. The CLI and Desktop apps run fully offline once you have a local
copy of the OpenAPI schema file. The Web UI fetches schemas over the
network only when you paste a URL — uploaded files are processed
locally.

## How big can my schema be?

The Web UI caps uploads and remote fetches at 10 MB. The CLI has no
hard cap; OpenAPI parsing happens in memory.

## Does ValIO write back to my repo?

ValIO never overwrites anything outside the directory you point
`--output` at. The Web and Desktop apps never write to the server's
disk for client work — Web streams a zip back to the browser, Desktop
writes to a path you pick through a native file dialog.

## Is the generated code commercially usable?

The generated code carries no license clauses from ValIO; it adopts
whatever license you choose for your project. ValIO itself is
open-source — see `LICENSE` in the repo root.
