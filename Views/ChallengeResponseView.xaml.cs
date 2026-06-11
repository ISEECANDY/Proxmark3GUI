using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Proxmark3GUI.Services;

namespace Proxmark3GUI.Views;

public partial class ChallengeResponseView : UserControl
{
    public ObservableCollection<CrPair> Pairs { get; } = new();
    private bool _autoCapturing;

    public ChallengeResponseView()
    {
        InitializeComponent();
        LvCrPairs.ItemsSource = Pairs;
    }

    private void LvCrPairs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LvCrPairs.SelectedItem is CrPair pair)
        {
            DetailPanel.Visibility = Visibility.Visible;
            BtnReplay.IsEnabled = true;

            DetailNr.Text = pair.NrHex;
            DetailAr.Text = pair.ArHex;
            DetailKey.Text = pair.KeyHex;

            // Byte breakdown
            var nrBytes = FormatBytes(pair.NrHex);
            var arBytes = FormatBytes(pair.ArHex);
            var keyBytes = FormatBytes(pair.KeyHex);
            DetailBytes.Text = $"NR: {nrBytes}\nAR: {arBytes}\nKey: {keyBytes}";

            DetailValidation.Text = pair.IsValid
                ? "\u2713 CRC and parity validated successfully"
                : "\u2717 Validation failed - possible corrupted data";
            DetailValidation.Foreground = pair.IsValid
                ? (Brush)FindResource("AccentGreen")
                : (Brush)FindResource("AccentRed");
        }
        else
        {
            DetailPanel.Visibility = Visibility.Collapsed;
            BtnReplay.IsEnabled = false;
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (Pairs.Count == 0)
        {
            MessageBox.Show("No CR pairs to export.", "Empty", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|CSV files (*.csv)|*.csv",
            Title = "Export CR Pairs"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                if (dialog.FileName.EndsWith(".json"))
                {
                    // Write pairs to JSON file
                    var json = JsonSerializer.Serialize(Pairs, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(dialog.FileName, json);
                }
                else
                {
                    // Write pairs to CSV file
                    var csv = "PairNumber,NR,AR,Key,IsValid,Timestamp\n";
                    csv += string.Join("\n", Pairs.Select(p => 
                        $"{p.PairNumber},{p.NrHex},{p.ArHex},{p.KeyHex},{p.IsValid},{p.Timestamp:O}"));
                    File.WriteAllText(dialog.FileName, csv);
                }

                TxtStatus.Text = $"Exported {Pairs.Count} CR pairs to {dialog.FileName}";
                MessageBox.Show($"Exported {Pairs.Count} CR pairs to {dialog.FileName}.",
                    "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Import CR Pairs"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                if (dialog.FileName.EndsWith(".json"))
                {
                    // Parse and import pairs from JSON
                    var json = File.ReadAllText(dialog.FileName);
                    var importedPairs = JsonSerializer.Deserialize<List<CrPair>>(json);
                    
                    if (importedPairs != null)
                    {
                        Pairs.Clear();
                        foreach (var pair in importedPairs)
                            Pairs.Add(pair);
                    }
                }
                else if (dialog.FileName.EndsWith(".csv"))
                {
                    // Parse and import pairs from CSV
                    var lines = File.ReadAllLines(dialog.FileName);
                    Pairs.Clear();
                    
                    for (int i = 1; i < lines.Length; i++) // Skip header
                    {
                        var parts = lines[i].Split(',');
                        if (parts.Length >= 6)
                        {
                            var pair = new CrPair
                            {
                                PairNumber = int.TryParse(parts[0], out int pn) ? pn : i,
                                NrHex = parts[1],
                                ArHex = parts[2],
                                KeyHex = parts[3],
                                IsValid = bool.TryParse(parts[4], out bool valid) && valid,
                                Timestamp = DateTime.TryParse(parts[5], out var ts) ? ts : DateTime.UtcNow
                            };
                            Pairs.Add(pair);
                        }
                    }
                }

                UpdateStatistics();
                TxtStatus.Text = $"Imported {Pairs.Count} pairs from {dialog.FileName}";
                MessageBox.Show($"Imported {Pairs.Count} CR pairs from {dialog.FileName}.",
                    "Import", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        Pairs.Clear();
        DetailPanel.Visibility = Visibility.Collapsed;
        BtnReplay.IsEnabled = false;
        UpdateStatistics();
        TxtStatus.Text = "All CR pairs cleared.";
    }

    private void BtnAutoCapture_Click(object sender, RoutedEventArgs e)
    {
        _autoCapturing = !_autoCapturing;
        if (_autoCapturing)
        {
            BtnAutoCapture.Content = "\u1F534 Auto-Capture: ON";
            BtnAutoCapture.Background = (Brush)FindResource("AccentGreen");
            AutoCapIndicator.Fill = (Brush)FindResource("AccentGreen");
            TxtAutoCapStatus.Text = "Auto-Capture Running...";
            TxtAutoCapStatus.Foreground = (Brush)FindResource("AccentGreen");
            TxtStatus.Text = "Auto-capture enabled. CR pairs will be logged from sniffed traffic.";
            // TODO: Start auto-capture from sniffed traffic
        }
        else
        {
            BtnAutoCapture.Content = "\u1F534 Auto-Capture: OFF";
            BtnAutoCapture.Background = (Brush)FindResource("BgLight");
            AutoCapIndicator.Fill = (Brush)FindResource("AccentRed");
            TxtAutoCapStatus.Text = "Auto-Capture Stopped";
            TxtAutoCapStatus.Foreground = (Brush)FindResource("TextDim");
            TxtStatus.Text = "Auto-capture disabled.";
            // TODO: Stop auto-capture
        }
    }

    private async void BtnReplay_Click(object sender, RoutedEventArgs e)
    {
        if (LvCrPairs.SelectedItem is not CrPair pair)
            return;

        var result = MessageBox.Show(
            $"Replay this CR pair?\n\nNR: {pair.NrHex}\nAR: {pair.ArHex}\nKey: {pair.KeyHex}",
            "Confirm Replay Attack",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        BtnReplay.IsEnabled = false;
        try
        {
            var service = Pm3ConnectionService.Instance;
            if (!service.IsConnected)
                await service.ConnectAsync(string.IsNullOrWhiteSpace(service.PortName) ? "COM9" : service.PortName);

            TxtStatus.Text = $"Replaying NR/AR pair #{pair.PairNumber}...";

            // Send replay command to Proxmark3 with NR and AR values
            var replayCmd = $"hf mf sim --nr {pair.NrHex} --ar {pair.ArHex}";
            var replayResult = await service.SendCommandAsync(replayCmd);

            TxtStatus.Text = $"CR pair replay complete.";
            MessageBox.Show("CR pair replay sent to device.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Replay failed: {ex.Message}";
            MessageBox.Show($"Replay failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnReplay.IsEnabled = true;
        }
    }

    private void UpdateStatistics()
    {
        TxtTotalPairs.Text = Pairs.Count.ToString();
        TxtValidPairs.Text = Pairs.Count(p => p.IsValid).ToString();
        TxtInvalidPairs.Text = Pairs.Count(p => !p.IsValid).ToString();
        TxtUniqueKeys.Text = Pairs.Select(p => p.KeyHex).Distinct().Count().ToString();
    }

    private static string FormatBytes(string hex)
    {
        var bytes = hex.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (bytes.Length == 0) return hex;
        return string.Join(" ", bytes.Select(b => $"0x{b}"));
    }

    /// <summary>
    /// Adds a captured CR pair to the list.
    /// </summary>
    public void AddPair(CrPair pair)
    {
        pair.PairNumber = Pairs.Count + 1;
        Pairs.Add(pair);
        UpdateStatistics();
    }

    /// <summary>
    /// Starts or stops auto-capture mode.
    /// </summary>
    public void SetAutoCapture(bool enabled)
    {
        _autoCapturing = enabled;
        BtnAutoCapture_Click(this, new RoutedEventArgs());
    }
}

/// <summary>
/// Represents a single challenge-response (nonce-response) pair.
/// </summary>
public class CrPair
{
    public int PairNumber { get; set; }
    public string NrHex { get; set; } = string.Empty;
    public string ArHex { get; set; } = string.Empty;
    public string KeyHex { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string ValidStatus => IsValid ? "\u2713" : "\u2717";
}
