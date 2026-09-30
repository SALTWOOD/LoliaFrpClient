using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LoliaFrpClient.ViewModels;

/// <summary>底栏的一个导航项。</summary>
public sealed partial class NavItem : ObservableObject
{
    public NavItem(string tag, string title, string iconPath)
    {
        Tag = tag;
        Title = title;

        // Parsed once here rather than bound as a string: a string binding would need the
        // geometry type converter to run on every re-evaluation.
        Icon = Geometry.Parse(iconPath);
    }

    public string Tag { get; }

    public string Title { get; }

    /// <summary>图标几何。与桌面端导航用的是同一组路径,不依赖任何字体。</summary>
    public Geometry Icon { get; }

    [ObservableProperty] public partial bool IsSelected { get; set; }
}