using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Proxmark3GUI.Models;
using Proxmark3GUI.Services;

namespace Proxmark3GUI.ViewModels;

// ═══════════════════════════════════════════════════════════════════════════════
// ObservableObject – Minimal INotifyPropertyChanged base class
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Base class for all ViewModels that provides <see cref="INotifyPropertyChanged"/>
/// with a concise <see cref="Set{T}(ref T, T, string?)"/> helper.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Sets the backing field and raises <see cref="PropertyChanged"/> when the value
    /// actually changes.  Returns <c>true</c> when a change was made.
    /// </summary>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

// ═══════════════════════════════════════════════════════════════════════════════
// RelayCommand – Minimal ICommand implementation
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A reusable <see cref="ICommand"/> delegate wrapper that supports both
/// parameter-less and generic-parameter execution with optional <c>CanExecute</c>.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>Convenience ctor for parameter-less delegates.</summary>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(o => execute(), canExecute is not null ? new Predicate<object?>(_ => canExecute()) : null)
    { }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>Raises <see cref="CanExecuteChanged"/> so the UI re-evaluates CanExecute.</summary>
    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}

// ═══════════════════════════════════════════════════════════════════════════════
// MainViewModel – Central ViewModel for the Proxmark3 GUI shell
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// The root ViewModel that owns the sidebar navigation, console I/O,
/// quick-action commands, and the injected service references.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    // ── Injected services ──────────────────────────────────────────────────────

    private readonly Pm3ConnectionService _pm3;
    private readonly CommandDatabase _cmdDb;

    // ── Backing fields ─────────────────────────────────────────────────────────

    private NavItem? _selectedNavItem;
    private Uri _currentViewUri = new("Views/DeviceInfoView.xaml", UriKind.Relative);
    private object? _currentView;
    private string _currentPanelTitle = "Device Information";
    private string _currentPanelSubtitle = "Overview, technical specs, antenna tuning, FPGA status";
    private bool _isConnected;
    private string _consoleOutput = string.Empty;
    private string _inputCommand = string.Empty;
    private string _activeCommand = string.Empty; // Track currently executing command for real-time routing

    // ── Events ──────────────────────────────────────────────────────────────────

    /// <summary>Fired whenever a line is appended to <see cref="ConsoleOutput"/>.</summary>
    public event EventHandler<string>? ConsoleLogReceived;

    /// <summary>Fired for real-time output from specific commands (searchHF, searchLF, deviceInfo, etc.)</summary>
    public event EventHandler<(string CommandType, string Line)>? CommandOutputReceived;

    // ═══════════════════════════════════════════════════════════════════════════
    // Properties
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>All sidebar navigation entries.</summary>
    public ObservableCollection<NavItem> NavigationItems { get; } = [];

    /// <summary>Currently selected sidebar item (drives <see cref="CurrentViewUri"/>).</summary>
    public NavItem? SelectedNavItem
    {
        get => _selectedNavItem;
        private set => Set(ref _selectedNavItem, value);
    }

    /// <summary>URI loaded by the main content <see cref="System.Windows.Controls.Frame"/>.</summary>
    public Uri CurrentViewUri
    {
        get => _currentViewUri;
        private set => Set(ref _currentViewUri, value);
    }

    /// <summary>The current View instance with injected ViewModel.</summary>
    public object? CurrentView
    {
        get => _currentView;
        private set => Set(ref _currentView, value);
    }

    /// <summary>Title displayed in the content panel header.</summary>
    public string CurrentPanelTitle
    {
        get => _currentPanelTitle;
        private set => Set(ref _currentPanelTitle, value);
    }

    /// <summary>Subtitle / description shown beneath the panel title.</summary>
    public string CurrentPanelSubtitle
    {
        get => _currentPanelSubtitle;
        private set => Set(ref _currentPanelSubtitle, value);
    }

    /// <summary><c>true</c> when the Proxmark3 device is connected.</summary>
    public bool IsConnected
    {
        get => _isConnected;
        private set => Set(ref _isConnected, value);
    }

    /// <summary>Accumulated console / log output text.</summary>
    public string ConsoleOutput
    {
        get => _consoleOutput;
        private set => Set(ref _consoleOutput, value);
    }

    /// <summary>Command text typed into the console input box.</summary>
    public string InputCommand
    {
        get => _inputCommand;
        set => Set(ref _inputCommand, value);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Public Methods - For Views to Execute Commands
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Executes a command and logs it to the console. Views should use this method.
    /// Real-time output is routed to CommandOutputReceived event for live module updates.
    /// </summary>
    public async Task<string> ExecuteCommandAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return string.Empty;

        if (!IsConnected)
        {
            AppendConsole("[Error] Not connected to Proxmark3. Connect first.");
            return string.Empty;
        }

        AppendConsole($"> {command}");
        
        // Track which command is executing for real-time routing
        _activeCommand = DetermineCommandType(command);

        try
        {
            var result = await _pm3.SendCommandAsync(command);
            if (!string.IsNullOrWhiteSpace(result))
            {
                AppendConsole(result.TrimEnd());
            }
            return result;
        }
        catch (Exception ex)
        {
            AppendConsole($"[Error] {ex.Message}");
            return string.Empty;
        }
        finally
        {
            _activeCommand = string.Empty;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Commands
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Navigates to the given <see cref="NavItem"/>.</summary>
    public ICommand SelectPanelCommand { get; }

    /// <summary>Sends <see cref="InputCommand"/> to the Proxmark3 device.</summary>
    public ICommand SendCommandCommand { get; }

    /// <summary>Clears the <see cref="ConsoleOutput"/> text.</summary>
    public ICommand ClearConsoleCommand { get; }

    /// <summary>Saves <see cref="ConsoleOutput"/> to a user-selected file.</summary>
    public ICommand SaveConsoleCommand { get; }

    /// <summary>Runs <c>"hf search"</c> on the device.</summary>
    public ICommand QuickScanCommand { get; }

    /// <summary>Runs <c>"hf mf autopwn"</c> on the device.</summary>
    public ICommand QuickAutoPwnCommand { get; }

    /// <summary>Runs <c>"hf mf dump"</c> on the device.</summary>
    public ICommand QuickDumpCommand { get; }

    /// <summary>Connects to the Proxmark3 device on the configured COM port.</summary>
    public ICommand ConnectCommand { get; }

    /// <summary>Disconnects from the Proxmark3 device.</summary>
    public ICommand DisconnectCommand { get; }

    // ═══════════════════════════════════════════════════════════════════════════
    // Constructor
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a new <see cref="MainViewModel"/>.
    /// </summary>
    /// <param name="pm3">Proxmark3 connection service (injected).</param>
    /// <param name="cmdDb">Command database service (injected).</param>
    public MainViewModel(Pm3ConnectionService pm3, CommandDatabase cmdDb)
    {
        _pm3 = pm3 ?? throw new ArgumentNullException(nameof(pm3));
        _cmdDb = cmdDb ?? throw new ArgumentNullException(nameof(cmdDb));

        // ── Wire service events ───────────────────────────────────────────────
        _pm3.OutputReceived += OnOutputReceived;
        _pm3.ConnectionChanged += OnConnectionChanged;

        // ── Initialize navigation items ──────────────────────────────────────
        InitializeNavigationItems();

        // ── Bind commands ────────────────────────────────────────────────────
        SelectPanelCommand = new RelayCommand(ExecuteSelectPanel);
        SendCommandCommand = new RelayCommand(ExecuteSendCommand, CanSendCommand);
        ClearConsoleCommand = new RelayCommand(ExecuteClearConsole);
        SaveConsoleCommand = new RelayCommand(ExecuteSaveConsole);
        QuickScanCommand = new RelayCommand(async () => await SendQuickCommandAsync("hf search"));
        QuickAutoPwnCommand = new RelayCommand(async () => await SendQuickCommandAsync("hf mf autopwn"));
        QuickDumpCommand = new RelayCommand(async () => await SendQuickCommandAsync("hf mf dump"));
        ConnectCommand = new RelayCommand(async () => await ExecuteConnectCommand(), () => !IsConnected);
        DisconnectCommand = new RelayCommand(() => ExecuteDisconnectCommand(), () => IsConnected);

        // ── Select the first panel (Device Info) ──────────────────────────────
        SelectPanel(NavigationItems[0]);

        // Device connection is operator-initiated from Settings or the Connect control.
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Navigation initialisation – 27 items
    // ═══════════════════════════════════════════════════════════════════════════

    private void InitializeNavigationItems()
    {
        var navItems = new[]
        {
            new NavItem(1,  "\U0001f4ca", "Device Info",          "Views/DeviceInfoView.xaml",         "Device Information",         "Overview, technical specs, antenna tuning, FPGA status"),
            new NavItem(2,  "\U0001f4cb", "Commands & Wizards",  "Views/CommandsView.xaml",           "Commands & Wizards",        "Searchable command browser with tags and quick wizards"),
            new NavItem(3,  "\U0001f513", "Key Cracking Suite",   "Views/KeyCrackingView.xaml",        "Key Cracking Suite",         "Attack selection, sector map, and progress tracking"),
            new NavItem(4,  "\U0001f4be", "Dump Explorer",        "Views/DumpExplorerView.xaml",       "Dump Explorer",              "Hex editor with offset navigation and ASCII view"),
            new NavItem(5,  "\u2699\ufe0f", "Settings",           "Views/SettingsView.xaml",           "Settings",                  "Connection, display, attack, and logging preferences"),
            new NavItem(6,  "\u26a1",       "Quick Actions",       "Views/QuickActionsView.xaml",       "Quick Actions",             "One-click operations for common tasks"),
            new NavItem(7,  "\U0001f4d6", "Reference",            "Views/ReferenceView.xaml",           "Reference",                 "Cheatsheets and Quick Start guides"),
            new NavItem(8,  "\U0001f3f7\ufe0f", "Tag Database",    "Views/TagDatabaseView.xaml",         "Tag Database",              "History of scanned tags with UID and keys"),
            new NavItem(9,  "\U0001f4dc", "Script Manager",       "Views/ScriptManagerView.xaml",       "Script Manager",            "Browse, edit, and run Lua/Cmd/Python scripts"),
            new NavItem(10, "\U0001f4c8", "Trace Analyzer",       "Views/TraceAnalyzerView.xaml",       "Trace Analyzer",            "Frame-by-frame protocol log with CRC status"),
            new NavItem(11, "\U0001f504", "Firmware Manager",     "Views/FirmwareManagerView.xaml",     "Firmware Manager",          "Manage and flash firmware versions"),
            new NavItem(12, "\U0001f4f6", "Antenna Tuning",       "Views/AntennaTuningView.xaml",       "Antenna Tuning",            "Real-time HF/LF voltage and impedance"),
            new NavItem(13, "\U0001f4d5", "Key Dictionary",       "Views/KeyDictionaryView.xaml",       "Key Dictionary Manager",    "Manage key dictionaries with statistics"),
            new NavItem(14, "\U0001f510", "Challenge-Response",   "Views/ChallengeResponseView.xaml",   "Challenge-Response Log",    "Log of CR pairs with replay and export"),
            new NavItem(15, "\U0001f9ec", "Protocol Analyzer",    "Views/ProtocolAnalyzerView.xaml",   "Protocol Analyzer",         "Real-time protocol statistics"),
            new NavItem(16, "\U0001f50b", "Power Management",     "Views/PowerManagementView.xaml",     "Power Management",          "Battery, USB, field status and power graph"),
            new NavItem(17, "\U0001f680", "Standalone Mode",      "Views/StandaloneModeView.xaml",      "Standalone Mode",          "Configure offline modes and auto-start"),
            new NavItem(18, "\U0001f0cf", "Card Emulation",      "Views/CardEmulationView.xaml",       "Card Emulation",            "Manage and run emulation profiles"),
            new NavItem(19, "\U0001f9d9", "Magic Card Config",    "Views/MagicCardView.xaml",           "Magic Card Configurator",   "Configure Gen1A/Gen2/Gen3 cards"),
            new NavItem(20, "\U0001f4dd", "Reader/Writer",        "Views/ReaderWriterView.xaml",        "Reader / Writer Tools",     "Raw block/sector read/write operations"),
            new NavItem(21, "\U0001f5e8\ufe0f", "Key Recovery",    "Views/KeyRecoveryView.xaml",        "Key Recovery Tools",        "Advanced key recovery attacks"),
            new NavItem(22, "\U0001f504", "Data Conversion",      "Views/DataConversionView.xaml",      "Data Conversion Tools",    "Convert between Bin, EML, JSON, MFD, Trace"),
            new NavItem(23, "\u23f0",       "Attack Scheduler",   "Views/AttackSchedulerView.xaml",     "Attack Scheduler",          "Queue and run multiple attacks"),
            new NavItem(24, "\u2696\ufe0f", "Tag Comparison",     "Views/TagComparisonView.xaml",       "Tag Comparison Tool",       "Side-by-side diff with key match report"),
            new NavItem(25, "\U0001f6e0\ufe0f", "Firmware Recovery", "Views/FirmwareRecoveryView.xaml", "Firmware Recovery Tools",  "JTAG, USB bootloader, recovery tools"),
            new NavItem(26, "\U0001f4ca", "Antenna Diagnostics",  "Views/AntennaDiagnosticsView.xaml",  "Antenna Diagnostics",      "Advanced HF/LF diagnostics and auto-tune"),
            new NavItem(27, "\U0001f465", "Multi-Tag Sim",       "Views/MultiTagSimulatorView.xaml",   "Multi-Tag Simulator",       "Simulate multiple tags simultaneously"),
        };

        foreach (var item in navItems)
        {
            NavigationItems.Add(item);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Command implementations
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Navigates to the given <see cref="NavItem"/>, updating the active state,
    /// view URI, and panel header text.
    /// </summary>
    private void ExecuteSelectPanel(object? parameter)
    {
        if (parameter is NavItem item)
            SelectPanel(item);
    }

    /// <summary>
    /// Sends the <see cref="InputCommand"/> text to the Proxmark3 device,
    /// appends an echo to the console, and clears the input field.
    /// </summary>
    private async void ExecuteSendCommand()
    {
        if (string.IsNullOrWhiteSpace(InputCommand))
            return;

        var cmd = InputCommand;
        InputCommand = string.Empty;

        AppendConsole($"[Executing '{cmd}' …]");

        try
        {
            await ExecuteCommandAsync(cmd);
        }
        catch (Exception ex)
        {
            AppendConsole($"[Error] {ex.Message}");
        }
    }

    private bool CanSendCommand() => !string.IsNullOrWhiteSpace(InputCommand) && IsConnected;

    /// <summary>Connects to the Proxmark3 device on the configured COM port.</summary>
    private async Task ExecuteConnectCommand()
    {
        try
        {
            AppendConsole("[Connecting] Initiating connection to Proxmark3 device...");
            await _pm3.ConnectAsync(_pm3.PortName);
            AppendConsole("[Connected] Proxmark3 device is now online.");
        }
        catch (Exception ex)
        {
            AppendConsole($"[Error] Failed to connect: {ex.Message}");
        }
    }

    /// <summary>Disconnects from the Proxmark3 device.</summary>
    private void ExecuteDisconnectCommand()
    {
        try
        {
            AppendConsole("[Disconnecting] Closing Proxmark3 connection...");
            _pm3.Disconnect();
            AppendConsole("[Disconnected] Connection closed.");
        }
        catch (Exception ex)
        {
            AppendConsole($"[Error] Failed to disconnect: {ex.Message}");
        }
    }

    /// <summary>Clears all text from <see cref="ConsoleOutput"/>.</summary>
    private void ExecuteClearConsole() => ConsoleOutput = string.Empty;

    /// <summary>Prompts the user to save <see cref="ConsoleOutput"/> to a text file.</summary>
    private void ExecuteSaveConsole()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save Console Output",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = $"pm3_console_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(dlg.FileName, ConsoleOutput);
                AppendConsole($"[Console output saved to {dlg.FileName}]");
            }
            catch (Exception ex)
            {
                AppendConsole($"[Error saving file] {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Sends a quick command string asynchronously and logs the result.
    /// </summary>
    private async Task SendQuickCommandAsync(string command)
    {
        if (!_pm3.IsConnected)
        {
            AppendConsole("[Error] Not connected to Proxmark3. Connect first.");
            return;
        }

        await ExecuteCommandAsync(command);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Private helpers
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Core panel-selection logic: deactivates the previous item, activates
    /// the new one, and updates all related display properties.
    /// </summary>
    private void SelectPanel(NavItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        // Deactivate the previously selected item
        if (_selectedNavItem is not null)
            _selectedNavItem.IsActive = false;

        // Activate the new item
        item.IsActive = true;
        SelectedNavItem = item;

        CurrentViewUri = item.ViewUri ?? new Uri("Views/DeviceInfoView.xaml", UriKind.Relative);
        CurrentPanelTitle = item.PanelTitle;
        CurrentPanelSubtitle = item.PanelSubtitle;

        // Create the View with injected ViewModel
        CurrentView = CreateViewWithViewModel(item);
    }

    /// <summary>
    /// Creates a View instance with its injected ViewModel based on the NavItem.
    /// </summary>
    private object? CreateViewWithViewModel(NavItem item)
    {
        var view = CreateViewInstance(item.ViewUri?.OriginalString);
        if (view == null)
            return CreateFallbackView(item, "Unknown view type.");

        var viewType = view.GetType();
        var viewModelType = GetViewModelTypeForView(viewType);
        var sp = App.ServiceProvider;

        try
        {
            if (viewModelType != null && sp != null)
            {
                var viewModel = sp.GetService(viewModelType);
                if (viewModel != null)
                {
                    var dcProperty = viewType.GetProperty("DataContext");
                    dcProperty?.SetValue(view, viewModel);
                }
            }
            return view;
        }
        catch (Exception ex)
        {
            return CreateFallbackView(item, $"{viewType.Name} failed to load: {ex.Message}");
        }
    }

    private static object? CreateViewInstance(string? viewPath)
    {
        var key = viewPath?.Replace("Views/", string.Empty).Replace(".xaml", string.Empty);
        return key switch
        {
            "DeviceInfoView" => new Proxmark3GUI.Views.DeviceInfoView(),
            "CommandsView" => new Proxmark3GUI.Views.CommandsView(),
            "KeyCrackingView" => new Proxmark3GUI.Views.KeyCrackingView(),
            "DumpExplorerView" => new Proxmark3GUI.Views.DumpExplorerView(),
            "SettingsView" => new Proxmark3GUI.Views.SettingsView(),
            "QuickActionsView" => new Proxmark3GUI.Views.QuickActionsView(),
            "ReferenceView" => new Proxmark3GUI.Views.ReferenceView(),
            "TagDatabaseView" => new Proxmark3GUI.Views.TagDatabaseView(),
            "ScriptManagerView" => new Proxmark3GUI.Views.ScriptManagerView(),
            "TraceAnalyzerView" => new Proxmark3GUI.Views.TraceAnalyzerView(),
            "FirmwareManagerView" => new Proxmark3GUI.Views.FirmwareManagerView(),
            "AntennaTuningView" => new Proxmark3GUI.Views.AntennaTuningView(),
            "KeyDictionaryView" => new Proxmark3GUI.Views.KeyDictionaryView(),
            "ChallengeResponseView" => new Proxmark3GUI.Views.ChallengeResponseView(),
            "ProtocolAnalyzerView" => new Proxmark3GUI.Views.ProtocolAnalyzerView(),
            "PowerManagementView" => new Proxmark3GUI.Views.PowerManagementView(),
            "StandaloneModeView" => new Proxmark3GUI.Views.StandaloneModeView(),
            "CardEmulationView" => new Proxmark3GUI.Views.CardEmulationView(),
            "MagicCardView" => new Proxmark3GUI.Views.MagicCardView(),
            "ReaderWriterView" => new Proxmark3GUI.Views.ReaderWriterView(),
            "KeyRecoveryView" => new Proxmark3GUI.Views.KeyRecoveryView(),
            "DataConversionView" => new Proxmark3GUI.Views.DataConversionView(),
            "AttackSchedulerView" => new Proxmark3GUI.Views.AttackSchedulerView(),
            "TagComparisonView" => new Proxmark3GUI.Views.TagComparisonView(),
            "FirmwareRecoveryView" => new Proxmark3GUI.Views.FirmwareRecoveryView(),
            "AntennaDiagnosticsView" => new Proxmark3GUI.Views.AntennaDiagnosticsView(),
            "MultiTagSimulatorView" => new Proxmark3GUI.Views.MultiTagSimulatorView(),
            _ => null
        };
    }

    /// <summary>
    /// Gets the View type from the URI path.
    /// </summary>
    private Type? GetViewTypeFromUri(Uri? uri)
    {
        if (uri == null)
            return null;

        var path = uri.OriginalString;
        if (!path.StartsWith("Views/"))
            return null;

        var filename = path["Views/".Length..].Replace(".xaml", "");
        return typeof(MainViewModel).Assembly.GetType($"Proxmark3GUI.Views.{filename}");
    }

    /// <summary>
    /// Gets the ViewModel type for a given View type.
    /// </summary>
    private Type? GetViewModelTypeForView(Type viewType)
    {
        var viewName = viewType.Name;
        var viewModelName = viewName.Replace("View", "ViewModel");
        return typeof(MainViewModel).Assembly.GetType($"Proxmark3GUI.ViewModels.{viewModelName}");
    }

    private static object CreateFallbackView(NavItem item, string message)
    {
        Brush BrushOrDefault(string key, string fallback)
            => (Application.Current?.TryFindResource(key) as Brush)
               ?? (Application.Current?.TryFindResource(fallback) as Brush)
               ?? Brushes.Transparent;

        var panel = new System.Windows.Controls.Border
        {
            Background = BrushOrDefault("BgMedium", "BgDark"),
            Padding = new Thickness(24),
            Child = new System.Windows.Controls.StackPanel
            {
                Children =
                {
                    new System.Windows.Controls.TextBlock
                    {
                        Text = item.PanelTitle,
                        FontSize = 20,
                        FontWeight = FontWeights.Bold,
                        Foreground = BrushOrDefault("TextPrimary", "TextSecondary"),
                        Margin = new Thickness(0, 0, 0, 8)
                    },
                    new System.Windows.Controls.TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = BrushOrDefault("TextSecondary", "TextPrimary")
                    }
                }
            }
        };

        return panel;
    }

    /// <summary>
    /// Appends a line of text to <see cref="ConsoleOutput"/> and raises
    /// <see cref="ConsoleLogReceived"/>.
    /// </summary>
    internal void AppendConsole(string text)
    {
        ConsoleOutput = string.IsNullOrEmpty(ConsoleOutput)
            ? text
            : ConsoleOutput + Environment.NewLine + text;

        ConsoleLogReceived?.Invoke(this, text);
    }

    // ── Service event handlers ────────────────────────────────────────────────

    private void OnOutputReceived(object? sender, string line)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            AppendConsole(line);
            
            // Route to command-specific handlers if a command is active
            if (!string.IsNullOrEmpty(_activeCommand) && _activeCommand != "general")
            {
                CommandOutputReceived?.Invoke(this, (_activeCommand, line));
            }
        });
    }

    private void OnConnectionChanged(object? sender, bool connected)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            IsConnected = connected;
            AppendConsole(connected
                ? "[Connected] Proxmark3 device is online."
                : "[Disconnected] Proxmark3 device is offline.");
            
            // Update command availability
            CommandManager.InvalidateRequerySuggested();
        });
    }

    /// <summary>Determines the command type for output routing to specific modules.</summary>
    private string DetermineCommandType(string command)
    {
        command = command.ToLower().Trim();
        
        return command switch
        {
            var c when c.StartsWith("hf search") => "searchHF",
            var c when c.StartsWith("lf search") => "searchLF",
            var c when c.StartsWith("hf tune") => "tuneHF",
            var c when c.StartsWith("lf tune") => "tuneLF",
            var c when c.StartsWith("hw info") => "deviceInfo",
            var c when c.StartsWith("hw version") => "deviceInfo",
            var c when c.StartsWith("hw status") => "powerStatus",
            var c when c.StartsWith("hw ping") => "devicePing",
            var c when c.StartsWith("hf mf rdbl") => "readBlock",
            var c when c.StartsWith("hf mf wrbl") => "writeBlock",
            var c when c.StartsWith("hf mf dump") => "dumpCard",
            var c when c.StartsWith("hf mf autopwn") => "keyCracking",
            var c when c.StartsWith("hf mf nested") => "keyRecovery",
            var c when c.StartsWith("hf mf darkside") => "keyCracking",
            var c when c.StartsWith("hf mf hardnested") => "keyCracking",
            var c when c.StartsWith("hf mf staticnested") => "keyCracking",
            var c when c.StartsWith("hf mf chk") => "keyCracking",
            var c when c.StartsWith("hf mf sim") => "cardEmulation",
            var c when c.StartsWith("hf mf csetuid") => "magicCard",
            var c when c.StartsWith("hf mfu sim") => "cardEmulation",
            var c when c.StartsWith("hf iclass sim") => "cardEmulation",
            var c when c.StartsWith("lf hid sim") => "cardEmulation",
            var c when c.StartsWith("script run") => "scriptExecution",
            var c when c.StartsWith("pyscript") => "scriptExecution",
            var c when c.StartsWith("pref standalone") => "standaloneMode",
            var c when c.StartsWith("pref") => "preferences",
            var c when c.StartsWith("hf mf chk") => "keyDictionary",
            var c when c.StartsWith("mem spiffs load") => "firmwareFlash",
            _ => "general"
        };
    }

    /// <summary>Routes command output to specific modules via CommandOutputReceived event.</summary>
    private void RouteCommandOutput(string commandType, string output)
    {
        if (!string.IsNullOrWhiteSpace(commandType) && commandType != "general")
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                foreach (var line in output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                {
                    CommandOutputReceived?.Invoke(this, (commandType, line));
                }
            });
        }
    }
}
