using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Proxmark3GUI.Models;
using Proxmark3GUI.Services;
using Proxmark3GUI.ViewModels;

namespace Proxmark3GUI.Views;

public partial class CommandsView : UserControl
{
    public ObservableCollection<CommandItem> CommandItems { get; } = new();
    private readonly CommandDatabase? _commandDb;
    private readonly ObservableCollection<CategoryLegendItem> _categoryLegendItems = new();
    private string _selectedTopCategory = string.Empty;
    private bool _commandGroupsExpanded = true;

    public CommandsView()
    {
        InitializeComponent();
        _commandDb = App.ServiceProvider?.GetService(typeof(CommandDatabase)) as CommandDatabase;
        LstCommands.ItemsSource = CommandItems;
        CategoryLegend.ItemsSource = _categoryLegendItems;
        LoadCommands();
        PopulateCategoryLegend();
        this.Loaded += CommandsView_Loaded;
    }

    private void CommandsView_Loaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to any command output for display
        if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
        {
            vm.CommandOutputReceived += (s, output) =>
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    // Update status bar with latest command output
                    if (TxtStatus != null)
                    {
                        TxtStatus.Text = output.Line;
                    }
                });
            };
        }
    }

    private void LoadCommands()
    {
        CommandItems.Clear();

        var commands = _commandDb?.GetAllCommands()
            ?? LoadCommandsFromDisk()
            ?? new List<Pm3Command>
            {
                new() { CommandName = "hf search", Description = "Search for HF tags in the vicinity", IsOffline = false, Category = "HF" },
                new() { CommandName = "lf search", Description = "Search for LF tags in the vicinity", IsOffline = false, Category = "LF" },
            };

        foreach (var cmd in commands.OrderBy(c => c.CommandName))
        {
            var categoryParts = (cmd.Category ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var topCategory = categoryParts.Length > 0 ? categoryParts[0] : "General";
            var subCategory = categoryParts.Length > 1 ? string.Join(' ', categoryParts.Skip(1)) : "General";

            CommandItems.Add(new CommandItem
            {
                Command = cmd.CommandName,
                Description = cmd.Description,
                IsOffline = cmd.IsOffline ? "Yes" : "No",
                Category = cmd.Category ?? string.Empty,
                TopCategory = topCategory,
                SubCategory = string.IsNullOrEmpty(subCategory) ? "General" : subCategory,
                Usage = cmd.Usage,
                Options = string.Join(Environment.NewLine, cmd.Options),
                Notes = string.Join(Environment.NewLine, cmd.Notes)
            });
        }

        var view = CollectionViewSource.GetDefaultView(LstCommands.ItemsSource ?? CommandItems);
        view.GroupDescriptions.Clear();
        view.GroupDescriptions.Add(new PropertyGroupDescription("TopCategory"));
        view.GroupDescriptions.Add(new PropertyGroupDescription("SubCategory"));
    }

    private void PopulateCategoryLegend()
    {
        _categoryLegendItems.Clear();

        var legendEntries = CommandItems
            .GroupBy(ci => ci.TopCategory)
            .OrderBy(g => g.Key)
            .Select(group => new CategoryLegendItem
            {
                Category = group.Key,
                DisplayText = $"{group.Key}: {string.Join(", ", group.Select(ci => ci.SubCategory).Distinct().OrderBy(x => x))}",
                IsSelected = string.Equals(group.Key, _selectedTopCategory, StringComparison.OrdinalIgnoreCase)
            });

        foreach (var legendEntry in legendEntries)
            _categoryLegendItems.Add(legendEntry);
    }

    private static List<Pm3Command>? LoadCommandsFromDisk()
    {
        try
        {
            var assemblyDir = Path.GetDirectoryName(typeof(CommandsView).Assembly.Location);
            if (string.IsNullOrWhiteSpace(assemblyDir))
                return null;

            var path = Path.Combine(assemblyDir, "commands.json");
            return File.Exists(path) ? new CommandDatabase(path).GetAllCommands() : null;
        }
        catch
        {
            return null;
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || TxtSearch == null)
            return;

        FilterCommands();
    }

    private void FilterCommands()
    {
        if (LstCommands == null || TxtSearch == null)
            return;

        var search = TxtSearch.Text?.ToLower() ?? string.Empty;

        CollectionViewSource.GetDefaultView(LstCommands.ItemsSource).Filter = item =>
        {
            if (item is not CommandItem ci)
                return false;

            return string.IsNullOrEmpty(search) ||
                   ci.Command.ToLowerInvariant().Contains(search) ||
                   ci.Description.ToLowerInvariant().Contains(search) ||
                   ci.Category.ToLowerInvariant().Contains(search) ||
                   ci.TopCategory.ToLowerInvariant().Contains(search) ||
                   ci.SubCategory.ToLowerInvariant().Contains(search) ||
                   ci.Notes.ToLowerInvariant().Contains(search) ||
                   ci.Usage.ToLowerInvariant().Contains(search);
        };

        CollectionViewSource.GetDefaultView(LstCommands.ItemsSource).Refresh();
    }

    private void ToggleCommandGroups_Click(object sender, RoutedEventArgs e)
    {
        _commandGroupsExpanded = !_commandGroupsExpanded;
        SetCommandGroupExpandState(LstCommands, _commandGroupsExpanded);

        if (BtnToggleCommandGroups != null)
        {
            BtnToggleCommandGroups.Content = _commandGroupsExpanded ? "Collapse all" : "Expand all";
            BtnToggleCommandGroups.ToolTip = _commandGroupsExpanded ? "Collapse all command groups" : "Expand all command groups";
        }
    }

    private void LegendToggle_Click(object sender, RoutedEventArgs e)
    {
        if (LegendExpander == null)
            return;

        LegendExpander.IsExpanded = !LegendExpander.IsExpanded;
    }

    private void LegendExpander_Expanded(object sender, RoutedEventArgs e)
    {
        if (BtnLegendToggle != null)
        {
            BtnLegendToggle.Content = "▾";
            BtnLegendToggle.ToolTip = "Collapse legend";
        }
    }

    private void LegendExpander_Collapsed(object sender, RoutedEventArgs e)
    {
        if (BtnLegendToggle != null)
        {
            BtnLegendToggle.Content = "▸";
            BtnLegendToggle.ToolTip = "Expand legend";
        }
    }

    private static void SetCommandGroupExpandState(DependencyObject container, bool isExpanded)
    {
        if (container == null)
            return;

        foreach (var expander in FindVisualChildren<Expander>(container))
        {
            expander.IsExpanded = isExpanded;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null)
            yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T typedChild)
                yield return typedChild;

            foreach (var childOfChild in FindVisualChildren<T>(child))
                yield return childOfChild;
        }
    }

    private void LstCommands_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstCommands.SelectedItem is CommandItem item)
        {
            _selectedTopCategory = item.TopCategory;
            PopulateCategoryLegend();

            DetailPanel.Visibility = Visibility.Visible;
            DetailName.Text = item.Command;
            DetailDescription.Text = item.Description;
            DetailUsage.Text = string.IsNullOrWhiteSpace(item.Usage)
                ? $"Usage: {item.Command} [options]"
                : $"Usage: {item.Usage}";
            DetailOptions.Text = string.IsNullOrWhiteSpace(item.Options)
                ? "No options available"
                : item.Options;
            DetailNotes.Text = string.IsNullOrWhiteSpace(item.Notes)
                ? "No notes available"
                : item.Notes;
        }
        else
        {
            _selectedTopCategory = string.Empty;
            PopulateCategoryLegend();
            DetailPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async void ExecuteCommand_Click(object sender, RoutedEventArgs e)
    {
        if (LstCommands.SelectedItem is not CommandItem item)
            return;

        try
        {
            if (Application.Current.MainWindow?.DataContext is not MainViewModel vm)
                return;

            await vm.ExecuteCommandAsync(item.Command);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to execute command: {ex.Message}", "Command Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CopySelectedHelp_Click(object sender, RoutedEventArgs e)
    {
        if (LstCommands.SelectedItem is not CommandItem item)
            return;

        var text = string.Join(Environment.NewLine,
            item.Command,
            item.Usage,
            item.Options,
            item.Notes).Trim();

        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            // ignore clipboard failures
        }
    }

    private void PasteSelectedCommand_Click(object sender, RoutedEventArgs e)
    {
        if (LstCommands.SelectedItem is not CommandItem item)
            return;

        if (Application.Current.MainWindow?.DataContext is ViewModels.MainViewModel vm)
            vm.InputCommand = item.Command;
    }

    // Quick Wizard handlers
    private async void WizardDetectCard_Click(object sender, RoutedEventArgs e) 
    { 
        await ExecuteWizardCommand("hf search"); 
    }
    
    private async void WizardDumpMifare_Click(object sender, RoutedEventArgs e) 
    { 
        await ExecuteWizardCommand("hf mf dump"); 
    }
    
    private void WizardCrackKeys_Click(object sender, RoutedEventArgs e) 
    { 
        if (Application.Current.MainWindow?.DataContext is ViewModels.MainViewModel vm)
        {
            // Navigate to KeyCrackingView
            for (int i = 0; i < vm.NavigationItems.Count; i++)
            {
                if (vm.NavigationItems[i].Label == "Key Cracking Suite")
                {
                    vm.SelectPanelCommand.Execute(vm.NavigationItems[i]);
                    break;
                }
            }
        }
    }
    
    private void WizardCloneCard_Click(object sender, RoutedEventArgs e) 
    { 
        if (Application.Current.MainWindow?.DataContext is ViewModels.MainViewModel vm)
        {
            // Navigate to MagicCardView (card cloning)
            for (int i = 0; i < vm.NavigationItems.Count; i++)
            {
                if (vm.NavigationItems[i].Label == "Magic Card")
                {
                    vm.SelectPanelCommand.Execute(vm.NavigationItems[i]);
                    break;
                }
            }
        }
    }

    private async Task ExecuteWizardCommand(string command)
    {
        try
        {
            if (Application.Current.MainWindow?.DataContext is not ViewModels.MainViewModel vm)
                return;

            await vm.ExecuteCommandAsync(command);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Wizard failed: {ex.Message}", "Wizard Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private class CategoryLegendItem
    {
        public string Category { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }
}

/// <summary>
/// Simple display model for a command list item.
/// </summary>
public class CommandItem
{
    public string Command { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IsOffline { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TopCategory { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
    public string Usage { get; set; } = string.Empty;
    public string Options { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
