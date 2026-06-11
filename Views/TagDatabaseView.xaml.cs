using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Proxmark3GUI.Services;
using Proxmark3GUI.ViewModels;

namespace Proxmark3GUI.Views;

public partial class TagDatabaseView : UserControl
{
    public ObservableCollection<TagItem> TagItems { get; } = new();

    public TagDatabaseView()
    {
        InitializeComponent();
        LstTags.ItemsSource = TagItems;
        UpdateStatusCounts();
        this.Loaded += TagDatabaseView_Loaded;
    }

    private void TagDatabaseView_Loaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to real-time tag scanning updates
        if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
        {
            vm.CommandOutputReceived += (s, output) =>
            {
                if (output.CommandType == "searchHF" || output.CommandType == "searchLF")
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        if (TxtScanLog != null)
                        {
                            TxtScanLog.AppendText(output.Line + Environment.NewLine);
                            TxtScanLog.ScrollToEnd();
                        }
                    });
                }
            };
        }
    }

    private async void BtnScanTags_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Application.Current.MainWindow?.DataContext is not MainViewModel vm)
                return;

            MessageBox.Show("Hold a tag near the Proxmark3 antenna.\n\nClick OK when ready to scan.",
                "Ready to Scan", MessageBoxButton.OK, MessageBoxImage.Information);

            // Execute hf search to detect and identify nearby tags
            var result = await vm.ExecuteCommandAsync("hf search");

            if (!string.IsNullOrWhiteSpace(result))
            {
                // Parse result to extract tag information
                var uid = ExtractTagInfo(result, "UID");
                var tagType = ExtractTagInfo(result, "Type");
                var atqa = ExtractTagInfo(result, "ATQA");
                var sak = ExtractTagInfo(result, "SAK");

                if (!string.IsNullOrWhiteSpace(uid))
                {
                    // Check if tag already exists
                    if (TagItems.Any(t => t.Uid.Equals(uid, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageBox.Show($"Tag {uid} already in database.", "Duplicate",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var newTag = new TagItem
                    {
                        Uid = uid.ToUpper(),
                        TagType = tagType ?? "Unknown",
                        Atqa = atqa ?? "--",
                        Sak = sak ?? "--",
                        DateScanned = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                        KeysFound = "0/2",
                        Notes = "Scanned from device"
                    };

                    TagItems.Add(newTag);
                    UpdateStatusCounts();
                    MessageBox.Show($"Tag {uid} ({tagType}) added to database.",
                        "Scan Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("No tag detected. Please try again.",
                        "No Tag Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Scan failed: {ex.Message}", "Scan Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string? ExtractTagInfo(string output, string infoType)
    {
        // Simple extraction - in real implementation would parse more robustly
        var lines = output.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Contains(infoType, System.StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(new[] { ':', '=' }, System.StringSplitOptions.None);
                if (parts.Length > 1)
                    return parts[1].Trim();
            }
        }
        return null;
    }

    private void UpdateStatusCounts()
    {
        if (TxtTotalCount == null || TxtFilteredCount == null || LstTags == null)
            return;

        TxtTotalCount.Text = TagItems.Count.ToString();
        TxtFilteredCount.Text = LstTags.Items.Count.ToString();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || TxtSearch == null)
            return;

        ApplyFilter();
    }

    private void CmbFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || CmbFilterType == null)
            return;

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (TxtSearch == null || CmbFilterType == null || LstTags == null)
            return;

        var search = TxtSearch.Text?.ToLower() ?? "";
        var typeFilter = (CmbFilterType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Types";

        LstTags.Items.Filter = _ =>
        {
            if (_ is TagItem item)
            {
                var matchesSearch = string.IsNullOrEmpty(search) ||
                                    item.Uid.ToLower().Contains(search) ||
                                    item.Notes.ToLower().Contains(search) ||
                                    item.TagType.ToLower().Contains(search);

                var matchesType = typeFilter == "All Types" || item.TagType.Contains(typeFilter);

                return matchesSearch && matchesType;
            }
            return false;
        };

        UpdateStatusCounts();
    }

    private void LstTags_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstTags.SelectedItem is TagItem item)
        {
            // Show expanded tag details (ATS, found keys, dump reference)
            MessageBox.Show($"Tag Details:\nUID: {item.Uid}\nType: {item.TagType}\nATQA: {item.Atqa}\nSAK: {item.Sak}\nKeys Found: {item.KeysFound}",
                "Tag Details", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnAddTag_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("To add a tag: scan a new tag with the Proxmark3 device,\nor use the Tag Editor tool to manually create entries.",
            "Add Tag", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (LstTags.SelectedItem is TagItem item)
        {
            TagItems.Remove(item);
            UpdateStatusCounts();
        }
    }

    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Delete all tag entries?", "Confirm",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            TagItems.Clear();
            UpdateStatusCounts();
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json|CSV (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Export Tag Database"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var ext = System.IO.Path.GetExtension(dialog.FileName).ToLower();
                
                if (ext == ".json")
                {
                    var data = new { tags = TagItems, exportDate = System.DateTime.Now };
                    var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(dialog.FileName, json);
                }
                else // CSV
                {
                    var lines = new System.Collections.Generic.List<string>();
                    lines.Add("UID,TagType,ATQA,SAK,DateScanned,KeysFound,Notes");
                    foreach (var tag in TagItems)
                        lines.Add($"\"{tag.Uid}\",\"{tag.TagType}\",\"{tag.Atqa}\",\"{tag.Sak}\",\"{tag.DateScanned}\",\"{tag.KeysFound}\",\"{tag.Notes}\"");
                    System.IO.File.WriteAllLines(dialog.FileName, lines);
                }
                
                MessageBox.Show($"Successfully exported {TagItems.Count} tags to {System.IO.Path.GetFileName(dialog.FileName)}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error exporting tags: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Context menu handlers
    private void MenuCopyUid_Click(object sender, RoutedEventArgs e)
    {
        if (LstTags.SelectedItem is TagItem item)
        {
            try { Clipboard.SetText(item.Uid); } catch { /* ignore */ }
        }
    }

    private void MenuViewDump_Click(object sender, RoutedEventArgs e)
    {
        if (LstTags.SelectedItem is TagItem item)
        {
            // TODO: Navigate to Dump Explorer with tag's dump file
        }
    }

    private void MenuExport_Click(object sender, RoutedEventArgs e)
    {
        BtnExport_Click(sender, e);
    }

    private void MenuDelete_Click(object sender, RoutedEventArgs e)
    {
        BtnDeleteSelected_Click(sender, e);
    }
}

/// <summary>
/// Display model for a tag entry in the database.
/// </summary>
public class TagItem
{
    public string Uid { get; set; } = string.Empty;
    public string TagType { get; set; } = string.Empty;
    public string Atqa { get; set; } = string.Empty;
    public string Sak { get; set; } = string.Empty;
    public string DateScanned { get; set; } = string.Empty;
    public string KeysFound { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
