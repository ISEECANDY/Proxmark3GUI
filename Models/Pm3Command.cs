namespace Proxmark3GUI.Models;

/// <summary>
/// Represents a single proxmark3 client command with its metadata.
/// </summary>
public class Pm3Command
{
    /// <summary>The command name as typed in the proxmark3 console (e.g. "hf mf chk").</summary>
    public string CommandName { get; set; } = string.Empty;

    /// <summary>Human-readable description of what the command does.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Usage synopsis string shown in help output.</summary>
    public string Usage { get; set; } = string.Empty;

    /// <summary>Optional notes / warnings associated with the command.</summary>
    public List<string> Notes { get; set; } = [];

    /// <summary>Accepted options / flags for the command.</summary>
    public List<string> Options { get; set; } = [];

    /// <summary>True when the command works without a connected device.</summary>
    public bool IsOffline { get; set; }

    /// <summary>Category grouping (e.g. "HF", "LF", "Info", "Utilities").</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Free-form tags for search / filtering.</summary>
    public List<string> Tags { get; set; } = [];
}
