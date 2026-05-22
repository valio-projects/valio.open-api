namespace ValIO.Cli.Init;

/// <summary>
/// Console-backed prompter. Reads from <see cref="Console.In"/>, writes to <see cref="Console.Out"/>.
/// Empty input accepts the offered default. ReadLine returning null (EOF) is also treated as "accept default".
/// </summary>
public sealed class ConsoleInitPrompter : IInitPrompter
{
    private readonly TextReader _reader;
    private readonly TextWriter _writer;

    public ConsoleInitPrompter() : this(Console.In, Console.Out) { }

    public ConsoleInitPrompter(TextReader reader, TextWriter writer)
    {
        _reader = reader;
        _writer = writer;
    }

    public string AskString(string question, string? defaultValue = null)
    {
        _writer.Write(defaultValue is not null ? $"? {question} ({defaultValue}): " : $"? {question}: ");
        var input = _reader.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? (defaultValue ?? string.Empty) : input;
    }

    public bool AskBool(string question, bool defaultValue)
    {
        var hint = defaultValue ? "Y/n" : "y/N";
        _writer.Write($"? {question} ({hint}): ");
        var input = _reader.ReadLine()?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(input)) return defaultValue;
        return input is "y" or "yes" or "true" or "1";
    }

    public int AskChoice(string question, IReadOnlyList<string> choices, int defaultIndex = 0)
    {
        if (choices.Count == 0) throw new ArgumentException("Choices must be non-empty", nameof(choices));
        if (defaultIndex < 0 || defaultIndex >= choices.Count) defaultIndex = 0;

        _writer.WriteLine($"? {question}");
        for (var i = 0; i < choices.Count; i++)
        {
            var marker = i == defaultIndex ? " [default]" : string.Empty;
            _writer.WriteLine($"  {i + 1}) {choices[i]}{marker}");
        }
        while (true)
        {
            _writer.Write($"Choose (1-{choices.Count}, default {defaultIndex + 1}): ");
            var input = _reader.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) return defaultIndex;
            if (int.TryParse(input, out var n) && n >= 1 && n <= choices.Count) return n - 1;
            _writer.WriteLine($"Invalid choice; enter a number between 1 and {choices.Count}.");
        }
    }

    public IReadOnlyList<int> AskMultiChoice(string question, IReadOnlyList<string> choices, IReadOnlyList<int>? defaultIndices = null)
    {
        if (choices.Count == 0) throw new ArgumentException("Choices must be non-empty", nameof(choices));

        _writer.WriteLine($"? {question}");
        for (var i = 0; i < choices.Count; i++)
        {
            var marker = defaultIndices?.Contains(i) == true ? " [default]" : string.Empty;
            _writer.WriteLine($"  {i + 1}) {choices[i]}{marker}");
        }

        var defaultHint = defaultIndices is null || defaultIndices.Count == 0
            ? "none"
            : string.Join(",", defaultIndices.Select(i => (i + 1).ToString()));

        while (true)
        {
            _writer.Write($"Choose (comma-separated 1-{choices.Count}, default {defaultHint}): ");
            var input = _reader.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) return defaultIndices ?? Array.Empty<int>();

            var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var result = new List<int>(parts.Length);
            var ok = true;
            foreach (var p in parts)
            {
                if (int.TryParse(p, out var n) && n >= 1 && n <= choices.Count)
                {
                    if (!result.Contains(n - 1)) result.Add(n - 1);
                }
                else
                {
                    ok = false;
                    break;
                }
            }
            if (ok) return result;
            _writer.WriteLine($"Invalid input; use comma-separated numbers between 1 and {choices.Count}.");
        }
    }

    public void WriteLine(string message) => _writer.WriteLine(message);
}
