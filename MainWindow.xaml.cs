using System.Windows;
using System.Windows.Input;
using Proxmark3GUI.Models;
using Proxmark3GUI.ViewModels;

namespace Proxmark3GUI;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        vm.ConsoleLogReceived += (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                ConsoleOutput.ScrollToEnd();
            });
        };
    }

    private void NavBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is FrameworkElement fe && fe.DataContext is NavItem item)
        {
            vm.SelectPanelCommand.Execute(item);
        }
    }

    private void CommandInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainViewModel vm)
        {
            vm.SendCommandCommand.Execute(null);
        }
    }
}
