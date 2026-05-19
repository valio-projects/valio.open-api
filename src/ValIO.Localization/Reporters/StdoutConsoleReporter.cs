namespace ValIO.Localization.Reporters;

/// <summary>Default reporter used by CLI: plain stdout/stderr with a level prefix.</summary>
public sealed class StdoutConsoleReporter : IConsoleReporter
{
    public void Info(string localizedMessage) => Console.Out.WriteLine(localizedMessage);
    public void Warn(string localizedMessage) => Console.Out.WriteLine($"warn: {localizedMessage}");
    public void Error(string localizedMessage) => Console.Error.WriteLine($"error: {localizedMessage}");
}
