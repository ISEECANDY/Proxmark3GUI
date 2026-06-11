namespace Proxmark3GUI.Models;

/// <summary>
/// Holds the configuration for an emulated card, including UID and per-sector keys.
/// </summary>
public class EmulationProfile
{
    /// <summary>User-defined profile name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Card type string (e.g. "MIFARE Classic 1K", "MIFARE DESFire").</summary>
    public string CardType { get; set; } = string.Empty;

    /// <summary>UID to emulate (hex string, 4 or 7 bytes depending on type).</summary>
    public string Uid { get; set; } = string.Empty;

    /// <summary>ATQA bytes (hex string, 2 bytes).</summary>
    public string Atqa { get; set; } = string.Empty;

    /// <summary>SAK byte (hex string, 1 byte).</summary>
    public string Sak { get; set; } = string.Empty;

    /// <summary>Answer To Select data (hex string).</summary>
    public string Ats { get; set; } = string.Empty;

    /// <summary>
    /// Per-sector keys for emulation. Keys are keyed by "A0"–"A15" / "B0"–"B15"
    /// with hex key values as strings.
    /// </summary>
    public Dictionary<string, string> SectorKeys { get; set; } = new();

    /// <summary>
    /// Arbitrary custom data blocks (block index → hex data) for advanced emulation.
    /// </summary>
    public Dictionary<int, string> CustomData { get; set; } = new();
}
