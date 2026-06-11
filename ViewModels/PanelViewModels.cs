using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Proxmark3GUI.Models;
using Proxmark3GUI.Services;

namespace Proxmark3GUI.ViewModels;

// ═══════════════════════════════════════════════════════════════════════════════
// Stub Panel ViewModels – Each view can bind to these or use code-behind directly.
// These are registered in DI and available for future expansion.
// ═══════════════════════════════════════════════════════════════════════════════

public class DeviceInfoViewModel : ObservableObject
{
    private readonly Pm3ConnectionService _pm3;
    public Pm3DeviceInfo DeviceInfo { get; private set; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand DetectCommand { get; }

    public DeviceInfoViewModel(Pm3ConnectionService pm3)
    {
        _pm3 = pm3;
        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
        DetectCommand = new RelayCommand(async () => await _pm3.SendCommandAsync("hf search"));
    }

    private async Task RefreshAsync()
    {
        try { DeviceInfo = await _pm3.GetDeviceInfoAsync(); OnPropertyChanged(nameof(DeviceInfo)); }
        catch { }
    }
}

public class CommandsViewModel : ObservableObject
{
    private readonly CommandDatabase _db;
    private string _searchQuery = string.Empty;
    private bool _filterOfflineOnly;

    public ObservableCollection<Pm3Command> FilteredCommands { get; } = [];
    public Pm3Command? SelectedCommand { get; set; }

    public string SearchQuery { get => _searchQuery; set { Set(ref _searchQuery, value); UpdateFilter(); } }
    public bool FilterOfflineOnly { get => _filterOfflineOnly; set { Set(ref _filterOfflineOnly, value); UpdateFilter(); } }

    public ICommand ExecuteCommand { get; }

    public CommandsViewModel(CommandDatabase db, Pm3ConnectionService pm3)
    {
        _db = db;
        ExecuteCommand = new RelayCommand(async () =>
        {
            if (SelectedCommand != null) await pm3.SendCommandAsync(SelectedCommand.CommandName);
        });
        UpdateFilter();
    }

    private void UpdateFilter()
    {
        FilteredCommands.Clear();
        var results = string.IsNullOrEmpty(SearchQuery) ? _db.GetAllCommands() : _db.SearchCommands(SearchQuery);
        if (FilterOfflineOnly) results = results.Where(c => c.IsOffline).ToList();
        foreach (var cmd in results) FilteredCommands.Add(cmd);
    }
}

public class KeyCrackingViewModel : ObservableObject
{
    private readonly Pm3ConnectionService _pm3;
    public string SelectedAttack { get; set; } = "hf mf autopwn";
    public ObservableCollection<bool> SectorStatus { get; } = new(Enumerable.Repeat(false, 40).Select(_ => false));
    public string LogOutput { get; set; } = string.Empty;

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

    public KeyCrackingViewModel(Pm3ConnectionService pm3)
    {
        _pm3 = pm3;
        StartCommand = new RelayCommand(async () => { LogOutput += $"\n[Running {SelectedAttack}...]\n"; OnPropertyChanged(nameof(LogOutput)); await _pm3.SendCommandAsync(SelectedAttack); });
        StopCommand = new RelayCommand(() => { LogOutput += "\n[Stopped]\n"; OnPropertyChanged(nameof(LogOutput)); });
    }
}

public class DumpExplorerViewModel : ObservableObject
{
    public byte[] DumpData { get; set; } = [];
    public int Offset { get; set; }
    public string FilePath { get; set; } = string.Empty;

    public ICommand OpenFileCommand { get; }
    public ICommand SaveAsCommand { get; }

    public DumpExplorerViewModel(Pm3ConnectionService pm3)
    {
        OpenFileCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Bin files (*.bin)|*.bin|All|*.*" };
            if (dlg.ShowDialog() == true) { FilePath = dlg.FileName; DumpData = File.ReadAllBytes(FilePath); OnPropertyChanged(nameof(DumpData)); OnPropertyChanged(nameof(FilePath)); }
        });
        SaveAsCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Bin files (*.bin)|*.bin" };
            if (dlg.ShowDialog() == true) File.WriteAllBytes(dlg.FileName, DumpData);
        });
    }
}

public class SettingsViewModel : ObservableObject
{
    public string PortName { get; set; } = "COM3";
    public int BaudRate { get; set; } = 115200;
    public string Pm3ExePath { get; set; } = "proxmark3.exe";
    public bool AutoConnect { get; set; }
    public bool ShowTimestamps { get; set; } = true;
    public bool ShowCommandEcho { get; set; } = true;
    public int MaxConsoleLines { get; set; } = 5000;
    public int FontSize { get; set; } = 11;
    public bool EnableLogging { get; set; }
    public string LogFilePath { get; set; } = "pm3_gui.log";

    public ICommand ApplyCommand { get; }
    public ICommand ResetCommand { get; }

    public SettingsViewModel(Pm3ConnectionService pm3)
    {
        ApplyCommand = new RelayCommand(() => { /* Save settings to config */ });
        ResetCommand = new RelayCommand(() => { /* Reset to defaults */ });
    }
}

public class QuickActionsViewModel : ObservableObject
{
    private readonly Pm3ConnectionService _pm3;
    public string LastActionResult { get; set; } = "No action performed yet.";

    public ICommand ScanHfCommand { get; }
    public ICommand ScanLfCommand { get; }
    public ICommand TuneAntennaCommand { get; }
    public ICommand AutoPwnCommand { get; }
    public ICommand DumpCommand { get; }
    public ICommand SniffHfCommand { get; }
    public ICommand SniffLfCommand { get; }
    public ICommand DeviceInfoCommand { get; }

    public QuickActionsViewModel(Pm3ConnectionService pm3)
    {
        _pm3 = pm3;
        async Task RunAction(string cmd) { LastActionResult = $"Running: {cmd}"; OnPropertyChanged(nameof(LastActionResult)); try { await pm3.SendCommandAsync(cmd); LastActionResult = $"Completed: {cmd}"; } catch { LastActionResult = $"Error: {cmd}"; } OnPropertyChanged(nameof(LastActionResult)); }
        ScanHfCommand = new RelayCommand(async () => await RunAction("hf search"));
        ScanLfCommand = new RelayCommand(async () => await RunAction("lf search"));
        TuneAntennaCommand = new RelayCommand(async () => await RunAction("hf tune"));
        AutoPwnCommand = new RelayCommand(async () => await RunAction("hf mf autopwn"));
        DumpCommand = new RelayCommand(async () => await RunAction("hf mf dump"));
        SniffHfCommand = new RelayCommand(async () => await RunAction("hf sniff"));
        SniffLfCommand = new RelayCommand(async () => await RunAction("lf sniff"));
        DeviceInfoCommand = new RelayCommand(async () => await RunAction("hw info"));
    }
}

public class ReferenceViewModel : ObservableObject { public ReferenceViewModel(CommandDatabase db) { } }

public class TagDatabaseViewModel : ObservableObject
{
    public ObservableCollection<TagEntry> Tags { get; } = [];
    public TagEntry? SelectedTag { get; set; }
    public ICommand ClearAllCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand DeleteSelectedCommand { get; }

    public TagDatabaseViewModel(Pm3ConnectionService pm3)
    {
        ClearAllCommand = new RelayCommand(() => Tags.Clear());
        ExportCommand = new RelayCommand(() => { });
        DeleteSelectedCommand = new RelayCommand(() => { if (SelectedTag != null) Tags.Remove(SelectedTag); });
    }
}

public class ScriptManagerViewModel : ObservableObject
{
    public ObservableCollection<ScriptEntry> Scripts { get; } = [];
    public ScriptEntry? SelectedScript { get; set; }
    public string EditorContent { get; set; } = string.Empty;

    public ICommand NewCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand StopCommand { get; }

    public ScriptManagerViewModel(Pm3ConnectionService pm3)
    {
        NewCommand = new RelayCommand(() => { Scripts.Add(new ScriptEntry { FileName = "untitled.lua", Language = ScriptLanguage.Lua }); });
        OpenCommand = new RelayCommand(() => { });
        SaveCommand = new RelayCommand(() => { });
        RunCommand = new RelayCommand(async () => { if (SelectedScript != null) await pm3.SendCommandAsync($"script run {SelectedScript.FileName}"); });
        StopCommand = new RelayCommand(() => { });
    }
}

public class TraceAnalyzerViewModel : ObservableObject
{
    public ObservableCollection<TraceFrame> Frames { get; } = [];
    public TraceFrame? SelectedFrame { get; set; }

    public ICommand ClearCommand { get; }
    public ICommand ExportPcapCommand { get; }
    public ICommand ExportCsvCommand { get; }

    public TraceAnalyzerViewModel(Pm3ConnectionService pm3)
    {
        ClearCommand = new RelayCommand(() => Frames.Clear());
        ExportPcapCommand = new RelayCommand(() => { });
        ExportCsvCommand = new RelayCommand(() => { });
    }
}

public class FirmwareManagerViewModel : ObservableObject
{
    public string CurrentVersion { get; set; } = "Unknown";
    public string FlashFirmwarePath { get; set; } = string.Empty;

    public ICommand FlashCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand FactoryResetCommand { get; }

    public FirmwareManagerViewModel(Pm3ConnectionService pm3)
    {
        FlashCommand = new RelayCommand(async () => { await pm3.SendCommandAsync("hw flash"); });
        BackupCommand = new RelayCommand(() => { });
        RestoreCommand = new RelayCommand(() => { });
        FactoryResetCommand = new RelayCommand(() => { });
    }
}

public class AntennaTuningViewModel : ObservableObject
{
    public double HfVoltage { get; set; }
    public double HfFrequency { get; set; }
    public double LfVoltage { get; set; }
    public double LfFrequency { get; set; }
    public double QFactor { get; set; }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand AutoTuneCommand { get; }

    public AntennaTuningViewModel(Pm3ConnectionService pm3)
    {
        StartCommand = new RelayCommand(async () => { await pm3.SendCommandAsync("hf tune"); });
        StopCommand = new RelayCommand(() => { });
        AutoTuneCommand = new RelayCommand(async () => { await pm3.SendCommandAsync("hf tune"); });
    }
}

public class KeyDictionaryViewModel : ObservableObject
{
    public ObservableCollection<KeyEntry> Keys { get; } = [];
    public KeyEntry? SelectedKey { get; set; }

    public ICommand ImportCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand AddKeyCommand { get; }
    public ICommand DeduplicateCommand { get; }
    public ICommand ClearCommand { get; }

    public KeyDictionaryViewModel(Pm3ConnectionService pm3)
    {
        ImportCommand = new RelayCommand(() => { });
        ExportCommand = new RelayCommand(() => { });
        AddKeyCommand = new RelayCommand(() => { Keys.Add(new KeyEntry { KeyHex = "FFFFFFFFFFFF", KeyType = "A", Sector = 0, Source = "Manual" }); });
        DeduplicateCommand = new RelayCommand(() => { var unique = Keys.DistinctBy(k => k.KeyHex).ToList(); Keys.Clear(); foreach (var k in unique) Keys.Add(k); });
        ClearCommand = new RelayCommand(() => Keys.Clear());
    }
}

public class ChallengeResponseViewModel : ObservableObject
{
    public ObservableCollection<object> CrPairs { get; } = [];

    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand ReplayCommand { get; }

    public ChallengeResponseViewModel(Pm3ConnectionService pm3)
    {
        ExportCommand = new RelayCommand(() => { });
        ImportCommand = new RelayCommand(() => { });
        ClearCommand = new RelayCommand(() => CrPairs.Clear());
        ReplayCommand = new RelayCommand(() => { });
    }
}

public class ProtocolAnalyzerViewModel : ObservableObject
{
    public Dictionary<string, int> ProtocolCounts { get; } = new() { ["ISO14443A"] = 0, ["MIFARE"] = 0, ["Ultralight"] = 0, ["iCLASS"] = 0, ["FeliCa"] = 0, ["ISO15693"] = 0, ["EMV"] = 0, ["ISO14443B"] = 0 };

    public ICommand ResetCommand { get; }

    public ProtocolAnalyzerViewModel(Pm3ConnectionService pm3)
    {
        ResetCommand = new RelayCommand(() => { foreach (var k in ProtocolCounts.Keys.ToList()) ProtocolCounts[k] = 0; });
    }
}

public class PowerManagementViewModel : ObservableObject
{
    public double BatteryVoltage { get; set; }
    public double UsbVoltage { get; set; }
    public bool HfFieldActive { get; set; }
    public bool LfFieldActive { get; set; }

    public ICommand RefreshCommand { get; }

    public PowerManagementViewModel(Pm3ConnectionService pm3)
    {
        RefreshCommand = new RelayCommand(async () =>
        {
            var info = await pm3.GetDeviceInfoAsync();
            BatteryVoltage = double.TryParse(info.BatteryVoltage, out var bv) ? bv : 0.0;
            UsbVoltage = double.TryParse(info.UsbVoltage, out var uv) ? uv : 0.0;
            OnPropertyChanged(nameof(BatteryVoltage));
            OnPropertyChanged(nameof(UsbVoltage));
        });
    }
}

public class StandaloneModeViewModel : ObservableObject
{
    public string SelectedMode { get; set; } = "HF Reader";
    public bool AutoStartEnabled { get; set; }
    public int AutoStartDelay { get; set; } = 5;

    public ICommand ApplyCommand { get; }
    public ICommand UploadCommand { get; }

    public StandaloneModeViewModel(Pm3ConnectionService pm3)
    {
        ApplyCommand = new RelayCommand(() => { });
        UploadCommand = new RelayCommand(async () => { await pm3.SendCommandAsync($"standalone set {SelectedMode.ToLower()}"); });
    }
}

public class CardEmulationViewModel : ObservableObject
{
    public ObservableCollection<EmulationProfile> Profiles { get; } = [];
    public EmulationProfile? SelectedProfile { get; set; }
    public bool IsEmulating { get; set; }

    public ICommand NewProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand SaveProfileCommand { get; }

    public CardEmulationViewModel(Pm3ConnectionService pm3)
    {
        NewProfileCommand = new RelayCommand(() => { Profiles.Add(new EmulationProfile { Name = "New Profile", CardType = "MIFARE Classic", Uid = "01020304" }); });
        DeleteProfileCommand = new RelayCommand(() => { if (SelectedProfile != null) Profiles.Remove(SelectedProfile); });
        StartCommand = new RelayCommand(async () => { IsEmulating = true; OnPropertyChanged(nameof(IsEmulating)); if (SelectedProfile != null) await pm3.SendCommandAsync($"hf mf sim -u {SelectedProfile.Uid}"); });
        StopCommand = new RelayCommand(() => { IsEmulating = false; OnPropertyChanged(nameof(IsEmulating)); });
        SaveProfileCommand = new RelayCommand(() => { });
    }
}

public class MagicCardViewModel : ObservableObject
{
    public string CardType { get; set; } = "Gen1A";
    public string Uid { get; set; } = string.Empty;
    public string Atqa { get; set; } = "0004";
    public string Sak { get; set; } = "08";
    public string Ats { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public ICommand WriteCommand { get; }
    public ICommand ReadCommand { get; }

    public MagicCardViewModel(Pm3ConnectionService pm3)
    {
        WriteCommand = new RelayCommand(async () => { await pm3.SendCommandAsync($"hf mf sim --magic -u {Uid} --atqa {Atqa} --sak {Sak}"); });
        ReadCommand = new RelayCommand(async () => { await pm3.SendCommandAsync("hf 14a info"); });
    }
}

public class ReaderWriterViewModel : ObservableObject
{
    public string Operation { get; set; } = "Read Block";
    public int BlockNumber { get; set; }
    public string KeyType { get; set; } = "A";
    public string Key { get; set; } = "FFFFFFFFFFFF";
    public string Data { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;

    public ICommand ExecuteCommand { get; }

    public ReaderWriterViewModel(Pm3ConnectionService pm3)
    {
        ExecuteCommand = new RelayCommand(async () =>
        {
            string cmd = Operation switch
            {
                "Read Block" => $"hf mf rdbl -b {BlockNumber} -k {Key}",
                "Write Block" => $"hf mf wrbl -b {BlockNumber} -k {Key} -d {Data}",
                "Read Sector" => $"hf mf rdsc -s {BlockNumber / 4} -k {Key}",
                _ => $"hf mf rdbl -b {BlockNumber} -k {Key}"
            };
            Result = await pm3.SendCommandAsync(cmd);
            OnPropertyChanged(nameof(Result));
        });
    }
}

public class KeyRecoveryViewModel : ObservableObject
{
    public string SelectedAttack { get; set; } = "Hardnested";
    public string LogOutput { get; set; } = string.Empty;
    public double Progress { get; set; }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

    public KeyRecoveryViewModel(Pm3ConnectionService pm3)
    {
        StartCommand = new RelayCommand(async () => { LogOutput += $"\n[Starting {SelectedAttack}...]\n"; OnPropertyChanged(nameof(LogOutput)); await pm3.SendCommandAsync($"hf mf {SelectedAttack.ToLower()}"); });
        StopCommand = new RelayCommand(() => { LogOutput += "\n[Stopped]\n"; OnPropertyChanged(nameof(LogOutput)); });
    }
}

public class DataConversionViewModel : ObservableObject
{
    public string SourceFormat { get; set; } = "Bin";
    public string TargetFormat { get; set; } = "JSON";
    public string SourcePath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string ConversionLog { get; set; } = string.Empty;

    public ICommand ConvertCommand { get; }
    public ICommand BrowseSourceCommand { get; }
    public ICommand BrowseTargetCommand { get; }

    public DataConversionViewModel(Pm3ConnectionService pm3)
    {
        BrowseSourceCommand = new RelayCommand(() => { var d = new Microsoft.Win32.OpenFileDialog(); if (d.ShowDialog() == true) { SourcePath = d.FileName; OnPropertyChanged(nameof(SourcePath)); } });
        BrowseTargetCommand = new RelayCommand(() => { var d = new Microsoft.Win32.SaveFileDialog(); if (d.ShowDialog() == true) { TargetPath = d.FileName; OnPropertyChanged(nameof(TargetPath)); } });
        ConvertCommand = new RelayCommand(() => { ConversionLog += $"[Converting {SourcePath} from {SourceFormat} to {TargetFormat}]\n"; OnPropertyChanged(nameof(ConversionLog)); });
    }
}

public class AttackSchedulerViewModel : ObservableObject
{
    public ObservableCollection<AttackJob> JobQueue { get; } = [];
    public AttackJob? SelectedJob { get; set; }

    public ICommand AddJobCommand { get; }
    public ICommand RemoveJobCommand { get; }
    public ICommand StartAllCommand { get; }
    public ICommand StopAllCommand { get; }
    public ICommand ClearQueueCommand { get; }

    public AttackSchedulerViewModel(Pm3ConnectionService pm3)
    {
        AddJobCommand = new RelayCommand(() => { JobQueue.Add(new AttackJob { Id = JobQueue.Count + 1, Name = $"Attack {JobQueue.Count + 1}", AttackType = "hf mf autopwn" }); });
        RemoveJobCommand = new RelayCommand(() => { if (SelectedJob != null) JobQueue.Remove(SelectedJob); });
        StartAllCommand = new RelayCommand(() => { });
        StopAllCommand = new RelayCommand(() => { });
        ClearQueueCommand = new RelayCommand(() => JobQueue.Clear());
    }
}

public class TagComparisonViewModel : ObservableObject
{
    public string TagAPath { get; set; } = string.Empty;
    public string TagBPath { get; set; } = string.Empty;
    public string ComparisonResult { get; set; } = "Select two dump files to compare.";

    public ICommand CompareCommand { get; }
    public ICommand BrowseACommand { get; }
    public ICommand BrowseBCommand { get; }
    public ICommand SwapCommand { get; }
    public ICommand ExportReportCommand { get; }

    public TagComparisonViewModel(Pm3ConnectionService pm3)
    {
        BrowseACommand = new RelayCommand(() => { var d = new Microsoft.Win32.OpenFileDialog(); if (d.ShowDialog() == true) { TagAPath = d.FileName; OnPropertyChanged(nameof(TagAPath)); } });
        BrowseBCommand = new RelayCommand(() => { var d = new Microsoft.Win32.OpenFileDialog(); if (d.ShowDialog() == true) { TagBPath = d.FileName; OnPropertyChanged(nameof(TagBPath)); } });
        CompareCommand = new RelayCommand(() => { ComparisonResult = $"Comparing {TagAPath} vs {TagBPath}...\nUse 'data diff -a {TagAPath} -b {TagBPath}'"; OnPropertyChanged(nameof(ComparisonResult)); });
        SwapCommand = new RelayCommand(() => { (TagAPath, TagBPath) = (TagBPath, TagAPath); OnPropertyChanged(nameof(TagAPath)); OnPropertyChanged(nameof(TagBPath)); });
        ExportReportCommand = new RelayCommand(() => { });
    }
}

public class FirmwareRecoveryViewModel : ObservableObject
{
    public string RecoveryMethod { get; set; } = "USB Bootloader";
    public string FirmwareImagePath { get; set; } = string.Empty;
    public string RecoveryLog { get; set; } = string.Empty;

    public ICommand FlashCommand { get; }
    public ICommand BrowseImageCommand { get; }
    public ICommand CheckDeviceCommand { get; }

    public FirmwareRecoveryViewModel(Pm3ConnectionService pm3)
    {
        BrowseImageCommand = new RelayCommand(() => { var d = new Microsoft.Win32.OpenFileDialog(); if (d.ShowDialog() == true) { FirmwareImagePath = d.FileName; OnPropertyChanged(nameof(FirmwareImagePath)); } });
        FlashCommand = new RelayCommand(() => { RecoveryLog += "[WARNING: Flashing recovery firmware...]\n"; OnPropertyChanged(nameof(RecoveryLog)); });
        CheckDeviceCommand = new RelayCommand(async () => { RecoveryLog += await pm3.SendCommandAsync("hw status"); OnPropertyChanged(nameof(RecoveryLog)); });
    }
}

public class AntennaDiagnosticsViewModel : ObservableObject
{
    public double HfPeakVoltage { get; set; }
    public double HfFrequency { get; set; }
    public double LfPeakVoltage { get; set; }
    public double LfFrequency { get; set; }
    public double Vswr { get; set; }
    public double QFactor { get; set; }
    public string Recommendations { get; set; } = "Run diagnostics to get tuning recommendations.";

    public ICommand RunDiagnosticsCommand { get; }
    public ICommand AutoTuneCommand { get; }

    public AntennaDiagnosticsViewModel(Pm3ConnectionService pm3)
    {
        RunDiagnosticsCommand = new RelayCommand(async () => { Recommendations = await pm3.SendCommandAsync("hf tune"); OnPropertyChanged(nameof(Recommendations)); });
        AutoTuneCommand = new RelayCommand(() => { });
    }
}

public class MultiTagSimulatorViewModel : ObservableObject
{
    public ObservableCollection<EmulationProfile> SimTags { get; } = [];
    public EmulationProfile? ActiveTag { get; set; }
    public string SimulationMode { get; set; } = "Round Robin";
    public int TimeInterval { get; set; } = 500;
    public int TotalCommands { get; set; }
    public bool IsSimulating { get; set; }

    public ICommand AddTagCommand { get; }
    public ICommand RemoveTagCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

    public MultiTagSimulatorViewModel(Pm3ConnectionService pm3)
    {
        AddTagCommand = new RelayCommand(() => { SimTags.Add(new EmulationProfile { Name = $"Tag {SimTags.Count + 1}", CardType = "MIFARE Classic", Uid = $"{SimTags.Count + 1:08X}" }); });
        RemoveTagCommand = new RelayCommand(() => { if (ActiveTag != null) SimTags.Remove(ActiveTag); });
        StartCommand = new RelayCommand(() => { IsSimulating = true; OnPropertyChanged(nameof(IsSimulating)); });
        StopCommand = new RelayCommand(() => { IsSimulating = false; OnPropertyChanged(nameof(IsSimulating)); });
    }
}
