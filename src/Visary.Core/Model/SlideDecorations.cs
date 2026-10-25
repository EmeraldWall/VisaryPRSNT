using Visary.Core.Themes;

namespace Visary.Core.Model;

/// <summary>Footer text and slide numbers. Generated on the fly, never stored on the slide.</summary>
public static class SlideDecorations
{
    public static List<TextElement> For(Presentation p, Slide s)
    {
        var list = new List<TextElement>();
        var isTitle = Factory.Layouts.FirstOrDefault(l => l.Id == s.Layout)?.IsTitle ?? false;
        if (isTitle) return list;
        var t = SlideThemes.Get(p.ThemeId);
        double W = p.Width, H = p.Height;
        var style = new TextStyle { FontFamily = t.BodyFont, FontSize = Math.Round(H * .018), Color = t.TextMuted, VerticalAlign = VerticalAlign.Middle };
        if (p.ShowSlideNumbers)
        {
            var num = style.Clone();
            num.Align = TextAlign.Right;
            list.Add(new TextElement { Id = "footer-number", X = W * .82, Y = H * .925, Width = W * .14, Height = H * .05, Text = (p.Slides.IndexOf(s) + 1).ToString(), Style = num, Padding = 0 });
        }
        if (!string.IsNullOrWhiteSpace(p.FooterText))
        {
            var foot = style.Clone();
            list.Add(new TextElement { Id = "footer-text", X = W * .04, Y = H * .925, Width = W * .6, Height = H * .05, Text = p.FooterText.Trim(), Style = foot, Padding = 0 });
        }
        return list;
    }
}
