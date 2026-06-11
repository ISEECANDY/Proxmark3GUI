using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Proxmark3GUI.Services;
using Proxmark3GUI.ViewModels;

namespace Proxmark3GUI.Views;

public partial class PowerManagementView : UserControl
{
    public PowerManagementView()
    {
        InitializeComponent();
        this.Loaded += PowerManagementView_Loaded;
    }

    private void PowerManagementView_Loaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to real-time power status updates
        if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
        {
            vm.CommandOutputReceived += (s, output) =>
            {
                if (output.CommandType == "powerStatus")
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        UpdatePowerMetricsFromOutput(output.Line);
                    });
                }
            };
        }
    }

    private void UpdatePowerMetricsFromOutput(string line)
    {
        // Parse power metrics from device output
        if (line.Contains("Battery", StringComparison.OrdinalIgnoreCase) || line.Contains("V"))
        {
            var voltage = ExtractVoltage(line);
            if (!string.IsNullOrEmpty(voltage) && double.TryParse(voltage, out var v))
            {
                TxtBatteryVoltage.Text = $"{v:F2}V";
            }
        }
        if (line.Contains("USB", StringComparison.OrdinalIgnoreCase))
        {
            var usbStatus = line.Contains("present") || line.Contains("connected") ? "Connected" : "Disconnected";
            TxtUsbStatus.Text = usbStatus;
        }
        if (line.Contains("Field", StringComparison.OrdinalIgnoreCase))
        {
            var fieldActive = line.Contains("ON", StringComparison.OrdinalIgnoreCase) || line.Contains("Active", StringComparison.OrdinalIgnoreCase);
            var fillBrush = fieldActive ? (Brush)FindResource("AccentGreen") : (Brush)FindResource("AccentGray");
            if (HfFieldIndicator != null)
                HfFieldIndicator.Fill = fillBrush;
            if (LfFieldIndicator != null)
                LfFieldIndicator.Fill = fillBrush;
            if (TxtHfField != null)
                TxtHfField.Text = fieldActive ? "Active" : "Inactive";
            if (TxtLfField != null)
                TxtLfField.Text = fieldActive ? "Active" : "Inactive";
        }
    }

    private string? ExtractVoltage(string line)
    {
        var regex = new System.Text.RegularExpressions.Regex(@"\d+\.\d+");
        var match = regex.Match(line);
        return match.Success ? match.Value : null;
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        TxtLastRefresh.Text = $"Last refresh: {DateTime.Now:HH:mm:ss}";
        BtnRefresh.IsEnabled = false;

        try
        {
            if (Application.Current.MainWindow?.DataContext is not MainViewModel vm)
                return;

            // Query device for current power status
            var statusResult = await vm.ExecuteCommandAsync("hw status");

            if (!string.IsNullOrWhiteSpace(statusResult))
            {
                foreach (var line in statusResult.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    UpdatePowerMetricsFromOutput(line);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to refresh power status: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnRefresh.IsEnabled = true;
        }
    }

    private async void ChkAutoSleep_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Application.Current.MainWindow?.DataContext is not MainViewModel vm)
                return;

            var isEnabled = ChkAutoSleep.IsChecked == true;
            var sleepTimeout = (int)SliderSleepTimeout.Value;

            // Enable/disable auto-sleep on device
            var command = isEnabled ? $"pref autosleep {sleepTimeout * 60000}" : "pref autosleep 0";
            await vm.ExecuteCommandAsync(command);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to set auto-sleep: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ChkScreenDim_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Application.Current.MainWindow?.DataContext is not MainViewModel vm)
                return;

            var isEnabled = ChkScreenDim.IsChecked == true;

            // Enable/disable screen dimming on device
            var command = isEnabled ? "pref screenbrightness 50" : "pref screenbrightness 100";
            await vm.ExecuteCommandAsync(command);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to set screen dimming: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ChkFieldTimeout_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Application.Current.MainWindow?.DataContext is not MainViewModel vm)
                return;

            var isEnabled = ChkFieldTimeout.IsChecked == true;
            var fieldTimeout = (int)SliderFieldTimeout.Value;

            // Enable/disable field auto-disable on device
            var command = isEnabled ? $"pref fieldtimeout {fieldTimeout * 1000}" : "pref fieldtimeout 0";
            await vm.ExecuteCommandAsync(command);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to set field timeout: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SliderSleepTimeout_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || TxtSleepTimeout == null)
            return;

        TxtSleepTimeout.Text = $"{(int)SliderSleepTimeout.Value} min";
    }

    private void SliderFieldTimeout_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || TxtFieldTimeout == null)
            return;

        TxtFieldTimeout.Text = $"{(int)SliderFieldTimeout.Value} sec";
    }

    /// <summary>
    /// Updates the battery status display.
    /// </summary>
    public void UpdateBatteryStatus(double voltage, double percent)
    {
        TxtBatteryVoltage.Text = $"{voltage:F2} V";

        // Color the voltage based on level
        if (voltage >= 3.7) TxtBatteryVoltage.Foreground = (Brush)FindResource("AccentGreen");
        else if (voltage >= 3.3) TxtBatteryVoltage.Foreground = (Brush)FindResource("AccentYellow");
        else TxtBatteryVoltage.Foreground = (Brush)FindResource("AccentRed");

        // Battery percentage bar
        var pct = Math.Clamp(percent, 0, 100);
        TxtBatteryPercent.Text = $"{pct:F0}%";

        if (pct >= 60) BatteryIndicator.Fill = (Brush)FindResource("AccentGreen");
        else if (pct >= 20) BatteryIndicator.Fill = (Brush)FindResource("AccentYellow");
        else BatteryIndicator.Fill = (Brush)FindResource("AccentRed");

        TxtBatteryStatus.Text = pct >= 60 ? "Good" : pct >= 20 ? "Low" : "Critical";
        TxtBatteryStatus.Foreground = BatteryIndicator.Fill;
    }

    /// <summary>
    /// Updates USB connection status.
    /// </summary>
    public void UpdateUsbStatus(bool connected, double voltage)
    {
        if (connected)
        {
            UsbIndicator.Fill = (Brush)FindResource("AccentGreen");
            TxtUsbStatus.Text = "Connected";
            TxtUsbStatus.Foreground = (Brush)FindResource("AccentGreen");
        }
        else
        {
            UsbIndicator.Fill = (Brush)FindResource("AccentRed");
            TxtUsbStatus.Text = "Disconnected";
            TxtUsbStatus.Foreground = (Brush)FindResource("AccentRed");
        }
        TxtUsbVoltage.Text = connected ? $"{voltage:F2} V" : "-- V";
    }

    /// <summary>
    /// Updates HF/LF field status indicators.
    /// </summary>
    public void UpdateFieldStatus(bool hfActive, bool lfActive, double fieldVoltage)
    {
        HfFieldIndicator.Fill = hfActive ? (Brush)FindResource("AccentGreen") : (Brush)FindResource("TextDim");
        TxtHfField.Text = hfActive ? "Active" : "Inactive";
        TxtHfField.Foreground = hfActive ? (Brush)FindResource("AccentGreen") : (Brush)FindResource("TextDim");

        LfFieldIndicator.Fill = lfActive ? (Brush)FindResource("AccentGreen") : (Brush)FindResource("TextDim");
        TxtLfField.Text = lfActive ? "Active" : "Inactive";
        TxtLfField.Foreground = lfActive ? (Brush)FindResource("AccentGreen") : (Brush)FindResource("TextDim");

        TxtFieldVoltage.Text = (hfActive || lfActive) ? $"{fieldVoltage:F1} V" : "-- V";
    }

    /// <summary>
    /// Updates current draw display and graph marker.
    /// </summary>
    public void UpdatePowerConsumption(double milliamps)
    {
        TxtCurrentDraw.Text = $"{milliamps:F0} mA";

        if (milliamps >= 100) TxtCurrentDraw.Foreground = (Brush)FindResource("AccentRed");
        else if (milliamps >= 60) TxtCurrentDraw.Foreground = (Brush)FindResource("AccentOrange");
        else TxtCurrentDraw.Foreground = (Brush)FindResource("AccentGreen");
    }

    /// <summary>
    /// Adds a data point to the power consumption graph.
    /// </summary>
    public void AddPowerDataPoint(double milliamps)
    {
        // TODO: Implement graph data point tracking and update polyline
        // This would shift all points left and add the new point on the right
        UpdatePowerConsumption(milliamps);
    }
}
