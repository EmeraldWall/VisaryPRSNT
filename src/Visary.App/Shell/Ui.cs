using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Visary.App.Shell;

/// <summary>Small factory helpers so the code built UI stays readable and consistent.</summary>
public static class Ui
{
    public static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static FontIcon Icon(string glyph, double size = 16, Brush? brush = null)
    {
        var i = new FontIcon { Glyph = glyph, FontFamily = IconFont, FontSize = size };
        if (brush != null) i.Foreground = brush;
        return i;
    }

    public static TextBlock Text(string text, double size = 13, Brush? brush = null, bool bold = false, TextWrapping wrap = TextWrapping.NoWrap)
    {
        var t = new TextBlock { Text = text, FontSize = size, TextWrapping = wrap, VerticalAlignment = VerticalAlignment.Center };
        if (brush != null) t.Foreground = brush;
        if (bold) t.FontWeight = FontWeights.SemiBold;
        return t;
    }

    static void Flat(Control b)
    {
        b.Background = Palette.Transparent;
        b.BorderBrush = Palette.Transparent;
        b.BorderThickness = new Thickness(1);
        b.Foreground = Palette.RibbonText;
        b.CornerRadius = new CornerRadius(4);
    }

    /// <summary>A tall ribbon button with the icon above the label.</summary>
    public static Button Large(string glyph, string label, Action onClick, string? tip = null)
    {
        var b = new Button
        {
            Content = new StackPanel
            {
                Spacing = 4,
                Children = { Icon(glyph, 22), new TextBlock { Text = label, FontSize = 11.5, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 76, TextWrapping = TextWrapping.WrapWholeWords } },
            },
            Padding = new Thickness(6, 4, 6, 4),
            MinWidth = 52,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Flat(b);
        b.Click += (_, _) => onClick();
        ToolTipService.SetToolTip(b, tip ?? label);
        return b;
    }

    /// <summary>A compact ribbon button: icon, optional label.</summary>
    public static Button Small(string glyph, string? label, Action onClick, string? tip = null)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Icon(glyph, 14) } };
        if (label != null) panel.Children.Add(new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        var b = new Button { Content = panel, Padding = new Thickness(6, 3, 6, 3), MinHeight = 28 };
        Flat(b);
        b.Click += (_, _) => onClick();
        ToolTipService.SetToolTip(b, tip ?? label);
        return b;
    }

    public static ToggleButton Toggle(string glyph, string tip, bool isOn, Action<bool> onToggle)
    {
        var b = new ToggleButton { Content = Icon(glyph, 14), Padding = new Thickness(6, 3, 6, 3), MinHeight = 28, MinWidth = 30, IsChecked = isOn };
        Flat(b);
        b.Click += (_, _) => onToggle(b.IsChecked == true);
        ToolTipService.SetToolTip(b, tip);
        return b;
    }

    public static DropDownButton Menu(string glyph, string label, MenuFlyout flyout, bool large = false, string? tip = null)
    {
        object content = large
            ? new StackPanel { Spacing = 4, Children = { Icon(glyph, 22), new TextBlock { Text = label, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center } } }
            : new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Icon(glyph, 14), new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } };
        var b = new DropDownButton { Content = content, Flyout = flyout, Padding = new Thickness(6, 3, 6, 3), VerticalAlignment = large ? VerticalAlignment.Stretch : VerticalAlignment.Center };
        Flat(b);
        ToolTipService.SetToolTip(b, tip ?? label);
        return b;
    }

    public static DropDownButton FlyoutButton(string glyph, string label, Flyout flyout, bool large = false)
    {
        var b = Menu(glyph, label, new MenuFlyout(), large);
        b.Flyout = flyout;
        return b;
    }

    public static MenuFlyoutItem Item(string text, Action onClick, string? glyph = null)
    {
        var i = new MenuFlyoutItem { Text = text };
        if (glyph != null) i.Icon = Icon(glyph);
        i.Click += (_, _) => onClick();
        return i;
    }

    /// <summary>A labelled ribbon group with a divider on its right.</summary>
    public static FrameworkElement Group(string title, params UIElement[] children)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Stretch };
        foreach (var c in children) content.Children.Add(c);
        var grid = new Grid { Padding = new Thickness(6, 2, 8, 0), BorderBrush = Palette.Border, BorderThickness = new Thickness(0, 0, 1, 0) };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(content);
        var label = new TextBlock { Text = title, FontSize = 11, Foreground = Palette.SubtleText, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 2) };
        Grid.SetRow(label, 1);
        grid.Children.Add(label);
        return grid;
    }

    public static StackPanel Rows(params UIElement[] rows)
    {
        var s = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        foreach (var r in rows) s.Children.Add(r);
        return s;
    }

    public static StackPanel Row(params UIElement[] items)
    {
        var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        foreach (var i in items) s.Children.Add(i);
        return s;
    }

    /* ---------------- pane fields ---------------- */

    public static StackPanel Section(string title, params UIElement[] children)
    {
        var s = new StackPanel { Spacing = 8, Padding = new Thickness(0, 12, 0, 12), BorderBrush = Palette.Border, BorderThickness = new Thickness(0, 0, 0, 1) };
        s.Children.Add(Text(title, 13, Palette.Text, bold: true));
        foreach (var c in children) s.Children.Add(c);
        return s;
    }

    public static FrameworkElement Field(string label, FrameworkElement control)
    {
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        return new StackPanel { Spacing = 3, Children = { Text(label, 11.5, Palette.SubtleText), control } };
    }

    public static Grid TwoColumns(UIElement a, UIElement b)
    {
        var g = new Grid { ColumnSpacing = 8 };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn((FrameworkElement)b, 1);
        g.Children.Add(a);
        g.Children.Add(b);
        return g;
    }

    public static NumberBox Number(double value, double min, double max, Action<double> onChange, double step = 1)
    {
        var n = new NumberBox
        {
            Value = Math.Round(value, 2), Minimum = min, Maximum = max, SmallChange = step, LargeChange = step * 10,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        };
        n.ValueChanged += (_, e) => { if (!double.IsNaN(e.NewValue) && Math.Abs(e.NewValue - e.OldValue) > 1e-9) onChange(e.NewValue); };
        return n;
    }

    public static Slider Slider(double value, double min, double max, Action<double> onChange, double step = 1)
    {
        var s = new Slider { Minimum = min, Maximum = max, StepFrequency = step, Value = value };
        s.ValueChanged += (_, e) => { if (Math.Abs(e.NewValue - e.OldValue) > 1e-9) onChange(e.NewValue); };
        return s;
    }

    public static ComboBox Combo<T>(IEnumerable<(T value, string label)> items, T selected, Action<T> onChange)
    {
        var list = items.ToList();
        var c = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var (_, label) in list) c.Items.Add(label);
        c.SelectedIndex = Math.Max(0, list.FindIndex(i => EqualityComparer<T>.Default.Equals(i.value, selected)));
        c.SelectionChanged += (_, _) => { if (c.SelectedIndex >= 0) onChange(list[c.SelectedIndex].value); };
        return c;
    }

    public static CheckBox Check(string label, bool value, Action<bool> onChange)
    {
        var c = new CheckBox { Content = label, IsChecked = value };
        c.Click += (_, _) => onChange(c.IsChecked == true);
        return c;
    }

    static readonly string[] Swatches =
    [
        "#000000", "#404040", "#7F7F7F", "#BFBFBF", "#FFFFFF", "#C43E1C", "#E8704F", "#D9A441", "#FFB900", "#107C10",
        "#2E7D52", "#038387", "#0F6CBD", "#4F46E5", "#7B5CB8", "#E3008C", "#A4262C", "#8C6D46", "#1F2937", "#F3F2F1",
    ];

    /// <summary>A colour button: theme colours, standard colours and a full colour picker.</summary>
    public static DropDownButton ColorButton(string color, Action<string> onChange, IReadOnlyList<string>? themeColors = null, string? glyph = null)
    {
        var chip = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(3), Background = ColorUtil.Brush(color), BorderBrush = Palette.Border, BorderThickness = new Thickness(1) };
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (glyph != null) content.Children.Add(Icon(glyph, 14));
        content.Children.Add(chip);
        var b = new DropDownButton { Content = content, Padding = new Thickness(6, 3, 6, 3) };
        var panel = new StackPanel { Spacing = 8, Width = 260 };
        void Pick(string c)
        {
            chip.Background = ColorUtil.Brush(c);
            onChange(c);
        }
        Grid SwatchGrid(IEnumerable<string> colors)
        {
            var g = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal, MaximumRowsOrColumns = 10, ItemWidth = 25, ItemHeight = 25 };
            foreach (var c in colors)
            {
                var sw = new Button { Width = 22, Height = 22, Padding = new Thickness(0), MinWidth = 0, MinHeight = 0, Background = ColorUtil.Brush(c), BorderBrush = Palette.Border, CornerRadius = new CornerRadius(3) };
                ToolTipService.SetToolTip(sw, c);
                sw.Click += (_, _) => { Pick(c); b.Flyout?.Hide(); };
                g.Children.Add(sw);
            }
            var wrap = new Grid();
            wrap.Children.Add(g);
            return wrap;
        }
        if (themeColors is { Count: > 0 })
        {
            panel.Children.Add(Text("Theme colours", 11.5, Palette.SubtleText));
            panel.Children.Add(SwatchGrid(themeColors));
        }
        panel.Children.Add(Text("Standard colours", 11.5, Palette.SubtleText));
        panel.Children.Add(SwatchGrid(Swatches));
        var picker = new ColorPicker
        {
            Color = ColorUtil.Parse(color), IsAlphaEnabled = true, IsMoreButtonVisible = false, IsColorChannelTextInputVisible = false,
            IsHexInputVisible = true, ColorSpectrumShape = ColorSpectrumShape.Ring,
        };
        picker.ColorChanged += (_, e) => Pick(ColorUtil.ToHex(e.NewColor));
        var more = new Expander { Header = "More colours", Content = picker, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(more);
        b.Flyout = new Flyout { Content = new ScrollViewer { Content = panel, MaxHeight = 640 } };
        return b;
    }

    public static Color Mix(Color a, Color b, double t) =>
        Color.FromArgb((byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
}
