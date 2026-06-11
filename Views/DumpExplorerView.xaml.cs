using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Proxmark3GUI.Views;

public partial class DumpExplorerView : UserControl
{
    public ObservableCollection<HexRow> HexRows { get; } = new();

    private byte[] _data = Array.Empty<byte>();
    private bool _hexFormat = true;

    public DumpExplorerView()
    {
        InitializeComponent();
        HexItemsControl.ItemsSource = HexRows;
        LoadEmptyState();
    }

    private void LoadEmptyState()
    {
        HexRows.Clear();
        TxtStatusOffset.Text = "0x00000000";
        TxtStatusSize.Text = "0 bytes";
        TxtStatusSelection.Text = "None";
    }

    /// <summary>
    /// Loads raw byte data into the hex viewer.
    /// </summary>
    public void LoadData(byte[] data)
    {
        _data = data ?? Array.Empty<byte>();
        RenderHexView();
    }

    private void RenderHexView()
    {
        HexRows.Clear();
        var lines = (_data.Length + 15) / 16;

        for (int i = 0; i < lines; i++)
        {
            var offset = i * 16;
            var hexChars = new System.Text.StringBuilder(48);
            var asciiChars = new System.Text.StringBuilder(16);

            for (int j = 0; j < 16; j++)
            {
                if (offset + j < _data.Length)
                {
                    hexChars.Append(_data[offset + j].ToString(_hexFormat ? "X2" : "D3"));
                    asciiChars.Append(IsPrintable(_data[offset + j]) ? (char)_data[offset + j] : '.');

                    if (j < 15)
                        hexChars.Append(' ');
                }
                else
                {
                    hexChars.Append(_hexFormat ? "   " : "    ");
                    asciiChars.Append(' ');
                }

                if (j == 7) hexChars.Append(' ');
            }

            HexRows.Add(new HexRow
            {
                Offset = _hexFormat ? $"0x{offset:X8}" : offset.ToString("D8"),
                HexData = hexChars.ToString(),
                AsciiData = asciiChars.ToString()
            });
        }

        TxtStatusSize.Text = $"{_data.Length} bytes ({_data.Length / 1024.0:F1} KB)";
    }

    private static bool IsPrintable(byte b) => b >= 0x20 && b <= 0x7E;

    private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Dump files (*.bin;*.dump;*.mfd;*.eml)|*.bin;*.dump;*.mfd;*.eml|All files (*.*)|*.*",
            Title = "Open Dump File"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _data = File.ReadAllBytes(dialog.FileName);
                RenderHexView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Binary (*.bin)|*.bin|EMF (*.eml)|*.eml|MFD (*.mfd)|*.mfd|All files (*.*)|*.*",
            Title = "Save Dump As"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllBytes(dialog.FileName, _data);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnGoToOffset_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Show GoToOffset dialog and scroll to the specified offset
        var input = new TextBox { Width = 200 };
        var dlg = new Window
        {
            Title = "Go To Offset",
            Width = 280,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (System.Windows.Media.Brush)FindResource("BgMedium")
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "Enter offset (hex or decimal):", Foreground = (System.Windows.Media.Brush)FindResource("TextPrimary"), Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(input);

        var btn = new Button { Content = "Go", Style = (Style)FindResource("AccentButton"), Padding = new Thickness(20, 8, 20, 8), Margin = new Thickness(0, 12, 0, 0) };
        btn.Click += (_, _) => dlg.Close();
        panel.Children.Add(btn);

        dlg.Content = panel;
        dlg.Owner = Window.GetWindow(this);
        dlg.ShowDialog();
    }

    private void CmbFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || CmbFormat == null)
            return;

        _hexFormat = CmbFormat.SelectedIndex != 1;
        RenderHexView();
    }
}

public class HexRow
{
    public string Offset { get; set; } = string.Empty;
    public string HexData { get; set; } = string.Empty;
    public string AsciiData { get; set; } = string.Empty;
}
