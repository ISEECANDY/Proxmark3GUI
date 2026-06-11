namespace Proxmark3GUI.Models;

/// <summary>
/// Represents a single recovered or known key for an MIFARE sector.
/// </summary>
public class KeyEntry
{
    /// <summary>Key value as a 12-character hex string (6 bytes).</summary>
    public string KeyHex { get; set; } = string.Empty;

    /// <summary>Key type — "A" or "B".</summary>
    public string KeyType { get; set; } = string.Empty;

    /// <summary>Zero-based sector number the key applies to.</summary>
    public int Sector { get; set; }

    /// <summary>Source that discovered the key (e.g. "nested", "mfck", "dictionary").</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Date and time when the key was found.</summary>
    public DateTime FoundDate { get; set; } = DateTime.UtcNow;
}
