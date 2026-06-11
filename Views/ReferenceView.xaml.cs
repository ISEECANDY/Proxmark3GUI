using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace Proxmark3GUI.Views;

public partial class ReferenceView : UserControl
{
    public ReferenceView()
    {
        InitializeComponent();
        LoadMifareMemoryMap();
    }

    private void LoadMifareMemoryMap()
    {
        var items = new ObservableCollection<MifareMemoryRow>();

        for (int sector = 0; sector < 16; sector++)
        {
            var blocks = sector == 0 ? 4 : (sector >= 32 && sector <= 39 ? 16 : 4);

            items.Add(new MifareMemoryRow
            {
                Sector = sector.ToString(),
                Block0 = $"Block {sector * 4}: Data / Manufacturer",
                Block1 = $"Block {sector * 4 + 1}: Data",
                Block2 = $"Block {sector * 4 + 2}: Data",
                Block3 = $"Block {sector * 4 + 3}: Sector Trailer (Key A | Access | Key B)"
            });
        }

        MifareMemoryMap.ItemsSource = items;
    }
}

/// <summary>
/// Row model for the MIFARE Classic memory map table.
/// </summary>
public class MifareMemoryRow
{
    public string Sector { get; set; } = string.Empty;
    public string Block0 { get; set; } = string.Empty;
    public string Block1 { get; set; } = string.Empty;
    public string Block2 { get; set; } = string.Empty;
    public string Block3 { get; set; } = string.Empty;
}
