namespace Proxmark3GUI.Models;

/// <summary>
/// Direction of a single trace frame.
/// </summary>
public enum TraceDirection
{
    /// <summary>Frame sent from the reader / proxmark3 to the tag.</summary>
    ReaderToTag,

    /// <summary>Frame received from the tag back to the reader / proxmark3.</summary>
    TagToReader,
}

/// <summary>
/// A single protocol frame captured during a sniffer / trace session.
/// </summary>
public class TraceFrame
{
    /// <summary>Sequential frame number in the trace.</summary>
    public int FrameNumber { get; set; }

    /// <summary>Timestamp relative to the start of the trace (seconds).</summary>
    public double Timestamp { get; set; }

    /// <summary>Direction of the frame.</summary>
    public TraceDirection Direction { get; set; }

    /// <summary>Raw hex data of the frame.</summary>
    public string RawData { get; set; } = string.Empty;

    /// <summary>Parity bit representation (if applicable).</summary>
    public string Parity { get; set; } = string.Empty;

    /// <summary>True when the frame's CRC / checksum validated correctly.</summary>
    public bool CrcValid { get; set; }

    /// <summary>Protocol name (e.g. "ISO14443A", "ISO15693", "EM410x").</summary>
    public string Protocol { get; set; } = string.Empty;

    /// <summary>Human-readable annotation decoded from the frame data.</summary>
    public string Annotation { get; set; } = string.Empty;
}
