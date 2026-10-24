namespace Visary.Core.Model;

public enum PathOp { Move, Line, Arc, Cubic, Close }

/// <summary>One drawing command. For arcs, P1 holds the radii and Large/Clockwise the flags.</summary>
public readonly record struct PathCommand(PathOp Op, double X = 0, double Y = 0, double X1 = 0, double Y1 = 0, double X2 = 0, double Y2 = 0, bool Large = false, bool Clockwise = true);

public sealed record ShapeInfo(ShapeKind Kind, string Name, string Group);

/// <summary>Resolution independent outlines for every shape, computed in the shape's own pixel box.</summary>
public static class ShapeGeometry
{
    public static readonly IReadOnlyList<ShapeInfo> Catalog =
    [
        new(ShapeKind.Rectangle, "Rectangle", "Basic"),
        new(ShapeKind.RoundedRectangle, "Rounded rectangle", "Basic"),
        new(ShapeKind.Ellipse, "Oval", "Basic"),
        new(ShapeKind.Triangle, "Triangle", "Basic"),
        new(ShapeKind.RightTriangle, "Right triangle", "Basic"),
        new(ShapeKind.Diamond, "Diamond", "Basic"),
        new(ShapeKind.Pentagon, "Pentagon", "Basic"),
        new(ShapeKind.Hexagon, "Hexagon", "Basic"),
        new(ShapeKind.Octagon, "Octagon", "Basic"),
        new(ShapeKind.Parallelogram, "Parallelogram", "Basic"),
        new(ShapeKind.Trapezoid, "Trapezoid", "Basic"),
        new(ShapeKind.Plus, "Plus", "Basic"),
        new(ShapeKind.Heart, "Heart", "Basic"),
        new(ShapeKind.Donut, "Donut", "Basic"),
        new(ShapeKind.Star4, "4 point star", "Stars"),
        new(ShapeKind.Star5, "5 point star", "Stars"),
        new(ShapeKind.Star6, "6 point star", "Stars"),
        new(ShapeKind.Callout, "Speech bubble", "Stars"),
        new(ShapeKind.ArrowRight, "Right arrow", "Arrows"),
        new(ShapeKind.ArrowLeft, "Left arrow", "Arrows"),
        new(ShapeKind.ArrowUp, "Up arrow", "Arrows"),
        new(ShapeKind.ArrowDown, "Down arrow", "Arrows"),
        new(ShapeKind.Chevron, "Chevron", "Arrows"),
        new(ShapeKind.Line, "Line", "Lines"),
        new(ShapeKind.ArrowLine, "Arrow", "Lines"),
    ];

    /// <summary>True when the outline consists of more than one closed figure filled with the even odd rule.</summary>
    public static bool UsesEvenOdd(ShapeKind k) => k == ShapeKind.Donut;

    public static List<PathCommand> Build(ShapeKind kind, double w, double h, double radius = 24)
    {
        var c = new List<PathCommand>();
        void Poly(params (double x, double y)[] p)
        {
            c.Add(new(PathOp.Move, p[0].x, p[0].y));
            for (var i = 1; i < p.Length; i++) c.Add(new(PathOp.Line, p[i].x, p[i].y));
            c.Add(new(PathOp.Close));
        }
        void Ellipse(double cx, double cy, double rx, double ry, bool clockwise = true)
        {
            c.Add(new(PathOp.Move, cx - rx, cy));
            c.Add(new(PathOp.Arc, cx + rx, cy, rx, ry, Clockwise: clockwise));
            c.Add(new(PathOp.Arc, cx - rx, cy, rx, ry, Clockwise: clockwise));
            c.Add(new(PathOp.Close));
        }
        (double, double)[] Regular(int n, double inner = 0)
        {
            var count = inner > 0 ? n * 2 : n;
            var pts = new (double, double)[count];
            for (var i = 0; i < count; i++)
            {
                var a = -Math.PI / 2 + i * 2 * Math.PI / count;
                var k = inner > 0 && i % 2 == 1 ? inner : 1;
                pts[i] = (w / 2 + w / 2 * k * Math.Cos(a), h / 2 + h / 2 * k * Math.Sin(a));
            }
            return pts;
        }

        switch (kind)
        {
            case ShapeKind.Rectangle: Poly((0, 0), (w, 0), (w, h), (0, h)); break;
            case ShapeKind.RoundedRectangle:
            {
                var r = Math.Max(0, Math.Min(radius, Math.Min(w, h) / 2));
                c.Add(new(PathOp.Move, r, 0));
                c.Add(new(PathOp.Line, w - r, 0));
                c.Add(new(PathOp.Arc, w, r, r, r));
                c.Add(new(PathOp.Line, w, h - r));
                c.Add(new(PathOp.Arc, w - r, h, r, r));
                c.Add(new(PathOp.Line, r, h));
                c.Add(new(PathOp.Arc, 0, h - r, r, r));
                c.Add(new(PathOp.Line, 0, r));
                c.Add(new(PathOp.Arc, r, 0, r, r));
                c.Add(new(PathOp.Close));
                break;
            }
            case ShapeKind.Ellipse: Ellipse(w / 2, h / 2, w / 2, h / 2); break;
            case ShapeKind.Donut:
                Ellipse(w / 2, h / 2, w / 2, h / 2);
                Ellipse(w / 2, h / 2, w * .3, h * .3, false);
                break;
            case ShapeKind.Triangle: Poly((w / 2, 0), (w, h), (0, h)); break;
            case ShapeKind.RightTriangle: Poly((0, 0), (w, h), (0, h)); break;
            case ShapeKind.Diamond: Poly((w / 2, 0), (w, h / 2), (w / 2, h), (0, h / 2)); break;
            case ShapeKind.Pentagon: Poly(Regular(5)); break;
            case ShapeKind.Hexagon: Poly((w * .25, 0), (w * .75, 0), (w, h / 2), (w * .75, h), (w * .25, h), (0, h / 2)); break;
            case ShapeKind.Octagon:
            {
                var a = Math.Min(w, h) * .29;
                Poly((a, 0), (w - a, 0), (w, a), (w, h - a), (w - a, h), (a, h), (0, h - a), (0, a));
                break;
            }
            case ShapeKind.Parallelogram: Poly((w * .25, 0), (w, 0), (w * .75, h), (0, h)); break;
            case ShapeKind.Trapezoid: Poly((w * .2, 0), (w * .8, 0), (w, h), (0, h)); break;
            case ShapeKind.Plus:
            {
                double a = w * .33, b = h * .33;
                Poly((a, 0), (w - a, 0), (w - a, b), (w, b), (w, h - b), (w - a, h - b), (w - a, h), (a, h), (a, h - b), (0, h - b), (0, b), (a, b));
                break;
            }
            case ShapeKind.Star4: Poly(Regular(4, .38)); break;
            case ShapeKind.Star5: Poly(Regular(5, .45)); break;
            case ShapeKind.Star6: Poly(Regular(6, .55)); break;
            case ShapeKind.ArrowRight: Poly((0, h * .25), (w * .6, h * .25), (w * .6, 0), (w, h / 2), (w * .6, h), (w * .6, h * .75), (0, h * .75)); break;
            case ShapeKind.ArrowLeft: Poly((w, h * .25), (w * .4, h * .25), (w * .4, 0), (0, h / 2), (w * .4, h), (w * .4, h * .75), (w, h * .75)); break;
            case ShapeKind.ArrowUp: Poly((w * .25, h), (w * .25, h * .4), (0, h * .4), (w / 2, 0), (w, h * .4), (w * .75, h * .4), (w * .75, h)); break;
            case ShapeKind.ArrowDown: Poly((w * .25, 0), (w * .25, h * .6), (0, h * .6), (w / 2, h), (w, h * .6), (w * .75, h * .6), (w * .75, 0)); break;
            case ShapeKind.Chevron: Poly((0, 0), (w * .7, 0), (w, h / 2), (w * .7, h), (0, h), (w * .3, h / 2)); break;
            case ShapeKind.Heart:
                c.Add(new(PathOp.Move, w / 2, h * .25));
                c.Add(new(PathOp.Cubic, w * .22, 0, w / 2, h * .1, w * .35, 0));
                c.Add(new(PathOp.Cubic, 0, h * .3, w * .08, 0, 0, h * .12));
                c.Add(new(PathOp.Cubic, w / 2, h, 0, h * .55, w * .3, h * .75));
                c.Add(new(PathOp.Cubic, w, h * .3, w * .7, h * .75, w, h * .55));
                c.Add(new(PathOp.Cubic, w * .78, 0, w, h * .12, w * .92, 0));
                c.Add(new(PathOp.Cubic, w / 2, h * .25, w * .65, 0, w / 2, h * .1));
                c.Add(new(PathOp.Close));
                break;
            case ShapeKind.Callout:
            {
                var bh = h * .78;
                Poly((0, 0), (w, 0), (w, bh), (w * .42, bh), (w * .22, h), (w * .26, bh), (0, bh));
                break;
            }
            case ShapeKind.Line:
            case ShapeKind.ArrowLine:
                c.Add(new(PathOp.Move, 0, h / 2));
                c.Add(new(PathOp.Line, w, h / 2));
                break;
        }
        return c;
    }
}
