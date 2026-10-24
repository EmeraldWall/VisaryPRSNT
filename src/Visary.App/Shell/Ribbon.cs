using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Visary.App.Editor;
using Visary.Core.Editing;
using Visary.Core.Model;
using Visary.Core.Themes;
using Visary.Core.Transitions;

namespace Visary.App.Shell;

public enum RibbonTab { Home, Insert, Design, Transitions, Animations, SlideShow, View }

/// <summary>PowerPoint style tab strip and ribbon, built in code.</summary>
public sealed partial class Ribbon : Grid
{
    readonly MainWindow _w;
    readonly StackPanel _tabStrip = new() { Orientation = Orientation.Horizontal, Spacing = 0 };
    readonly ScrollViewer _body = new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        HorizontalScrollMode = ScrollMode.Auto, VerticalScrollMode = ScrollMode.Disabled, Height = 104,
    };
    RibbonTab _tab = RibbonTab.Home;
    string _lastTransitionKey = "";

    DocumentEditor Ed => _w.Editor;

    public Ribbon(MainWindow w)
    {
        _w = w;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var strip = new Border { Background = Palette.TabStrip, Child = _tabStrip, Padding = new Thickness(6, 0, 6, 0) };
        Children.Add(strip);
        var body = new Border { Background = Palette.Ribbon, Child = _body, BorderBrush = Palette.Border, BorderThickness = new Thickness(0, 0, 0, 1) };
        Grid.SetRow(body, 1);
        Children.Add(body);
        Ed.Changed += OnChanged;
        Palette.Changed += Build;
        Build();
    }

    public RibbonTab Tab
    {
        get => _tab;
        set
        {
            _tab = value;
            Build();
            _w.Side.Mode = value switch
            {
                RibbonTab.Transitions => PaneMode.Transitions,
                RibbonTab.Animations => PaneMode.Animations,
                RibbonTab.Design => PaneMode.Design,
                _ => _w.Side.Mode,
            };
        }
    }

    void OnChanged(ChangeKind k)
    {
        switch (_tab)
        {
            case RibbonTab.Home when k.HasFlag(ChangeKind.Selection) || k.HasFlag(ChangeKind.CurrentSlide):
            case RibbonTab.Design when k.HasFlag(ChangeKind.Document):
            case RibbonTab.SlideShow when k.HasFlag(ChangeKind.CurrentSlide) || k.HasFlag(ChangeKind.Slides):
                BuildBody();
                break;
            case RibbonTab.Transitions:
                var t = Ed.Current.Transition;
                var key = $"{Ed.Current.Id}|{t.Effect}|{t.Variant}|{t.Ease}|{t.Intensity}|{t.AdvanceOnClick}|{t.AdvanceAfterSeconds != null}";
                if (key != _lastTransitionKey) BuildBody();
                break;
            case RibbonTab.Animations when k.HasFlag(ChangeKind.Selection):
                BuildBody();
                break;
        }
        if (k.HasFlag(ChangeKind.Document) || k == ChangeKind.All) BuildStrip();
    }

    void Build()
    {
        BuildStrip();
        BuildBody();
    }

    void BuildStrip()
    {
        _tabStrip.Children.Clear();
        var file = TabButton("File", false);
        file.Click += (_, _) => _w.ShowBackstage();
        _tabStrip.Children.Add(file);
        foreach (var (tab, label) in new[] { (RibbonTab.Home, "Home"), (RibbonTab.Insert, "Insert"), (RibbonTab.Design, "Design"), (RibbonTab.Transitions, "Transitions"), (RibbonTab.Animations, "Animations"), (RibbonTab.SlideShow, "Slide Show"), (RibbonTab.View, "View") })
        {
            var b = TabButton(label, tab == _tab);
            b.Click += (_, _) => Tab = tab;
            _tabStrip.Children.Add(b);
        }
    }

    static Button TabButton(string label, bool selected) => new()
    {
        Content = new TextBlock { Text = label, FontSize = 13, FontWeight = selected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal },
        Background = selected ? Palette.Ribbon : Palette.Transparent,
        Foreground = selected ? Palette.TabSelectedText : Palette.TabText,
        BorderThickness = new Thickness(0),
        CornerRadius = new CornerRadius(4, 4, 0, 0),
        Padding = new Thickness(14, 6, 14, 6),
        Margin = new Thickness(0, 4, 0, 0),
    };

    void BuildBody()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Padding = new Thickness(4, 4, 4, 0), Spacing = 0 };
        try
        {
            switch (_tab)
            {
                case RibbonTab.Home: Home(row); break;
                case RibbonTab.Insert: Insert(row); break;
                case RibbonTab.Design: Design(row); break;
                case RibbonTab.Transitions: TransitionsTab(row); break;
                case RibbonTab.Animations: Animations(row); break;
                case RibbonTab.SlideShow: SlideShow(row); break;
                case RibbonTab.View: View(row); break;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Ribbon", ex);
        }
        _body.Content = row;
    }

    MenuFlyout LayoutMenu()
    {
        var m = new MenuFlyout();
        foreach (var l in Factory.Layouts) m.Items.Add(Ui.Item(l.Name, () => Ed.AddSlide(l.Id)));
        return m;
    }

    public Flyout ShapeGallery()
    {
        var flyout = new Flyout();
        var panel = new StackPanel { Spacing = 6, Width = 330 };
        foreach (var group in ShapeGeometry.Catalog.GroupBy(s => s.Group))
        {
            panel.Children.Add(Ui.Text(group.Key, 11.5, Palette.SubtleText, bold: true));
            var wrap = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal, ItemWidth = 40, ItemHeight = 40 };
            foreach (var s in group)
            {
                var line = s.Kind is ShapeKind.Line or ShapeKind.ArrowLine;
                var path = new Microsoft.UI.Xaml.Shapes.Path
                {
                    Data = Rendering.SlideView.Geometry(s.Kind, 26, 26, 6),
                    Fill = line ? null : Palette.AccentSoft, Stroke = Palette.Text, StrokeThickness = 1.4,
                };
                var b = new Button { Content = path, Width = 38, Height = 38, Padding = new Thickness(5), Background = Palette.Transparent, BorderThickness = new Thickness(0) };
                ToolTipService.SetToolTip(b, s.Name);
                var kind = s.Kind;
                b.Click += (_, _) => { Ed.InsertShape(kind); flyout.Hide(); };
                wrap.Children.Add(b);
            }
            panel.Children.Add(wrap);
        }
        flyout.Content = panel;
        return flyout;
    }

    void Home(StackPanel row)
    {
        var newSlide = new SplitButton
        {
            Content = new StackPanel { Spacing = 4, Children = { Ui.Icon("", 22), new TextBlock { Text = "New slide", FontSize = 11.5 } } },
            Flyout = LayoutMenu(), Background = Palette.Transparent, BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Stretch,
        };
        newSlide.Click += (_, _) => Ed.AddSlide();
        row.Children.Add(Ui.Group("Slides", newSlide,
            Ui.Rows(Ui.Small("", "Duplicate", () => Ed.DuplicateSlide()), Ui.Small("", "Delete", () => Ed.DeleteSlide()), Ui.Small("", Ed.Current.Hidden ? "Unhide" : "Hide", () => Ed.ToggleHidden()))));
        row.Children.Add(Ui.Group("Clipboard", Ui.Large("", "Paste", Ed.Paste, "Paste (Ctrl+V)"),
            Ui.Rows(Ui.Small("", "Cut", Ed.Cut), Ui.Small("", "Copy", Ed.Copy), Ui.Small("", "Duplicate", Ed.DuplicateSelection, "Duplicate (Ctrl+D)"))));

        var target = Ed.SelectedElements.OfType<ITextHost>().FirstOrDefault();
        var st = target?.Style ?? new TextStyle { FontFamily = Ed.Theme.BodyFont };
        void S(Action<TextStyle> f, string key) => Ed.UpdateSelected(x => { if (x is ITextHost h) f(h.Style); }, key);
        var font = new ComboBox { IsEditable = true, Width = 170, ItemsSource = SlideThemes.Fonts.ToList(), Text = st.FontFamily, IsEnabled = target != null };
        font.SelectionChanged += (_, _) => { if (font.SelectedItem is string f) S(x => x.FontFamily = f, "font"); };
        font.TextSubmitted += (_, a) => S(x => x.FontFamily = a.Text, "font");
        var size = Ui.Number(st.FontSize, 6, 600, v => S(x => x.FontSize = v, "size"));
        size.Width = 84;
        size.IsEnabled = target != null;
        row.Children.Add(Ui.Group("Font", Ui.Rows(
            Ui.Row(font, size, Ui.Small("", null, () => S(x => x.FontSize = Math.Round(x.FontSize * 1.12), "size"), "Increase font size"), Ui.Small("", null, () => S(x => x.FontSize = Math.Max(6, Math.Round(x.FontSize * .89)), "size"), "Decrease font size")),
            Ui.Row(
                Ui.Toggle("", "Bold (Ctrl+B)", st.Bold, v => S(x => x.Bold = v, "b")),
                Ui.Toggle("", "Italic (Ctrl+I)", st.Italic, v => S(x => x.Italic = v, "i")),
                Ui.Toggle("", "Underline (Ctrl+U)", st.Underline, v => S(x => x.Underline = v, "u")),
                Ui.Toggle("", "Strikethrough", st.Strikethrough, v => S(x => x.Strikethrough = v, "s")),
                Ui.ColorButton(st.Color, c => S(x => x.Color = c, "color"), [.. Ed.Theme.Accents, Ed.Theme.Text, "#FFFFFF"], "")))));

        row.Children.Add(Ui.Group("Paragraph", Ui.Rows(
            Ui.Row(
                Ui.Toggle("", "Bullets", st.List == ListStyle.Bullet, v => S(x => x.List = v ? ListStyle.Bullet : ListStyle.None, "list")),
                Ui.Toggle("", "Numbering", st.List == ListStyle.Number, v => S(x => x.List = v ? ListStyle.Number : ListStyle.None, "list")),
                Ui.Menu("", "Spacing", SpacingMenu(S))),
            Ui.Row(
                Ui.Toggle("", "Align left", st.Align == TextAlign.Left, _ => S(x => x.Align = TextAlign.Left, "align")),
                Ui.Toggle("", "Center", st.Align == TextAlign.Center, _ => S(x => x.Align = TextAlign.Center, "align")),
                Ui.Toggle("", "Align right", st.Align == TextAlign.Right, _ => S(x => x.Align = TextAlign.Right, "align")),
                Ui.Toggle("", "Justify", st.Align == TextAlign.Justify, _ => S(x => x.Align = TextAlign.Justify, "align"))))));

        var arrange = new MenuFlyout();
        foreach (var (z, label) in new[] { (ZOrder.Front, "Bring to front"), (ZOrder.Forward, "Bring forward"), (ZOrder.Backward, "Send backward"), (ZOrder.Back, "Send to back") })
            arrange.Items.Add(Ui.Item(label, () => Ed.Arrange(z)));
        arrange.Items.Add(new MenuFlyoutSeparator());
        foreach (var (a, label) in new[] { (AlignKind.Left, "Align left"), (AlignKind.Center, "Align center"), (AlignKind.Right, "Align right"), (AlignKind.Top, "Align top"), (AlignKind.Middle, "Align middle"), (AlignKind.Bottom, "Align bottom"), (AlignKind.DistributeHorizontally, "Distribute horizontally"), (AlignKind.DistributeVertically, "Distribute vertically") })
            arrange.Items.Add(Ui.Item(label, () => Ed.Align(a)));
        row.Children.Add(Ui.Group("Drawing",
            Ui.FlyoutButton("", "Shapes", ShapeGallery(), large: true),
            Ui.Menu("", "Arrange", arrange, large: true)));
        row.Children.Add(Ui.Group("Editing", Ui.Rows(
            Ui.Small("", "Find and replace", () => _ = _w.FindReplaceAsync(), "Find and replace (Ctrl+H)"),
            Ui.Small("", "Select all", Ed.SelectAll, "Select all (Ctrl+A)"))));
    }

    static MenuFlyout SpacingMenu(Action<Action<TextStyle>, string> set)
    {
        var m = new MenuFlyout();
        foreach (var v in new[] { 1.0, 1.15, 1.3, 1.5, 2.0, 2.5 })
            m.Items.Add(Ui.Item($"{v:0.##}", () => set(x => x.LineSpacing = v, "line")));
        return m;
    }

    void Insert(StackPanel row)
    {
        row.Children.Add(Ui.Group("Slides", Ui.Menu("", "New slide", LayoutMenu(), large: true)));
        row.Children.Add(Ui.Group("Images", Ui.Large("", "Pictures", () => _ = Commands.InsertPictureAsync(Ed))));
        row.Children.Add(Ui.Group("Illustrations", Ui.FlyoutButton("", "Shapes", ShapeGallery(), large: true)));
        row.Children.Add(Ui.Group("Text",
            Ui.Large("", "Text box", () => _w.Canvas.BeginTextEdit(Ed.InsertTextBox().Id)),
            Ui.Large("", "Heading", () => _w.Canvas.BeginTextEdit(Ed.InsertTextBox(heading: true).Id))));
        row.Children.Add(Ui.Group("Lines",
            Ui.Large("", "Line", () => Ed.InsertShape(ShapeKind.Line)),
            Ui.Large("", "Arrow", () => Ed.InsertShape(ShapeKind.ArrowLine))));
    }

    void Design(StackPanel row)
    {
        var themes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        foreach (var t in SlideThemes.All)
        {
            var bg = t.TitleBackground();
            var sw = new Border
            {
                Width = 86, Height = 52, CornerRadius = new CornerRadius(4), Background = Rendering.Paint.Fill(bg.Fill),
                BorderBrush = t.Id == Ed.Doc.ThemeId ? Palette.Accent : Palette.Border, BorderThickness = new Thickness(t.Id == Ed.Doc.ThemeId ? 2 : 1),
                Child = new Grid
                {
                    Children =
                    {
                        new TextBlock { Text = "Aa", FontFamily = new FontFamily(t.HeadingFont), FontSize = 18, Foreground = ColorUtil.Brush(t.TitleText), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                        new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Children = { new Border { Height = 5, Width = 21, Background = ColorUtil.Brush(t.Accents[0]) }, new Border { Height = 5, Width = 21, Background = ColorUtil.Brush(t.Accents[1]) }, new Border { Height = 5, Width = 21, Background = ColorUtil.Brush(t.Accents[2]) }, new Border { Height = 5, Width = 21, Background = ColorUtil.Brush(t.Accents[3]) } } },
                    },
                },
            };
            var b = new Button { Content = sw, Padding = new Thickness(0), Background = Palette.Transparent, BorderThickness = new Thickness(0) };
            ToolTipService.SetToolTip(b, t.Name);
            var id = t.Id;
            b.Click += (_, _) => Ed.ApplyTheme(id);
            themes.Children.Add(b);
        }
        row.Children.Add(Ui.Group("Themes", themes));
        var sizes = new MenuFlyout();
        foreach (var s in Factory.Sizes) sizes.Items.Add(Ui.Item(s.Name, () => Ed.ResizeSlides(s.Width, s.Height)));
        row.Children.Add(Ui.Group("Customize",
            Ui.Menu("", "Slide size", sizes, large: true),
            Ui.Large("", "Format background", () => _w.Side.Mode = PaneMode.Design)));
    }

    void TransitionsTab(StackPanel row)
    {
        var t = Ed.Current.Transition;
        _lastTransitionKey = $"{Ed.Current.Id}|{t.Effect}|{t.Variant}|{t.Ease}|{t.Intensity}|{t.AdvanceOnClick}|{t.AdvanceAfterSeconds != null}";
        var info = TransitionCatalog.Get(t.Effect);
        row.Children.Add(Ui.Group("Preview", Ui.Large("", "Preview", () => _ = _w.Canvas.PreviewTransitionAsync())));

        var gallery = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
        foreach (var x in TransitionCatalog.All) gallery.Children.Add(_w.Side.TransitionTile(x, x.Id == t.Effect, 74));
        var galleryScroll = new ScrollViewer
        {
            Content = gallery, Width = 560, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Disabled,
        };
        var options = new MenuFlyout();
        foreach (var v in info.Variants) options.Items.Add(new ToggleMenuFlyoutItem { Text = v.Name, IsChecked = (t.Variant ?? info.DefaultVariant) == v.Id }.Also(i => i.Click += (_, _) => Ed.SetTransition(s => s.Variant = v.Id)));
        var optionsButton = Ui.Menu("", "Effect options", options, large: true);
        optionsButton.IsEnabled = info.Variants.Count > 0;
        row.Children.Add(Ui.Group("Transition to this slide", galleryScroll, optionsButton));

        var dur = Ui.Number(t.DurationMs / 1000.0, .2, 10, v => Ed.SetTransition(s => s.DurationMs = (int)Math.Round(v * 1000), "dur"), .1);
        dur.Width = 100;
        var motion = Ui.Combo([(TransitionEase.Smooth, "Smooth"), (TransitionEase.Fluid, "Fluid"), (TransitionEase.Snappy, "Snappy"), (TransitionEase.Spring, "Spring"), (TransitionEase.Linear, "Linear")], t.Ease, v => Ed.SetTransition(s => s.Ease = v));
        motion.Width = 110;
        var intensity = Ui.Combo([(TransitionIntensity.Subtle, "Subtle"), (TransitionIntensity.Balanced, "Balanced"), (TransitionIntensity.Bold, "Bold")], t.Intensity, v => Ed.SetTransition(s => s.Intensity = v));
        intensity.Width = 110;
        row.Children.Add(Ui.Group("Feel", Ui.Rows(
            Ui.Row(Ui.Text("Duration (s)", 12), dur),
            Ui.Row(Ui.Text("Motion", 12), motion, Ui.Text("Intensity", 12), intensity))));
        var after = Ui.Number(t.AdvanceAfterSeconds ?? 5, 0, 3600, v => { if (Ed.Current.Transition.AdvanceAfterSeconds != null) Ed.SetTransition(s => s.AdvanceAfterSeconds = v, "after"); }, .5);
        after.Width = 96;
        after.IsEnabled = t.AdvanceAfterSeconds != null;
        row.Children.Add(Ui.Group("Timing", Ui.Rows(
            Ui.Row(Ui.Check("On mouse click", t.AdvanceOnClick, v => Ed.SetTransition(s => s.AdvanceOnClick = v))),
            Ui.Row(Ui.Check("After", t.AdvanceAfterSeconds != null, v => Ed.SetTransition(s => s.AdvanceAfterSeconds = v ? 5 : null)), after)),
            Ui.Large("", "Apply to all", Ed.ApplyTransitionToAll)));
    }

    void Animations(StackPanel row)
    {
        var hasSel = Ed.Selection.Count > 0;
        foreach (var (kind, label, glyph) in new[] { (BuildKind.Entrance, "Entrance", ""), (BuildKind.Emphasis, "Emphasis", ""), (BuildKind.Exit, "Exit", "") })
        {
            var menu = new MenuFlyout();
            foreach (var fx in BuildCatalog.For(kind)) menu.Items.Add(Ui.Item(fx.Name, () => Ed.AddBuild(kind, fx.Id)));
            var b = Ui.Menu(glyph, label, menu, large: true);
            b.IsEnabled = hasSel;
            row.Children.Add(Ui.Group(label, b));
        }
        row.Children.Add(Ui.Group("Preview", Ui.Large("", "Preview", () => _ = _w.Canvas.PreviewBuildsAsync())));
        row.Children.Add(Ui.Group("Advanced", Ui.Large("", "Animation pane", () => _w.Side.Mode = PaneMode.Animations)));
    }

    void SlideShow(StackPanel row)
    {
        row.Children.Add(Ui.Group("Start slide show",
            Ui.Large("", "From beginning", () => _w.StartShow(0), "From beginning (F5)"),
            Ui.Large("", "From current slide", () => _w.StartShow(Ed.CurrentIndex), "From current slide (Shift+F5)"),
            Ui.Large("", "Presenter view", () => _w.StartShow(Ed.CurrentIndex, forcePresenter: true))));
        row.Children.Add(Ui.Group("Set up",
            Ui.Rows(
                Ui.Check("Loop until Esc", Ed.Doc.Loop, v => Ed.Mutate(d => d.Loop = v, ChangeKind.Document)),
                Ui.Check("Use presenter view with two screens", Settings.Current.PresenterView, v => { Settings.Current.PresenterView = v; Settings.Current.Save(); })),
            Ui.Large("", Ed.Current.Hidden ? "Unhide slide" : "Hide slide", () => Ed.ToggleHidden())));
    }

    void View(StackPanel row)
    {
        row.Children.Add(Ui.Group("Show", Ui.Rows(
            Ui.Check("Gridlines", _w.Canvas.ShowGridlines, v => { _w.Canvas.ShowGridlines = v; _w.Canvas.Rebuild(); }),
            Ui.Check("Smart guides", _w.Canvas.SmartGuides, v => _w.Canvas.SmartGuides = v),
            Ui.Check("Notes", Settings.Current.ShowNotes, v => _w.SetNotesVisible(v)))));
        row.Children.Add(Ui.Group("Zoom",
            Ui.Large("", "Zoom in", () => _w.Canvas.SetZoom(_w.Canvas.Zoom * 1.2)),
            Ui.Large("", "Zoom out", () => _w.Canvas.SetZoom(_w.Canvas.Zoom / 1.2)),
            Ui.Large("", "Fit to window", () => _w.Canvas.SetZoom(null))));
        var themes = new MenuFlyout();
        foreach (var p in AppThemes.All)
            themes.Items.Add(new ToggleMenuFlyoutItem { Text = p.Name, IsChecked = p.Id == Palette.Current.Id }.Also(i => i.Click += (_, _) => _w.ApplyAppTheme(p.Id)));
        row.Children.Add(Ui.Group("Window", Ui.Menu("", "Office theme", themes, large: true)));
    }
}

static class Fluent
{
    public static T Also<T>(this T value, Action<T> configure)
    {
        configure(value);
        return value;
    }
}
