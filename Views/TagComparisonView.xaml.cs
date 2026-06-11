using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Proxmark3GUI.Views;

public partial class TagComparisonView : UserControl
{
    public ObservableCollection<BlockCompareEntry> BlockComparison { get; } = new();
    public ObservableCollection<KeyCompareEntry> KeyComparison { get; } = new();

    public TagComparisonView()
    {
        InitializeComponent();
        LvBlockComparison.ItemsSource = BlockComparison;
        LvKeyComparison.ItemsSource = KeyComparison;
    }

    private void BtnBrowseTagA_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Tag dumps (*.bin;*.eml;*.json;*.mfd)|*.bin;*.eml;*.json;*.mfd|All files (*.*)|*.*",
            Title = "Select Tag A Dump"
        };
        if (dialog.ShowDialog() == true)
            TxtTagAPath.Text = dialog.FileName;
    }

    private void BtnBrowseTagB_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Tag dumps (*.bin;*.eml;*.json;*.mfd)|*.bin;*.eml;*.json;*.mfd|All files (*.*)|*.*",
            Title = "Select Tag B Dump"
        };
        if (dialog.ShowDialog() == true)
            TxtTagBPath.Text = dialog.FileName;
    }

    private void BtnSwap_Click(object sender, RoutedEventArgs e)
    {
        (TxtTagAPath.Text, TxtTagBPath.Text) = (TxtTagBPath.Text, TxtTagAPath.Text);
        TxtStatus.Text = "Swapped Tag A and Tag B.";
    }

    private void BtnCompare_Click(object sender, RoutedEventArgs e)
    {
        var pathA = TxtTagAPath.Text.Trim();
        var pathB = TxtTagBPath.Text.Trim();

        if (!File.Exists(pathA))
        {
            MessageBox.Show("Tag A file not found.", "File Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!File.Exists(pathB))
        {
            MessageBox.Show("Tag B file not found.", "File Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BlockComparison.Clear();
        KeyComparison.Clear();

        var dataA = File.ReadAllBytes(pathA);
        var dataB = File.ReadAllBytes(pathB);

        int maxBlocks = Math.Max(dataA.Length, dataB.Length) / 16;
        int matching = 0;
        int different = 0;

        for (int i = 0; i < maxBlocks && i < 64; i++)
        {
            bool hasA = (i * 16 + 16) <= dataA.Length;
            bool hasB = (i * 16 + 16) <= dataB.Length;

            var entry = new BlockCompareEntry
            {
                BlockNum = i.ToString()
            };

            if (hasA)
                entry.DataA = BitConverter.ToString(dataA, i * 16, Math.Min(16, dataA.Length - i * 16));
            else
                entry.DataA = "-";

            if (hasB)
                entry.DataB = BitConverter.ToString(dataB, i * 16, Math.Min(16, dataB.Length - i * 16));
            else
                entry.DataB = "-";

            if (!hasA || !hasB)
            {
                entry.Status = "Missing";
            }
            else if (entry.DataA == entry.DataB)
            {
                entry.Status = "Match";
                matching++;
            }
            else
            {
                entry.Status = "Different";
                different++;
            }

            BlockComparison.Add(entry);
        }

        // Generate key comparison for sector trailer blocks
        int keyAMatch = 0, keyBMatch = 0, keyTotal = 0;
        for (int sector = 0; sector < 16; sector++)
        {
            int trailerBlock = sector switch
            {
                <= 15 => sector * 4 + 3,
                _ => 0
            };

            if (trailerBlock >= maxBlocks) break;

            var keyEntry = new KeyCompareEntry { Sector = sector.ToString() };

            if (trailerBlock * 16 + 16 <= dataA.Length)
            {
                keyEntry.KeyATagA = BitConverter.ToString(dataA, trailerBlock * 16, 6).Replace("-", "");
                keyEntry.KeyBTagA = BitConverter.ToString(dataA, trailerBlock * 16 + 10, 6).Replace("-", "");
            }
            if (trailerBlock * 16 + 16 <= dataB.Length)
            {
                keyEntry.KeyATagB = BitConverter.ToString(dataB, trailerBlock * 16, 6).Replace("-", "");
                keyEntry.KeyBTagB = BitConverter.ToString(dataB, trailerBlock * 16 + 10, 6).Replace("-", "");
            }

            keyEntry.KeyAMatch = keyEntry.KeyATagA == keyEntry.KeyATagB ? "✓" : "✗";
            keyEntry.KeyBMatch = keyEntry.KeyBTagA == keyEntry.KeyBTagB ? "✓" : "✗";

            keyTotal++;
            if (keyEntry.KeyAMatch == "✓") keyAMatch++;
            if (keyEntry.KeyBMatch == "✓") keyBMatch++;

            KeyComparison.Add(keyEntry);
        }

        // Update summary
        int total = matching + different;
        TxtTotalBlocks.Text = total.ToString();
        TxtMatchingBlocks.Text = matching.ToString();
        TxtDifferentBlocks.Text = different.ToString();
        TxtMatchPercent.Text = total > 0 ? $"{matching * 100 / total}%" : "0%";
        TxtKeyAMatch.Text = $"{keyAMatch}/{keyTotal}";
        TxtKeyBMatch.Text = $"{keyBMatch}/{keyTotal}";

        TxtStatus.Text = $"Comparison complete. {matching} matching, {different} different blocks.";
    }

    private void BtnExportDiff_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Export Diff Report"
        };
        if (dialog.ShowDialog() == true)
        {
            using var writer = new StreamWriter(dialog.FileName);
            writer.WriteLine("TAG COMPARISON REPORT");
            writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"Tag A: {TxtTagAPath.Text}");
            writer.WriteLine($"Tag B: {TxtTagBPath.Text}");
            writer.WriteLine();
            writer.WriteLine($"Total Blocks: {TxtTotalBlocks.Text}");
            writer.WriteLine($"Matching: {TxtMatchingBlocks.Text} ({TxtMatchPercent.Text})");
            writer.WriteLine($"Different: {TxtDifferentBlocks.Text}");
            writer.WriteLine();
            writer.WriteLine("BLOCK DIFFERENCES:");
            writer.WriteLine(new string('-', 90));

            foreach (var block in BlockComparison.Where(b => b.Status == "Different"))
            {
                writer.WriteLine($"Block {block.BlockNum}:");
                writer.WriteLine($"  Tag A: {block.DataA}");
                writer.WriteLine($"  Tag B: {block.DataB}");
                writer.WriteLine();
            }

            TxtStatus.Text = $"Diff report exported to {dialog.FileName}";
        }
    }
}

public class BlockCompareEntry
{
    public string BlockNum { get; set; } = "";
    public string DataA { get; set; } = "-";
    public string DataB { get; set; } = "-";
    public string Status { get; set; } = "";
}

public class KeyCompareEntry
{
    public string Sector { get; set; } = "";
    public string KeyATagA { get; set; } = "-";
    public string KeyATagB { get; set; } = "-";
    public string KeyAMatch { get; set; } = "";
    public string KeyBTagA { get; set; } = "-";
    public string KeyBTagB { get; set; } = "-";
    public string KeyBMatch { get; set; } = "";
}
