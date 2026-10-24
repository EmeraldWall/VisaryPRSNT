using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Visary.Core.Themes;
using Windows.UI;

namespace Visary.App.Shell;

/// <summary>
/// Shared brushes for the application chrome. Changing a theme recolours these brush objects
/// in place, so every control that uses them updates live without being rebuilt.
/// </summary>
public static class Palette
{
    public static AppPalette Current { get; private set; } = AppThemes.All[0];

    public static readonly SolidColorBrush TitleBar = new();
    public static readonly SolidColorBrush TitleBarText = new();
    public static readonly SolidColorBrush TabStrip = new();
    public static readonly SolidColorBrush TabText = new();
    public static readonly SolidColorBrush TabSelectedText = new();
    public static readonly SolidColorBrush Ribbon = new();
    public static readonly SolidColorBrush RibbonText = new();
    public static readonly SolidColorBrush Accent = new();
    public static readonly SolidColorBrush AccentText = new();
    public static readonly SolidColorBrush AccentSoft = new();
    public static readonly SolidColorBrush Pane = new();
    public static readonly SolidColorBrush PaneAlt = new();
    public static readonly SolidColorBrush Border = new();
    public static readonly SolidColorBrush Canvas = new();
    public static readonly SolidColorBrush Text = new();
    public static readonly SolidColorBrush SubtleText = new();
    public static readonly SolidColorBrush Hover = new();
    public static readonly SolidColorBrush Selection = new();
    public static readonly SolidColorBrush Transparent = new(Microsoft.UI.Colors.Transparent);

    public static event Action? Changed;

    static Palette() => Apply(AppThemes.All[0]);

    public static void Apply(AppPalette p)
    {
        Current = p;
        TitleBar.Color = ColorUtil.Parse(p.TitleBar);
        TitleBarText.Color = ColorUtil.Parse(p.TitleBarText);
        TabStrip.Color = ColorUtil.Parse(p.TabStrip);
        TabText.Color = ColorUtil.Parse(p.TabText);
        TabSelectedText.Color = ColorUtil.Parse(p.TabSelectedText);
        Ribbon.Color = ColorUtil.Parse(p.Ribbon);
        RibbonText.Color = ColorUtil.Parse(p.RibbonText);
        Accent.Color = ColorUtil.Parse(p.Accent);
        AccentText.Color = ColorUtil.Parse(p.AccentText);
        AccentSoft.Color = ColorUtil.Parse(p.AccentSoft);
        Pane.Color = ColorUtil.Parse(p.Pane);
        PaneAlt.Color = ColorUtil.Parse(p.PaneAlt);
        Border.Color = ColorUtil.Parse(p.Border);
        Canvas.Color = ColorUtil.Parse(p.Canvas);
        Text.Color = ColorUtil.Parse(p.Text);
        SubtleText.Color = ColorUtil.Parse(p.SubtleText);
        Hover.Color = ColorUtil.Parse(p.Hover);
        Selection.Color = ColorUtil.Parse(p.Selection);

        // Tint WinUI's own accent coloured controls (toggle switches, sliders, focus) to match.
        var res = Application.Current?.Resources;
        if (res != null && Environment.GetEnvironmentVariable("VISARY_NOACCENT") != "1")
        {
            var accent = ColorUtil.Parse(p.Accent);
            foreach (var key in new[] { "SystemAccentColor", "SystemAccentColorLight1", "SystemAccentColorLight2", "SystemAccentColorDark1", "SystemAccentColorDark2" })
                res[key] = accent;
            res["AccentFillColorDefaultBrush"] = new SolidColorBrush(accent);
            res["AccentFillColorSecondaryBrush"] = new SolidColorBrush(ColorUtil.WithAlpha(accent, 0xE6));
            res["AccentFillColorTertiaryBrush"] = new SolidColorBrush(ColorUtil.WithAlpha(accent, 0xCC));
        }
        Changed?.Invoke();
    }

    public static ElementTheme ElementTheme => Current.IsDark ? ElementTheme.Dark : ElementTheme.Light;
}

public static class ColorUtil
{
    /// <summary>Parses "#RRGGBB", "#AARRGGBB" or "#RGB". Invalid input gives black.</summary>
    public static Color Parse(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return Microsoft.UI.Colors.Black;
        var h = s.Trim().TrimStart('#');
        try
        {
            if (h.Length == 3) h = string.Concat(h.Select(c => $"{c}{c}"));
            if (h.Length == 6) return Color.FromArgb(255, Convert.ToByte(h[..2], 16), Convert.ToByte(h[2..4], 16), Convert.ToByte(h[4..6], 16));
            if (h.Length == 8) return Color.FromArgb(Convert.ToByte(h[..2], 16), Convert.ToByte(h[2..4], 16), Convert.ToByte(h[4..6], 16), Convert.ToByte(h[6..8], 16));
        }
        catch (FormatException)
        {
        }
        return Microsoft.UI.Colors.Black;
    }

    public static string ToHex(Color c) => c.A == 255 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    public static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    public static SolidColorBrush Brush(string? s) => new(Parse(s));
}
