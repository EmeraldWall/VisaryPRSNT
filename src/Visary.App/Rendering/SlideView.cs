using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Visary.App.Shell;
using Visary.Core.Model;
using Windows.Foundation;
using Windows.Storage.Streams;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Visary.App.Rendering;

public enum SlideViewMode { Edit, Thumbnail, Show }

/// <summary>The XAML pieces of one rendered object.</summary>
public sealed record ElementParts(Grid Host, Grid BuildLayer, Grid Content);

/// <summary>
/// Renders a slide at its logical size (for example 1920 x 1080). Callers scale it.
/// Structure: root, background layer, then one host per object:
/// Host (position, rotation, opacity) &gt; BuildLayer (object animations) &gt; Content (what is drawn).
/// </summary>
public sealed partial class SlideView : Grid
{
    readonly List<Task> _pending = [];

    public Slide Slide { get; }
    public Presentation Presentation { get; }
    public Grid BackgroundLayer { get; } = new();
    public Canvas ElementsLayer { get; } = new();
    public Dictionary<string, ElementParts> Parts { get; } = [];

    public SlideView(Presentation pres, Slide slide, SlideViewMode mode, string? hiddenElementId = null)
    {
        Presentation = pres;
        Slide = slide;
        Width = pres.Width;
        Height = pres.Height;
        IsHitTestVisible = false;
        // Clip to the slide so nothing outside it shows in thumbnails or during transitions.
        Clip = new RectangleGeometry { Rect = new Rect(0, 0, pres.Width, pres.Height) };
        BackgroundLayer.Width = pres.Width;
        BackgroundLayer.Height = pres.Height;
        BuildBackground();
        Children.Add(BackgroundLayer);
        ElementsLayer.Width = pres.Width;
        ElementsLayer.Height = pres.Height;
        Children.Add(ElementsLayer);
        foreach (var e in slide.Elements)
        {
            var parts = BuildElement(e);
            if (e.Id == hiddenElementId) parts.Host.Opacity = 0;
            Parts[e.Id] = parts;
            ElementsLayer.Children.Add(parts.Host);
        }
        _ = mode;
    }

    /// <summary>Completes when pictures are decoded, so GPU captures of this slide are not blank.</summary>
    public async Task ReadyAsync()
    {
        try
        {
            await Task.WhenAll(_pending).WaitAsync(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error("Loading pictures", ex);
        }
    }

    void BuildBackground()
    {
        var bg = Slide.Background;
        BackgroundLayer.Background = Paint.Fill(bg.Fill) ?? new SolidColorBrush(Microsoft.UI.Colors.White);
        if (bg.ImageMediaId is { } id && Presentation.Media.ContainsKey(id))
        {
            var (bmp, task) = MediaCache.Get(Presentation, id);
            _pending.Add(task);
            BackgroundLayer.Children.Add(new Border { Background = new ImageBrush { ImageSource = bmp, Stretch = Stretch.UniformToFill } });
            if (bg.ImageDim > 0)
                BackgroundLayer.Children.Add(new Border { Background = new SolidColorBrush(ColorUtil.WithAlpha(Microsoft.UI.Colors.Black, (byte)(Math.Clamp(bg.ImageDim, 0, 1) * 255))) });
        }
    }

    ElementParts BuildElement(SlideElement e)
    {
        var content = new Grid { Width = e.Width, Height = e.Height };
        switch (e)
        {
            case TextElement t:
                var box = new Border
                {
                    Background = Paint.Fill(t.Fill),
                    BorderBrush = t.Outline.Width > 0 ? ColorUtil.Brush(t.Outline.Color) : null,
                    BorderThickness = new Thickness(t.Outline.Width),
                    CornerRadius = new CornerRadius(t.CornerRadius),
                    Padding = new Thickness(t.Padding),
                    Child = TextBlockFor(t.Text, t.Style),
                };
                content.Children.Add(box);
                break;
            case ShapeElement s:
                content.Children.Add(ShapePath(s));
                if (s.IsLine && s.Kind == ShapeKind.ArrowLine) content.Children.Add(ArrowHead(s));
                if (!s.IsLine && !string.IsNullOrEmpty(s.Text))
                    content.Children.Add(new Border { Padding = new Thickness(Math.Min(s.Width, s.Height) * .08), Child = TextBlockFor(s.Text, s.Style) });
                break;
            case ImageElement img:
                var border = new Border
                {
                    CornerRadius = new CornerRadius(img.CornerRadius),
                    BorderBrush = img.Outline.Width > 0 ? ColorUtil.Brush(img.Outline.Color) : null,
                    BorderThickness = new Thickness(img.Outline.Width),
                };
                if (Presentation.Media.ContainsKey(img.MediaId))
                {
                    var (bmp, task) = MediaCache.Get(Presentation, img.MediaId);
                    _pending.Add(task);
                    border.Background = new ImageBrush
                    {
                        ImageSource = bmp,
                        Stretch = img.Fit switch { ImageFit.Fit => Stretch.Uniform, ImageFit.Stretch => Stretch.Fill, _ => Stretch.UniformToFill },
                    };
                }
                content.Children.Add(border);
                break;
        }
        var buildLayer = new Grid { Width = e.Width, Height = e.Height, Children = { content } };
        var host = new Grid { Width = e.Width, Height = e.Height, Opacity = e.Opacity, Children = { buildLayer } };
        if (e.Rotation != 0) host.RenderTransform = new RotateTransform { Angle = e.Rotation, CenterX = e.Width / 2, CenterY = e.Height / 2 };
        Canvas.SetLeft(host, e.X);
        Canvas.SetTop(host, e.Y);
        return new ElementParts(host, buildLayer, content);
    }

    /// <summary>Paragraphs with optional bullets or numbers and a hanging indent.</summary>
    public static FrameworkElement TextBlockFor(string text, TextStyle st)
    {
        var panel = new StackPanel
        {
            VerticalAlignment = st.VerticalAlign switch { VerticalAlign.Middle => VerticalAlignment.Center, VerticalAlign.Bottom => VerticalAlignment.Bottom, _ => VerticalAlignment.Top },
        };
        var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
        var number = 0;
        foreach (var line in lines)
        {
            var tb = Styled(new TextBlock { Text = line.Length == 0 ? " " : line, TextWrapping = TextWrapping.Wrap }, st);
            if (st.List == ListStyle.None || line.Length == 0)
            {
                panel.Children.Add(tb);
                continue;
            }
            number++;
            var marker = Styled(new TextBlock { Text = st.List == ListStyle.Number ? $"{number}." : "•" }, st);
            marker.TextAlignment = TextAlignment.Left;
            marker.Width = st.FontSize * (st.List == ListStyle.Number ? 1.6 : 1.1);
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(marker);
            Grid.SetColumn(tb, 1);
            row.Children.Add(tb);
            panel.Children.Add(row);
        }
        return panel;
    }

    static readonly Dictionary<string, FontFamily> Fonts = [];

    public static TextBlock Styled(TextBlock tb, TextStyle st)
    {
        if (!Fonts.TryGetValue(st.FontFamily, out var ff)) Fonts[st.FontFamily] = ff = new FontFamily(st.FontFamily);
        tb.FontFamily = ff;
        tb.FontSize = Math.Max(1, st.FontSize);
        tb.Foreground = ColorUtil.Brush(st.Color);
        tb.FontWeight = st.Bold ? FontWeights.Bold : FontWeights.Normal;
        tb.FontStyle = st.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal;
        var deco = Windows.UI.Text.TextDecorations.None;
        if (st.Underline) deco |= Windows.UI.Text.TextDecorations.Underline;
        if (st.Strikethrough) deco |= Windows.UI.Text.TextDecorations.Strikethrough;
        tb.TextDecorations = deco;
        tb.TextAlignment = st.Align switch { TextAlign.Center => TextAlignment.Center, TextAlign.Right => TextAlignment.Right, TextAlign.Justify => TextAlignment.Justify, _ => TextAlignment.Left };
        tb.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        tb.LineHeight = Math.Max(1, st.FontSize * st.LineSpacing * 1.2);
        if (st.CharacterSpacing != 0) tb.CharacterSpacing = (int)Math.Round(st.CharacterSpacing / Math.Max(1, st.FontSize) * 1000);
        return tb;
    }

    public static Path ShapePath(ShapeElement s)
    {
        var path = new Path
        {
            Data = Geometry(s.Kind, s.Width, s.Height, s.CornerRadius),
            Fill = s.IsLine ? null : Paint.Fill(s.Fill),
            Stroke = s.Outline.Width > 0 ? ColorUtil.Brush(s.Outline.Color) : null,
            StrokeThickness = s.Outline.Width,
            StrokeLineJoin = PenLineJoin.Round,
            Stretch = Stretch.None,
        };
        if (s.IsLine)
        {
            path.StrokeStartLineCap = PenLineCap.Round;
            path.StrokeEndLineCap = PenLineCap.Round;
        }
        if (s.Outline.Dash == DashStyle.Dash) path.StrokeDashArray = new DoubleCollection { 3, 2 };
        if (s.Outline.Dash == DashStyle.Dot) path.StrokeDashArray = new DoubleCollection { 1, 1.5 };
        return path;
    }

    static Path ArrowHead(ShapeElement s)
    {
        var a = Math.Max(14, s.Outline.Width * 3.2);
        var y = s.Height / 2;
        var fig = new PathFigure { StartPoint = new Point(s.Width + a * .25, y), IsClosed = true, IsFilled = true };
        fig.Segments.Add(new LineSegment { Point = new Point(s.Width - a * .75, y - a / 2) });
        fig.Segments.Add(new LineSegment { Point = new Point(s.Width - a * .75, y + a / 2) });
        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        return new Path { Data = geo, Fill = ColorUtil.Brush(s.Outline.Color) };
    }

    public static PathGeometry Geometry(ShapeKind kind, double w, double h, double radius)
    {
        var geo = new PathGeometry { FillRule = ShapeGeometry.UsesEvenOdd(kind) ? FillRule.EvenOdd : FillRule.Nonzero };
        PathFigure? fig = null;
        foreach (var c in ShapeGeometry.Build(kind, w, h, radius))
        {
            switch (c.Op)
            {
                case PathOp.Move:
                    fig = new PathFigure { StartPoint = new Point(c.X, c.Y), IsFilled = true, IsClosed = false };
                    geo.Figures.Add(fig);
                    break;
                case PathOp.Line:
                    fig?.Segments.Add(new LineSegment { Point = new Point(c.X, c.Y) });
                    break;
                case PathOp.Arc:
                    fig?.Segments.Add(new ArcSegment
                    {
                        Point = new Point(c.X, c.Y), Size = new Size(c.X1, c.Y1), IsLargeArc = c.Large,
                        SweepDirection = c.Clockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
                    });
                    break;
                case PathOp.Cubic:
                    fig?.Segments.Add(new BezierSegment { Point1 = new Point(c.X1, c.Y1), Point2 = new Point(c.X2, c.Y2), Point3 = new Point(c.X, c.Y) });
                    break;
                case PathOp.Close:
                    if (fig != null) fig.IsClosed = true;
                    break;
            }
        }
        return geo;
    }
}

public static class Paint
{
    public static Brush? Fill(FillSpec f)
    {
        switch (f.Kind)
        {
            case FillKind.None:
                return null;
            case FillKind.Gradient:
                var a = f.Angle * Math.PI / 180;
                double dx = Math.Cos(a) / 2, dy = Math.Sin(a) / 2;
                var g = new LinearGradientBrush { StartPoint = new Point(.5 - dx, .5 - dy), EndPoint = new Point(.5 + dx, .5 + dy) };
                g.GradientStops.Add(new GradientStop { Color = ColorUtil.Parse(f.Color), Offset = 0 });
                g.GradientStops.Add(new GradientStop { Color = ColorUtil.Parse(f.Color2), Offset = 1 });
                return g;
            default:
                return ColorUtil.Brush(f.Color);
        }
    }
}

/// <summary>Decoded pictures shared by every view of a presentation.</summary>
public static class MediaCache
{
    static readonly Dictionary<string, (BitmapImage bmp, Task task)> Cache = [];

    public static (BitmapImage bmp, Task task) Get(Presentation pres, string id)
    {
        if (Cache.TryGetValue(id, out var hit)) return hit;
        var bmp = new BitmapImage();
        var task = LoadAsync(bmp, pres.Media[id].Data);
        Cache[id] = (bmp, task);
        return (bmp, task);
    }

    static async Task LoadAsync(BitmapImage bmp, byte[] data)
    {
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(data.AsBuffer());
        stream.Seek(0);
        await bmp.SetSourceAsync(stream);
    }
}
