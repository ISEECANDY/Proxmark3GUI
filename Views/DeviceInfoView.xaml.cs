using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Proxmark3GUI.Services;
using Proxmark3GUI.ViewModels;

namespace Proxmark3GUI.Views;

public partial class DeviceInfoView : UserControl
{
    private EventHandler<(string CommandType, string Line)>? _commandOutputHandler;

    public DeviceInfoView()
    {
        InitializeComponent();
        this.Loaded += DeviceInfoView_Loaded;
        this.Unloaded += DeviceInfoView_Unloaded;
    }

    private void DeviceInfoView_Loaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to real-time device info and tag detection updates
        if (Application.Current.MainWindow?.DataContext is MainViewModel vm && _commandOutputHandler is null)
        {
            _commandOutputHandler = (s, output) =>
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    switch (output.CommandType)
                    {
                        case "deviceInfo":
                        case "powerStatus":
                            ParseAndDisplayDeviceInfo(output.Line);
                            break;
                        case "searchHF":
                        case "searchLF":
                            if (TxtTagSearchResults != null)
                            {
                                TxtTagSearchResults.AppendText(output.Line + Environment.NewLine);
                                TxtTagSearchResults.ScrollToEnd();
                            }
                            break;
                    }
                });
            };

            vm.CommandOutputReceived += _commandOutputHandler;
        }
    }

    private void DeviceInfoView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow?.DataContext is MainViewModel vm && _commandOutputHandler is not null)
        {
            vm.CommandOutputReceived -= _commandOutputHandler;
            _commandOutputHandler = null;
        }
    }

    private void ParseAndDisplayDeviceInfo(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        var normalized = line.Trim().Trim('[', '+', ']', ' ');
        if (normalized.Contains(".ncd", StringComparison.OrdinalIgnoreCase) && !normalized.Contains(":") && !normalized.Contains("=") && !normalized.Contains(".."))
        {
            if (TxtFpgaVersion != null)
                TxtFpgaVersion.Text = normalized;

            UpdateFpgaStatus(normalized);
            return;
        }

        var parts = normalized.Split(new[] { ':', '=' }, 2, StringSplitOptions.None);
        if (parts.Length < 2)
        {
            parts = SplitKeyValueByDots(normalized);
            if (parts.Length < 2)
            {
                UpdateFpgaStatus(normalized);
                return;
            }
        }

        var key = parts[0].Trim();
        var value = parts[1].Trim();

        if (TxtFirmwareVersion != null && (key.Contains("firmware", StringComparison.OrdinalIgnoreCase) || key.Equals("fw", StringComparison.OrdinalIgnoreCase)))
            TxtFirmwareVersion.Text = value;
        else if (TxtHardwareVersion != null && (key.Contains("platform", StringComparison.OrdinalIgnoreCase) || key.Contains("hardware", StringComparison.OrdinalIgnoreCase) || key.Equals("hw", StringComparison.OrdinalIgnoreCase)))
            TxtHardwareVersion.Text = value;
        else if (TxtPm3Uid != null && (key.Contains("unique", StringComparison.OrdinalIgnoreCase) || key.Contains("uid", StringComparison.OrdinalIgnoreCase)))
            TxtPm3Uid.Text = value;
        else if (TxtFpgaVersion != null && key.Contains(".ncd", StringComparison.OrdinalIgnoreCase))
            TxtFpgaVersion.Text = key + (string.IsNullOrWhiteSpace(value) ? string.Empty : " " + value);
        else if (TxtFpgaVersion != null && key.Contains("mode", StringComparison.OrdinalIgnoreCase) && value.Contains(".ncd", StringComparison.OrdinalIgnoreCase))
            TxtFpgaVersion.Text = value;
        else if (TxtFpgaVersion != null && key.Contains("fpga", StringComparison.OrdinalIgnoreCase))
            TxtFpgaVersion.Text = value;
        else if (TxtFlashSize != null && key.Contains("flash", StringComparison.OrdinalIgnoreCase))
            TxtFlashSize.Text = value;
        else if (TxtSerialNumber != null && key.Contains("serial", StringComparison.OrdinalIgnoreCase))
            TxtSerialNumber.Text = value;
        else if (TxtBootloaderVersion != null && (key.Contains("bootloader", StringComparison.OrdinalIgnoreCase) || key.Contains("bootrom", StringComparison.OrdinalIgnoreCase)))
            TxtBootloaderVersion.Text = value;
        else if (TxtUartSpeed != null && key.Contains("uart", StringComparison.OrdinalIgnoreCase))
            TxtUartSpeed.Text = value;
        else if (TxtHfPeakV != null && key.Contains("hf", StringComparison.OrdinalIgnoreCase) && key.Contains("peak", StringComparison.OrdinalIgnoreCase))
            TxtHfPeakV.Text = value;
        else if (TxtHfFreq != null && key.Contains("hf", StringComparison.OrdinalIgnoreCase) && key.Contains("freq", StringComparison.OrdinalIgnoreCase))
            TxtHfFreq.Text = value;
        else if (TxtLfPeakV != null && key.Contains("lf", StringComparison.OrdinalIgnoreCase) && key.Contains("peak", StringComparison.OrdinalIgnoreCase))
            TxtLfPeakV.Text = value;
        else if (TxtLfFreq != null && key.Contains("lf", StringComparison.OrdinalIgnoreCase) && key.Contains("freq", StringComparison.OrdinalIgnoreCase))
            TxtLfFreq.Text = value;

        UpdateFpgaStatus(normalized);
    }

    private string[] SplitKeyValueByDots(string line)
    {
        var match = System.Text.RegularExpressions.Regex.Match(line, @"^(.*?)\.{{2,}}\s*(.+)$");
        if (match.Success)
            return new[] { match.Groups[1].Value, match.Groups[2].Value };

        match = System.Text.RegularExpressions.Regex.Match(line, @"^(.+?)\s{2,}(.+)$");
        if (match.Success)
            return new[] { match.Groups[1].Value, match.Groups[2].Value };

        return new string[0];
    }

    private void UpdateFpgaStatus(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || RbFpgaReady == null || RbFpgaLoading == null || RbFpgaError == null)
            return;

        var lower = line.ToLowerInvariant();
        if (lower.Contains("error") || lower.Contains("fail"))
        {
            RbFpgaError.IsChecked = true;
        }
        else if (lower.Contains("loading") || lower.Contains("loaded") || lower.Contains("init"))
        {
            RbFpgaLoading.IsChecked = true;
        }
        else if (lower.Contains("ready") || lower.Contains("ok") || lower.Contains("running"))
        {
            RbFpgaReady.IsChecked = true;
        }
    }

    /// <summary>
    /// Sends "hw version" and "hw status" commands to refresh device information live.
    /// </summary>
    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        BtnRefresh.IsEnabled = false;
        ClearDeviceInfoDisplay();
        try
        {
            var vm = Application.Current.MainWindow?.DataContext as MainViewModel;
            if (vm is not null)
            {
                if (!vm.IsConnected)
                {
                    var service = Pm3ConnectionService.Instance;
                    await service.ConnectAsync(string.IsNullOrWhiteSpace(service.PortName) ? "COM9" : service.PortName);
                }

                await vm.ExecuteCommandAsync("hw version");
                await vm.ExecuteCommandAsync("hw status");
                return;
            }

            var fallbackService = Pm3ConnectionService.Instance;
            if (!fallbackService.IsConnected)
                await fallbackService.ConnectAsync(string.IsNullOrWhiteSpace(fallbackService.PortName) ? "COM9" : fallbackService.PortName);

            UpdateDeviceInfo(await fallbackService.GetDeviceInfoAsync());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to refresh device info: {ex.Message}", "Device Info",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnRefresh.IsEnabled = true;
        }
    }

    /// <summary>
    /// Sends "hf search" command to detect nearby HF tags.
    /// </summary>
    private async void BtnDetectTags_Click(object sender, RoutedEventArgs e)
    {
        BtnDetectTags.IsEnabled = false;
        try
        {
            var service = Pm3ConnectionService.Instance;
            if (!service.IsConnected)
                await service.ConnectAsync(string.IsNullOrWhiteSpace(service.PortName) ? "COM9" : service.PortName);

            await service.SendCommandAsync("hf search");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to detect tags: {ex.Message}", "Detect Tags",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnDetectTags.IsEnabled = true;
        }
    }

    /// <summary>
    /// Updates the stat card fields from a Pm3DeviceInfo model.
    /// </summary>
    public void UpdateDeviceInfo(Models.Pm3DeviceInfo info)
    {
        if (info == null) return;

        TxtFirmwareVersion.Text = string.IsNullOrWhiteSpace(info.FirmwareVersion) ? "--" : info.FirmwareVersion;
        TxtHardwareVersion.Text = string.IsNullOrWhiteSpace(info.HardwareVersion) ? "--" : info.HardwareVersion;
        TxtPm3Uid.Text = string.IsNullOrWhiteSpace(info.Pm3Uid) ? "--" : info.Pm3Uid;
        TxtFpgaVersion.Text = string.IsNullOrWhiteSpace(info.FpgaVersion) ? "--" : info.FpgaVersion;
        TxtFlashSize.Text = string.IsNullOrWhiteSpace(info.FlashSize) ? "--" : info.FlashSize;
        TxtSerialNumber.Text = string.IsNullOrWhiteSpace(info.SerialNumber) ? "--" : info.SerialNumber;
        TxtBootloaderVersion.Text = string.IsNullOrWhiteSpace(info.BootloaderVersion) ? "--" : info.BootloaderVersion;
        TxtUartSpeed.Text = string.IsNullOrWhiteSpace(info.UartSpeed) ? "--" : info.UartSpeed;

        TxtHfPeakV.Text = string.IsNullOrWhiteSpace(info.HfPeakV) ? "-- V" : $"{info.HfPeakV} V";
        TxtHfFreq.Text = string.IsNullOrWhiteSpace(info.HfFreq) ? "-- MHz" : $"{info.HfFreq} MHz";
        TxtLfPeakV.Text = string.IsNullOrWhiteSpace(info.LfPeakV) ? "-- V" : $"{info.LfPeakV} V";
        TxtLfFreq.Text = string.IsNullOrWhiteSpace(info.LfFreq) ? "-- kHz" : $"{info.LfFreq} kHz";
    }

    private void ClearDeviceInfoDisplay()
    {
        if (TxtFirmwareVersion != null) TxtFirmwareVersion.Text = "Refreshing...";
        if (TxtHardwareVersion != null) TxtHardwareVersion.Text = "Refreshing...";
        if (TxtPm3Uid != null) TxtPm3Uid.Text = "Refreshing...";
        if (TxtFpgaVersion != null) TxtFpgaVersion.Text = "Refreshing...";
        if (TxtFlashSize != null) TxtFlashSize.Text = "Refreshing...";
        if (TxtSerialNumber != null) TxtSerialNumber.Text = "Refreshing...";
        if (TxtBootloaderVersion != null) TxtBootloaderVersion.Text = "Refreshing...";
        if (TxtUartSpeed != null) TxtUartSpeed.Text = "Refreshing...";
        if (TxtHfPeakV != null) TxtHfPeakV.Text = "-- V";
        if (TxtHfFreq != null) TxtHfFreq.Text = "-- MHz";
        if (TxtLfPeakV != null) TxtLfPeakV.Text = "-- V";
        if (TxtLfFreq != null) TxtLfFreq.Text = "-- kHz";
    }

    /// <summary>
    /// Sets an LED indicator color (pass the Ellipse name and desired brush).
    /// </summary>
    public void SetLed(string ledName, Brush brush)
    {
        if (FindName(ledName) is Ellipse led)
        {
            led.Fill = brush;
        }
    }
}
