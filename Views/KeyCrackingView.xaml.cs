using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Proxmark3GUI.ViewModels;

namespace Proxmark3GUI.Views;

public partial class KeyCrackingView : UserControl
{
    private const int SectorCount = 16;
    private bool _isAttackRunning;

    public KeyCrackingView()
    {
        InitializeComponent();
        BuildSectorMap();
        this.Loaded += KeyCrackingView_Loaded;
    }

    private void KeyCrackingView_Loaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to real-time key cracking and recovery output
        if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
        {
            vm.CommandOutputReceived += (s, output) =>
            {
                if (output.CommandType == "keyCracking" || output.CommandType == "keyRecovery" || output.CommandType == "keyDictionary")
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        LogMessage(output.Line);
                        ParseSectorStatus(output.Line);
                        ParseAttackProgress(output.Line);
                    });
                }
            };
        }
    }

    private void ParseSectorStatus(string line)
    {
        // Parse lines like "[+] Sector 0: Key found: FFFFFFFFFFFF"
        var lower = line.ToLowerInvariant();
        if (lower.Contains("sector") && int.TryParse(ExtractNumber(line), out int sector))
        {
            if (lower.Contains("found") || lower.Contains("ok") || lower.Contains("key found"))
                SetSectorStatus(sector, "found");
            else if (lower.Contains("not found") || lower.Contains("notfound") || lower.Contains("fail") || lower.Contains("error"))
                SetSectorStatus(sector, "failed");
        }
    }

    private string ExtractNumber(string text)
    {
        return new string(text.Where(char.IsDigit).ToArray());
    }

    private void CmbAttackType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || TxtAttackHint == null)
            return;

        var selected = CmbAttackType.SelectedItem as ComboBoxItem;
        if (selected != null)
        {
            TxtAttackHint.Text = selected.Content.ToString() switch
            {
                "AutoPwn" => "Automatically detects and applies the best attack strategy",
                "Darkside" => "Fast darkside attack, requires card vulnerability",
                "Nested" => "Requires at least one known key for a sector",
                "Hardnested" => "Slow but works on most cards, brute-force based",
                "StaticNested" => "Static nested attack, works on certain card types",
                "Brute Force" => "Try all possible keys, very slow but always works",
                _ => "Select an attack type"
            };
        }
    }

    private void BuildSectorMap()
    {
        SectorMapGrid.ColumnDefinitions.Clear();
        SectorMapGrid.Children.Clear();

        for (int i = 0; i < SectorCount; i++)
        {
            SectorMapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4),
                Margin = new Thickness(2),
                Cursor = Cursors.Hand,
                Tag = i
            };

            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            var label = new TextBlock
            {
                Text = $"S{i}",
                Foreground = (Brush)FindResource("TextPrimary"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var status = new TextBlock
            {
                Text = "--",
                Foreground = (Brush)FindResource("TextDim"),
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            sp.Children.Add(label);
            sp.Children.Add(status);
            border.Child = sp;

            border.MouseLeftButtonUp += (s, e) => { };

            Grid.SetColumn(border, i);
            SectorMapGrid.Children.Add(border);
        }
    }

    public void SetSectorStatus(int sector, string state)
    {
        if (sector < 0 || sector >= SectorCount) return;

        foreach (var child in SectorMapGrid.Children)
        {
            if (child is Border border && (int)border.Tag == sector)
            {
                var brush = state switch
                {
                    "found" => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
                    "notfound" => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                    _ => new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B))
                };

                border.Background = brush;

                if (border.Child is StackPanel sp && sp.Children.Count >= 2 && sp.Children[1] is TextBlock statusText)
                {
                    statusText.Text = state switch
                    {
                        "found" => "✓",
                        "notfound" => "✗",
                        _ => "--"
                    };
                }

                break;
            }
        }

        var processed = CountProcessedSectors();
        UpdateProgress(processed * 100.0 / SectorCount);
    }

    private void ParseAttackProgress(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        if (TryParsePercentage(line, out var percent))
        {
            UpdateProgress(percent);
            return;
        }

        if (TryParseFraction(line, out var current, out var total) && total > 0)
        {
            UpdateProgress(current * 100.0 / total);
            return;
        }
    }

    private bool TryParsePercentage(string line, out double percent)
    {
        var match = Regex.Match(line, @"(\d{1,3}(?:\.\d+)?)\s*%");
        if (match.Success && double.TryParse(match.Groups[1].Value, out percent))
        {
            percent = Math.Clamp(percent, 0, 100);
            return true;
        }

        percent = 0;
        return false;
    }

    private bool TryParseFraction(string line, out int current, out int total)
    {
        var match = Regex.Match(line, @"\b(\d+)\s*/\s*(\d+)\b");
        if (match.Success && int.TryParse(match.Groups[1].Value, out current) && int.TryParse(match.Groups[2].Value, out total))
            return true;

        current = 0;
        total = 0;
        return false;
    }

    private void UpdateProgress(double percent)
    {
        var value = Math.Clamp(percent, 0, 100);
        ProgressBar.Value = value;
        TxtProgress.Text = $"{value:F0}%";
    }

    private int CountProcessedSectors()
    {
        var count = 0;
        foreach (var child in SectorMapGrid.Children)
        {
            if (child is Border border && border.Child is StackPanel sp && sp.Children.Count >= 2 && sp.Children[1] is TextBlock statusText)
            {
                if (statusText.Text == "✓" || statusText.Text == "✗")
                    count++;
            }
        }

        return count;
    }

    private void BtnBrowseDict_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Dictionary files (*.dic;*.txt)|*.dic;*.txt|All files (*.*)|*.*",
            Title = "Select Key Dictionary"
        };

        if (dialog.ShowDialog() == true)
        {
            TxtDictPath.Text = dialog.FileName;
        }
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (_isAttackRunning)
        {
            LogMessage("[WARN] Attack already running.");
            return;
        }

        var attackType = (CmbAttackType.SelectedItem as ComboBoxItem)?.Content as string;
        if (string.IsNullOrWhiteSpace(attackType))
        {
            LogMessage($"[ERROR] No attack type selected.");
            return;
        }

        _isAttackRunning = true;
        BtnStart.IsEnabled = false;
        BtnStop.IsEnabled = true;
        BuildSectorMap();
        ProgressBar.Value = 0;
        TxtProgress.Text = "0%";

        try
        {
            if (Application.Current.MainWindow?.DataContext is not ViewModels.MainViewModel vm)
                return;

            // Connect if needed
            var service = Services.Pm3ConnectionService.Instance;
            if (!service.IsConnected)
                await service.ConnectAsync();

            // Build attack command based on selected type
            var attackCmd = attackType switch
            {
                "AutoPwn" => "hf mf autopwn",
                "Darkside" => "hf mf darkside",
                "Nested" => "hf mf nested 0 A FFFFFFFFFFFF",
                "Hardnested" => "hf mf hardnested 0 A FFFFFFFFFFFF",
                "StaticNested" => "hf mf staticnested",
                "Brute Force" => $"hf mf chk *1 ? d {TxtDictPath.Text}",
                _ => "hf mf autopwn"
            };

            LogMessage($"[{DateTime.Now:HH:mm:ss}] Executing: {attackCmd}");
            var result = await vm.ExecuteCommandAsync(attackCmd);
            LogMessage($"[{DateTime.Now:HH:mm:ss}] Attack complete.");
            UpdateProgress(100);
        }
        catch (Exception ex)
        {
            LogMessage($"[ERROR] {ex.Message}");
        }
        finally
        {
            _isAttackRunning = false;
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
        }
    }

    private async void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _isAttackRunning = false;
        BtnStart.IsEnabled = true;
        BtnStop.IsEnabled = false;
        LogMessage($"[{DateTime.Now:HH:mm:ss}] Stopping attack...");

        try
        {
            var service = Services.Pm3ConnectionService.Instance;
            if (service.IsConnected)
                await service.SendCommandAsync("hw ping"); // Send ping to interrupt
        }
        catch { }

        LogMessage($"[{DateTime.Now:HH:mm:ss}] Attack stopped by user.");
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        TxtLog.Clear();
        TxtDictPath.Clear();
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        TxtLog.Clear();
    }

    private void LogMessage(string message)
    {
        TxtLog.AppendText(message + Environment.NewLine);
        TxtLog.ScrollToEnd();
    }
}
