namespace Proxmark3GUI.Models;

/// <summary>
/// Holds device information reported by the proxmark3 hardware.
/// </summary>
public class Pm3DeviceInfo
{
    public string FirmwareVersion { get; set; } = string.Empty;
    public string HardwareVersion { get; set; } = string.Empty;
    public string Pm3Uid { get; set; } = string.Empty;
    public string FpgaVersion { get; set; } = string.Empty;
    public string FlashSize { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string BootloaderVersion { get; set; } = string.Empty;

    // ── Voltage / frequency readings ──────────────────────────────────────

    public string BatteryVoltage { get; set; } = string.Empty;
    public string UsbVoltage { get; set; } = string.Empty;
    public string FieldVoltage { get; set; } = string.Empty;
    public string HfPeakV { get; set; } = string.Empty;
    public string HfFreq { get; set; } = string.Empty;
    public string LfPeakV { get; set; } = string.Empty;
    public string LfFreq { get; set; } = string.Empty;

    // ── Connection state ──────────────────────────────────────────────────

    /// <summary>True when a device is currently connected and communicating.</summary>
    public bool IsConnected { get; set; }

    /// <summary>Current UART baud rate (e.g. 460800).</summary>
    public string UartSpeed { get; set; } = string.Empty;

    /// <summary>OS-level serial port path (e.g. /dev/ttyACM0 or COM3).</summary>
    public string DevicePath { get; set; } = string.Empty;
}
