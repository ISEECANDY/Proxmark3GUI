using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Proxmark3GUI.Services;
using Proxmark3GUI.ViewModels;

namespace Proxmark3GUI;

public partial class App : Application
{
    public static ServiceProvider? ServiceProvider { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        // Register services using the existing singleton instance
        services.AddSingleton(Pm3ConnectionService.Instance);
        services.AddSingleton<CommandDatabase>();

        // Register ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DeviceInfoViewModel>();
        services.AddTransient<CommandsViewModel>();
        services.AddTransient<KeyCrackingViewModel>();
        services.AddTransient<DumpExplorerViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<QuickActionsViewModel>();
        services.AddTransient<ReferenceViewModel>();
        services.AddTransient<TagDatabaseViewModel>();
        services.AddTransient<ScriptManagerViewModel>();
        services.AddTransient<TraceAnalyzerViewModel>();
        services.AddTransient<FirmwareManagerViewModel>();
        services.AddTransient<AntennaTuningViewModel>();
        services.AddTransient<KeyDictionaryViewModel>();
        services.AddTransient<ChallengeResponseViewModel>();
        services.AddTransient<ProtocolAnalyzerViewModel>();
        services.AddTransient<PowerManagementViewModel>();
        services.AddTransient<StandaloneModeViewModel>();
        services.AddTransient<CardEmulationViewModel>();
        services.AddTransient<MagicCardViewModel>();
        services.AddTransient<ReaderWriterViewModel>();
        services.AddTransient<KeyRecoveryViewModel>();
        services.AddTransient<DataConversionViewModel>();
        services.AddTransient<AttackSchedulerViewModel>();
        services.AddTransient<TagComparisonViewModel>();
        services.AddTransient<FirmwareRecoveryViewModel>();
        services.AddTransient<AntennaDiagnosticsViewModel>();
        services.AddTransient<MultiTagSimulatorViewModel>();

        ServiceProvider = services.BuildServiceProvider();

        var cmdDb = ServiceProvider.GetRequiredService<CommandDatabase>();
        cmdDb.LoadFromFile("commands.json");

        var mainWindow = new MainWindow(ServiceProvider.GetRequiredService<MainViewModel>());
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ServiceProvider?.Dispose();
        base.OnExit(e);
    }
}
