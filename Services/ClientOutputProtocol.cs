using System.Text.RegularExpressions;

namespace Proxmark3GUI.Services;

/// <summary>
/// Defines the line-level protocol shared by the redirected Proxmark3 client and GUI host.
/// Kept independent of Process and WPF so the protocol can be tested on any .NET 8 runner.
/// </summary>
public static class ClientOutputProtocol
{
    public const string CompletionMarker = "__PM3GUI_COMMAND_COMPLETE__";

    private static readonly Regex AnsiRegex = new(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);

    /// <summary>
    /// Removes terminal escape sequences and trailing whitespace from a client output line.
    /// Returns false for null, empty, or whitespace-only lines.
    /// </summary>
    public static bool TryNormalizeLine(string? rawLine, out string line)
    {
        line = string.Empty;
        if (string.IsNullOrWhiteSpace(rawLine))
            return false;

        line = AnsiRegex.Replace(rawLine, string.Empty).TrimEnd();
        return !string.IsNullOrWhiteSpace(line);
    }

    public static bool IsCompletionMarker(string line) =>
        string.Equals(line.Trim(), CompletionMarker, StringComparison.Ordinal);
}
