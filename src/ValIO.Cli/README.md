# ValIO.Cli

The `valio` command-line tool — the primary frontend. Built on **System.CommandLine**, packaged as a
.NET global tool and (via `packaging/npm`) as an npm binary.

```bash
valio generate -s ./openapi.yaml -o ./out -l typescript --opt validation=zod4
```

It only parses arguments / config and calls `ValIO.Application`; all generation logic lives in the
engine and the generator plugins. Plugins are registered through
`ValIO.Plugins.Bootstrap.AddAllGeneratorPlugins`.
