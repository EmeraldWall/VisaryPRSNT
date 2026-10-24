namespace Visary.Core.Themes;

/// <summary>Colours for the application chrome (title bar, ribbon, panes), not the slides.</summary>
public sealed record AppPalette(
    string Id,
    string Name,
    bool IsDark,
    string TitleBar,
    string TitleBarText,
    string TabStrip,
    string TabText,
    string TabSelectedText,
    string Ribbon,
    string RibbonText,
    string Accent,
    string AccentText,
    string AccentSoft,
    string Pane,
    string PaneAlt,
    string Border,
    string Canvas,
    string Text,
    string SubtleText,
    string Hover,
    string Selection);

public static class AppThemes
{
    /// <summary>PowerPoint's brand red-orange, used for the default theme.</summary>
    public const string PowerPointAccent = "#C43E1C";

    public static readonly IReadOnlyList<AppPalette> All =
    [
        new("powerpoint", "PowerPoint (colourful)", false,
            TitleBar: "#B7472A", TitleBarText: "#FFFFFF", TabStrip: "#B7472A", TabText: "#FBE3DC", TabSelectedText: "#B7472A",
            Ribbon: "#F3F2F1", RibbonText: "#252423", Accent: "#C43E1C", AccentText: "#FFFFFF", AccentSoft: "#FCE9E4",
            Pane: "#F3F2F1", PaneAlt: "#FFFFFF", Border: "#E1DFDD", Canvas: "#E6E6E6", Text: "#252423", SubtleText: "#605E5C",
            Hover: "#E1DFDD", Selection: "#C43E1C"),
        new("powerpoint-white", "PowerPoint (white)", false,
            TitleBar: "#FFFFFF", TitleBarText: "#252423", TabStrip: "#FFFFFF", TabText: "#424242", TabSelectedText: "#C43E1C",
            Ribbon: "#FFFFFF", RibbonText: "#252423", Accent: "#C43E1C", AccentText: "#FFFFFF", AccentSoft: "#FCE9E4",
            Pane: "#FAF9F8", PaneAlt: "#FFFFFF", Border: "#EDEBE9", Canvas: "#EDEBE9", Text: "#252423", SubtleText: "#605E5C",
            Hover: "#F3F2F1", Selection: "#C43E1C"),
        new("dark-gray", "Dark gray", true,
            TitleBar: "#323130", TitleBarText: "#F3F2F1", TabStrip: "#323130", TabText: "#C8C6C4", TabSelectedText: "#F1856A",
            Ribbon: "#3B3A39", RibbonText: "#F3F2F1", Accent: "#E8704F", AccentText: "#1B1A19", AccentSoft: "#4A2E26",
            Pane: "#292827", PaneAlt: "#323130", Border: "#484644", Canvas: "#201F1E", Text: "#F3F2F1", SubtleText: "#C8C6C4",
            Hover: "#484644", Selection: "#F1856A"),
        new("black", "Black", true,
            TitleBar: "#0A0A0A", TitleBarText: "#FFFFFF", TabStrip: "#0A0A0A", TabText: "#BDBDBD", TabSelectedText: "#FF8A65",
            Ribbon: "#1A1A1A", RibbonText: "#FFFFFF", Accent: "#FF7A50", AccentText: "#0A0A0A", AccentSoft: "#3A1E14",
            Pane: "#111111", PaneAlt: "#1A1A1A", Border: "#2E2E2E", Canvas: "#000000", Text: "#F5F5F5", SubtleText: "#A8A8A8",
            Hover: "#2A2A2A", Selection: "#FF8A65"),
        new("midnight", "Midnight", true,
            TitleBar: "#0F172A", TitleBarText: "#E2E8F0", TabStrip: "#0F172A", TabText: "#94A3B8", TabSelectedText: "#A5B4FC",
            Ribbon: "#1E293B", RibbonText: "#E2E8F0", Accent: "#818CF8", AccentText: "#0F172A", AccentSoft: "#272F57",
            Pane: "#111827", PaneAlt: "#1E293B", Border: "#334155", Canvas: "#0B1120", Text: "#E2E8F0", SubtleText: "#94A3B8",
            Hover: "#334155", Selection: "#A5B4FC"),
        new("ocean", "Ocean", false,
            TitleBar: "#0F6CBD", TitleBarText: "#FFFFFF", TabStrip: "#0F6CBD", TabText: "#D6E9FA", TabSelectedText: "#0F6CBD",
            Ribbon: "#F3F7FB", RibbonText: "#1B2A3A", Accent: "#0F6CBD", AccentText: "#FFFFFF", AccentSoft: "#DCEBFA",
            Pane: "#F3F7FB", PaneAlt: "#FFFFFF", Border: "#D8E3EE", Canvas: "#E4EBF2", Text: "#1B2A3A", SubtleText: "#5B6B7C",
            Hover: "#DCE7F2", Selection: "#0F6CBD"),
        new("forest", "Forest", false,
            TitleBar: "#2E6B4A", TitleBarText: "#FFFFFF", TabStrip: "#2E6B4A", TabText: "#D7EDE1", TabSelectedText: "#2E6B4A",
            Ribbon: "#F4F7F4", RibbonText: "#1D2B23", Accent: "#2E7D52", AccentText: "#FFFFFF", AccentSoft: "#DDEFE4",
            Pane: "#F4F7F4", PaneAlt: "#FFFFFF", Border: "#DAE4DD", Canvas: "#E5ECE7", Text: "#1D2B23", SubtleText: "#55665B",
            Hover: "#DFE9E2", Selection: "#2E7D52"),
        new("lavender", "Lavender", false,
            TitleBar: "#6B4FA0", TitleBarText: "#FFFFFF", TabStrip: "#6B4FA0", TabText: "#E7DEF6", TabSelectedText: "#6B4FA0",
            Ribbon: "#F7F5FB", RibbonText: "#2A2238", Accent: "#7B5CB8", AccentText: "#FFFFFF", AccentSoft: "#ECE5F8",
            Pane: "#F7F5FB", PaneAlt: "#FFFFFF", Border: "#E3DCF0", Canvas: "#ECE8F3", Text: "#2A2238", SubtleText: "#6A5F7D",
            Hover: "#E9E2F5", Selection: "#7B5CB8"),
        new("graphite-gold", "Graphite and gold", true,
            TitleBar: "#1C1C1E", TitleBarText: "#F2E6C9", TabStrip: "#1C1C1E", TabText: "#B8AE98", TabSelectedText: "#E3B65B",
            Ribbon: "#2A2A2D", RibbonText: "#F2EEE6", Accent: "#D9A441", AccentText: "#1C1C1E", AccentSoft: "#3D3424",
            Pane: "#1F1F22", PaneAlt: "#2A2A2D", Border: "#3A3A3E", Canvas: "#141416", Text: "#F2EEE6", SubtleText: "#ADA79C",
            Hover: "#38383C", Selection: "#E3B65B"),
    ];

    public static AppPalette Get(string? id) => All.FirstOrDefault(p => p.Id == id) ?? All[0];
}
