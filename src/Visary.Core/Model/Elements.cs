using System.Text.Json.Serialization;

namespace Visary.Core.Model;

public enum FillKind { None, Solid, Gradient }

/// <summary>Solid colour or two stop linear gradient. Colours are "#RRGGBB" or "#AARRGGBB".</summary>
public sealed class FillSpec
{
    public FillKind Kind { get; set; } = FillKind.Solid;
    public string Color { get; set; } = "#FFFFFF";
    public string Color2 { get; set; } = "#FFFFFF";
    /// <summary>Gradient angle in degrees, 0 points right and 90 points down.</summary>
    public double Angle { get; set; } = 45;

    public static FillSpec None() => new() { Kind = FillKind.None };
    public static FillSpec Solid(string color) => new() { Kind = FillKind.Solid, Color = color };
    public static FillSpec Gradient(string a, string b, double angle) => new() { Kind = FillKind.Gradient, Color = a, Color2 = b, Angle = angle };
    public FillSpec Clone() => (FillSpec)MemberwiseClone();
}

public enum DashStyle { Solid, Dash, Dot }

public sealed class Outline
{
    public string Color { get; set; } = "#000000";
    public double Width { get; set; }
    public DashStyle Dash { get; set; } = DashStyle.Solid;
    public Outline Clone() => (Outline)MemberwiseClone();
}

public enum TextAlign { Left, Center, Right, Justify }
public enum VerticalAlign { Top, Middle, Bottom }
public enum ListStyle { None, Bullet, Number }

public sealed class TextStyle
{
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 36;
    public string Color { get; set; } = "#1F1F1F";
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public bool Strikethrough { get; set; }
    public TextAlign Align { get; set; } = TextAlign.Left;
    public VerticalAlign VerticalAlign { get; set; } = VerticalAlign.Top;
    public double LineSpacing { get; set; } = 1.15;
    public double CharacterSpacing { get; set; }
    public ListStyle List { get; set; } = ListStyle.None;
    public TextStyle Clone() => (TextStyle)MemberwiseClone();
}

public enum TextRole { None, Title, Subtitle, Body, Caption }

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TextElement), "text")]
[JsonDerivedType(typeof(ShapeElement), "shape")]
[JsonDerivedType(typeof(ImageElement), "image")]
public abstract class SlideElement
{
    public string Id { get; set; } = Ids.New();
    /// <summary>Optional name. Morph pairs objects with the same id or the same name.</summary>
    public string? Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 200;
    public double Height { get; set; } = 100;
    public double Rotation { get; set; }
    public double Opacity { get; set; } = 1;
    public bool Locked { get; set; }

    [JsonIgnore] public RectD Bounds => new(X, Y, Width, Height);

    public abstract string KindName { get; }
}

/// <summary>An element that carries text (text boxes and shapes).</summary>
public interface ITextHost
{
    string Text { get; set; }
    TextStyle Style { get; set; }
}

public sealed class TextElement : SlideElement, ITextHost
{
    public string Text { get; set; } = "";
    public TextStyle Style { get; set; } = new();
    public FillSpec Fill { get; set; } = FillSpec.None();
    public Outline Outline { get; set; } = new();
    public double Padding { get; set; } = 12;
    public double CornerRadius { get; set; }
    public TextRole Role { get; set; } = TextRole.None;
    public override string KindName => "Text box";
}

public enum ShapeKind
{
    Rectangle, RoundedRectangle, Ellipse, Triangle, RightTriangle, Diamond, Pentagon, Hexagon, Octagon,
    Parallelogram, Trapezoid, Star4, Star5, Star6, Plus, Heart, ArrowRight, ArrowLeft, ArrowUp, ArrowDown,
    Chevron, Callout, Donut, Line, ArrowLine,
}

public sealed class ShapeElement : SlideElement, ITextHost
{
    public ShapeKind Kind { get; set; } = ShapeKind.Rectangle;
    public FillSpec Fill { get; set; } = FillSpec.Solid("#C43E1C");
    public Outline Outline { get; set; } = new();
    public double CornerRadius { get; set; } = 24;
    public string Text { get; set; } = "";
    public TextStyle Style { get; set; } = new() { Color = "#FFFFFF", Align = TextAlign.Center, VerticalAlign = VerticalAlign.Middle, FontSize = 32 };
    [JsonIgnore] public bool IsLine => Kind is ShapeKind.Line or ShapeKind.ArrowLine;
    public override string KindName => IsLine ? "Line" : "Shape";
}

public enum ImageFit { Fill, Fit, Stretch }

public sealed class ImageElement : SlideElement
{
    /// <summary>Key into <see cref="Presentation.Media"/>.</summary>
    public string MediaId { get; set; } = "";
    public ImageFit Fit { get; set; } = ImageFit.Fill;
    public double CornerRadius { get; set; }
    public Outline Outline { get; set; } = new();
    public override string KindName => "Picture";
}

public static class Ids
{
    public static string New() => Guid.NewGuid().ToString("N")[..12];
}
