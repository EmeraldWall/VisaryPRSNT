using Visary.Core.Themes;

namespace Visary.Core.Model;

public sealed record SlideSize(string Id, string Name, double Width, double Height);
public sealed record LayoutInfo(string Id, string Name, bool IsTitle);

/// <summary>Creates elements, slides and presentations with sensible, theme aware defaults.</summary>
public static class Factory
{
    public static readonly IReadOnlyList<SlideSize> Sizes =
    [
        new("16:9", "Widescreen (16:9)", 1920, 1080),
        new("4:3", "Standard (4:3)", 1440, 1080),
        new("16:10", "Widescreen (16:10)", 1920, 1200),
        new("1:1", "Square (1:1)", 1080, 1080),
        new("9:16", "Portrait story (9:16)", 1080, 1920),
    ];

    public static readonly IReadOnlyList<LayoutInfo> Layouts =
    [
        new("title", "Title slide", true),
        new("title-content", "Title and content", false),
        new("section", "Section header", true),
        new("two-content", "Two content", false),
        new("title-only", "Title only", false),
        new("quote", "Quote", false),
        new("big-number", "Big number", false),
        new("blank", "Blank", false),
    ];

    public static TextElement Text(double x, double y, double w, double h, string text, TextStyle style, TextRole role = TextRole.None) =>
        new() { X = x, Y = y, Width = w, Height = h, Text = text, Style = style, Role = role };

    public static ShapeElement Shape(ShapeKind kind, double x, double y, double w, double h, string color)
    {
        var line = kind is ShapeKind.Line or ShapeKind.ArrowLine;
        return new ShapeElement
        {
            Kind = kind, X = x, Y = y, Width = w, Height = line ? Math.Max(h, 24) : h,
            Fill = line ? FillSpec.None() : FillSpec.Solid(color),
            Outline = line ? new Outline { Color = color, Width = 6 } : new Outline(),
        };
    }

    public static ImageElement Image(string mediaId, double x, double y, double w, double h) =>
        new() { MediaId = mediaId, X = x, Y = y, Width = w, Height = h };

    public static Background ThemeBackground(SlideTheme theme, bool title) => title ? theme.TitleBackground() : theme.ContentBackground();

    public static Slide CreateSlide(SlideTheme t, string layoutId, double W, double H)
    {
        var layout = Layouts.FirstOrDefault(l => l.Id == layoutId) ?? Layouts[1];
        var slide = new Slide { Layout = layout.Id, Background = ThemeBackground(t, layout.IsTitle) };
        var heading = new TextStyle { FontFamily = t.HeadingFont, Color = t.Text, Bold = true, LineSpacing = 1.05 };
        var body = new TextStyle { FontFamily = t.BodyFont, Color = t.Text, LineSpacing = 1.3 };
        var titleColor = layout.IsTitle ? t.TitleText : t.Text;
        TextStyle S(TextStyle s, double size, TextAlign a = TextAlign.Left, VerticalAlign v = VerticalAlign.Top, string? color = null)
        {
            var c = s.Clone();
            c.FontSize = Math.Round(size);
            c.Align = a;
            c.VerticalAlign = v;
            if (color != null) c.Color = color;
            return c;
        }

        var els = slide.Elements;
        switch (layout.Id)
        {
            case "title":
                els.Add(Text(W * .1, H * .28, W * .8, H * .26, "Presentation title", S(heading, H * .095, TextAlign.Center, VerticalAlign.Bottom, titleColor), TextRole.Title));
                els.Add(Text(W * .15, H * .57, W * .7, H * .1, "Subtitle or presenter name", S(body, H * .036, TextAlign.Center, color: titleColor), TextRole.Subtitle));
                break;
            case "title-content":
                els.Add(Text(W * .06, H * .06, W * .88, H * .15, "Slide title", S(heading, H * .062, v: VerticalAlign.Bottom), TextRole.Title));
                var list = S(body, H * .036);
                list.List = ListStyle.Bullet;
                els.Add(Text(W * .06, H * .26, W * .88, H * .64, "First key point\nSecond key point\nThird key point", list, TextRole.Body));
                break;
            case "section":
                els.Add(Shape(ShapeKind.Rectangle, W * .08, H * .63, W * .1, H * .008, t.Accents[0]));
                els.Add(Text(W * .08, H * .3, W * .84, H * .3, "Section title", S(heading, H * .085, v: VerticalAlign.Bottom, color: titleColor), TextRole.Title));
                els.Add(Text(W * .08, H * .67, W * .84, H * .1, "A short description of this section", S(body, H * .034, color: titleColor), TextRole.Subtitle));
                break;
            case "two-content":
                els.Add(Text(W * .06, H * .06, W * .88, H * .15, "Two column title", S(heading, H * .062, v: VerticalAlign.Bottom), TextRole.Title));
                var l2 = S(body, H * .033);
                l2.List = ListStyle.Bullet;
                els.Add(Text(W * .06, H * .27, W * .42, H * .63, "Left point one\nLeft point two", l2, TextRole.Body));
                els.Add(Text(W * .52, H * .27, W * .42, H * .63, "Right point one\nRight point two", l2.Clone(), TextRole.Body));
                break;
            case "title-only":
                els.Add(Text(W * .06, H * .06, W * .88, H * .15, "Slide title", S(heading, H * .062, v: VerticalAlign.Bottom), TextRole.Title));
                break;
            case "quote":
                els.Add(Text(W * .12, H * .26, W * .76, H * .34, "A memorable line that captures the idea.", WithItalic(S(heading, H * .058, TextAlign.Center, VerticalAlign.Middle)), TextRole.Title));
                els.Add(Text(W * .12, H * .64, W * .76, H * .08, "Author name", S(body, H * .03, TextAlign.Center, color: t.TextMuted), TextRole.Caption));
                break;
            case "big-number":
                els.Add(Text(W * .1, H * .16, W * .8, H * .42, "87%", S(heading, H * .26, TextAlign.Center, VerticalAlign.Middle, t.Accents[0]), TextRole.Title));
                els.Add(Text(W * .15, H * .62, W * .7, H * .12, "of people remember a number shown this way", S(body, H * .04, TextAlign.Center), TextRole.Body));
                break;
        }
        return slide;
    }

    static TextStyle WithItalic(TextStyle s)
    {
        s.Italic = true;
        s.Bold = false;
        return s;
    }

    public static Presentation CreatePresentation(string themeId = "office", double width = 1920, double height = 1080, bool sample = false)
    {
        var t = SlideThemes.Get(themeId);
        var p = new Presentation { ThemeId = t.Id, Width = width, Height = height, Title = sample ? $"{t.Name} deck" : "Untitled presentation" };
        if (!sample)
        {
            p.Slides.Add(CreateSlide(t, "title", width, height));
            return p;
        }

        // A short starter deck that shows the signature transitions.
        var s1 = CreateSlide(t, "title", width, height);
        ((TextElement)s1.Elements[0]).Text = $"{t.Name}";
        ((TextElement)s1.Elements[1]).Text = "Made with Visary PRSNT";
        s1.Transition = new TransitionSettings { Effect = "silk", DurationMs = 1100 };

        var s2 = CreateSlide(t, "title-content", width, height);
        ((TextElement)s2.Elements[0]).Text = "Agenda";
        ((TextElement)s2.Elements[1]).Text = "Where we are\nWhat we learned\nWhere we go next";
        s2.Transition = new TransitionSettings { Effect = "cascade", DurationMs = 1300 };

        var s3 = CreateSlide(t, "big-number", width, height);
        s3.Transition = new TransitionSettings { Effect = "depth", DurationMs = 1200 };

        // Slides 4 and 5 share element ids so Morph can glide between them.
        var s4 = CreateSlide(t, "title-only", width, height);
        ((TextElement)s4.Elements[0]).Text = "Morph";
        for (var i = 0; i < 3; i++)
            s4.Elements.Add(Shape(ShapeKind.Ellipse, width * (.16 + i * .26), height * .42, height * .3, height * .3, t.Accents[i % t.Accents.Count]));
        s4.Transition = new TransitionSettings { Effect = "sweep", DurationMs = 1200 };

        var s5 = Documents.DocumentCloner.CloneSlide(s4, keepIds: true);
        ((TextElement)s5.Elements[0]).Text = "Morph moves what stays";
        for (var i = 1; i < s5.Elements.Count; i++)
        {
            var e = s5.Elements[i];
            e.Width = e.Height = height * (.18 + i * .07);
            e.X = width * .55 + (i - 1) * width * .1;
            e.Y = height * (.25 + (i - 1) * .14);
        }
        s5.Transition = new TransitionSettings { Effect = "morph", DurationMs = 1400 };

        var s6 = CreateSlide(t, "section", width, height);
        ((TextElement)s6.Elements[1]).Text = "Thank you";
        ((TextElement)s6.Elements[2]).Text = "Questions?";
        s6.Transition = new TransitionSettings { Effect = "aurora", DurationMs = 1400 };
        p.Slides.AddRange([s1, s2, s3, s4, s5, s6]);
        return p;
    }
}
