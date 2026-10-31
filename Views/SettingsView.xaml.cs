using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Proxmark3GUI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        LoadDefaultSettings();
        RefreshPorts();

        SldTimeout.ValueChanged += (_, _) =>
        {
            TxtTimeoutValue.Text = $"{SldTimeout.Value:F0}s";
        };
    }

    private void LoadDefaultSettings()
    {
        // Load default/saved settings
        if (CmbPortName != null) CmbPortName.SelectedIndex = 0;
        if (CmbBaudRate != null) CmbBaudRate.SelectedIndex = 4; // 115200
        if (TxtPm3Path != null) TxtPm3Path.Text = Path.Combine(AppContext.BaseDirectory, "client", "proxmark3.exe");
        if (CmbColorTheme != null) CmbColorTheme.SelectedIndex = 0;
        if (SldFontSize != null) { SldFontSize.Value = 12; TxtFontSizeValue.Text = "12px"; }
        if (SldTimeout != null) { SldTimeout.Value = 30; TxtTimeoutValue.Text = "30s"; }
    }

    private void RefreshPorts()
    {
        if (CmbPortName == null)
            return;

        var current = (CmbPortName.SelectedItem as ComboBoxItem)?.Content?.ToString();
        CmbPortName.Items.Clear();

        var ports = SerialPort.GetPortNames()
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        CmbPortName.Items.Add(new ComboBoxItem { Content = "Select a serial port..." });

        foreach (var port in ports)
            CmbPortName.Items.Add(new ComboBoxItem { Content = port });

        // Never guess which device the operator intends to use. Preserve an existing
        // selection when it is still present; otherwise require an explicit choice.
        var selectedIndex = ports.FindIndex(p => string.Equals(p, current, StringComparison.OrdinalIgnoreCase));
        CmbPortName.SelectedIndex = selectedIndex >= 0 ? selectedIndex + 1 : 0;
    }

    private void SldFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtFontSizeValue != null && SldFontSize != null)
            TxtFontSizeValue.Text = $"{SldFontSize.Value:F0}px";
    }

    private void BtnBrowsePm3_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Select PM3 Executable"
        };

        if (dialog.ShowDialog() == true)
        {
            TxtPm3Path.Text = dialog.FileName;
        }
    }

    private void BtnRefreshPorts_Click(object sender, RoutedEventArgs e)
    {
        RefreshPorts();
    }

    private void BtnBrowseDict_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Dictionary files (*.dic;*.txt)|*.dic;*.txt|All files (*.*)|*.*",
            Title = "Select Default Dictionary"
        };

        if (dialog.ShowDialog() == true)
        {
            TxtDefaultDict.Text = dialog.FileName;
        }
    }

    private void BtnBrowseDefaultDict_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Dictionary files (*.dic;*.txt)|*.dic;*.txt|All files (*.*)|*.*",
            Title = "Select Default Dictionary"
        };

        if (dialog.ShowDialog() == true)
        {
            TxtDefaultDict.Text = dialog.FileName;
        }
    }

    private void BtnBrowseLog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Log files (*.log;*.txt)|*.log;*.txt|All files (*.*)|*.*",
            Title = "Select Log File Location"
        };

        if (dialog.ShowDialog() == true)
        {
            // TxtLogPath.Text = dialog.FileName; // Adjust based on actual XAML
        }
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Clear all logs?",
            "Clear Log", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            // Clear logs here
        }
    }

    private async void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get the Pm3ConnectionService singleton
            var service = Proxmark3GUI.App.ServiceProvider?.GetService(typeof(Proxmark3GUI.Services.Pm3ConnectionService)) 
                         as Proxmark3GUI.Services.Pm3ConnectionService;
            
            if (service == null)
            {
                MessageBox.Show("Connection service not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Apply settings
            service.PortName = CmbPortName.SelectedIndex > 0
                ? (CmbPortName.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty
                : string.Empty;
            service.BaudRate = int.Parse((CmbBaudRate.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "115200");
            service.Pm3ExePath = TxtPm3Path.Text;

            if (string.IsNullOrWhiteSpace(service.PortName))
            {
                MessageBox.Show("Please select a serial port.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Try to connect
            BtnApply.IsEnabled = false;
            BtnApply.Content = "⏳ Connecting...";

            try
            {
                await service.ConnectAsync(service.PortName);
                MessageBox.Show($"Connected to Proxmark3 on {service.PortName}!", "Success", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                BtnApply.Content = "✓ Connected";

            }
            catch (FileNotFoundException)
            {
                MessageBox.Show($"Proxmark3 executable not found at: {service.Pm3ExePath}\n\nPlease check the path and try again.", 
                    "Executable Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnApply.Content = "✗ Failed";
                BtnApply.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}\n\nMake sure the device is connected and the port is correct.", 
                    "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnApply.Content = "✗ Failed";
                BtnApply.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        // Reset all controls to default values
        CmbBaudRate.SelectedIndex = 4; // 115200
        TxtPm3Path.Text = Path.Combine(AppContext.BaseDirectory, "client", "proxmark3.exe");
        ChkAutoConnect.IsChecked = false;
        SldTimeout.Value = 5;
        ChkShowTimestamps.IsChecked = true;
        ChkShowCommandEcho.IsChecked = true;
        TxtMaxConsoleLines.Text = "1000";
        SldFontSize.Value = 11;
        TxtDefaultDict.Text = "default.dic";
        TxtMaxTimeout.Text = "300";
        ChkAutoSaveDumps.IsChecked = true;
        ChkFastCheck.IsChecked = false;
        ChkEnableLogging.IsChecked = true;
        TxtLogPath.Text = "pm3gui.log";
        CmbLogLevel.SelectedIndex = 1; // Info
    }

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow?.DataContext is not Proxmark3GUI.ViewModels.MainViewModel vm)
            return;

        vm.ConnectCommand.Execute(null);
    }

    private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow?.DataContext is not Proxmark3GUI.ViewModels.MainViewModel vm)
            return;

        vm.DisconnectCommand.Execute(null);
    }
}
