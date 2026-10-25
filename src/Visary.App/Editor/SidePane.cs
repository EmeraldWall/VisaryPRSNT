using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Visary.App.Rendering;
using Visary.App.Shell;
using Visary.Core.Editing;
using Visary.Core.Model;
using Visary.Core.Themes;
using Visary.Core.Transitions;

namespace Visary.App.Editor;

public enum PaneMode { Format, Design, Transitions, Animations, Selection }

/// <summary>The task pane on the right.</summary>
public sealed partial class SidePane : Grid
{
    readonly DocumentEditor _ed;
    readonly SelectorBar _tabs = new() { Padding = new Thickness(4, 4, 4, 0) };
    static readonly (PaneMode mode, string label, string glyph)[] TabList =
    [
        (PaneMode.Format, "Format", "\uE790"), (PaneMode.Design, "Design", "\uE771"), (PaneMode.Transitions, "Transitions", "\uE8AB"),
        (PaneMode.Animations, "Animations", "\uE945"), (PaneMode.Selection, "Selection", "\uE8FD"),
    ];
    bool _syncingTabs;
    readonly ScrollViewer _scroll = new() { Padding = new Thickness(16, 0, 16, 24), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    readonly List<Action> _refreshers = [];
    bool _refreshing;
    PaneMode _mode = PaneMode.Format;

    /// <summary>Plays the current slide's transition, or the given settings when hovering a tile.</summary>
    public Func<TransitionSettings?, Task>? PreviewTransition { get; set; }
    public Action? CancelPreview { get; set; }
    public Func<Task>? PreviewBuilds { get; set; }

    public SidePane(DocumentEditor ed)
    {
        _ed = ed;
        Width = 330;
        Background = Palette.PaneAlt;
        BorderBrush = Palette.Border;
        BorderThickness = new Thickness(1, 0, 0, 0);
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition());
        foreach (var (mode, label, glyph) in TabList)
            _tabs.Items.Add(new SelectorBarItem { Text = label, Icon = Ui.Icon(glyph, 14), Tag = mode });
        _tabs.SelectionChanged += (_, _) =>
        {
            if (_syncingTabs || _tabs.SelectedItem?.Tag is not PaneMode m || m == _mode) return;
            Mode = m;
        };
        Children.Add(new ScrollViewer { Content = _tabs, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollMode = ScrollMode.Auto, VerticalScrollMode = ScrollMode.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Grid.SetRow(_scroll, 1);
        Children.Add(_scroll);
        _ed.Changed += OnChanged;
        Build();
    }

    public PaneMode Mode
    {
        get => _mode;
        set { _mode = value; Build(); }
    }

    void OnChanged(ChangeKind k)
    {
        if (k.HasFlag(ChangeKind.Selection) || k.HasFlag(ChangeKind.CurrentSlide) || k.HasFlag(ChangeKind.Document) || k == ChangeKind.All)
        {
            Build();
            return;
        }
        var t = _ed.Current.Transition;
        var effectKey = $"{_ed.Current.Id}|{t.Effect}|{t.Variant}|{t.AdvanceAfterSeconds != null}|{_ed.Current.Builds.Count}";
        if (_mode == PaneMode.Selection && !_renaming)
        {
            Build();
            return;
        }
        if (effectKey != _effectKey && _mode is PaneMode.Transitions or PaneMode.Animations)
        {
            Build();
            return;
        }
        if (k.HasFlag(ChangeKind.Content) || k.HasFlag(ChangeKind.Slides))
        {
            // Refresh values in place so a slider being dragged keeps its focus.
            _refreshing = true;
            foreach (var r in _refreshers) r();
            _refreshing = false;
        }
    }

    void Tabs()
    {
        _syncingTabs = true;
        foreach (var item in _tabs.Items)
            if (item.Tag is PaneMode m && m == _mode) _tabs.SelectedItem = item;
        _syncingTabs = false;
    }

    string _effectKey = "";

    void Build()
    {
        var tr = _ed.Current.Transition;
        _effectKey = $"{_ed.Current.Id}|{tr.Effect}|{tr.Variant}|{tr.AdvanceAfterSeconds != null}|{_ed.Current.Builds.Count}";
        Tabs();
        _refreshers.Clear();
        var body = new StackPanel { Spacing = 0 };
        try
        {
            switch (_mode)
            {
                case PaneMode.Format: BuildFormat(body); break;
                case PaneMode.Design: BuildDesign(body); break;
                case PaneMode.Transitions: BuildTransitions(body); break;
                case PaneMode.Animations: BuildAnimations(body); break;
                case PaneMode.Selection: BuildSelection(body); break;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Task pane", ex);
        }
        _scroll.Content = body;
    }

    IReadOnlyList<string> ThemeColors()
    {
        var t = _ed.Theme;
        return [t.Text, t.TextMuted, t.Surface.Length == 7 ? t.Surface : "#F3F2F1", "#FFFFFF", .. t.Accents];
    }

    /// <summary>Registers a refresh for a value shown in the pane, guarded against feedback loops.</summary>
    void Bind(Action refresh) => _refreshers.Add(refresh);

    void Update(Action<SlideElement> change, string key)
    {
        if (_refreshing) return;
        _ed.UpdateSelected(change, key);
    }

    /* ---------------- Format ---------------- */

    void BuildFormat(StackPanel body)
    {
        var sel = _ed.SelectedElements.ToList();
        if (sel.Count == 0)
        {
            body.Children.Add(Ui.Section("Format background", BackgroundEditor()));
            body.Children.Add(Ui.Text("Select an object on the slide to format it. Double click text to edit it.", 12, Palette.SubtleText, wrap: TextWrapping.Wrap));
            return;
        }
        var e = sel[0];
        body.Children.Add(Ui.Section(sel.Count > 1 ? $"{sel.Count} objects" : $"Format {e.KindName.ToLowerInvariant()}"));
        if (sel.Count == 1)
        {
            var name = new TextBox { Text = e.Name ?? "", PlaceholderText = "Name (pairs objects for Morph)" };
            name.TextChanged += (_, _) => Update(x => x.Name = string.IsNullOrWhiteSpace(name.Text) ? null : name.Text, "name");
            body.Children.Add(Ui.Field("Name", name));
        }
        if (e is ITextHost th && e is not ShapeElement { IsLine: true }) body.Children.Add(TextSection(th.Style));
        switch (e)
        {
            case TextElement t:
                body.Children.Add(Ui.Section("Box", FillEditor(t.Fill, f => Update(x => { if (x is TextElement y) y.Fill = f; }, "fill")),
                    OutlineEditor(t.Outline, o => Update(x => { if (x is TextElement y) y.Outline = o; }, "outline")),
                    Ui.Field("Corner radius", Ui.Slider(t.CornerRadius, 0, 200, v => Update(x => { if (x is TextElement y) y.CornerRadius = v; }, "radius"))),
                    Ui.Field("Inner margin", Ui.Slider(t.Padding, 0, 200, v => Update(x => { if (x is TextElement y) y.Padding = v; }, "pad")))));
                break;
            case ShapeElement s:
                var kinds = ShapeGeometry.Catalog.Select(c => (c.Kind, c.Name));
                var kind = Ui.Field("Shape", Ui.Combo(kinds, s.Kind, k => Update(x => { if (x is ShapeElement y) y.Kind = k; }, "kind")));
                if (s.IsLine)
                    body.Children.Add(Ui.Section("Line", kind, OutlineEditor(s.Outline, o => Update(x => { if (x is ShapeElement y) y.Outline = o; }, "outline"))));
                else
                    body.Children.Add(Ui.Section("Shape", kind,
                        FillEditor(s.Fill, f => Update(x => { if (x is ShapeElement y) y.Fill = f; }, "fill")),
                        OutlineEditor(s.Outline, o => Update(x => { if (x is ShapeElement y) y.Outline = o; }, "outline")),
                        Ui.Field("Corner radius (rounded rectangle)", Ui.Slider(s.CornerRadius, 0, Math.Max(1, Math.Min(s.Width, s.Height) / 2), v => Update(x => { if (x is ShapeElement y) y.CornerRadius = v; }, "radius")))));
                break;
            case ImageElement img:
                var replace = new Button { Content = "Replace picture" };
                replace.Click += async (_, _) =>
                {
                    if (await Commands.PickPictureBytesAsync() is not { } pic) return;
                    var id = Ids.New();
                    _ed.Doc.Media[id] = new MediaItem { ContentType = pic.type, Data = pic.data };
                    Update(x => { if (x is ImageElement y) y.MediaId = id; }, "replace");
                };
                body.Children.Add(Ui.Section("Picture", replace,
                    Ui.Field("Fit", Ui.Combo([(ImageFit.Fill, "Fill and crop"), (ImageFit.Fit, "Fit inside"), (ImageFit.Stretch, "Stretch")], img.Fit, f => Update(x => { if (x is ImageElement y) y.Fit = f; }, "fit"))),
                    Ui.Field("Corner radius", Ui.Slider(img.CornerRadius, 0, Math.Min(img.Width, img.Height) / 2, v => Update(x => { if (x is ImageElement y) y.CornerRadius = v; }, "radius"))),
                    OutlineEditor(img.Outline, o => Update(x => { if (x is ImageElement y) y.Outline = o; }, "outline"))));
                break;
            case TableElement tb:
                body.Children.Add(TableSection(tb));
                break;
            case Core.Model.IconElement ic:
                body.Children.Add(IconSection(ic));
                break;
        }
        body.Children.Add(GeometrySection(e));
    }

    FrameworkElement TextSection(TextStyle st)
    {
        void S(Action<TextStyle> f, string key) => Update(x => { if (x is ITextHost h) f(h.Style); }, key);
        var font = new ComboBox { IsEditable = true, HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = SlideThemes.Fonts.ToList(), Text = st.FontFamily };
        font.SelectionChanged += (_, _) => { if (font.SelectedItem is string f) S(x => x.FontFamily = f, "font"); };
        font.TextSubmitted += (_, a) => S(x => x.FontFamily = a.Text, "font");
        var size = Ui.Number(st.FontSize, 6, 600, v => S(x => x.FontSize = v, "size"));
        var styles = Ui.Row(
            Ui.Toggle("", "Bold", st.Bold, v => S(x => x.Bold = v, "b")),
            Ui.Toggle("", "Italic", st.Italic, v => S(x => x.Italic = v, "i")),
            Ui.Toggle("", "Underline", st.Underline, v => S(x => x.Underline = v, "u")),
            Ui.Toggle("", "Strikethrough", st.Strikethrough, v => S(x => x.Strikethrough = v, "s")),
            Ui.ColorButton(st.Color, c => S(x => x.Color = c, "color"), ThemeColors(), ""));
        return Ui.Section("Text",
            Ui.Field("Font", font),
            Ui.TwoColumns(Ui.Field("Size", size), Ui.Field("Character spacing", Ui.Number(st.CharacterSpacing, -20, 100, v => S(x => x.CharacterSpacing = v, "spc")))),
            styles,
            Ui.TwoColumns(
                Ui.Field("Align", Ui.Combo([(TextAlign.Left, "Left"), (TextAlign.Center, "Center"), (TextAlign.Right, "Right"), (TextAlign.Justify, "Justify")], st.Align, v => S(x => x.Align = v, "align"))),
                Ui.Field("Vertical", Ui.Combo([(VerticalAlign.Top, "Top"), (VerticalAlign.Middle, "Middle"), (VerticalAlign.Bottom, "Bottom")], st.VerticalAlign, v => S(x => x.VerticalAlign = v, "valign")))),
            Ui.TwoColumns(
                Ui.Field("List", Ui.Combo([(ListStyle.None, "None"), (ListStyle.Bullet, "Bullets"), (ListStyle.Number, "Numbers")], st.List, v => S(x => x.List = v, "list"))),
                Ui.Field("Line spacing", Ui.Number(st.LineSpacing, .7, 3, v => S(x => x.LineSpacing = v, "line"), .05))));
    }

    FrameworkElement FillEditor(FillSpec fill, Action<FillSpec> set)
    {
        var f = fill.Clone();
        var panel = new StackPanel { Spacing = 8 };
        void Rebuild()
        {
            panel.Children.Clear();
            panel.Children.Add(Ui.Field("Fill", Ui.Combo([(FillKind.None, "No fill"), (FillKind.Solid, "Solid"), (FillKind.Gradient, "Gradient")], f.Kind, k => { f.Kind = k; set(f.Clone()); Rebuild(); })));
            if (f.Kind == FillKind.None) return;
            var c1 = Ui.ColorButton(f.Color, c => { f.Color = c; set(f.Clone()); }, ThemeColors());
            if (f.Kind == FillKind.Solid)
            {
                panel.Children.Add(Ui.Field("Colour", c1));
                return;
            }
            var c2 = Ui.ColorButton(f.Color2, c => { f.Color2 = c; set(f.Clone()); }, ThemeColors());
            panel.Children.Add(Ui.TwoColumns(Ui.Field("From", c1), Ui.Field("To", c2)));
            panel.Children.Add(Ui.Field("Angle", Ui.Slider(f.Angle, 0, 360, v => { f.Angle = v; set(f.Clone()); })));
        }
        Rebuild();
        return panel;
    }

    FrameworkElement OutlineEditor(Outline outline, Action<Outline> set)
    {
        var o = outline.Clone();
        return new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Ui.TwoColumns(
                    Ui.Field("Outline width", Ui.Number(o.Width, 0, 100, v => { o.Width = v; set(o.Clone()); })),
                    Ui.Field("Style", Ui.Combo([(DashStyle.Solid, "Solid"), (DashStyle.Dash, "Dashed"), (DashStyle.Dot, "Dotted")], o.Dash, d => { o.Dash = d; set(o.Clone()); }))),
                Ui.Field("Outline colour", Ui.ColorButton(o.Color, c => { o.Color = c; set(o.Clone()); }, ThemeColors())),
            },
        };
    }

    FrameworkElement GeometrySection(SlideElement e)
    {
        var id = e.Id;
        SlideElement? Live() => _ed.Current.Find(id);
        NumberBox Num(Func<SlideElement, double> get, Action<SlideElement, double> set, string key, double min = -10000)
        {
            var n = Ui.Number(get(e), min, 20000, v => Update(x => set(x, v), key));
            Bind(() => { if (Live() is { } l && n.FocusState == FocusState.Unfocused) n.Value = Math.Round(get(l), 2); });
            return n;
        }
        var opacity = Ui.Slider(e.Opacity * 100, 0, 100, v => Update(x => x.Opacity = v / 100, "opacity"));
        var rotation = Num(x => x.Rotation, (x, v) => x.Rotation = ((v % 360) + 360) % 360, "rot");
        var align = new MenuFlyout();
        foreach (var (k, label) in new[] { (AlignKind.Left, "Align left"), (AlignKind.Center, "Align center"), (AlignKind.Right, "Align right"), (AlignKind.Top, "Align top"), (AlignKind.Middle, "Align middle"), (AlignKind.Bottom, "Align bottom"), (AlignKind.DistributeHorizontally, "Distribute horizontally"), (AlignKind.DistributeVertically, "Distribute vertically") })
            align.Items.Add(Ui.Item(label, () => _ed.Align(k)));
        var order = new MenuFlyout();
        foreach (var (z, label) in new[] { (ZOrder.Front, "Bring to front"), (ZOrder.Forward, "Bring forward"), (ZOrder.Backward, "Send backward"), (ZOrder.Back, "Send to back") })
            order.Items.Add(Ui.Item(label, () => _ed.Arrange(z)));
        return Ui.Section("Size and position",
            Ui.TwoColumns(Ui.Field("X", Num(x => x.X, (x, v) => x.X = v, "x")), Ui.Field("Y", Num(x => x.Y, (x, v) => x.Y = v, "y"))),
            Ui.TwoColumns(Ui.Field("Width", Num(x => x.Width, (x, v) => x.Width = Math.Max(1, v), "w", 1)), Ui.Field("Height", Num(x => x.Height, (x, v) => x.Height = Math.Max(1, v), "h", 1))),
            Ui.TwoColumns(Ui.Field("Rotation", rotation), Ui.Field("Opacity", opacity)),
            Ui.Row(Ui.Menu("", "Align", align), Ui.Menu("", "Arrange", order),
                Ui.Toggle("", "Lock position", e.Locked, v => Update(x => x.Locked = v, "lock")),
                Ui.Small("\uF0E2", null, _ed.Group, "Group (Ctrl+G)"),
                Ui.Small("\uF5ED", null, _ed.Ungroup, "Ungroup (Ctrl+Shift+G)")));
    }

    FrameworkElement BackgroundEditor()
    {
        var bg = _ed.Current.Background.Clone();
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(FillEditor(bg.Fill, f => { bg.Fill = f; _ed.SetBackground(bg, coalesce: "bg"); }));
        var pick = new Button { Content = bg.ImageMediaId == null ? "Picture background" : "Change picture" };
        pick.Click += async (_, _) =>
        {
            if (await Commands.PickPictureBytesAsync() is not { } pic) return;
            var id = Ids.New();
            _ed.Doc.Media[id] = new MediaItem { ContentType = pic.type, Data = pic.data };
            bg.ImageMediaId = id;
            _ed.SetBackground(bg);
        };
        var row = Ui.Row(pick);
        if (bg.ImageMediaId != null)
        {
            var remove = new Button { Content = "Remove picture" };
            remove.Click += (_, _) => { bg.ImageMediaId = null; _ed.SetBackground(bg); };
            row.Children.Add(remove);
        }
        panel.Children.Add(row);
        if (bg.ImageMediaId != null)
            panel.Children.Add(Ui.Field("Darken picture", Ui.Slider(bg.ImageDim * 100, 0, 90, v => { bg.ImageDim = v / 100; _ed.SetBackground(bg, coalesce: "dim"); })));
        var all = new Button { Content = "Apply to all slides", HorizontalAlignment = HorizontalAlignment.Stretch };
        all.Click += (_, _) => _ed.SetBackground(_ed.Current.Background, allSlides: true);
        panel.Children.Add(all);
        return panel;
    }

    /* ---------------- Design ---------------- */

    void BuildDesign(StackPanel body)
    {
        var grid = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal, ItemWidth = 146, ItemHeight = 108 };
        foreach (var t in SlideThemes.All)
        {
            var preview = Factory.CreatePresentation(t.Id, 1920, 1080);
            ((TextElement)preview.Slides[0].Elements[0]).Text = t.Name;
            ((TextElement)preview.Slides[0].Elements[1]).Text = "Aa  " + string.Join(" ", t.Accents.Take(0));
            var thumb = new Viewbox { Width = 136, Height = 76.5, Child = new SlideView(preview, preview.Slides[0], SlideViewMode.Thumbnail) };
            var dots = Ui.Row(t.Accents.Take(5).Select(a => (UIElement)new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = ColorUtil.Brush(a) }).ToArray());
            var b = new Button
            {
                Content = new StackPanel { Spacing = 4, Children = { thumb, dots } },
                Padding = new Thickness(3), Margin = new Thickness(0, 0, 6, 6),
                BorderBrush = t.Id == _ed.Doc.ThemeId ? Palette.Accent : Palette.Border, BorderThickness = new Thickness(t.Id == _ed.Doc.ThemeId ? 2 : 1),
                Background = Palette.Transparent,
            };
            ToolTipService.SetToolTip(b, t.Name);
            b.Click += (_, _) => _ed.ApplyTheme(t.Id);
            grid.Children.Add(b);
        }
        body.Children.Add(Ui.Section("Themes", grid));
        body.Children.Add(Ui.Section("Background", BackgroundEditor()));
        var sizes = Ui.Combo(Factory.Sizes.Select(s => (s.Id, $"{s.Name}  {s.Width} x {s.Height}")),
            Factory.Sizes.FirstOrDefault(s => s.Width == _ed.Doc.Width && s.Height == _ed.Doc.Height)?.Id ?? "16:9",
            id => { var s = Factory.Sizes.First(x => x.Id == id); if (s.Width != _ed.Doc.Width || s.Height != _ed.Doc.Height) _ed.ResizeSlides(s.Width, s.Height); });
        body.Children.Add(Ui.Section("Slide size", sizes));

        var footer = new TextBox { Text = _ed.Doc.FooterText, PlaceholderText = "Footer text on every slide" };
        footer.TextChanged += (_, _) => { if (footer.Text != _ed.Doc.FooterText) _ed.Mutate(p => p.FooterText = footer.Text, ChangeKind.Content | ChangeKind.Slides, "footer"); };
        body.Children.Add(Ui.Section("Header and footer",
            Ui.Check("Slide number", _ed.Doc.ShowSlideNumbers, v => _ed.Mutate(p => p.ShowSlideNumbers = v, ChangeKind.Content | ChangeKind.Slides)),
            Ui.Field("Footer", footer),
            Ui.Text("Shown on content slides. Title slides stay clean, as in PowerPoint.", 11.5, Palette.SubtleText, wrap: TextWrapping.Wrap)));
    }

    /* ---------------- Transitions ---------------- */

    void BuildTransitions(StackPanel body)
    {
        var t = _ed.Current.Transition;
        var info = TransitionCatalog.Get(t.Effect);
        var preview = new Button { Content = Ui.Row(Ui.Icon("", 14), Ui.Text("Preview", 13)), Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        preview.Click += async (_, _) => { if (PreviewTransition != null) await PreviewTransition(null); };
        var card = new Border
        {
            Background = Palette.AccentSoft, CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Margin = new Thickness(0, 12, 0, 0),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    Ui.Row(Ui.Icon(info.Glyph, 22, Palette.Accent), Ui.Text(info.Name, 18, Palette.Text, bold: true)),
                    Ui.Text(info.Description, 12.5, Palette.SubtleText, wrap: TextWrapping.Wrap),
                    preview,
                },
            },
        };
        body.Children.Add(card);

        var options = new List<UIElement>();
        if (info.Variants.Count > 0)
            options.Add(Ui.Field("Effect options", Ui.Combo(info.Variants.Select(v => (v.Id, v.Name)), t.Variant ?? info.DefaultVariant, v => _ed.SetTransition(x => x.Variant = v))));
        var duration = Ui.Slider(t.DurationMs / 1000.0, .2, 4, v => { if (!_refreshing) _ed.SetTransition(x => x.DurationMs = (int)Math.Round(v * 1000), "dur"); }, .05);
        var durLabel = Ui.Text($"Duration  {t.DurationMs / 1000.0:0.00} s", 11.5, Palette.SubtleText);
        Bind(() => durLabel.Text = $"Duration  {_ed.Current.Transition.DurationMs / 1000.0:0.00} s");
        options.Add(new StackPanel { Children = { durLabel, duration } });
        options.Add(Ui.TwoColumns(
            Ui.Field("Motion", Ui.Combo([(TransitionEase.Smooth, "Smooth"), (TransitionEase.Fluid, "Fluid"), (TransitionEase.Snappy, "Snappy"), (TransitionEase.Spring, "Spring"), (TransitionEase.Linear, "Linear")], t.Ease, v => _ed.SetTransition(x => x.Ease = v))),
            Ui.Field("Intensity", Ui.Combo([(TransitionIntensity.Subtle, "Subtle"), (TransitionIntensity.Balanced, "Balanced"), (TransitionIntensity.Bold, "Bold")], t.Intensity, v => _ed.SetTransition(x => x.Intensity = v)))));
        body.Children.Add(Ui.Section("Effect", options.ToArray()));

        var after = Ui.Number(t.AdvanceAfterSeconds ?? 5, 0, 3600, v => { if (_ed.Current.Transition.AdvanceAfterSeconds != null) _ed.SetTransition(x => x.AdvanceAfterSeconds = v, "after"); }, .5);
        after.IsEnabled = t.AdvanceAfterSeconds != null;
        var applyAll = new Button { Content = "Apply to all slides", HorizontalAlignment = HorizontalAlignment.Stretch };
        applyAll.Click += (_, _) => _ed.ApplyTransitionToAll();
        body.Children.Add(Ui.Section("Timing",
            Ui.Check("Advance on mouse click", t.AdvanceOnClick, v => _ed.SetTransition(x => x.AdvanceOnClick = v)),
            Ui.Check("Advance automatically after", t.AdvanceAfterSeconds != null, v => _ed.SetTransition(x => x.AdvanceAfterSeconds = v ? 5 : null)),
            after,
            applyAll));

        foreach (var cat in TransitionCatalog.Categories)
        {
            var wrap = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal, ItemWidth = 98, ItemHeight = 74 };
            foreach (var x in TransitionCatalog.All.Where(i => i.Category == cat))
                wrap.Children.Add(TransitionTile(x, x.Id == t.Effect));
            body.Children.Add(Ui.Section(cat, wrap));
        }
    }

    public Button TransitionTile(TransitionInfo x, bool selected, double width = 92)
    {
        var b = new Button
        {
            Width = width, Height = 68, Padding = new Thickness(2), Margin = new Thickness(0, 0, 4, 4),
            Background = selected ? Palette.AccentSoft : Palette.Transparent,
            BorderBrush = selected ? Palette.Accent : Palette.Border, BorderThickness = new Thickness(selected ? 2 : 1),
            Content = new StackPanel
            {
                Spacing = 5, HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    Ui.Icon(x.Glyph, 20, selected ? Palette.Accent : Palette.Text),
                    new TextBlock { Text = x.Name, FontSize = 11.5, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = width - 8 },
                },
            },
        };
        ToolTipService.SetToolTip(b, $"{x.Name}: {x.Description}");
        b.Click += async (_, _) =>
        {
            _hoverTimer?.Stop();
            _ed.ApplyEffect(x.Id);
            if (x.Id != "none" && PreviewTransition != null) await PreviewTransition(null);
        };
        AttachHoverPreview(b, x, PreviewTransition, CancelPreview);
        return b;
    }

    /* ---------------- Animations ---------------- */

    static readonly (BuildKind kind, string label, string color)[] Kinds =
        [(BuildKind.Entrance, "Entrance", "#2E7D52"), (BuildKind.Emphasis, "Emphasis", "#D9A441"), (BuildKind.Exit, "Exit", "#C43E1C")];

    void BuildAnimations(StackPanel body)
    {
        var hasSel = _ed.Selection.Count > 0;
        var add = Ui.Row();
        foreach (var (kind, label, _) in Kinds)
        {
            var menu = new MenuFlyout();
            foreach (var fx in BuildCatalog.For(kind)) menu.Items.Add(Ui.Item(fx.Name, () => _ed.AddBuild(kind, fx.Id)));
            var b = Ui.Menu(kind switch { BuildKind.Entrance => "", BuildKind.Emphasis => "", _ => "" }, label, menu);
            b.IsEnabled = hasSel;
            add.Children.Add(b);
        }
        var preview = new Button { Content = "Preview", IsEnabled = _ed.Current.Builds.Count > 0 };
        preview.Click += async (_, _) => { if (PreviewBuilds != null) await PreviewBuilds(); };
        body.Children.Add(Ui.Section("Add animation", add,
            Ui.Text(hasSel ? "Adds the effect to the selected objects." : "Select an object on the slide first.", 12, Palette.SubtleText, wrap: TextWrapping.Wrap),
            preview));

        var list = new StackPanel { Spacing = 6 };
        var step = 0;
        for (var i = 0; i < _ed.Current.Builds.Count; i++)
        {
            var b = _ed.Current.Builds[i];
            if (b.Trigger == BuildTrigger.OnClick) step++;
            var el = _ed.Current.Find(b.ElementId);
            var name = el switch { null => "Missing object", { Name: { Length: > 0 } n } => n, ITextHost th when th.Text.Length > 0 => th.Text.Split('\n')[0], _ => el.KindName };
            var fx = BuildCatalog.All.FirstOrDefault(x => x.Kind == b.Kind && x.Id == b.Effect)?.Name ?? b.Effect;
            var color = Kinds.First(k => k.kind == b.Kind).color;
            var id = b.Id;
            var header = Ui.Row(
                Ui.Text(b.Trigger == BuildTrigger.OnClick ? step.ToString() : " ", 12, Palette.SubtleText, bold: true),
                new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(5), Background = ColorUtil.Brush(color), Margin = new Thickness(4, 0, 4, 0) },
                Ui.Text($"{fx}  ", 12.5, Palette.Text, bold: true),
                new TextBlock { Text = name, FontSize = 12.5, Foreground = Palette.SubtleText, MaxWidth = 120, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
            var tools = Ui.Row(
                Ui.Small("", null, () => _ed.MoveBuild(id, -1), "Move earlier"),
                Ui.Small("", null, () => _ed.MoveBuild(id, 1), "Move later"),
                Ui.Small("", null, () => _ed.RemoveBuild(id), "Remove"));
            var detail = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    Ui.TwoColumns(
                        Ui.Field("Start", Ui.Combo([(BuildTrigger.OnClick, "On click"), (BuildTrigger.WithPrevious, "With previous"), (BuildTrigger.AfterPrevious, "After previous")], b.Trigger, v => _ed.UpdateBuild(id, x => x.Trigger = v))),
                        Ui.Field("Effect", Ui.Combo(BuildCatalog.For(b.Kind).Select(x => (x.Id, x.Name)), b.Effect, v => _ed.UpdateBuild(id, x => x.Effect = v)))),
                    Ui.TwoColumns(
                        Ui.Field("Duration (s)", Ui.Number(b.DurationMs / 1000.0, .05, 20, v => _ed.UpdateBuild(id, x => x.DurationMs = (int)(v * 1000), $"bd{id}"), .1)),
                        Ui.Field("Delay (s)", Ui.Number(b.DelayMs / 1000.0, 0, 60, v => _ed.UpdateBuild(id, x => x.DelayMs = (int)(v * 1000), $"bl{id}"), .1))),
                },
            };
            var row = new Border
            {
                BorderBrush = _ed.Selection.Contains(b.ElementId) ? Palette.Accent : Palette.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(8),
                Child = new StackPanel { Spacing = 6, Children = { header, tools, detail } },
            };
            list.Children.Add(row);
        }
        if (_ed.Current.Builds.Count == 0) list.Children.Add(Ui.Text("No animations on this slide yet.", 12, Palette.SubtleText));
        body.Children.Add(Ui.Section("Animation pane", list));
    }

    /* ---------------- Tables and icons ---------------- */

    FrameworkElement TableSection(TableElement t)
    {
        var id = t.Id;
        void T(Action<TableElement> f, string? key = null)
        {
            if (!_refreshing) _ed.UpdateTable(id, f, key);
        }
        void Structure(Action<TableElement> f)
        {
            T(f);
            Build();
        }
        var cols = t.ColumnCount;
        var rows = t.Rows.Count;
        var shape = Ui.Row(
            Ui.Small("\uE710", "Row", () => Structure(x => x.Rows.Add(Enumerable.Repeat("", cols).ToList())), "Insert row below"),
            Ui.Small("\uE738", "Row", () => Structure(x => { if (x.Rows.Count > 1) x.Rows.RemoveAt(x.Rows.Count - 1); }), "Delete last row"),
            Ui.Small("\uE710", "Column", () => Structure(x => { foreach (var r in x.Rows) r.Add(""); }), "Insert column right"),
            Ui.Small("\uE738", "Column", () => Structure(x => { if (x.ColumnCount > 1) foreach (var r in x.Rows) r.RemoveAt(r.Count - 1); }), "Delete last column"));
        shape.Children[1].SetValue(Control.IsEnabledProperty, rows > 1);
        shape.Children[3].SetValue(Control.IsEnabledProperty, cols > 1);

        // A small spreadsheet of text boxes edits the cells in place.
        var cells = new Grid { ColumnSpacing = 3, RowSpacing = 3 };
        for (var c = 0; c < cols; c++) cells.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var r = 0; r < rows; r++)
        {
            cells.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var c = 0; c < cols; c++)
            {
                int rr = r, cc = c;
                var box = new TextBox { Text = t.Rows[r][c], FontSize = 11.5, MinWidth = 0, Padding = new Thickness(5, 3, 5, 3), AcceptsReturn = false, PlaceholderText = r == 0 && t.HeaderRow ? "Header" : "" };
                if (r == 0 && t.HeaderRow) box.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                box.TextChanged += (_, _) =>
                {
                    // TextBox also reports the initial text; only real edits become undo steps.
                    if (_ed.Current.Find(id) is not TableElement live || rr >= live.Rows.Count || cc >= live.Rows[rr].Count || live.Rows[rr][cc] == box.Text) return;
                    T(x => x.Rows[rr][cc] = box.Text, $"cell{rr}-{cc}");
                };
                Grid.SetRow(box, r);
                Grid.SetColumn(box, c);
                cells.Children.Add(box);
            }
        }
        var cellScroll = new ScrollViewer { Content = cells, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = 360 };

        var st = t.Style;
        var font = Ui.Combo(SlideThemes.Fonts.Select(f => (f, f)), st.FontFamily, f => T(x => x.Style.FontFamily = f, "tfont"));
        return Ui.Section("Table",
            shape,
            Ui.Field("Cells", cellScroll),
            Ui.TwoColumns(
                Ui.Check("Header row", t.HeaderRow, v => Structure(x => x.HeaderRow = v)),
                Ui.Check("Banded rows", t.BandedRows, v => T(x => x.BandedRows = v))),
            Ui.TwoColumns(Ui.Field("Font", font), Ui.Field("Size", Ui.Number(st.FontSize, 6, 200, v => T(x => x.Style.FontSize = v, "tsize")))),
            Ui.TwoColumns(
                Ui.Field("Header", Ui.ColorButton(t.HeaderFill, c => T(x => x.HeaderFill = c, "thf"), ThemeColors())),
                Ui.Field("Header text", Ui.ColorButton(t.HeaderTextColor, c => T(x => x.HeaderTextColor = c, "tht"), ThemeColors()))),
            Ui.TwoColumns(
                Ui.Field("Cells", Ui.ColorButton(t.CellFill, c => T(x => x.CellFill = c, "tcf"), ThemeColors())),
                Ui.Field("Bands", Ui.ColorButton(t.BandFill, c => T(x => x.BandFill = c, "tbf"), ThemeColors()))),
            Ui.TwoColumns(
                Ui.Field("Text", Ui.ColorButton(st.Color, c => T(x => x.Style.Color = c, "ttc"), ThemeColors())),
                Ui.Field("Align", Ui.Combo([(TextAlign.Left, "Left"), (TextAlign.Center, "Center"), (TextAlign.Right, "Right")], st.Align, v => T(x => x.Style.Align = v)))),
            Ui.TwoColumns(
                Ui.Field("Borders", Ui.ColorButton(t.BorderColor, c => T(x => x.BorderColor = c, "tbc"), ThemeColors())),
                Ui.Field("Border width", Ui.Number(t.BorderWidth, 0, 20, v => T(x => x.BorderWidth = v, "tbw")))));
    }

    /// <summary>A curated set of Segoe Fluent Icons glyphs that read well on slides.</summary>
    public static readonly (string glyph, string name)[] IconGallery =
    [
        ("\uE734", "Star"), ("\uEB51", "Heart"), ("\uE8E1", "Like"), ("\uE73E", "Check"), ("\uE711", "Cross"), ("\uE946", "Info"),
        ("\uE7BA", "Warning"), ("\uE897", "Help"), ("\uE713", "Settings"), ("\uE80F", "Home"), ("\uE77B", "Person"), ("\uE716", "People"),
        ("\uE715", "Mail"), ("\uE717", "Phone"), ("\uE8BD", "Chat"), ("\uE774", "Globe"), ("\uE707", "Location"), ("\uE787", "Calendar"),
        ("\uE823", "Clock"), ("\uE916", "Stopwatch"), ("\uE9D2", "Chart"), ("\uEB05", "Pie chart"), ("\uE8D4", "Trending"), ("\uE7C1", "Flag"),
        ("\uEA80", "Lightbulb"), ("\uE945", "Lightning"), ("\uE7F4", "Laptop"), ("\uE8EA", "Mobile"), ("\uE753", "Cloud"), ("\uE72E", "Lock"),
        ("\uE8D7", "Shield"), ("\uE719", "Cart"), ("\uE8C7", "Money"), ("\uE7B8", "Package"), ("\uE806", "Car"), ("\uE709", "Airplane"),
        ("\uE7FC", "Education"), ("\uE736", "Book"), ("\uE8B8", "Folder"), ("\uE8A5", "Document"), ("\uE722", "Camera"), ("\uE8B9", "Photo"),
        ("\uE768", "Play"), ("\uE720", "Microphone"), ("\uE767", "Speaker"), ("\uE8D6", "Music"), ("\uE721", "Search"), ("\uE71B", "Link"),
        ("\uE72A", "Forward"), ("\uE72B", "Back"), ("\uE74A", "Up"), ("\uE74B", "Down"), ("\uE895", "Sync"), ("\uE8FB", "Target"),
        ("\uE7EF", "Trophy"), ("\uE90F", "Repair"), ("\uE9CA", "Rocket"), ("\uE706", "Sun"), ("\uE708", "Moon"), ("\uE9F3", "Puzzle"),
    ];

    /// <summary>A grid of icon buttons used by the Insert tab and the icon format section.</summary>
    public static FrameworkElement IconPicker(Action<string> pick, string? current = null)
    {
        var wrap = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal, ItemWidth = 40, ItemHeight = 40, MaximumRowsOrColumns = 8 };
        foreach (var (glyph, name) in IconGallery)
        {
            var b = new Button
            {
                Width = 36, Height = 36, Padding = new Thickness(0), MinWidth = 0, MinHeight = 0, Content = Ui.Icon(glyph, 18),
                Background = glyph == current ? Palette.AccentSoft : Palette.Transparent, BorderBrush = glyph == current ? Palette.Accent : Palette.Transparent,
            };
            ToolTipService.SetToolTip(b, name);
            b.Click += (_, _) => pick(glyph);
            wrap.Children.Add(b);
        }
        return wrap;
    }

    FrameworkElement IconSection(Core.Model.IconElement ic)
    {
        var id = ic.Id;
        return Ui.Section("Icon",
            Ui.Field("Colour", Ui.ColorButton(ic.Color, c => Update(x => { if (x is Core.Model.IconElement y) y.Color = c; }, "icolor"), ThemeColors())),
            Ui.Field("Change icon", IconPicker(g => { Update(x => { if (x is Core.Model.IconElement y) y.Glyph = g; }, "iglyph"); Build(); }, ic.Glyph)));
    }

    /* ---------------- Selection pane ---------------- */

    bool _renaming;

    void BuildSelection(StackPanel body)
    {
        var elements = _ed.Current.Elements;
        var anyHidden = elements.Any(e => e.Hidden);
        body.Children.Add(Ui.Section("Selection",
            Ui.Row(
                Ui.Small("\uE7B3", "Show all", () => { foreach (var e in elements.ToList()) if (e.Hidden) _ed.SetHidden(e.Id, false); }),
                Ui.Small("\uED1A", "Hide all", () => { foreach (var e in elements.ToList()) if (!e.Hidden) _ed.SetHidden(e.Id, true); _ed.ClearSelection(); })),
            Ui.Text("Top of the list is front of the slide. Hidden objects stay in the file but do not show or play.", 11.5, Palette.SubtleText, wrap: TextWrapping.Wrap)));
        _ = anyHidden;
        var list = new StackPanel { Spacing = 2, Margin = new Thickness(0, 8, 0, 0) };
        for (var i = elements.Count - 1; i >= 0; i--)
        {
            var e = elements[i];
            var id = e.Id;
            var selected = _ed.Selection.Contains(id);
            var label = e switch
            {
                { Name: { Length: > 0 } n } => n,
                ITextHost th when th.Text.Trim().Length > 0 => th.Text.Trim().Split('\n')[0],
                _ => $"{e.KindName} {elements.Count(x => x.KindName == e.KindName && elements.IndexOf(x) <= elements.IndexOf(e))}",
            };
            var name = new TextBox
            {
                Text = label, BorderThickness = new Thickness(0), Background = Palette.Transparent, Padding = new Thickness(4, 3, 4, 3), MinHeight = 0,
                FontSize = 12.5, Opacity = e.Hidden ? .5 : 1, IsReadOnly = true,
            };
            ToolTipService.SetToolTip(name, "Click to select, double click to rename");
            name.GotFocus += (_, _) => { if (name.IsReadOnly && !_ed.Selection.Contains(id)) _ed.Select([id]); };
            name.DoubleTapped += (_, _) => { name.IsReadOnly = false; _renaming = true; name.SelectAll(); };
            name.LostFocus += (_, _) =>
            {
                if (name.IsReadOnly) return;
                name.IsReadOnly = true;
                _renaming = false;
                if (name.Text != label) _ed.Rename(id, name.Text);
            };
            name.KeyDown += (_, a) =>
            {
                if (a.Key is Windows.System.VirtualKey.Enter or Windows.System.VirtualKey.Escape && !name.IsReadOnly)
                {
                    if (a.Key == Windows.System.VirtualKey.Escape) name.Text = label;
                    a.Handled = true;
                    _scroll.Focus(FocusState.Programmatic);
                }
            };
            var kind = Ui.Icon(e switch { TextElement => "\uE8D2", ShapeElement => "\uF158", ImageElement => "\uE8B9", TableElement => "\uE80A", _ => "\uE734" }, 13, Palette.SubtleText);
            var tools = Ui.Row(
                Ui.Small("\uE70E", null, () => _ed.MoveInStack(id, 1), "Bring forward"),
                Ui.Small("\uE70D", null, () => _ed.MoveInStack(id, -1), "Send backward"),
                Ui.Small(e.Locked ? "\uE72E" : "\uE785", null, () => _ed.SetLocked(id, !e.Locked), e.Locked ? "Unlock" : "Lock"),
                Ui.Small(e.Hidden ? "\uED1A" : "\uE7B3", null, () => _ed.SetHidden(id, !e.Hidden), e.Hidden ? "Show" : "Hide"));
            var row = new Grid
            {
                ColumnSpacing = 6, Padding = new Thickness(6, 2, 2, 2), CornerRadius = new CornerRadius(4),
                Background = selected ? Palette.AccentSoft : Palette.Transparent,
                BorderBrush = selected ? Palette.Accent : Palette.Transparent, BorderThickness = new Thickness(1),
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(kind);
            Grid.SetColumn(name, 1);
            row.Children.Add(name);
            Grid.SetColumn(tools, 2);
            row.Children.Add(tools);
            if (e.GroupId != null) row.Margin = new Thickness(10, 0, 0, 0);
            list.Children.Add(row);
        }
        if (elements.Count == 0) list.Children.Add(Ui.Text("This slide has no objects.", 12, Palette.SubtleText));
        body.Children.Add(list);
    }

    /* ---------------- Live transition preview ---------------- */

    static DispatcherTimer? _hoverTimer;

    /// <summary>Hovering a transition tile for a moment previews it on the canvas without applying it, like Office live preview.</summary>
    public static void AttachHoverPreview(FrameworkElement tile, TransitionInfo info, Func<TransitionSettings?, Task>? preview, Action? cancel)
    {
        if (preview == null || info.Id == "none") return;
        tile.PointerEntered += (_, _) =>
        {
            _hoverTimer?.Stop();
            _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            _hoverTimer.Tick += async (t, _) =>
            {
                ((DispatcherTimer)t!).Stop();
                if (!Settings.Current.LivePreview) return;
                var s = new TransitionSettings { Effect = info.Id, Variant = info.DefaultVariant, DurationMs = info.DefaultDurationMs };
                await preview(s);
            };
            _hoverTimer.Start();
        };
        tile.PointerExited += (_, _) =>
        {
            _hoverTimer?.Stop();
            cancel?.Invoke();
        };
    }
}
