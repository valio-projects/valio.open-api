namespace ValIO.Cli.Init;

/// <summary>
/// Abstraction over interactive prompting so InitCommand can be exercised with a scripted
/// prompter in tests. The real implementation is <see cref="ConsoleInitPrompter"/>.
/// </summary>
public interface IInitPrompter
{
    string AskString(string question, string? defaultValue = null);
    bool AskBool(string question, bool defaultValue);
    int AskChoice(string question, IReadOnlyList<string> choices, int defaultIndex = 0);
    IReadOnlyList<int> AskMultiChoice(string question, IReadOnlyList<string> choices, IReadOnlyList<int>? defaultIndices = null);
    void WriteLine(string message);
}
