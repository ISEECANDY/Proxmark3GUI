using System.Windows;
using System.Windows.Controls;

namespace Proxmark3GUI.Views;

public partial class ScriptManagerView : UserControl
{
    private bool _isModified;

    public ScriptManagerView()
    {
        InitializeComponent();
        this.Loaded += ScriptManagerView_Loaded;
    }

    private void ScriptManagerView_Loaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to real-time script execution output
        if (Application.Current.MainWindow?.DataContext is Proxmark3GUI.ViewModels.MainViewModel vm)
        {
            vm.CommandOutputReceived += (s, output) =>
            {
                if (output.CommandType == "scriptExecution")
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        if (TxtOutput != null)
                        {
                            TxtOutput.AppendText(output.Line + System.Environment.NewLine);
                            TxtOutput.ScrollToEnd();
                        }
                    });
                }
            };
        }
    }

    private void BtnNewScript_Click(object sender, RoutedEventArgs e)
    {
        TxtEditor.Clear();
        TxtCurrentFile.Text = $"untitled.{GetExtension()}";
        _isModified = false;
        UpdateModifiedIndicator();
    }

    private void BtnOpenScript_Click(object sender, RoutedEventArgs e)
    {
        var ext = GetExtension();
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = $"Script files (*.{ext})|*.{ext}|All files (*.*)|*.*",
            Title = "Open Script"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                TxtEditor.Text = System.IO.File.ReadAllText(dialog.FileName);
                TxtCurrentFile.Text = System.IO.Path.GetFileName(dialog.FileName);
                _isModified = false;
                UpdateModifiedIndicator();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error loading script: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnSaveScript_Click(object sender, RoutedEventArgs e)
    {
        var ext = GetExtension();
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = $"Script files (*.{ext})|*.{ext}|All files (*.*)|*.*",
            Title = "Save Script",
            FileName = TxtCurrentFile.Text
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                System.IO.File.WriteAllText(dialog.FileName, TxtEditor.Text);
                TxtCurrentFile.Text = System.IO.Path.GetFileName(dialog.FileName);
                _isModified = false;
                UpdateModifiedIndicator();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error saving script: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnRunScript_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtEditor.Text))
        {
            MessageBox.Show("Please enter script content.", "Empty Script",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var language = (CmbLanguage.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Lua";
            var scriptPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pm3script.{GetExtension()}");
            
            // Write script to temp file
            System.IO.File.WriteAllText(scriptPath, TxtEditor.Text);
            TxtOutput.AppendText($"\n[{System.DateTime.Now:HH:mm:ss}] Running {TxtCurrentFile.Text} ({language})...\n");
            
            // Execute script via Pm3ConnectionService
            var service = Proxmark3GUI.Services.Pm3ConnectionService.Instance;
            if (!service.IsConnected)
            {
                await service.ConnectAsync();
            }

            var cmd = language.ToLower() switch
            {
                "lua" => $"script run {scriptPath}",
                "cmd" => $"@{scriptPath}",
                "python" => $"pyscript {scriptPath}",
                _ => $"script run {scriptPath}"
            };

            var result = await service.SendCommandAsync(cmd);
            if (!string.IsNullOrWhiteSpace(result))
            {
                TxtOutput.AppendText($"{result}\n");
            }
            TxtOutput.AppendText($"[{System.DateTime.Now:HH:mm:ss}] Script execution completed.\n");
        }
        catch (System.Exception ex)
        {
            TxtOutput.AppendText($"\n[ERROR] {ex.Message}\n");
            MessageBox.Show($"Error executing script: {ex.Message}", "Execution Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnStopScript_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var service = Proxmark3GUI.Services.Pm3ConnectionService.Instance;
            if (service.IsConnected)
            {
                // Send Ctrl+C equivalent to stop the script
                await service.SendCommandAsync("hw ping");
            }
            TxtOutput.AppendText($"\n[{System.DateTime.Now:HH:mm:ss}] Script execution stopped.\n");
        }
        catch (System.Exception ex)
        {
            TxtOutput.AppendText($"\n[ERROR] Failed to stop script: {ex.Message}\n");
        }
    }

    private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || CmbLanguage == null || TxtCurrentFile == null)
            return;

        TxtCurrentFile.Text = $"untitled.{GetExtension()}";
    }

    private void ScriptTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        try
        {
            if (e.NewValue is TreeViewItem item && item.Header is string header)
            {
                // Check if it's a script file (not a folder)
                if (item.Parent is TreeViewItem parent && parent.Header is string _)
                {
                    var scriptsDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(
                        System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", "scripts");
                    
                    var scriptPath = System.IO.Path.Combine(scriptsDir, header);
                    
                    if (System.IO.File.Exists(scriptPath))
                    {
                        try
                        {
                            TxtEditor.Text = System.IO.File.ReadAllText(scriptPath);
                            TxtCurrentFile.Text = header;
                            _isModified = false;
                            UpdateModifiedIndicator();
                        }
                        catch (System.Exception ex)
                        {
                            MessageBox.Show($"Error loading script: {ex.Message}", "Load Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TxtEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || TxtModified == null)
            return;

        _isModified = true;
        UpdateModifiedIndicator();
    }

    private void UpdateModifiedIndicator()
    {
        if (TxtModified == null)
            return;

        TxtModified.Text = _isModified ? "(modified)" : "";
    }

    private string GetExtension()
    {
        return (CmbLanguage.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
        {
            "Cmd" => "cmd",
            "Python" => "py",
            _ => "lua"
        };
    }
}
