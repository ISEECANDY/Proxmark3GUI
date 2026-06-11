namespace Proxmark3GUI.Models;

/// <summary>
/// Supported scripting / automation languages for proxmark3.
/// </summary>
public enum ScriptLanguage
{
    /// <summary>Lua script (.lua).</summary>
    Lua,

    /// <summary>Proxmark3 command file (.cmd).</summary>
    Cmd,

    /// <summary>Python helper script (.py).</summary>
    Python,
}

/// <summary>
/// Represents a script file that can be loaded and executed by the proxmark3.
/// </summary>
public class ScriptEntry
{
    /// <summary>File name (without directory path).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Full path to the script file on disk.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Language / type of the script.</summary>
    public ScriptLanguage Language { get; set; }

    /// <summary>Current text content of the script.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>True when the in-memory content differs from the on-disk version.</summary>
    public bool IsModified { get; set; }
}
