using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace ValIO.CLI;

/// <summary>
/// Корневая команда приложения
/// </summary>
public class RootCommand : System.CommandLine.RootCommand
{
    public RootCommand(IServiceProvider serviceProvider)
    {
        Description = "ValIO - Code generator from OpenAPI schemas";
        
        var generateCommand = new GenerateCommand(serviceProvider);
        var validateCommand = new ValidateCommand(serviceProvider);
        
        Add(generateCommand);
        Add(validateCommand);
    }
}

