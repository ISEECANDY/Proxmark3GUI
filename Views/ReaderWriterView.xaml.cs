using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Proxmark3GUI.Services;
using Proxmark3GUI.ViewModels;

namespace Proxmark3GUI.Views;

public partial class ReaderWriterView : UserControl
{
    public ObservableCollection<OperationEntry> History { get; } = new();

    public ReaderWriterView()
    {
        InitializeComponent();
        LvHistory.ItemsSource = History;
        UpdateWriteVisibility();
        this.Loaded += ReaderWriterView_Loaded;
    }

    private void ReaderWriterView_Loaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to real-time block read/write updates
        if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
        {
            vm.CommandOutputReceived += (s, output) =>
            {
                if (output.CommandType == "readBlock" || output.CommandType == "writeBlock")
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        if (TxtResult != null)
                        {
                            TxtResult.Text += output.Line + Environment.NewLine;
                        }
                    });
                }
            };
        }
    }

    private void CmbOperation_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateWriteVisibility();
    }

    private void UpdateWriteVisibility()
    {
        var op = (CmbOperation.SelectedItem as ComboBoxItem)?.Content as string ?? "Read Block";
        var isWrite = op.StartsWith("Write");
        TxtData.IsEnabled = isWrite;
        TxtData.Opacity = isWrite ? 1.0 : 0.5;
    }

    private void TxtBlockNum_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }

    private void CmbKeySource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbKeySource.SelectedItem is ComboBoxItem item && item.Content is string key)
        {
            if (key != "Manual entry")
            {
                TxtKey.Text = key;
            }
        }
    }

    private async void BtnExecute_Click(object sender, RoutedEventArgs e)
    {
        var op = (CmbOperation.SelectedItem as ComboBoxItem)?.Content as string ?? "Read Block";
        var block = TxtBlockNum.Text.Trim();
        var keyType = (CmbKeyType.SelectedItem as ComboBoxItem)?.Content as string ?? "Use A key";
        var key = TxtKey.Text.Trim();

        if (key.Length != 12)
        {
            MessageBox.Show("Key must be exactly 12 hex characters (6 bytes).",
                "Invalid Key", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (op.Contains("Block") && (!int.TryParse(block, out int bn) || bn < 0 || bn > 255))
        {
            MessageBox.Show("Block number must be 0-255.", "Invalid Block", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (op.StartsWith("Write"))
        {
            var data = TxtData.Text.Trim();
            if (data.Length != 32)
            {
                MessageBox.Show("Data must be exactly 32 hex characters (16 bytes).",
                    "Invalid Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var sector = (CmbSector.SelectedItem as ComboBoxItem)?.Content as string ?? "Sector 0";
        TxtStatus.Text = $"Executing {op} on {sector}...";
        BtnExecute.IsEnabled = false;

        try
        {
            if (Application.Current.MainWindow?.DataContext is not MainViewModel vm)
                return;

            string result = string.Empty;
            string command = string.Empty;

            if (op.Contains("Read"))
            {
                // Read command: hf mf rdbl <block> <keytype> <key>
                var keyTypeChar = keyType.Contains("A") ? "A" : "B";
                command = $"hf mf rdbl {block} {keyTypeChar} {key}";
                result = await vm.ExecuteCommandAsync(command);
            }
            else if (op.StartsWith("Write"))
            {
                // Write command: hf mf wrbl <block> <keytype> <key> <data>
                var keyTypeChar = keyType.Contains("A") ? "A" : "B";
                var data = TxtData.Text.Trim();
                command = $"hf mf wrbl {block} {keyTypeChar} {key} {data}";
                result = await vm.ExecuteCommandAsync(command);
            }

            TxtResult.Text = $"[{op}] {sector} (Block {block})\nKey Type: {keyType}\nKey: {key}\nCommand: {command}\nResult: {(string.IsNullOrWhiteSpace(result) ? "Success" : result.Substring(0, Math.Min(200, result.Length)))}";
            TxtStatus.Text = $"{op} completed successfully.";

            History.Add(new OperationEntry
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                Operation = op,
                Target = sector,
                Key = key,
                Status = "OK",
                Result = string.IsNullOrWhiteSpace(result) ? "Success" : result
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Operation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            TxtStatus.Text = $"{op} failed: {ex.Message}";
            TxtResult.Text = ex.Message;

            History.Add(new OperationEntry
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                Operation = op,
                Target = sector,
                Key = key,
                Status = "Failed",
                Result = ex.Message
            });
        }
        finally
        {
            BtnExecute.IsEnabled = true;
        }
    }

    private async void BtnFillZeros_Click(object sender, RoutedEventArgs e)
    {
        var sector = (CmbSector.SelectedItem as ComboBoxItem)?.Content as string ?? "Sector 0";
        var result = MessageBox.Show(
            $"Fill {sector} with zeros?\n\nThis will erase all data in the sector.",
            "Confirm Fill", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            BtnFillZeros.IsEnabled = false;
            try
            {
                if (Application.Current.MainWindow?.DataContext is not MainViewModel vm)
                    return;

                var key = TxtKey.Text.Trim();
                var keyType = (CmbKeyType.SelectedItem as ComboBoxItem)?.Content as string ?? "Use A key";

                TxtStatus.Text = $"Filling {sector} with zeros...";
                
                // Get sector number from text
                var sectorNum = sector.Replace("Sector ", "");
                if (!int.TryParse(sectorNum, out int sn))
                    sn = 0;

                var keyTypeChar = keyType.Contains("A") ? "A" : "B";
                var zeroData = new string('0', 32); // 16 bytes = 32 hex chars

                // Write zeros to all blocks in sector
                for (int block = sn * 4; block < (sn + 1) * 4; block++)
                {
                    var writeCommand = $"hf mf wrbl {block} {keyTypeChar} {key} {zeroData}";
                    await vm.ExecuteCommandAsync(writeCommand);
                }

                TxtResult.Text = $"[Write Sector] {sector}\nKey Type: {keyType}\nKey: {key}\nSector filled with zeros successfully.";
                TxtStatus.Text = $"{sector} filled with zeros.";

                History.Add(new OperationEntry
                {
                    Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                    Operation = "Fill Zeros",
                    Target = sector,
                    Key = key,
                    Status = "OK",
                    Result = "Sector filled with zeros"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fill operation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = $"Fill failed: {ex.Message}";
                TxtResult.Text = ex.Message;
            }
            finally
            {
                BtnFillZeros.IsEnabled = true;
            }
        }
    }
}

public class OperationEntry
{
    public string Timestamp { get; set; } = "";
    public string Operation { get; set; } = "";
    public string Target { get; set; } = "";
    public string Key { get; set; } = "";
    public string Status { get; set; } = "";
    public string Result { get; set; } = "";
}
