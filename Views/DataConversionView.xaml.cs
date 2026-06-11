using System.Windows;
using System.Windows.Controls;

namespace Proxmark3GUI.Views;

public partial class DataConversionView : UserControl
{
    public DataConversionView()
    {
        InitializeComponent();
    }

    private void BtnBrowseSource_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Tag dump files (*.bin;*.eml;*.json;*.mfd;*.trace)|*.bin;*.eml;*.json;*.mfd;*.trace|All files (*.*)|*.*",
            Title = "Select Source File"
        };
        if (dialog.ShowDialog() == true)
        {
            TxtSourcePath.Text = dialog.FileName;
            AutoDetectSourceFormat(dialog.FileName);
            PreviewSourceFile(dialog.FileName);
        }
    }

    private void BtnBrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Tag dump files (*.bin;*.eml;*.json;*.mfd;*.trace)|*.bin;*.eml;*.json;*.mfd;*.trace",
            Title = "Select Target File"
        };
        if (dialog.ShowDialog() == true)
        {
            TxtTargetPath.Text = dialog.FileName;
        }
    }

    private void AutoDetectSourceFormat(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLower();
        var formatMap = new Dictionary<string, int>
        {
            { ".bin", 0 },
            { ".eml", 1 },
            { ".json", 2 },
            { ".mfd", 3 },
            { ".trace", 4 }
        };

        if (formatMap.TryGetValue(ext, out int idx))
        {
            CmbSourceFormat.SelectedIndex = idx;
        }
    }

    private void PreviewSourceFile(string path)
    {
        try
        {
            var bytes = System.IO.File.ReadAllBytes(path);
            var previewBytes = bytes.Take(256).ToArray();
            var hexLines = new System.Text.StringBuilder();

            for (int i = 0; i < previewBytes.Length; i += 16)
            {
                var offset = i.ToString("X4");
                var hex = string.Join(" ", previewBytes.Skip(i).Take(16).Select(b => b.ToString("X2")));
                var ascii = new string(previewBytes.Skip(i).Take(16).Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
                hexLines.AppendLine($"{offset}: {hex,-48} {ascii}");
            }

            TxtPreview.Text = hexLines.ToString();
            TxtStatus.Text = $"Loaded {bytes.Length} bytes from {System.IO.Path.GetFileName(path)}";
            TxtConvLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Opened source file: {path} ({bytes.Length} bytes)\n");
        }
        catch (Exception ex)
        {
            TxtPreview.Text = $"Error reading file: {ex.Message}";
            TxtStatus.Text = $"Error: {ex.Message}";
        }
    }

    private void BtnConvert_Click(object sender, RoutedEventArgs e)
    {
        var sourcePath = TxtSourcePath.Text.Trim();
        var targetPath = TxtTargetPath.Text.Trim();
        var sourceFormat = (CmbSourceFormat.SelectedItem as ComboBoxItem)?.Content as string ?? "Bin";
        var targetFormat = (CmbTargetFormat.SelectedItem as ComboBoxItem)?.Content as string ?? "EML";

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            MessageBox.Show("Please select a source file.", "No Source File",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!System.IO.File.Exists(sourcePath))
        {
            MessageBox.Show($"Source file not found: {sourcePath}", "File Not Found",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            MessageBox.Show("Please select a target file.", "No Target File",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (sourceFormat == targetFormat)
        {
            MessageBox.Show("Source and target formats are the same.", "Same Format",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        TxtStatus.Text = $"Converting {sourceFormat} to {targetFormat}...";
        ProgressConvert.Value = 10;
        TxtConvLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Starting conversion: {sourceFormat} -> {targetFormat}\n");

        try
        {
            byte[] data = System.IO.File.ReadAllBytes(sourcePath);
            ProgressConvert.Value = 30;
            TxtConvLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Read {data.Length} bytes from source\n");

            byte[] outputData = ConvertData(data, sourceFormat, targetFormat);
            ProgressConvert.Value = 70;
            TxtConvLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Converted to {targetFormat} format\n");

            System.IO.File.WriteAllBytes(targetPath, outputData);
            ProgressConvert.Value = 100;

            TxtConvLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Conversion complete: {targetPath}\n");
            TxtStatus.Text = $"✓ Conversion complete: {sourceFormat} -> {targetFormat}";
            MessageBox.Show($"Successfully converted {sourceFormat} to {targetFormat}.\n\nOutput: {targetPath}",
                "Conversion Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (System.Exception ex)
        {
            TxtConvLog.AppendText($"[ERROR] Conversion failed: {ex.Message}\n");
            TxtStatus.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Conversion failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            ProgressConvert.Value = 0;
        }
    }

    private byte[] ConvertData(byte[] data, string fromFormat, string toFormat)
    {
        // Convert to intermediate HEX format first
        var hexData = ConvertToHex(data, fromFormat);
        TxtConvLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Intermediate hex: {hexData.Length} bytes\n");
        
        // Then convert from HEX to target format
        return ConvertFromHex(hexData, toFormat);
    }

    private byte[] ConvertToHex(byte[] data, string sourceFormat)
    {
        return sourceFormat.ToLower() switch
        {
            "bin" => data, // Already binary
            "eml" => ParseEmlFormat(data),
            "json" => ParseJsonFormat(data),
            "mfd" => data.Skip(4).Take(data.Length - 4).ToArray(), // Skip MFD header
            "trace" => data, // Trace format is binary
            _ => data
        };
    }

    private byte[] ConvertFromHex(byte[] hexData, string targetFormat)
    {
        return targetFormat.ToLower() switch
        {
            "bin" => hexData,
            "eml" => WriteEmlFormat(hexData),
            "json" => WriteJsonFormat(hexData),
            "mfd" => AddMfdHeader(hexData),
            "trace" => hexData,
            _ => hexData
        };
    }

    private byte[] ParseEmlFormat(byte[] data)
    {
        var text = System.Text.Encoding.ASCII.GetString(data);
        var lines = text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        var result = new System.Collections.Generic.List<byte>();
        
        foreach (var line in lines)
        {
            var cleaned = line.Trim();
            if (cleaned.Length % 2 == 0 && cleaned.All(c => "0123456789ABCDEFabcdef".Contains(c)))
            {
                for (int i = 0; i < cleaned.Length; i += 2)
                    result.Add(System.Convert.ToByte(cleaned.Substring(i, 2), 16));
            }
        }
        return result.ToArray();
    }

    private byte[] ParseJsonFormat(byte[] data)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(
                System.Text.Encoding.UTF8.GetString(data));
            if (json?.ContainsKey("data") == true && json["data"] is System.Text.Json.JsonElement elem && elem.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var hexStr = elem.GetString() ?? "";
                var result = new System.Collections.Generic.List<byte>();
                for (int i = 0; i < hexStr.Length; i += 2)
                    if (i + 1 < hexStr.Length)
                        result.Add(System.Convert.ToByte(hexStr.Substring(i, 2), 16));
                return result.ToArray();
            }
        }
        catch { }
        return data;
    }

    private byte[] WriteEmlFormat(byte[] data)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < data.Length; i += 16)
        {
            for (int j = 0; j < 16 && i + j < data.Length; j++)
                sb.Append(data[i + j].ToString("X2"));
            sb.AppendLine();
        }
        return System.Text.Encoding.ASCII.GetBytes(sb.ToString());
    }

    private byte[] WriteJsonFormat(byte[] data)
    {
        var hexStr = string.Concat(data.Select(b => b.ToString("X2")));
        var json = System.Text.Json.JsonSerializer.Serialize(new { data = hexStr, length = data.Length });
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    private byte[] AddMfdHeader(byte[] data)
    {
        var result = new byte[data.Length + 4];
        System.Buffer.BlockCopy(new byte[] { 0x00, 0x00, 0x00, 0x00 }, 0, result, 0, 4); // MFD magic
        System.Buffer.BlockCopy(data, 0, result, 4, data.Length);
        return result;
    }
}
