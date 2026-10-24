using Visary.Core.Model;

namespace Visary.Core.Themes;

/// <summary>Design theme for slides: colours, fonts and backgrounds.</summary>
public sealed record SlideTheme(
    string Id,
    string Name,
    string HeadingFont,
    string BodyFont,
    string Text,
    string TextMuted,
    string Surface,
    IReadOnlyList<string> Accents,
    Func<Background> ContentBackground,
    Func<Background> TitleBackground,
    string TitleText);

public static class SlideThemes
{
    // Every font here ships with Windows 10 and 11, so decks look the same on any PC.
    public static readonly IReadOnlyList<string> Fonts =
    [
        "Segoe UI", "Segoe UI Variable Display", "Segoe UI Variable Text", "Segoe UI Semibold", "Bahnschrift",
        "Calibri", "Cambria", "Candara", "Constantia", "Corbel", "Georgia", "Palatino Linotype", "Sitka Heading",
        "Sitka Text", "Cascadia Code", "Consolas", "Arial", "Times New Roman", "Verdana", "Trebuchet MS",
        "Gabriola", "Ink Free", "Segoe Print", "Impact",
    ];

    static Func<Background> Solid(string c) => () => new Background { Fill = FillSpec.Solid(c) };
    static Func<Background> Grad(string a, string b, double angle) => () => new Background { Fill = FillSpec.Gradient(a, b, angle) };

    public static readonly IReadOnlyList<SlideTheme> All =
    [
        new("office", "Office", "Segoe UI Variable Display", "Segoe UI", "#1F1F1F", "#605E5C", "#F3F2F1",
            ["#C43E1C", "#0F6CBD", "#2E7D52", "#D9A441", "#7B5CB8", "#038387"],
            Solid("#FFFFFF"), Solid("#FFFFFF"), "#1F1F1F"),
        new("ember", "Ember", "Segoe UI Variable Display", "Segoe UI Variable Text", "#FFFFFF", "#F6D5CB", "#FFFFFF33",
            ["#FFB59E", "#FFD27A", "#FFFFFF", "#FF8A65", "#FCE9E4", "#F7A072"],
            Grad("#7A2414", "#C43E1C", 30), Grad("#5E1A0E", "#D35230", 30), "#FFFFFF"),
        new("midnight", "Midnight", "Bahnschrift", "Segoe UI", "#F1F5F9", "#94A3B8", "#1E293B",
            ["#38BDF8", "#A78BFA", "#F472B6", "#34D399", "#FBBF24", "#FB7185"],
            Solid("#0F172A"), Grad("#0F172A", "#312E81", 35), "#FFFFFF"),
        new("aurora", "Aurora", "Segoe UI Variable Display", "Segoe UI Variable Text", "#0B1A2E", "#40566E", "#E6F2FB",
            ["#0F6CBD", "#00B7C3", "#8764B8", "#E3008C", "#FFB900", "#107C10"],
            Solid("#F7FBFF"), Grad("#0F3D70", "#00B7C3", 25), "#FFFFFF"),
        new("gallery", "Gallery", "Georgia", "Segoe UI", "#2B2B2B", "#6E6E6E", "#F2EEE8",
            ["#8C6D46", "#2F4F4F", "#A0522D", "#556B2F", "#7F6A93", "#B08D57"],
            Solid("#FBF8F3"), Solid("#F2EEE8"), "#2B2B2B"),
        new("noir", "Noir", "Segoe UI Variable Display", "Segoe UI Variable Text", "#F5F5F5", "#A3A3A3", "#1C1C1C",
            ["#FFFFFF", "#D9A441", "#9CA3AF", "#E8704F", "#6EE7B7", "#93C5FD"],
            Solid("#0A0A0A"), Grad("#000000", "#262626", 90), "#FFFFFF"),
        new("botanic", "Botanic", "Constantia", "Corbel", "#1D2B23", "#55665B", "#E7F0EA",
            ["#2E7D52", "#7BA05B", "#C9A227", "#3E6D8E", "#A35D3D", "#5B8C85"],
            Solid("#F6FAF7"), Grad("#1E4D36", "#2E7D52", 40), "#FFFFFF"),
        new("studio", "Studio", "Bahnschrift", "Segoe UI", "#18181B", "#52525B", "#F4F4F5",
            ["#7C3AED", "#DB2777", "#F59E0B", "#10B981", "#3B82F6", "#EF4444"],
            Solid("#FFFFFF"), Grad("#4C1D95", "#DB2777", 20), "#FFFFFF"),
        new("dune", "Dune", "Sitka Heading", "Sitka Text", "#3B2F2A", "#7A6A60", "#F3E9DD",
            ["#C27C4E", "#8A5A44", "#D9A441", "#6B7B5E", "#A0675C", "#5E6E7E"],
            Solid("#FBF5EE"), Grad("#E9C9A7", "#C27C4E", 45), "#3B2F2A"),
        new("ocean", "Ocean", "Segoe UI Semibold", "Segoe UI", "#0C2D48", "#4A6A85", "#E1EEF8",
            ["#0F6CBD", "#2BA3C4", "#14B8A6", "#6366F1", "#F97316", "#E11D48"],
            Solid("#F4F9FD"), Grad("#0C4A6E", "#0EA5E9", 120), "#FFFFFF"),
    ];

    public static SlideTheme Get(string? id) => All.FirstOrDefault(t => t.Id == id) ?? All[0];
}
