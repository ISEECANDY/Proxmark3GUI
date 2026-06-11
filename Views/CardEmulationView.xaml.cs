using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Proxmark3GUI.Models;
using Proxmark3GUI.Services;
using Proxmark3GUI.ViewModels;

namespace Proxmark3GUI.Views;

public partial class CardEmulationView : UserControl
{
    public ObservableCollection<EmulationProfile> Profiles { get; } = new();
    private EmulationProfile? _selectedProfile;
    private bool _isEmulating;

    public CardEmulationView()
    {
        InitializeComponent();
        LvProfiles.ItemsSource = Profiles;
        this.Loaded += CardEmulationView_Loaded;
    }

    private void CardEmulationView_Loaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to real-time card emulation output
        if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
        {
            vm.CommandOutputReceived += (s, output) =>
            {
                if (output.CommandType == "cardEmulation")
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        if (TxtStatus != null)
                        {
                            TxtStatus.Text = output.Line;
                        }
                    });
                }
            };
        }
    }

    private void CmbCardType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || CmbCardType == null)
            return;

        var type = (CmbCardType.SelectedItem as ComboBoxItem)?.Content as string ?? string.Empty;

        PanelMifareClassic.Visibility = type == "MIFARE Classic" ? Visibility.Visible : Visibility.Collapsed;
        PanelUltralight.Visibility = type == "MIFARE Ultralight" ? Visibility.Visible : Visibility.Collapsed;
        PanelIclass.Visibility = type == "iCLASS" ? Visibility.Visible : Visibility.Collapsed;
        PanelHidProx.Visibility = type == "HID Prox" ? Visibility.Visible : Visibility.Collapsed;
        PanelCustomNdef.Visibility = type == "Custom NDEF" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LvProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || LvProfiles == null)
            return;

        if (LvProfiles.SelectedItem is EmulationProfile profile)
        {
            _selectedProfile = profile;
            LoadProfileIntoEditor(profile);
            TxtStatus.Text = $"Editing profile: {profile.Name}";
        }
    }

    private void LoadProfileIntoEditor(EmulationProfile profile)
    {
        TxtProfileName.Text = profile.Name;

        // Select card type
        for (int i = 0; i < CmbCardType.Items.Count; i++)
        {
            if ((CmbCardType.Items[i] as ComboBoxItem)?.Content as string == profile.CardType)
            {
                CmbCardType.SelectedIndex = i;
                break;
            }
        }

        // Load card-type-specific fields
        TxtMfUid.Text = profile.Uid;
        TxtMfAtqa.Text = profile.Atqa;
        TxtMfSak.Text = profile.Sak;
        TxtMfAts.Text = profile.Ats;

        BuildSectorKeyGrid();
    }

    private void BuildSectorKeyGrid()
    {
        LvSectorKeys.Items.Clear();

        for (int i = 0; i < 16; i++)
        {
            var sector = new SectorKeyRow
            {
                Sector = i,
                KeyA = _selectedProfile?.SectorKeys.GetValueOrDefault($"A{i}", "FFFFFFFFFFFF") ?? "FFFFFFFFFFFF",
                KeyB = _selectedProfile?.SectorKeys.GetValueOrDefault($"B{i}", "FFFFFFFFFFFF") ?? "FFFFFFFFFFFF",
                AccessBits = "0xFF"
            };
            LvSectorKeys.Items.Add(sector);
        }
    }

    private void BtnNewProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = new EmulationProfile
        {
            Name = $"New Profile {Profiles.Count + 1}",
            CardType = "MIFARE Classic",
            Uid = "00000000",
            Atqa = "0004",
            Sak = "08",
        };

        Profiles.Add(profile);
        LvProfiles.SelectedItem = profile;
        TxtStatus.Text = "New profile created. Edit and save.";
    }

    private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (LvProfiles.SelectedItem is EmulationProfile profile)
        {
            var result = MessageBox.Show($"Delete profile '{profile.Name}'?",
                "Delete Profile", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Profiles.Remove(profile);
                _selectedProfile = null;
                TxtProfileName.Text = string.Empty;
                TxtStatus.Text = "Profile deleted.";
            }
        }
    }

    private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile == null)
        {
            MessageBox.Show("No profile selected.", "Save", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Gather data from editor fields
        _selectedProfile.Name = TxtProfileName.Text;
        _selectedProfile.CardType = (CmbCardType.SelectedItem as ComboBoxItem)?.Content as string ?? string.Empty;

        // MIFARE fields
        _selectedProfile.Uid = TxtMfUid.Text;
        _selectedProfile.Atqa = TxtMfAtqa.Text;
        _selectedProfile.Sak = TxtMfSak.Text;
        _selectedProfile.Ats = TxtMfAts.Text;

        // Update sector keys from ListView
        if (_selectedProfile.CardType == "MIFARE Classic")
        {
            foreach (SectorKeyRow row in LvSectorKeys.Items)
            {
                _selectedProfile.SectorKeys[$"A{row.Sector}"] = row.KeyA;
                _selectedProfile.SectorKeys[$"B{row.Sector}"] = row.KeyB;
            }
        }

        // Refresh list display
        LvProfiles.Items.Refresh();
        TxtStatus.Text = $"Profile '{_selectedProfile.Name}' saved successfully.";

        MessageBox.Show($"Profile '{_selectedProfile.Name}' saved.", "Save",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnStartEmulation_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile == null)
        {
            MessageBox.Show("Select a profile to emulate first.",
                "Start Emulation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (Application.Current.MainWindow?.DataContext is not MainViewModel vm)
                return;

            _isEmulating = true;
            BtnStartEmulation.IsEnabled = false;
            BtnStopEmulation.IsEnabled = true;
            EmulationIndicator.Fill = (Brush)FindResource("AccentGreen");
            TxtEmulationStatus.Text = "Emulating";
            TxtEmulationStatus.Foreground = (Brush)FindResource("AccentGreen");
            TxtStatus.Text = $"Emulating: {_selectedProfile.Name} ({_selectedProfile.CardType})";

            // Send emulation command to Proxmark3 based on card type
            var command = _selectedProfile.CardType switch
            {
                "MIFARE Classic" => $"hf mf sim -t 1 -u {_selectedProfile.Uid}",
                "MIFARE Ultralight" => $"hf mfu sim -t 2 -u {_selectedProfile.Uid}",
                "iCLASS" => $"hf iclass sim -t {_selectedProfile.Uid}",
                "HID Prox" => $"lf hid sim -c {_selectedProfile.Uid}",
                _ => "hf mf sim"
            };

            var result = await vm.ExecuteCommandAsync(command);
            if (!string.IsNullOrWhiteSpace(result))
                TxtStatus.Text += "\n" + result.Substring(0, Math.Min(100, result.Length));
        }
        catch (Exception ex)
        {
            _isEmulating = false;
            BtnStartEmulation.IsEnabled = true;
            BtnStopEmulation.IsEnabled = false;
            EmulationIndicator.Fill = (Brush)FindResource("AccentRed");
            TxtEmulationStatus.Text = "Error";
            TxtEmulationStatus.Foreground = (Brush)FindResource("AccentRed");
            TxtStatus.Text = $"Emulation failed: {ex.Message}";
            MessageBox.Show($"Failed to start emulation: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnStopEmulation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Application.Current.MainWindow?.DataContext is not MainViewModel vm)
                return;

            // Send stop command - typically interrupt the running simulation
            await vm.ExecuteCommandAsync("quit");

            _isEmulating = false;
            BtnStartEmulation.IsEnabled = true;
            BtnStopEmulation.IsEnabled = false;
            EmulationIndicator.Fill = (Brush)FindResource("TextDim");
            TxtEmulationStatus.Text = "Stopped";
            TxtEmulationStatus.Foreground = (Brush)FindResource("TextDim");
            TxtStatus.Text = "Emulation stopped.";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Error stopping emulation: {ex.Message}";
            MessageBox.Show($"Failed to stop emulation: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Gets whether emulation is currently active.
    /// </summary>
    public bool IsEmulating() => _isEmulating;

    /// <summary>
    /// Gets the currently selected profile.
    /// </summary>
    public EmulationProfile? GetSelectedProfile() => _selectedProfile;
}

/// <summary>
/// Represents a row in the sector key configuration ListView.
/// </summary>
public class SectorKeyRow
{
    public int Sector { get; set; }
    public string KeyA { get; set; } = "FFFFFFFFFFFF";
    public string KeyB { get; set; } = "FFFFFFFFFFFF";
    public string AccessBits { get; set; } = "0xFF";
}
