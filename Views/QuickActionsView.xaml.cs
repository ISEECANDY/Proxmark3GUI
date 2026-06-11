using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Proxmark3GUI.Views;

public partial class QuickActionsView : UserControl
{
    public QuickActionsView()
    {
        InitializeComponent();
        this.Loaded += QuickActionsView_Loaded;
    }

    private void QuickActionsView_Loaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to real-time command output
        if (Application.Current.MainWindow?.DataContext is Proxmark3GUI.ViewModels.MainViewModel vm)
        {
            vm.CommandOutputReceived += (s, output) =>
            {
                if (output.CommandType == "searchHF" || output.CommandType == "searchLF")
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        if (TxtLastAction != null)
                        {
                            TxtLastAction.Text = output.Line;
                            TxtLastAction.Foreground = (System.Windows.Media.Brush)FindResource("AccentGreen");
                        }
                    });
                }
            };
        }
    }

    /// <summary>
    /// Common handler for quick action clicks. Sends the appropriate PM3 command.
    /// </summary>
    private async void SendQuickAction(string command, string displayName)
    {
        try
        {
            if (Application.Current.MainWindow?.DataContext is not Proxmark3GUI.ViewModels.MainViewModel vm)
            {
                TxtLastAction.Text = $"Error: MainViewModel not found";
                TxtLastAction.Foreground = (System.Windows.Media.Brush)FindResource("AccentRed");
                return;
            }

            TxtLastAction.Text = $"Executing: {displayName}...";
            TxtLastAction.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondary");

            var result = await vm.ExecuteCommandAsync(command);

            if (string.IsNullOrWhiteSpace(result))
            {
                TxtLastAction.Text = $"Executed: {displayName} (no response)";
            }
            else
            {
                TxtLastAction.Text = $"✓ {displayName} - Check console for details";
            }
            TxtLastAction.Foreground = (System.Windows.Media.Brush)FindResource("AccentGreen");
        }
        catch (Exception ex)
        {
            TxtLastAction.Text = $"Error: {ex.Message}";
            TxtLastAction.Foreground = (System.Windows.Media.Brush)FindResource("AccentRed");
        }
    }

    private void ActionScanHF_Click(object sender, MouseButtonEventArgs e)
    {
        SendQuickAction("hf search", "Scan HF");
    }

    private void ActionScanLF_Click(object sender, MouseButtonEventArgs e)
    {
        SendQuickAction("lf search", "Scan LF");
    }

    private void ActionTune_Click(object sender, MouseButtonEventArgs e)
    {
        SendQuickAction("hf tune", "Tune Antenna");
    }

    private void ActionAutoPwn_Click(object sender, MouseButtonEventArgs e)
    {
        SendQuickAction("hf mf autopwn", "AutoPwn");
    }

    private void ActionDump_Click(object sender, MouseButtonEventArgs e)
    {
        SendQuickAction("hf mf dump", "Dump Card");
    }

    private void ActionSniffHF_Click(object sender, MouseButtonEventArgs e)
    {
        SendQuickAction("hf sniff", "Sniff HF");
    }

    private void ActionSniffLF_Click(object sender, MouseButtonEventArgs e)
    {
        SendQuickAction("lf sniff", "Sniff LF");
    }

    private void ActionDeviceInfo_Click(object sender, MouseButtonEventArgs e)
    {
        SendQuickAction("hw info", "Device Info");
    }
}
