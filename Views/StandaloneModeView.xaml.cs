using System.Windows;
using System.Windows.Controls;
using Proxmark3GUI.ViewModels;

namespace Proxmark3GUI.Views;

public partial class StandaloneModeView : UserControl
{
    // Mode descriptions
    private static readonly string[] ModeDescriptions =
    {
        "Detect and log HF RFID tags automatically",
        "Capture and record LF signal data continuously",
        "Emulate a saved card profile on the device",
        "Run automated MIFARE key cracking offline",
        "Universal key cracking attack (nested/darkside/brute)",
        "Perform relay attacks between reader and tag"
    };

    public StandaloneModeView()
    {
        InitializeComponent();
        this.Loaded += StandaloneModeView_Loaded;
    }

    private void StandaloneModeView_Loaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to real-time standalone mode configuration updates
        if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
        {
            vm.CommandOutputReceived += (s, output) =>
            {
                if (output.CommandType == "standaloneMode" || output.CommandType == "preferences")
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        // Log configuration changes
                        if (TxtLog != null)
                        {
                            TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {output.Line}\n");
                            TxtLog.ScrollToEnd();
                        }
                    });
                }
            };
        }
    }

    private void CmbMode_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (!IsLoaded || CmbMode == null)
            return;

        var idx = CmbMode.SelectedIndex;

        // Update description
        if (idx >= 0 && idx < ModeDescriptions.Length)
            TxtModeDescription.Text = ModeDescriptions[idx];

        // Show/hide configuration panels
        PanelHfReader.Visibility = idx == 0 ? Visibility.Visible : Visibility.Collapsed;
        PanelLfSniffer.Visibility = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
        PanelTagEmulator.Visibility = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
        PanelKeyCracker.Visibility = idx == 3 ? Visibility.Visible : Visibility.Collapsed;
        PanelMfuck.Visibility = idx == 4 ? Visibility.Visible : Visibility.Collapsed;
        PanelRatt.Visibility = idx == 5 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SliderAutoStartDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || TxtAutoStartDelay == null)
            return;

        TxtAutoStartDelay.Text = $"{(int)SliderAutoStartDelay.Value} sec";
    }

    private async void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        var modeName = CmbMode.SelectedItem is ComboBoxItem selectedItem
            ? selectedItem.Content?.ToString() ?? "Unknown"
            : "Unknown";

        var result = MessageBox.Show(
            $"Apply standalone mode configuration to device?\n\nMode: {modeName}\n" +
            $"Auto-Start: {(ChkAutoStart.IsChecked == true ? "Enabled" : "Disabled")}",
            "Upload Configuration",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                BtnApply.IsEnabled = false;
                BtnApply.Content = "Uploading...";
                
                var service = Proxmark3GUI.Services.Pm3ConnectionService.Instance;
                
                if (!service.IsConnected)
                    await service.ConnectAsync();
                
                var cmd = $"pref standalone {CmbMode.SelectedIndex}";
                await service.SendCommandAsync(cmd);
                
                MessageBox.Show("Configuration uploaded successfully.\n\nDisconnect USB to enter standalone mode.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                BtnApply.Content = "✓ Applied";
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error uploading configuration: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                BtnApply.Content = "✗ Failed";
            }
            finally
            {
                BtnApply.IsEnabled = true;
                if (BtnApply.Content.ToString() != "✓ Applied")
                    BtnApply.Content = "Apply Configuration";
            }
        }
    }

    private void BtnResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Reset all standalone mode settings to defaults?",
            "Reset Defaults",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            CmbMode.SelectedIndex = 0;
            ChkAutoStart.IsChecked = false;
            SliderAutoStartDelay.Value = 0;
            CmbMode_SelectionChanged(null, null);
        }
    }

    /// <summary>
    /// Gets the currently selected standalone mode name.
    /// </summary>
    public string GetSelectedMode()
    {
        return (CmbMode.SelectedItem as ComboBoxItem)?.Content as string ?? string.Empty;
    }

    /// <summary>
    /// Gets whether auto-start is enabled.
    /// </summary>
    public bool IsAutoStartEnabled()
    {
        return ChkAutoStart.IsChecked == true;
    }
}
