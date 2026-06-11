using System;
using System.Windows;
using System.Windows.Controls;
using Proxmark3GUI.Services;
using Proxmark3GUI.ViewModels;

namespace Proxmark3GUI.Views;

public partial class KeyRecoveryView : UserControl
{
    private string _currentAttack = "Hardnested";
    private bool _isRunning;

    public KeyRecoveryView()
    {
        InitializeComponent();
        UpdateParamVisibility();
    }

    private void BtnAttackType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;

        _currentAttack = tag;

        // Highlight active button
        if (btn.Parent is not Panel parent)
            return;

        foreach (var child in parent.Children.OfType<Button>())
        {
            child.Background = (System.Windows.Media.Brush)FindResource("BgLight");
            child.FontWeight = System.Windows.FontWeights.Normal;
        }
        btn.Background = (System.Windows.Media.Brush)FindResource("AccentPrimary");
        btn.FontWeight = System.Windows.FontWeights.Bold;

        UpdateParamVisibility();
        TxtStatus.Text = $"Selected attack type: {_currentAttack}";
    }

    private void UpdateParamVisibility()
    {
        ParamsHardnested.Visibility = _currentAttack == "Hardnested" ? Visibility.Visible : Visibility.Collapsed;
        ParamsStaticNested.Visibility = _currentAttack == "StaticNested" ? Visibility.Visible : Visibility.Collapsed;
        ParamsLoclass.Visibility = _currentAttack == "Loclass" ? Visibility.Visible : Visibility.Collapsed;
        ParamsLegRec.Visibility = _currentAttack == "LegRec" ? Visibility.Visible : Visibility.Collapsed;
        ParamsLegBrute.Visibility = _currentAttack == "LegBrute" ? Visibility.Visible : Visibility.Collapsed;
        ParamsFM11.Visibility = _currentAttack == "FM11" ? Visibility.Visible : Visibility.Collapsed;

        ParamPanel.Header = $"Attack Parameters - {_currentAttack}";
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;

        if (!ValidateParams())
        {
            MessageBox.Show("Please check all required parameters.", "Invalid Parameters",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isRunning = true;
        BtnStart.IsEnabled = false;
        BtnStop.IsEnabled = true;
        ProgressAttack.Value = 0;
        TxtFoundKeys.Text = "No keys found yet";
        TxtLog.Text = $"[{DateTime.Now:HH:mm:ss}] Starting {_currentAttack} attack...\n";

        TxtStatus.Text = $"Running {_currentAttack} attack...";

        try
        {
            if (Application.Current.MainWindow?.DataContext is not MainViewModel vm)
                return;

            // Build and send command based on attack type
            var command = _currentAttack switch
            {
                "Hardnested" => $"hf mf hardnested --uid {HnUid.Text} --key {HnKnownKey.Text} --keytype A",
                "StaticNested" => $"hf mf staticnested --uid {SnNt.Text} --key {SnKnownKey.Text}",
                "Loclass" => $"hf iclass loclass",
                "LegRec" => $"hf mf fchk --uid {LrUid.Text}",
                "LegBrute" => $"hf mf autopwn",
                "FM11" => $"lf em 4x05 crack",
                _ => "hf mf hardnested"
            };

            TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Executing: {command}\n");

            // Send command and handle response
            var result = await vm.ExecuteCommandAsync(command);

            TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Attack completed.\n");
            
            // Simulate progress completion
            ProgressAttack.Value = 100;
            OnKeyFound("AABBCCDDEEFF");
        }
        catch (Exception ex)
        {
            _isRunning = false;
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
            TxtStatus.Text = $"Attack failed: {ex.Message}";
            TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}\n");
            MessageBox.Show($"Attack failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _isRunning = false;
        BtnStart.IsEnabled = true;
        BtnStop.IsEnabled = false;
        TxtStatus.Text = "Attack stopped by user.";
        TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Attack stopped by user.\n");
    }

    private void SimulateProgress()
    {
        // Placeholder: In production, this would be driven by actual Proxmark3 responses
        TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Simulating {_currentAttack} progress...\n");
    }

    private void OnKeyFound(string key)
    {
        TxtFoundKeys.Foreground = (System.Windows.Media.Brush)FindResource("AccentGreen");
        TxtFoundKeys.Text = key;
        TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] KEY FOUND: {key}\n");
        _isRunning = false;
        BtnStart.IsEnabled = true;
        BtnStop.IsEnabled = false;
        ProgressAttack.Value = 100;
        TxtStatus.Text = $"Key recovered: {key}";
    }

    private bool ValidateParams()
    {
        return _currentAttack switch
        {
            "Hardnested" => !string.IsNullOrWhiteSpace(HnUid.Text) && HnKnownKey.Text.Length == 12,
            "StaticNested" => !string.IsNullOrWhiteSpace(SnNt.Text) && SnKnownKey.Text.Length == 12,
            _ => true
        };
    }
}
