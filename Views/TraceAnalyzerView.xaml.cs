using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Proxmark3GUI.Models;
using Proxmark3GUI.ViewModels;

namespace Proxmark3GUI.Views;

public partial class TraceAnalyzerView : UserControl
{
    public ObservableCollection<TraceFrameDisplay> Frames { get; } = new();

    public TraceAnalyzerView()
    {
        InitializeComponent();
        LvTraceFrames.ItemsSource = Frames;
        this.Loaded += TraceAnalyzerView_Loaded;
    }

    private void TraceAnalyzerView_Loaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to real-time trace frame data
        if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
        {
            vm.CommandOutputReceived += (s, output) =>
            {
                if (output.CommandType == "searchHF" || output.CommandType == "searchLF")
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        ParseTraceDataLine(output.Line);
                    });
                }
            };
        }
    }

    private void ParseTraceDataLine(string line)
    {
        // Parse trace frame from command output
        if (line.Contains("Tracing") || line.Contains("Frame") || line.Contains("RX") || line.Contains("TX"))
        {
            var frameNum = Frames.Count + 1;
            var direction = line.Contains("RX") ? TraceDirection.TagToReader : TraceDirection.ReaderToTag;
            var protocol = "ISO14443A"; // Default, can be extracted from line
            var rawData = ExtractHexData(line);

            if (!string.IsNullOrEmpty(rawData))
            {
                Frames.Add(new TraceFrameDisplay
                {
                    FrameNumber = frameNum,
                    Timestamp = Frames.Count > 0 ? Frames.Last().Timestamp + 0.05 : 0.0,
                    Direction = direction,
                    Protocol = protocol,
                    RawData = rawData,
                    Parity = string.Empty,
                    CrcValid = true,
                    Annotation = "Live captured"
                });
                UpdateSummary();
            }
        }
    }

    private string ExtractHexData(string line)
    {
        // Extract hex data from line using regex
        var regex = new System.Text.RegularExpressions.Regex(@"[0-9A-Fa-f]{2,}");
        var match = regex.Match(line);
        return match.Success ? match.Value : string.Empty;
    }

    private void LvTraceFrames_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LvTraceFrames.SelectedItem is TraceFrameDisplay frame)
        {
            DetailPanel.Visibility = Visibility.Visible;
            DetailFrameNum.Text = frame.FrameNumber.ToString();
            DetailRawHex.Text = frame.RawData;
            DetailParity.Text = frame.Parity;
            DetailBytes.Text = FormatByteBreakdown(frame.RawData);
            DetailProtocol.Text = frame.Protocol;
            DetailAnnotation.Text = frame.Annotation;
        }
        else
        {
            DetailPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        Frames.Clear();
        DetailPanel.Visibility = Visibility.Collapsed;
        UpdateSummary();
    }

    private void BtnExportPcap_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PCAP files (*.pcap)|*.pcap",
            Title = "Export Trace as PCAP"
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                using (var stream = System.IO.File.Create(dialog.FileName))
                {
                    // Write PCAP global header
                    var header = new byte[] { 0xD4, 0xC3, 0xB2, 0xA1 }; // Magic number
                    stream.Write(header, 0, 4);
                    stream.Write(System.BitConverter.GetBytes(2), 0, 4); // Version major
                    stream.Write(System.BitConverter.GetBytes(4), 0, 4); // Version minor
                    stream.Write(System.BitConverter.GetBytes(0), 0, 4); // Timezone
                    stream.Write(System.BitConverter.GetBytes(0), 0, 4); // Timestamp accuracy
                    stream.Write(System.BitConverter.GetBytes(65535), 0, 4); // Snaplen
                    stream.Write(System.BitConverter.GetBytes(251), 0, 4); // Data link type (LINKTYPE_BLUETOOTH)

                    // Write each frame
                    foreach (var frame in Frames)
                    {
                        var data = System.Text.RegularExpressions.Regex.Replace(frame.RawData, "[^0-9A-Fa-f]", "");
                        var bytes = new byte[data.Length / 2];
                        for (int i = 0; i < bytes.Length; i++)
                        {
                            bytes[i] = System.Convert.ToByte(data.Substring(i * 2, 2), 16);
                        }

                        stream.Write(System.BitConverter.GetBytes((int)(frame.Timestamp)), 0, 4);
                        stream.Write(System.BitConverter.GetBytes(0), 0, 4);
                        stream.Write(System.BitConverter.GetBytes(bytes.Length), 0, 4);
                        stream.Write(System.BitConverter.GetBytes(bytes.Length), 0, 4);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }

                MessageBox.Show($"Exported {Frames.Count} frames to {dialog.FileName}", "Export PCAP",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error exporting PCAP: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            Title = "Export Trace as CSV"
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var lines = new System.Collections.Generic.List<string>();
                lines.Add("FrameNum,Timestamp(ms),Direction,Protocol,RawData,CRC,Annotation");

                foreach (var frame in Frames)
                {
                    var direction = frame.Direction == TraceDirection.ReaderToTag ? "→" : "←";
                    var crc = frame.CrcValid ? "OK" : "FAIL";
                    lines.Add($"{frame.FrameNumber},{frame.Timestamp:F2},{direction},{frame.Protocol},{frame.RawData},{crc},{frame.Annotation}");
                }

                System.IO.File.WriteAllLines(dialog.FileName, lines);
                MessageBox.Show($"Exported {Frames.Count} frames to {dialog.FileName}", "Export CSV",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error exporting CSV: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnZoomFit_Click(object sender, RoutedEventArgs e)
    {
        if (Frames.Count > 0)
        {
            LvTraceFrames.ScrollIntoView(Frames[0]);
            LvTraceFrames.ScrollIntoView(Frames[Frames.Count - 1]);
        }
    }

    private void CmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || CmbFilter == null)
            return;

        ApplyFilter();
    }

    private void ChkShowOnlyErrors_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || ChkShowOnlyErrors == null)
            return;

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (CmbFilter == null || ChkShowOnlyErrors == null || LvTraceFrames == null)
            return;

        var filter = (CmbFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
        var showOnlyErrors = ChkShowOnlyErrors.IsChecked == true;

        LvTraceFrames.ItemsSource = null;
        var filtered = Frames.Where(f =>
        {
            var protocolMatch = filter == "All" || f.Protocol.Contains(filter);
            var errorMatch = !showOnlyErrors || !f.CrcValid;
            return protocolMatch && errorMatch;
        }).ToList();

        LvTraceFrames.ItemsSource = filtered;
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        TxtFrameCount.Text = $"Frames: {Frames.Count}";
        if (Frames.Count > 0)
        {
            TxtTimeRange.Text = $"Time: {Frames.Min(f => f.Timestamp):F2} - {Frames.Max(f => f.Timestamp):F2} ms";
            var dist = Frames.GroupBy(f => f.Protocol)
                .Select(g => $"{g.Key}:{g.Count()}")
                .Aggregate((a, b) => $"{a}, {b}");
            TxtProtocolDist.Text = $"Protocols: {dist}";
        }
        else
        {
            TxtTimeRange.Text = "Time: 0.00 - 0.00 ms";
            TxtProtocolDist.Text = "Protocols: -";
        }
    }

    private static string FormatByteBreakdown(string hex)
    {
        var bytes = hex.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join("  ", bytes.Select(b => $"0x{b}"));
    }

    /// <summary>
    /// Adds a trace frame to the list.
    /// </summary>
    public void AddFrame(TraceFrame frame)
    {
        Frames.Add(new TraceFrameDisplay(frame));
        UpdateSummary();
    }

    /// <summary>
    /// Clears all frames and resets the view.
    /// </summary>
    public void ClearTrace()
    {
        Frames.Clear();
        DetailPanel.Visibility = Visibility.Collapsed;
        UpdateSummary();
    }
}

/// <summary>
/// Display wrapper for TraceFrame with UI-friendly properties.
/// </summary>
public class TraceFrameDisplay : TraceFrame
{
    public TraceFrameDisplay() { }

    public TraceFrameDisplay(TraceFrame frame)
    {
        FrameNumber = frame.FrameNumber;
        Timestamp = frame.Timestamp;
        Direction = frame.Direction;
        RawData = frame.RawData;
        Parity = frame.Parity;
        CrcValid = frame.CrcValid;
        Protocol = frame.Protocol;
        Annotation = frame.Annotation;
    }

    public string DirectionDisplay => Direction == TraceDirection.ReaderToTag ? "\u2192" : "\u2190";
    public string CrcStatus => CrcValid ? "\u2713" : "\u2717";
}
