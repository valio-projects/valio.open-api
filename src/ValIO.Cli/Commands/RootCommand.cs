using System.CommandLine;

namespace ValIO.Cli.Commands;

/// <summary>
/// Root CLI command — registers all sub-commands and the global --lang option.
/// </summary>
public sealed class RootCommand : System.CommandLine.RootCommand
{
    public RootCommand(IServiceProvider serviceProvider)
    {
        Description = "ValIO — code generator from OpenAPI schemas";

        // --lang is consumed by Program.Main BEFORE DI is built (so localization picks it up).
        // We still declare it here so System.CommandLine doesn't flag it as unknown.
        var langOption = new Option<string?>("--lang")
        {
            Description = "UI culture for messages (en, ru, de, zh-CN). Also reads VALIO_LANG env var."
        };
        Options.Add(langOption);

        Add(new GenerateCommand(serviceProvider));
        Add(new ValidateCommand(serviceProvider));
    }
}
