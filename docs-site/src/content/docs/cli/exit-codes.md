---
title: Exit codes
description: Values returned to the shell.
---

The CLI returns one of two exit codes today:

| Code | Meaning                                                       |
|------|---------------------------------------------------------------|
| `0`  | Success — every selected project generated successfully.      |
| `1`  | Failure — at least one project failed; see log for specifics. |

For the full failure-mode breakdown, common error messages, and how
this maps to `generate` vs `validate`, see the canonical
[Reference / Exit codes](/reference/exit-codes/) page.
