namespace ValIO.Localization.Reporters;

/// <summary>
/// Front-agnostic reporter for user-facing messages. The CLI binds it to stdout; the Web/Desktop
/// fronts bind it to their own surface (UI snackbar, log view, etc).
/// </summary>
public interface IConsoleReporter
{
    void Info(string localizedMessage);
    void Warn(string localizedMessage);
    void Error(string localizedMessage);
}
