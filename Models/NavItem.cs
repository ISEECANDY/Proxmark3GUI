using System.Windows;

namespace Proxmark3GUI.Models;

/// <summary>
/// Describes a single item in the sidebar navigation panel.
/// </summary>
public class NavItem
{
    public int Id { get; }

    /// <summary>Icon name or Glyph key for the nav icon.</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>Display label shown in the sidebar.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>URI of the view to navigate to when this item is selected.</summary>
    public Uri? ViewUri { get; init; }

    /// <summary>True when this item is the currently active navigation target.</summary>
    public bool IsActive { get; set; }

    /// <summary>Title text shown in the content panel header.</summary>
    public string PanelTitle { get; init; } = string.Empty;

    /// <summary>Optional subtitle / description shown under the panel title.</summary>
    public string PanelSubtitle { get; init; } = string.Empty;

    /// <summary>
    /// Creates a new <see cref="NavItem"/>.
    /// </summary>
    public NavItem(int id, string icon, string label, string viewUri, string panelTitle, string panelSubtitle = "")
    {
        Id = id;
        Icon = icon;
        Label = label;
        ViewUri = new Uri(viewUri, UriKind.RelativeOrAbsolute);
        PanelTitle = panelTitle;
        PanelSubtitle = panelSubtitle;
    }
}
