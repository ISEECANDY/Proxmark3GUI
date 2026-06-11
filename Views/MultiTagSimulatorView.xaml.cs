using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Proxmark3GUI.Models;
using Proxmark3GUI.Services;
using Proxmark3GUI.ViewModels;

namespace Proxmark3GUI.Views;

public partial class MultiTagSimulatorView : UserControl
{
    public ObservableCollection<EmulationProfile> SimulationProfiles { get; } = new();
    private EmulationProfile? _selectedProfile;
    private bool _isSimulating;
    private int _activeTagCount;
    private EventHandler<(string CommandType, string Line)>? _commandOutputHandler;

    public MultiTagSimulatorView()
    {
        InitializeComponent();
        UpdateStatusDisplay();
        Loaded += MultiTagSimulatorView_Loaded;
        Unloaded += MultiTagSimulatorView_Unloaded;
    }

    private void BtnAddProfile_Click(object sender, RoutedEventArgs e)
    {
        var newProfile = new EmulationProfile
        {
            Name = $"New Profile {SimulationProfiles.Count + 1}",
            CardType = "MIFARE Classic",
            Uid = GenerateRandomUid(),
            Atqa = "0004",
            Sak = "08"
        };

        SimulationProfiles.Add(newProfile);
        _selectedProfile = newProfile;
        
        if (TxtStatus != null)
            TxtStatus.Text = "Added new profile. Configure and save.";
        LogMessage($"Added new profile: {newProfile.Name}");
    }

    private void BtnRemoveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile == null)
        {
            MessageBox.Show("Please select a profile to remove.", "No Selection",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Remove profile '{_selectedProfile.Name}'?",
            "Confirm Removal",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            SimulationProfiles.Remove(_selectedProfile);
            _selectedProfile = null;
            if (TxtStatus != null)
                TxtStatus.Text = "Profile removed.";
            LogMessage("Removed profile");
        }
    }

    private void BtnStartSim_Click(object sender, RoutedEventArgs e)
    {
        if (SimulationProfiles.Count == 0)
        {
            MessageBox.Show("Add at least one profile before starting simulation.", "No Profiles",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_isSimulating)
        {
            MessageBox.Show("Simulation already running.", "Already Running",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _isSimulating = true;
        _activeTagCount = SimulationProfiles.Count;

        BtnStartSim.IsEnabled = false;
        BtnAddProfile.IsEnabled = false;
        BtnRemoveProfile.IsEnabled = false;
        BtnStopSim.IsEnabled = true;

        if (TxtStatus != null)
            TxtStatus.Text = $"Simulating {_activeTagCount} tag(s)...";
        LogMessage($"Started multi-tag simulation with {_activeTagCount} profile(s)");

        _ = StartSimulationAsync();
    }

    private async Task StartSimulationAsync()
    {
        try
        {
            var service = Pm3ConnectionService.Instance;
            
            if (!service.IsConnected)
            {
                await service.ConnectAsync();
            }

            foreach (var profile in SimulationProfiles)
            {
                try
                {
                    var emulCmd = $"hf mf sim -t 1 -u {profile.Uid}";
                    await service.SendCommandAsync(emulCmd);
                    
                    LogMessage($"Loaded profile: {profile.Name}");
                }
                catch (Exception ex)
                {
                    LogMessage($"Error loading {profile.Name}: {ex.Message}");
                }
            }

            if (TxtStatus != null)
                TxtStatus.Text = $"Simulation running with {_activeTagCount} tags";
            LogMessage("All profiles loaded successfully");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error starting simulation: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            LogMessage($"Simulation error: {ex.Message}");
            _isSimulating = false;
            ResetSimulationButtons();
        }
    }

    private void BtnStopSim_Click(object sender, RoutedEventArgs e)
    {
        if (!_isSimulating)
        {
            MessageBox.Show("No simulation is currently running.", "Not Running",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _ = StopSimulationAsync();
    }

    private async Task StopSimulationAsync()
    {
        try
        {
            var service = Pm3ConnectionService.Instance;
            
            if (service.IsConnected)
            {
                await service.SendCommandAsync("hw ping");
            }

            _isSimulating = false;
            ResetSimulationButtons();
            
            if (TxtStatus != null)
                TxtStatus.Text = "Simulation stopped.";
            LogMessage("Stopped multi-tag simulation");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error stopping simulation: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            LogMessage($"Stop error: {ex.Message}");
        }
    }

    private void ResetSimulationButtons()
    {
        BtnStartSim.IsEnabled = true;
        BtnAddProfile.IsEnabled = true;
        BtnRemoveProfile.IsEnabled = true;
        BtnStopSim.IsEnabled = false;
    }

    private void MultiTagSimulatorView_Loaded(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
        {
            _commandOutputHandler = (_, output) =>
            {
                if (output.CommandType != "cardEmulation")
                    return;

                Dispatcher.BeginInvoke(() =>
                {
                    if (TxtConvLog != null)
                    {
                        TxtConvLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {output.Line}\n");
                        TxtConvLog.ScrollToEnd();
                    }
                });
            };

            vm.CommandOutputReceived += _commandOutputHandler;
        }
    }

    private void MultiTagSimulatorView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow?.DataContext is MainViewModel vm && _commandOutputHandler != null)
        {
            vm.CommandOutputReceived -= _commandOutputHandler;
            _commandOutputHandler = null;
        }
    }

    private void LvProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedProfile = (e.AddedItems.Count > 0 ? e.AddedItems[0] : null) as EmulationProfile;
        if (_selectedProfile != null && TxtStatus != null)
        {
            TxtStatus.Text = $"Selected: {_selectedProfile.Name}";
        }
    }

    private void UpdateStatusDisplay()
    {
        UpdateStatusCounts();
    }

    private void UpdateStatusCounts()
    {
        if (TxtProfileCount != null)
            TxtProfileCount.Text = $"{SimulationProfiles.Count} profiles";
        if (TxtSimStatus != null)
            TxtSimStatus.Text = _isSimulating ? "Running" : "Stopped";
        if (TxtActiveCount != null)
            TxtActiveCount.Text = _activeTagCount.ToString();
    }

    private static string GenerateRandomUid()
    {
        var rand = new Random();
        return $"{rand.Next(256):X2}{rand.Next(256):X2}{rand.Next(256):X2}{rand.Next(256):X2}";
    }

    private void LogMessage(string message)
    {
        if (TxtConvLog != null)
        {
            TxtConvLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
        }
    }
}
