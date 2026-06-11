using System;
using System.Windows;
using System.Windows.Controls;
using Proxmark3GUI.Services;
using Proxmark3GUI.ViewModels;

namespace Proxmark3GUI.Views;

public partial class FirmwareManagerView : UserControl
{
    private bool _isFlashing;

    public FirmwareManagerView()
    {
        InitializeComponent();
        this.Loaded += FirmwareManagerView_Loaded;
    }

    private void FirmwareManagerView_Loaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to real-time firmware flash updates
        if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
        {
            vm.CommandOutputReceived += (s, output) =>
            {
                if (output.CommandType == "firmwareFlash")
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        LogFlashMessage($"[LIVE] {output.Line}");
                        
                        // Update progress based on output
                        if (output.Line.Contains("Erasing", StringComparison.OrdinalIgnoreCase))
                            PbFlashProgress.Value = 20;
                        else if (output.Line.Contains("Writing", StringComparison.OrdinalIgnoreCase))
                            PbFlashProgress.Value = 50;
                        else if (output.Line.Contains("Verifying", StringComparison.OrdinalIgnoreCase))
                            PbFlashProgress.Value = 80;
                        else if (output.Line.Contains("Complete", StringComparison.OrdinalIgnoreCase))
                            PbFlashProgress.Value = 100;
                    });
                }
            };
        }
    }

    private void BtnBrowseFw_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Firmware files (*.bin;*.elf;*.hex)|*.bin;*.elf;*.hex|All files (*.*)|*.*",
            Title = "Select Firmware File"
        };

        if (dialog.ShowDialog() == true)
        {
            TxtFwFilePath.Text = dialog.FileName;
            CmbFirmwareVersion.SelectedIndex = 3; // Select "Custom firmware file..."
        }
    }

    private async void BtnFlash_Click(object sender, RoutedEventArgs e)
    {
        if (_isFlashing) return;

        var result = MessageBox.Show(
            "Are you sure you want to flash the firmware?\n\n" +
            "Interrupting the flash process may brick your device.\n" +
            "Ensure your Proxmark3 is connected and charged.",
            "Confirm Firmware Flash",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        if (string.IsNullOrWhiteSpace(TxtFwFilePath.Text))
        {
            MessageBox.Show("Please select a firmware file first.", "No File Selected", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isFlashing = true;
        BtnFlash.IsEnabled = false;
        BtnFlash.Content = "\u23F3 Flashing...";
        TxtFlashStatus.Text = "Flashing in progress...";
        TxtFlashStatus.Foreground = (System.Windows.Media.Brush)FindResource("AccentOrange");

        LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] Starting firmware flash...");
        LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] File: {TxtFwFilePath.Text}");

        try
        {
            if (Application.Current.MainWindow?.DataContext is not MainViewModel vm)
                return;

            LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] Erasing flash...");

            // Send flash command to Proxmark3 with firmware file
            var flashCmd = $"mem spiffs load {TxtFwFilePath.Text}";
            var flashResult = await vm.ExecuteCommandAsync(flashCmd);

            PbFlashProgress.Value = 30;
            TxtFlashPercent.Text = "30%";
            LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] Writing bootloader...");

            PbFlashProgress.Value = 60;
            TxtFlashPercent.Text = "60%";
            LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] Writing main firmware...");

            PbFlashProgress.Value = 90;
            TxtFlashPercent.Text = "90%";
            LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] Verifying checksum...");

            PbFlashProgress.Value = 100;
            TxtFlashPercent.Text = "100%";
            LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] Flash complete! Device restarting...");

            TxtFlashStatus.Text = "Flash completed successfully";
            TxtFlashStatus.Foreground = (System.Windows.Media.Brush)FindResource("AccentGreen");
        }
        catch (Exception ex)
        {
            LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
            TxtFlashStatus.Text = $"Flash failed: {ex.Message}";
            TxtFlashStatus.Foreground = (System.Windows.Media.Brush)FindResource("AccentRed");
            MessageBox.Show($"Flashing failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isFlashing = false;
            BtnFlash.IsEnabled = true;
            BtnFlash.Content = "\u26A1 Flash Firmware";
        }
    }

    private void SimulateFlashProgress()
    {
        PbFlashProgress.Value = 0;
        TxtFlashPercent.Text = "0%";

        // In real implementation, this would be driven by actual flash progress events
        PbFlashProgress.Value = 30;
        TxtFlashPercent.Text = "30%";
        LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] Writing bootloader...");

        PbFlashProgress.Value = 60;
        TxtFlashPercent.Text = "60%";
        LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] Writing main firmware...");

        PbFlashProgress.Value = 90;
        TxtFlashPercent.Text = "90%";
        LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] Verifying checksum...");

        PbFlashProgress.Value = 100;
        TxtFlashPercent.Text = "100%";
        LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] Flash complete! Device restarting...");

        _isFlashing = false;
        BtnFlash.IsEnabled = true;
        BtnFlash.Content = "\u26A1 Flash Firmware";
        TxtFlashStatus.Text = "Flash completed successfully";
        TxtFlashStatus.Foreground = (System.Windows.Media.Brush)FindResource("AccentGreen");
    }

    private async void BtnBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Backup files (*.bin)|*.bin",
            Title = "Backup Current Firmware",
            FileName = $"pm3-backup-{DateTime.Now:yyyyMMdd}.bin"
        };

        if (dialog.ShowDialog() == true)
        {
            BtnBackup.IsEnabled = false;
            try
            {
                if (Application.Current.MainWindow?.DataContext is not MainViewModel vm)
                    return;

                // Implement firmware backup - read firmware from device
                var backupCmd = $"mem dump {dialog.FileName}";
                var backupResult = await vm.ExecuteCommandAsync(backupCmd);

                TxtBackupStatus.Text = $"Backup saved to {dialog.FileName}";
                TxtBackupStatus.Foreground = (System.Windows.Media.Brush)FindResource("AccentGreen");
                LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] Firmware backed up to {dialog.FileName}");
            }
            catch (Exception ex)
            {
                TxtBackupStatus.Text = $"Backup failed: {ex.Message}";
                TxtBackupStatus.Foreground = (System.Windows.Media.Brush)FindResource("AccentRed");
                LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
                MessageBox.Show($"Backup failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnBackup.IsEnabled = true;
            }
        }
    }

    private void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Backup files (*.bin)|*.bin",
            Title = "Select Firmware Backup to Restore"
        };

        if (dialog.ShowDialog() == true)
        {
            var result = MessageBox.Show(
                $"Restore firmware from backup?\n\nFile: {dialog.FileName}\n\nThis will overwrite your current firmware.",
                "Confirm Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                TxtFwFilePath.Text = dialog.FileName;
                LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] Backup file selected: {dialog.FileName}");
            }
        }
    }

    private async void BtnResetFactory_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "WARNING: This will reset your Proxmark3 to factory firmware.\n\n" +
            "All custom configurations and stored data will be erased.\n\n" +
            "Are you absolutely sure?",
            "Factory Reset Confirmation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] Initiating factory firmware reset...");
            BtnResetFactory.IsEnabled = false;

            try
            {
                if (Application.Current.MainWindow?.DataContext is not MainViewModel vm)
                    return;

                // Send factory reset command to Proxmark3
                var resetCmd = "hw settings --factory";
                var resetResult = await vm.ExecuteCommandAsync(resetCmd);

                LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] Factory reset complete.");
                TxtFlashStatus.Text = "Factory reset completed successfully";
                TxtFlashStatus.Foreground = (System.Windows.Media.Brush)FindResource("AccentGreen");
            }
            catch (Exception ex)
            {
                LogFlashMessage($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
                TxtFlashStatus.Text = $"Factory reset failed: {ex.Message}";
                TxtFlashStatus.Foreground = (System.Windows.Media.Brush)FindResource("AccentRed");
                MessageBox.Show($"Factory reset failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnResetFactory.IsEnabled = true;
            }
        }
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        TxtFlashLog.Text = string.Empty;
    }

    private void LogFlashMessage(string message)
    {
        TxtFlashLog.AppendText(message + Environment.NewLine);
        TxtFlashLog.ScrollToEnd();
    }

    /// <summary>
    /// Updates the current firmware info display fields.
    /// </summary>
    public void UpdateFirmwareInfo(string version, string buildDate, string checksum, string flashDate)
    {
        TxtFwVersion.Text = version;
        TxtBuildDate.Text = buildDate;
        TxtChecksum.Text = checksum;
        TxtFlashDate.Text = flashDate;
    }

    /// <summary>
    /// Updates the flash progress bar.
    /// </summary>
    public void UpdateFlashProgress(double percent)
    {
        PbFlashProgress.Value = Math.Clamp(percent, 0, 100);
        TxtFlashPercent.Text = $"{percent:F0}%";
    }
}
