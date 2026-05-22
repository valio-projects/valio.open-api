---
title: Exit codes
description: Every value the ValIO CLI returns to the shell.
---

The ValIO CLI returns one of two values today:

| Code | Name               | Meaning                                                                                            |
|------|--------------------|----------------------------------------------------------------------------------------------------|
| `0`  | Success            | All selected projects parsed, generated, and wrote successfully.                                   |
| `1`  | Failure            | At least one of: parse error, missing required CLI args, configuration error, generator pipeline threw, output write failed. |

The `1` bucket today is intentionally coarse — every failure path in
`GenerateCommand` and `ValidateCommand` logs a specific error message
and then `return 1`. CI scripts should rely on the log output for
diagnosis and on the exit code only for *did it pass / did it fail*.

## Common failures (all currently `1`)

| Symptom                                                         | What happened                                          |
|-----------------------------------------------------------------|--------------------------------------------------------|
| `--schema, --output, and --language are required when not using --config` | One of those three was missing from a non-`-c` invocation. |
| `Configuration file contains no projects`                       | The `projects` object was empty.                       |
| `Project(s) not found: X. Available: …`                         | `-p X` didn't match a key under `projects`.            |
| `Failed to parse OpenAPI schema: …`                             | OpenAPI parser rejected the document.                  |
| `No generator found for language: <id>`                         | The language id wasn't registered (typo / unknown).    |
| `Invalid options for '<id>': …`                                 | Options descriptor returned validation errors.         |
| `Generator pipeline failed: …`                                  | A step threw — see the log for the inner exception.    |

## Multi-project runs

When `-c` selects more than one project, ValIO runs them sequentially:

- Each failure is logged.
- The run *continues* with the next project.
- The CLI exits `1` if any project failed, `0` only if every one of
  them succeeded.

This way, one bad project doesn't mask problems in later ones — you
see every error in a single log.

## Future expansion

If/when the codes are split out per category, the table above will be
expanded with `2` (parse error), `3` (config error), `4` (generator
error), etc., while keeping `0`/`1` semantically compatible. Until
that lands, treat any non-zero exit as "look at the log".
