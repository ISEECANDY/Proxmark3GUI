namespace Proxmark3GUI.Models;

/// <summary>
/// Represents a tag that has been scanned / detected by the proxmark3.
/// </summary>
public class TagEntry
{
    /// <summary>Unique identifier of the tag (hex string).</summary>
    public string Uid { get; set; } = string.Empty;

    /// <summary>Human-readable tag type (e.g. "MIFARE Classic 1K").</summary>
    public string TagType { get; set; } = string.Empty;

    /// <summary>Answer To Request Type A (hex string).</summary>
    public string Atqa { get; set; } = string.Empty;

    /// <summary>Select Acknowledge byte.</summary>
    public string Sak { get; set; } = string.Empty;

    /// <summary>Answer To Select data (hex string).</summary>
    public string Ats { get; set; } = string.Empty;

    /// <summary>Timestamp when the tag was first scanned.</summary>
    public DateTime ScanDate { get; set; } = DateTime.UtcNow;

    /// <summary>Total number of keys associated with this tag.</summary>
    public int KeyCount { get; set; }

    /// <summary>List of recovered / known keys (hex strings).</summary>
    public List<string> KeysFound { get; set; } = [];

    /// <summary>Free-form notes about the tag.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Path to a saved dump file on disk (if any).</summary>
    public string DumpFile { get; set; } = string.Empty;
}
