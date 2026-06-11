using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Proxmark3GUI.ViewModels;

namespace Proxmark3GUI.Views;

public partial class ProtocolAnalyzerView : UserControl
{
    private readonly Dictionary<string, int> _counters = new();
    private int _totalFrames;
    private int _totalBytes;
    private int _totalErrors;
    private int _crcFailures;

    // Color mapping for each protocol
    private static readonly Dictionary<string, string> ProtocolColors = new()
    {
        { "ISO14443A", "#3b82f6" },
        { "MIFARE Classic", "#7c3aed" },
        { "MIFARE Ultralight", "#22c55e" },
        { "iCLASS", "#06b6d4" },
        { "FeliCa", "#ef4444" },
        { "ISO15693", "#f97316" },
        { "EMV", "#eab308" },
        { "ISO14443B", "#a855f7" }
    };

    // Mapping from protocol name to counter TextBlock x:Name
    private static readonly Dictionary<string, string> ProtocolTextBoxes = new()
    {
        { "ISO14443A", "TxtCount14443A" },
        { "MIFARE Classic", "TxtCountMfClassic" },
        { "MIFARE Ultralight", "TxtCountMfUltralight" },
        { "iCLASS", "TxtCountIclass" },
        { "FeliCa", "TxtCountFelica" },
        { "ISO15693", "TxtCount15693" },
        { "EMV", "TxtCountEmv" },
        { "ISO14443B", "TxtCount14443B" }
    };

    public ProtocolAnalyzerView()
    {
        InitializeComponent();

        // Initialize all counters
        foreach (var protocol in ProtocolColors.Keys)
        {
            _counters[protocol] = 0;
        }

        this.Loaded += ProtocolAnalyzerView_Loaded;
    }

    private void ProtocolAnalyzerView_Loaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to real-time protocol data from searches
        if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
        {
            vm.CommandOutputReceived += (s, output) =>
            {
                if (output.CommandType == "searchHF" || output.CommandType == "searchLF")
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        ParseProtocolFromOutput(output.Line);
                    });
                }
            };
        }
    }

    private void ParseProtocolFromOutput(string line)
    {
        // Extract protocol type from output line
        var protocol = line switch
        {
            var l when l.Contains("14443A", StringComparison.OrdinalIgnoreCase) => "ISO14443A",
            var l when l.Contains("MIFARE Classic", StringComparison.OrdinalIgnoreCase) => "MIFARE Classic",
            var l when l.Contains("Ultralight", StringComparison.OrdinalIgnoreCase) => "MIFARE Ultralight",
            var l when l.Contains("iCLASS", StringComparison.OrdinalIgnoreCase) => "iCLASS",
            var l when l.Contains("FeliCa", StringComparison.OrdinalIgnoreCase) => "FeliCa",
            var l when l.Contains("15693", StringComparison.OrdinalIgnoreCase) => "ISO15693",
            var l when l.Contains("EMV", StringComparison.OrdinalIgnoreCase) => "EMV",
            var l when l.Contains("14443B", StringComparison.OrdinalIgnoreCase) => "ISO14443B",
            _ => null
        };

        if (!string.IsNullOrEmpty(protocol))
        {
            IncrementCounter(protocol);
        }
    }

    private void BtnResetCounters_Click(object sender, RoutedEventArgs e)
    {
        foreach (var key in _counters.Keys.ToList())
            _counters[key] = 0;

        _totalFrames = 0;
        _totalBytes = 0;
        _totalErrors = 0;
        _crcFailures = 0;

        RefreshDisplay();
    }

    private void CmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbFilter == null)
            return;

        var selectedProtocol = (CmbFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
        
        // Highlight only the selected protocol card
        foreach (var protocol in ProtocolColors.Keys)
        {
            if (ProtocolTextBoxes.TryGetValue(protocol, out var tbName))
            {
                if (FindName(tbName) is TextBlock tb)
                {
                    if (selectedProtocol == "All Protocols" || selectedProtocol == protocol)
                    {
                        tb.Opacity = 1.0;
                    }
                    else
                    {
                        tb.Opacity = 0.3;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Increments a specific protocol counter.
    /// </summary>
    public void IncrementCounter(string protocol, int count = 1)
    {
        if (_counters.ContainsKey(protocol))
            _counters[protocol] += count;
        _totalFrames += count;
        RefreshDisplay();
    }

    /// <summary>
    /// Updates all display elements with current counter values.
    /// </summary>
    private void RefreshDisplay()
    {
        // Update individual protocol cards
        foreach (var kvp in ProtocolTextBoxes)
        {
            if (FindName(kvp.Value) is TextBlock tb)
            {
                tb.Text = _counters.GetValueOrDefault(kvp.Key, 0).ToString();
            }
        }

        // Update live statistics
        TxtTotalFrames.Text = _totalFrames.ToString();
        TxtBytesTotal.Text = FormatBytes(_totalBytes);
        TxtErrors.Text = _totalErrors.ToString();
        TxtCrcFailures.Text = _crcFailures.ToString();

        // Update distribution chart
        BuildDistributionChart();
    }

    /// <summary>
    /// Builds a horizontal bar chart showing protocol distribution.
    /// </summary>
    private void BuildDistributionChart()
    {
        DistributionBars.Items.Clear();

        var maxCount = _counters.Values.Max();
        if (maxCount == 0) return;

        foreach (var kvp in _counters.OrderByDescending(k => k.Value))
        {
            var percent = (double)kvp.Value / maxCount;
            var color = ProtocolColors.GetValueOrDefault(kvp.Key, "#64748b");

            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            var labelPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };

            var nameText = new TextBlock
            {
                Text = kvp.Key,
                Foreground = (Brush)FindResource("TextSecondary"),
                FontSize = 11,
                Width = 140
            };

            var countText = new TextBlock
            {
                Text = kvp.Value.ToString(),
                Foreground = (Brush)FindResource("TextPrimary"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(8, 0, 0, 0)
            };

            var pctText = new TextBlock
            {
                Text = $"({(double)kvp.Value / Math.Max(_totalFrames, 1) * 100:F1}%)",
                Foreground = (Brush)FindResource("TextDim"),
                FontSize = 10,
                Margin = new Thickness(4, 0, 0, 0)
            };

            labelPanel.Children.Add(nameText);
            labelPanel.Children.Add(countText);
            labelPanel.Children.Add(pctText);

            var barBg = new Border
            {
                Background = (Brush)FindResource("BgDark"),
                CornerRadius = new CornerRadius(4),
                Height = 16
            };

            var barFill = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = double.NaN // auto
            };

            barFill.SetBinding(WidthProperty, new System.Windows.Data.Binding("ActualWidth")
            {
                Source = barBg,
                Converter = new PercentConverter(percent),
                Mode = System.Windows.Data.BindingMode.OneWay
            });

            barBg.Child = barFill;

            panel.Children.Add(labelPanel);
            panel.Children.Add(barBg);

            DistributionBars.Items.Add(panel);
        }
    }

    private static string FormatBytes(int bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1048576 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / 1048576.0:F1} MB"
        };
    }

    /// <summary>
    /// Updates error and CRC failure counts.
    /// </summary>
    public void UpdateErrors(int errors, int crcFailures)
    {
        _totalErrors = errors;
        _crcFailures = crcFailures;
        TxtErrors.Text = errors.ToString();
        TxtCrcFailures.Text = crcFailures.ToString();
    }

    /// <summary>
    /// Updates the total bytes transmitted count.
    /// </summary>
    public void UpdateBytesTransmitted(int bytes)
    {
        _totalBytes = bytes;
        TxtBytesTotal.Text = FormatBytes(bytes);
    }
}

/// <summary>
/// Converts a percentage (0-1) to a proportional width.
/// </summary>
internal class PercentConverter : System.Windows.Data.IValueConverter
{
    private readonly double _percent;

    public PercentConverter(double percent) => _percent = percent;

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is double width && width > 0)
            return width * _percent;
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}
