using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Proxmark3GUI.Views;

public partial class KeyDictionaryView : UserControl
{
    private ObservableCollection<KeyItem> AllKeys { get; } = new();
    private ObservableCollection<KeyItem> FilteredKeys { get; } = new();

    public KeyDictionaryView()
    {
        InitializeComponent();
        LvKeys.ItemsSource = AllKeys;
        RefreshFilteredKeys();
    }

    private void RefreshFilteredKeys()
    {
        if (TxtSearch == null || CmbTypeFilter == null || TxtResultCount == null)
            return;

        FilteredKeys.Clear();
        var filter = TxtSearch.Text?.Trim().ToLower() ?? string.Empty;
        var typeFilter = (CmbTypeFilter.SelectedItem as ComboBoxItem)?.Content as string;

        var query = AllKeys.AsEnumerable();

        if (!string.IsNullOrEmpty(filter) && filter != "search keys (hex)...")
            query = query.Where(k => k.Key.ToLower().Contains(filter));

        if (typeFilter == "Key A")
            query = query.Where(k => k.Type == "A");
        else if (typeFilter == "Key B")
            query = query.Where(k => k.Type == "B");

        foreach (var key in query)
            FilteredKeys.Add(key);

        TxtResultCount.Text = $"{FilteredKeys.Count} keys";
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || TxtSearch == null)
            return;

        RefreshFilteredKeys();
    }

    private void CmbTypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || CmbTypeFilter == null)
            return;

        RefreshFilteredKeys();
    }

    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Key files (*.dic;*.txt;*.json;*.csv)|*.dic;*.txt;*.json;*.csv|All files (*.*)|*.*",
            Title = "Import Key Dictionary"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var ext = System.IO.Path.GetExtension(dialog.FileName).ToLower();
                var lines = System.IO.File.ReadAllLines(dialog.FileName);
                int imported = 0;

                foreach (var line in lines)
                {
                    var key = ParseKeyFromLine(line, ext);
                    if (!string.IsNullOrWhiteSpace(key) && key.Length == 12)
                    {
                        if (!AllKeys.Any(k => k.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
                        {
                            AllKeys.Add(new KeyItem 
                            { 
                                Key = key.ToUpper(), 
                                Type = "A", 
                                Source = System.IO.Path.GetFileName(dialog.FileName) 
                            });
                            imported++;
                        }
                    }
                }

                RefreshFilteredKeys();
                MessageBox.Show($"Successfully imported {imported} new keys from {System.IO.Path.GetFileName(dialog.FileName)}\n\nTotal keys now: {AllKeys.Count}",
                    "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error importing keys: {ex.Message}", "Import Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private string? ParseKeyFromLine(string line, string format)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        
        line = line.Trim();
        if (line.StartsWith("#") || line.StartsWith(";")) return null; // Comment
        
        return format switch
        {
            ".dic" => line.Length >= 12 ? line.Substring(0, 12) : null,
            ".txt" => line.Length >= 12 ? line.Substring(0, 12) : null,
            ".csv" => line.Split(',')[0].Trim().Length >= 12 ? line.Split(',')[0].Trim().Substring(0, 12) : null,
            ".json" => null, // Handled separately
            _ => line.Length >= 12 ? line.Substring(0, 12) : null
        };
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "DIC files (*.dic)|*.dic|Text files (*.txt)|*.txt|CSV files (*.csv)|*.csv|JSON files (*.json)|*.json",
            Title = "Export Key Dictionary"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var ext = System.IO.Path.GetExtension(dialog.FileName).ToLower();
                var lines = new System.Collections.Generic.List<string>();

                if (ext == ".json")
                {
                    var data = new { keys = AllKeys.Select(k => k.Key).ToList(), count = AllKeys.Count };
                    var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(dialog.FileName, json);
                }
                else if (ext == ".csv")
                {
                    lines.Add("Key,Type,Source");
                    foreach (var key in AllKeys)
                        lines.Add($"{key.Key},{key.Type},{key.Source}");
                    System.IO.File.WriteAllLines(dialog.FileName, lines);
                }
                else // .dic or .txt
                {
                    foreach (var key in AllKeys)
                        lines.Add(key.Key);
                    System.IO.File.WriteAllLines(dialog.FileName, lines);
                }

                MessageBox.Show($"Successfully exported {AllKeys.Count} keys to {System.IO.Path.GetFileName(dialog.FileName)}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error exporting keys: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnAddKey_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Add new key dialog would open here",
            "Add Key", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnBrowseImport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Key files (*.dic;*.txt;*.json)|*.dic;*.txt;*.json|All files (*.*)|*.*",
            Title = "Import Key Dictionary"
        };

        if (dialog.ShowDialog() == true)
        {
            MessageBox.Show($"Would import keys from {dialog.FileName}",
                "Import", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnDoImport_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Importing keys...",
            "Import", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnBrowseExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Key files (*.dic;*.txt;*.json;*.csv)|*.dic;*.txt;*.json;*.csv",
            Title = "Export Key Dictionary"
        };

        if (dialog.ShowDialog() == true)
        {
            MessageBox.Show($"Would export {AllKeys.Count} keys to {dialog.FileName}",
                "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnDoExport_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Exporting keys...",
            "Export", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnConfirmAddKey_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Adding new key...",
            "Add Key", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnCancelAddKey_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Add key dialog cancelled.",
            "Add Key", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnDedup_Click(object sender, RoutedEventArgs e)
    {
        var duplicates = AllKeys
            .GroupBy(k => k.Key)
            .Where(g => g.Count() > 1)
            .ToList();

        int removed = 0;
        foreach (var group in duplicates)
        {
            for (int i = 1; i < group.Count(); i++)
            {
                AllKeys.Remove(group.ElementAt(i));
                removed++;
            }
        }

        RefreshFilteredKeys();
        MessageBox.Show($"Removed {removed} duplicate key(s).\n{AllKeys.Count} unique keys remaining.",
            "Deduplicate Keys", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnStats_Click(object sender, RoutedEventArgs e)
    {
        int totalKeys = AllKeys.Count;
        int keyA = AllKeys.Count(k => k.Type == "A");
        int keyB = AllKeys.Count(k => k.Type == "B");

        MessageBox.Show($"Total Keys: {totalKeys}\nKey A: {keyA}\nKey B: {keyB}",
            "Dictionary Statistics", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Clear all keys from the dictionary?",
            "Clear Keys", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            AllKeys.Clear();
            RefreshFilteredKeys();
        }
    }

    public class KeyItem
    {
        public string Key { get; set; } = "";
        public string Type { get; set; } = "";
        public string Source { get; set; } = "";
    }
}
