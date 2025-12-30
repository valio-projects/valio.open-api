using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace ValIO.CLI;

/// <summary>
/// Корневая команда приложения
/// </summary>
public class RootCommand : System.CommandLine.RootCommand
{
    public RootCommand(IServiceProvider serviceProvider)
        : base("ValIO - Code generator from OpenAPI schemas")
    {
        this.AddCommand(new GenerateCommand(serviceProvider));
        this.AddCommand(new ValidateCommand(serviceProvider));
    }
}

