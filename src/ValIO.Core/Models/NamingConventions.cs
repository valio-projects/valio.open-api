namespace ValIO.Core.Models;

/// <summary>
/// Настройки конвенций именования
/// </summary>
public class NamingConventions
{
    public NamingConvention Models { get; set; } = NamingConvention.PascalCase;
    public NamingConvention Properties { get; set; } = NamingConvention.CamelCase;
    public NamingConvention Methods { get; set; } = NamingConvention.CamelCase;
    public NamingConvention Enums { get; set; } = NamingConvention.PascalCase;
}

