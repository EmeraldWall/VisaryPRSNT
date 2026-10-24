using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Visary.App.Editor;
using Visary.App.Rendering;
using Visary.Core.Model;
using Visary.Core.Themes;

namespace Visary.App.Shell;

/// <summary>The File view: new, open, save, export and options, like PowerPoint's backstage.</summary>
public sealed partial class Backstage : Grid
{
    readonly MainWindow _w;
    readonly ScrollViewer _content = new() { Padding = new Thickness(40, 28, 40, 40) };
    readonly StackPanel _nav = new() { Spacing = 2, Padding = new Thickness(0, 12, 0, 12) };
    string _page;

    public Backstage(MainWindow w, string page)
    {
        _w = w;
        _page = page;
        Background = Palette.PaneAlt;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        ColumnDefinitions.Add(new ColumnDefinition());
        Children.Add(new Border { Background = Palette.TitleBar, Child = _nav });
        Grid.SetColumn(_content, 1);
        Children.Add(_content);
        Build();
    }

    void Build()
    {
        _nav.Children.Clear();
        var back = NavButton("", "Back", () => _w.HideBackstage(), false);
        _nav.Children.Add(back);
        foreach (var (id, glyph, label) in new[] { ("home", "", "Home"), ("new", "", "New"), ("open", "", "Open") })
            _nav.Children.Add(NavButton(glyph, label, () => Go(id), id == _page));
        _nav.Children.Add(NavButton("", "Save", async () => { if (await Commands.SaveAsync(_w.Editor)) _w.HideBackstage(); }, false));
        _nav.Children.Add(NavButton("", "Save as", async () => { if (await Commands.SaveAsync(_w.Editor, saveAs: true)) _w.HideBackstage(); }, false));
        _nav.Children.Add(NavButton("", "Export", () => Go("export"), _page == "export"));
        _nav.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(ColorUtil.WithAlpha(ColorUtil.Parse(Palette.Current.TitleBarText), 60)), Margin = new Thickness(16, 10, 16, 10) });
        _nav.Children.Add(NavButton("", "Options", () => Go("options"), _page == "options"));
        _content.Content = _page switch
        {
            "new" => NewPage(full: true),
            "open" => OpenPage(),
            "export" => ExportPage(),
            "options" => OptionsPage(),
            _ => HomePage(),
        };
    }

    void Go(string page)
    {
        _page = page;
        Build();
    }

    static Button NavButton(string glyph, string label, Action onClick, bool selected)
    {
        var b = new Button
        {
            Content = Ui.Row(Ui.Icon(glyph, 16, Palette.TitleBarText), new TextBlock { Text = label, FontSize = 14, Foreground = Palette.TitleBarText, Margin = new Thickness(8, 0, 0, 0) }),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = selected ? new SolidColorBrush(ColorUtil.WithAlpha(ColorUtil.Parse(Palette.Current.TitleBarText), 40)) : Palette.Transparent,
            BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(0), Padding = new Thickness(20, 10, 12, 10),
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    static TextBlock Heading(string t) => new() { Text = t, FontSize = 26, Foreground = Palette.Text, Margin = new Thickness(0, 0, 0, 18) };

    FrameworkElement HomePage()
    {
        var p = new StackPanel { Spacing = 18 };
        p.Children.Add(Heading("Good to see you"));
        p.Children.Add(NewPage(full: false));
        p.Children.Add(Ui.Text("Recent", 16, Palette.Text, bold: true));
        p.Children.Add(RecentList());
        return p;
    }

    FrameworkElement NewPage(bool full)
    {
        var p = new StackPanel { Spacing = 14 };
        if (full) p.Children.Add(Heading("New"));
        var wrap = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal, ItemWidth = 230, ItemHeight = 168 };
        var blank = Factory.CreatePresentation("office");
        wrap.Children.Add(Card(blank, "Blank presentation", () => _ = _w.NewAsync(Factory.CreatePresentation("office"))));
        foreach (var t in full ? SlideThemes.All : SlideThemes.All.Take(4))
        {
            var sample = Factory.CreatePresentation(t.Id, sample: true);
            wrap.Children.Add(Card(sample, t.Name, () => _ = _w.NewAsync(Factory.CreatePresentation(t.Id, sample: true))));
        }
        p.Children.Add(wrap);
        if (full)
        {
            p.Children.Add(Ui.Text("Blank in other sizes", 14, Palette.Text, bold: true));
            var sizes = Ui.Row();
            foreach (var s in Factory.Sizes)
            {
                var b = new Button { Content = s.Name, Margin = new Thickness(0, 0, 8, 0) };
                b.Click += (_, _) => _ = _w.NewAsync(Factory.CreatePresentation("office", s.Width, s.Height));
                sizes.Children.Add(b);
            }
            p.Children.Add(sizes);
        }
        else
        {
            var more = new HyperlinkButton { Content = "More themes" };
            more.Click += (_, _) => Go("new");
            p.Children.Add(more);
        }
        return p;
    }

    static Button Card(Presentation p, string label, Action onClick)
    {
        var thumb = new Border
        {
            Width = 214, Height = 120, BorderBrush = Palette.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Child = new Viewbox { Child = new SlideView(p, p.Slides[0], SlideViewMode.Thumbnail) },
        };
        var b = new Button
        {
            Content = new StackPanel { Spacing = 6, Children = { thumb, Ui.Text(label, 13, Palette.Text) } },
            Background = Palette.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(4),
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    FrameworkElement RecentList()
    {
        var list = new StackPanel { Spacing = 2 };
        var recent = Settings.Current.RecentFiles.Where(File.Exists).ToList();
        if (recent.Count == 0) list.Children.Add(Ui.Text("Presentations you open or save appear here.", 13, Palette.SubtleText));
        foreach (var path in recent)
        {
            var b = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Palette.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(10, 8, 10, 8),
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 12,
                    Children =
                    {
                        Ui.Icon("", 20, Palette.Accent),
                        new StackPanel { Children = { Ui.Text(Path.GetFileNameWithoutExtension(path), 13.5, Palette.Text), Ui.Text(Path.GetDirectoryName(path) ?? "", 11.5, Palette.SubtleText) } },
                    },
                },
            };
            var file = path;
            b.Click += (_, _) => _w.OpenFile(file);
            list.Children.Add(b);
        }
        return list;
    }

    FrameworkElement OpenPage()
    {
        var p = new StackPanel { Spacing = 14 };
        p.Children.Add(Heading("Open"));
        var browse = new Button { Content = Ui.Row(Ui.Icon("", 16), Ui.Text("Browse", 13)), Padding = new Thickness(16, 8, 16, 8) };
        browse.Click += (_, _) => _ = _w.OpenAsync();
        p.Children.Add(browse);
        p.Children.Add(Ui.Text("Recent", 16, Palette.Text, bold: true));
        p.Children.Add(RecentList());
        return p;
    }

    FrameworkElement ExportPage()
    {
        var p = new StackPanel { Spacing = 14, MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left };
        p.Children.Add(Heading("Export"));
        var pptx = new Button { Content = Ui.Row(Ui.Icon("", 18), Ui.Text("PowerPoint presentation (.pptx)", 13.5)), Padding = new Thickness(16, 10, 16, 10) };
        pptx.Click += async (_, _) => { await Commands.ExportPptxAsync(_w.Editor); };
        p.Children.Add(pptx);
        p.Children.Add(Ui.Text("Text, shapes, pictures, backgrounds, hidden slides and transitions are exported. Visary's signature transitions become their closest PowerPoint transition, and Morph stays Morph because objects keep matching names.", 12.5, Palette.SubtleText, wrap: TextWrapping.Wrap));
        return p;
    }

    FrameworkElement OptionsPage()
    {
        var p = new StackPanel { Spacing = 14 };
        p.Children.Add(Heading("Options"));
        p.Children.Add(Ui.Text("Office theme", 16, Palette.Text, bold: true));
        p.Children.Add(Ui.Text("Colours for the window, ribbon and panes. Slides are not affected.", 12.5, Palette.SubtleText));
        var wrap = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal, ItemWidth = 210, ItemHeight = 150 };
        foreach (var t in AppThemes.All)
        {
            var selected = t.Id == Palette.Current.Id;
            var preview = new Grid { Width = 190, Height = 100, CornerRadius = new CornerRadius(6), BorderBrush = selected ? Palette.Accent : Palette.Border, BorderThickness = new Thickness(selected ? 2 : 1) };
            preview.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            preview.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
            preview.RowDefinitions.Add(new RowDefinition());
            preview.Children.Add(new Border { Background = ColorUtil.Brush(t.TitleBar), CornerRadius = new CornerRadius(5, 5, 0, 0) });
            var ribbon = new Border { Background = ColorUtil.Brush(t.Ribbon), Child = new Border { Width = 40, Height = 4, Background = ColorUtil.Brush(t.Accent), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(8, 0, 0, 2) } };
            Grid.SetRow(ribbon, 1);
            preview.Children.Add(ribbon);
            var body = new Grid { Background = ColorUtil.Brush(t.Canvas) };
            body.Children.Add(new Border { Width = 90, Height = 46, Background = new SolidColorBrush(Microsoft.UI.Colors.White), CornerRadius = new CornerRadius(2) });
            Grid.SetRow(body, 2);
            preview.Children.Add(body);
            var b = new Button { Content = new StackPanel { Spacing = 6, Children = { preview, Ui.Text(t.Name, 13, Palette.Text) } }, Background = Palette.Transparent, BorderThickness = new Thickness(0) };
            var id = t.Id;
            b.Click += (_, _) => { _w.ApplyAppTheme(id); Build(); };
            wrap.Children.Add(b);
        }
        p.Children.Add(wrap);
        p.Children.Add(Ui.Text("Slide show", 16, Palette.Text, bold: true));
        p.Children.Add(Ui.Check("Use presenter view when a second screen is connected", Settings.Current.PresenterView, v => { Settings.Current.PresenterView = v; Settings.Current.Save(); }));
        p.Children.Add(Ui.Text($"Settings and logs: {Settings.Folder}", 11.5, Palette.SubtleText));
        return p;
    }
}
