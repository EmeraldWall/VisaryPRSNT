using System.Globalization;
using System.Security;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using Visary.Core.Model;
using Visary.Core.Themes;
using P = DocumentFormat.OpenXml.Presentation;
using D = DocumentFormat.OpenXml.Drawing;

namespace Visary.Core.Export;

/// <summary>
/// Writes a PowerPoint file. Text, shapes, pictures, backgrounds, hidden slides and transitions
/// are exported. Each signature transition maps to the closest native PowerPoint transition,
/// and objects are named "!!id" so PowerPoint's Morph pairs the same objects Visary does.
/// </summary>
public static class PptxExporter
{
    const long EmuPerPx = 6350; // 1920 px wide = 13.333 in, PowerPoint's widescreen size.
    const string NsDecl = "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"";
    const string GroupHeader = "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>";

    public static void Export(Presentation pres, string path)
    {
        using var fs = File.Create(path);
        Export(pres, fs);
    }

    public static void Export(Presentation pres, Stream stream)
    {
        using var doc = PresentationDocument.Create(stream, DocumentFormat.OpenXml.PresentationDocumentType.Presentation);
        var presPart = doc.AddPresentationPart();
        var theme = SlideThemes.Get(pres.ThemeId);

        var master = presPart.AddNewPart<SlideMasterPart>("rId1");
        var layout = master.AddNewPart<SlideLayoutPart>("rId1");
        var themePart = master.AddNewPart<ThemePart>("rId2");
        themePart.Theme = new D.Theme(ThemeXml(theme));
        layout.SlideLayout = new P.SlideLayout($"<p:sldLayout {NsDecl} type=\"blank\" preserve=\"1\"><p:cSld name=\"Blank\"><p:spTree>{GroupHeader}</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sldLayout>");
        layout.AddPart(master, "rId1");
        master.SlideMaster = new P.SlideMaster($"<p:sldMaster {NsDecl}><p:cSld><p:spTree>{GroupHeader}</p:spTree></p:cSld><p:clrMap bg1=\"lt1\" tx1=\"dk1\" bg2=\"lt2\" tx2=\"dk2\" accent1=\"accent1\" accent2=\"accent2\" accent3=\"accent3\" accent4=\"accent4\" accent5=\"accent5\" accent6=\"accent6\" hlink=\"hlink\" folHlink=\"folHlink\"/><p:sldLayoutIdLst><p:sldLayoutId id=\"2147483649\" r:id=\"rId1\"/></p:sldLayoutIdLst></p:sldMaster>");
        presPart.AddPart(themePart, "rId2");

        var slideIds = new StringBuilder();
        for (var i = 0; i < pres.Slides.Count; i++)
        {
            var rid = $"rIdS{i + 1}";
            var slidePart = presPart.AddNewPart<SlidePart>(rid);
            slidePart.AddPart(layout, "rId1");
            slidePart.Slide = new P.Slide(SlideXml(pres, pres.Slides[i], slidePart));
            slideIds.Append($"<p:sldId id=\"{256 + i}\" r:id=\"{rid}\"/>");
        }

        presPart.Presentation = new P.Presentation(
            $"<p:presentation {NsDecl} saveSubsetFonts=\"1\"><p:sldMasterIdLst><p:sldMasterId id=\"2147483648\" r:id=\"rId1\"/></p:sldMasterIdLst>" +
            $"<p:sldIdLst>{slideIds}</p:sldIdLst><p:sldSz cx=\"{Emu(pres.Width)}\" cy=\"{Emu(pres.Height)}\"/><p:notesSz cx=\"6858000\" cy=\"9144000\"/></p:presentation>");
        doc.PackageProperties.Title = pres.Title;
        doc.PackageProperties.Creator = "Visary PRSNT";
    }

    static long Emu(double px) => (long)Math.Round(px * EmuPerPx);
    static string I(double v) => Math.Round(v).ToString(CultureInfo.InvariantCulture);
    static string X(string s) => SecurityElement.Escape(s) ?? "";

    static (string rgb, int alpha) Color(string c, double opacity = 1)
    {
        var h = c.TrimStart('#');
        var a = 255;
        if (h.Length == 8) { a = Convert.ToInt32(h[..2], 16); h = h[2..]; }
        if (h.Length == 3) h = string.Concat(h.Select(ch => $"{ch}{ch}"));
        if (h.Length != 6 || !h.All(Uri.IsHexDigit)) h = "000000";
        return (h.ToUpperInvariant(), (int)Math.Round(a / 255.0 * Math.Clamp(opacity, 0, 1) * 100000));
    }

    static string Clr(string c, double opacity = 1)
    {
        var (rgb, alpha) = Color(c, opacity);
        return alpha >= 100000 ? $"<a:srgbClr val=\"{rgb}\"/>" : $"<a:srgbClr val=\"{rgb}\"><a:alpha val=\"{alpha}\"/></a:srgbClr>";
    }

    static string Fill(FillSpec f, double opacity = 1) => f.Kind switch
    {
        FillKind.None => "<a:noFill/>",
        FillKind.Gradient => $"<a:gradFill rotWithShape=\"1\"><a:gsLst><a:gs pos=\"0\">{Clr(f.Color, opacity)}</a:gs><a:gs pos=\"100000\">{Clr(f.Color2, opacity)}</a:gs></a:gsLst><a:lin ang=\"{I(((f.Angle % 360) + 360) % 360 * 60000)}\" scaled=\"0\"/></a:gradFill>",
        _ => $"<a:solidFill>{Clr(f.Color, opacity)}</a:solidFill>",
    };

    static string Line(Outline o, double opacity, bool arrow = false)
    {
        if (o.Width <= 0) return "<a:ln><a:noFill/></a:ln>";
        var dash = o.Dash switch { DashStyle.Dash => "dash", DashStyle.Dot => "sysDot", _ => "solid" };
        return $"<a:ln w=\"{Emu(o.Width)}\"><a:solidFill>{Clr(o.Color, opacity)}</a:solidFill><a:prstDash val=\"{dash}\"/>{(arrow ? "<a:tailEnd type=\"triangle\"/>" : "")}</a:ln>";
    }

    static string Xfrm(double x, double y, double w, double h, double rot) =>
        $"<a:xfrm{(rot != 0 ? $" rot=\"{I((((rot % 360) + 360) % 360) * 60000)}\"" : "")}><a:off x=\"{Emu(x)}\" y=\"{Emu(y)}\"/><a:ext cx=\"{Emu(Math.Max(0, w))}\" cy=\"{Emu(Math.Max(0, h))}\"/></a:xfrm>";

    static string SlideXml(Presentation pres, Slide s, SlidePart part)
    {
        var sb = new StringBuilder();
        sb.Append($"<p:sld {NsDecl}{(s.Hidden ? " show=\"0\"" : "")}><p:cSld>");
        sb.Append("<p:bg><p:bgPr>");
        if (s.Background.ImageMediaId is { } bgId && pres.Media.TryGetValue(bgId, out var bgMedia))
            sb.Append($"<a:blipFill dpi=\"0\" rotWithShape=\"1\"><a:blip r:embed=\"{AddImage(part, bgMedia)}\"/><a:srcRect/><a:stretch><a:fillRect/></a:stretch></a:blipFill>");
        else
            sb.Append(s.Background.Fill.Kind == FillKind.None ? "<a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill>" : Fill(s.Background.Fill));
        sb.Append("<a:effectLst/></p:bgPr></p:bg><p:spTree>").Append(GroupHeader);

        var id = 2;
        foreach (var e in s.Elements)
        {
            var name = $"!!{X(e.Id)}";
            switch (e)
            {
                case TextElement t:
                    sb.Append($"<p:sp><p:nvSpPr><p:cNvPr id=\"{id++}\" name=\"{name}\"/><p:cNvSpPr txBox=\"1\"/><p:nvPr/></p:nvSpPr><p:spPr>{Xfrm(t.X, t.Y, t.Width, t.Height, t.Rotation)}");
                    sb.Append(t.CornerRadius > 0 ? RoundGeom(t.CornerRadius, t.Width, t.Height) : "<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom>");
                    sb.Append(Fill(t.Fill, t.Opacity)).Append(Line(t.Outline, t.Opacity)).Append("</p:spPr>");
                    sb.Append(TextBody(t.Text, t.Style, t.Padding, t.Opacity)).Append("</p:sp>");
                    break;
                case ShapeElement sh:
                {
                    var line = sh.IsLine;
                    var y = line ? sh.Y + sh.Height / 2 : sh.Y;
                    var h = line ? 0 : sh.Height;
                    sb.Append($"<p:sp><p:nvSpPr><p:cNvPr id=\"{id++}\" name=\"{name}\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr><p:spPr>{Xfrm(sh.X, y, sh.Width, h, sh.Rotation)}");
                    sb.Append(sh.Kind == ShapeKind.RoundedRectangle ? RoundGeom(sh.CornerRadius, sh.Width, sh.Height) : $"<a:prstGeom prst=\"{Preset(sh.Kind)}\"><a:avLst/></a:prstGeom>");
                    sb.Append(line ? "<a:noFill/>" : Fill(sh.Fill, sh.Opacity)).Append(Line(sh.Outline, sh.Opacity, sh.Kind == ShapeKind.ArrowLine)).Append("</p:spPr>");
                    if (!line) sb.Append(TextBody(sh.Text, sh.Style, Math.Min(sh.Width, sh.Height) * .08, sh.Opacity));
                    sb.Append("</p:sp>");
                    break;
                }
                case ImageElement img when pres.Media.TryGetValue(img.MediaId, out var media):
                {
                    var rid = AddImage(part, media);
                    double x = img.X, y = img.Y, w = img.Width, h = img.Height;
                    var src = "";
                    if (ImageInfo.TryGetSize(media.Data, out var pw, out var ph))
                    {
                        var boxRatio = w / h;
                        var imgRatio = (double)pw / ph;
                        if (img.Fit == ImageFit.Fill)
                        {
                            // Crop the picture to fill the box, like the editor does.
                            if (imgRatio > boxRatio)
                            {
                                var crop = (1 - boxRatio / imgRatio) / 2 * 100000;
                                src = $"<a:srcRect l=\"{I(crop)}\" r=\"{I(crop)}\"/>";
                            }
                            else if (imgRatio < boxRatio)
                            {
                                var crop = (1 - imgRatio / boxRatio) / 2 * 100000;
                                src = $"<a:srcRect t=\"{I(crop)}\" b=\"{I(crop)}\"/>";
                            }
                        }
                        else if (img.Fit == ImageFit.Fit)
                        {
                            if (imgRatio > boxRatio) { var nh = w / imgRatio; y += (h - nh) / 2; h = nh; }
                            else { var nw = h * imgRatio; x += (w - nw) / 2; w = nw; }
                        }
                    }
                    sb.Append($"<p:pic><p:nvPicPr><p:cNvPr id=\"{id++}\" name=\"{name}\"/><p:cNvPicPr><a:picLocks noChangeAspect=\"1\"/></p:cNvPicPr><p:nvPr/></p:nvPicPr>");
                    sb.Append($"<p:blipFill><a:blip r:embed=\"{rid}\">{(img.Opacity < 1 ? $"<a:alphaModFix amt=\"{I(img.Opacity * 100000)}\"/>" : "")}</a:blip>{src}<a:stretch><a:fillRect/></a:stretch></p:blipFill>");
                    sb.Append($"<p:spPr>{Xfrm(x, y, w, h, img.Rotation)}");
                    sb.Append(img.CornerRadius > 0 ? RoundGeom(img.CornerRadius, w, h) : "<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom>");
                    sb.Append(Line(img.Outline, img.Opacity)).Append("</p:spPr></p:pic>");
                    break;
                }
            }
        }
        sb.Append("</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>");
        sb.Append(TransitionXml(s.Transition));
        sb.Append("</p:sld>");
        return sb.ToString();
    }

    static string AddImage(SlidePart part, MediaItem media)
    {
        var type = media.ContentType is "image/png" or "image/jpeg" or "image/gif" or "image/bmp" ? media.ContentType : "image/png";
        var img = part.AddImagePart(type);
        using (var ms = new MemoryStream(media.Data)) img.FeedData(ms);
        return part.GetIdOfPart(img);
    }

    static string RoundGeom(double radius, double w, double h)
    {
        var adj = Math.Clamp(radius / Math.Max(1, Math.Min(w, h)) * 100000, 0, 50000);
        return $"<a:prstGeom prst=\"roundRect\"><a:avLst><a:gd name=\"adj\" fmla=\"val {I(adj)}\"/></a:avLst></a:prstGeom>";
    }

    static string Preset(ShapeKind k) => k switch
    {
        ShapeKind.Rectangle => "rect",
        ShapeKind.RoundedRectangle => "roundRect",
        ShapeKind.Ellipse => "ellipse",
        ShapeKind.Triangle => "triangle",
        ShapeKind.RightTriangle => "rtTriangle",
        ShapeKind.Diamond => "diamond",
        ShapeKind.Pentagon => "pentagon",
        ShapeKind.Hexagon => "hexagon",
        ShapeKind.Octagon => "octagon",
        ShapeKind.Parallelogram => "parallelogram",
        ShapeKind.Trapezoid => "trapezoid",
        ShapeKind.Star4 => "star4",
        ShapeKind.Star5 => "star5",
        ShapeKind.Star6 => "star6",
        ShapeKind.Plus => "plus",
        ShapeKind.Heart => "heart",
        ShapeKind.ArrowRight => "rightArrow",
        ShapeKind.ArrowLeft => "leftArrow",
        ShapeKind.ArrowUp => "upArrow",
        ShapeKind.ArrowDown => "downArrow",
        ShapeKind.Chevron => "chevron",
        ShapeKind.Callout => "wedgeRectCallout",
        ShapeKind.Donut => "donut",
        ShapeKind.Line or ShapeKind.ArrowLine => "line",
        _ => "rect",
    };

    static string TextBody(string text, TextStyle st, double inset, double opacity)
    {
        var anchor = st.VerticalAlign switch { VerticalAlign.Middle => "ctr", VerticalAlign.Bottom => "b", _ => "t" };
        var algn = st.Align switch { TextAlign.Center => "ctr", TextAlign.Right => "r", TextAlign.Justify => "just", _ => "l" };
        var ins = Emu(inset);
        var sb = new StringBuilder($"<p:txBody><a:bodyPr wrap=\"square\" lIns=\"{ins}\" tIns=\"{ins}\" rIns=\"{ins}\" bIns=\"{ins}\" anchor=\"{anchor}\" rtlCol=\"0\"><a:noAutofit/></a:bodyPr><a:lstStyle/>");
        var sz = I(Math.Clamp(st.FontSize * 50, 100, 400000));
        var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
        var indent = Emu(st.FontSize * 1.1);
        for (var i = 0; i < lines.Length; i++)
        {
            var bullet = st.List switch
            {
                ListStyle.Bullet => $" marL=\"{indent}\" indent=\"-{indent}\"",
                ListStyle.Number => $" marL=\"{indent}\" indent=\"-{indent}\"",
                _ => "",
            };
            sb.Append($"<a:p><a:pPr algn=\"{algn}\"{bullet}><a:lnSpc><a:spcPct val=\"{I(st.LineSpacing * 100000)}\"/></a:lnSpc>");
            sb.Append(st.List switch
            {
                ListStyle.Bullet => "<a:buFont typeface=\"Arial\"/><a:buChar char=\"•\"/>",
                ListStyle.Number => "<a:buFont typeface=\"+mj-lt\"/><a:buAutoNum type=\"arabicPeriod\"/>",
                _ => "<a:buNone/>",
            });
            sb.Append("</a:pPr>");
            var rPr = $"lang=\"en-US\" sz=\"{sz}\"{(st.Bold ? " b=\"1\"" : "")}{(st.Italic ? " i=\"1\"" : "")}{(st.Underline ? " u=\"sng\"" : "")}{(st.Strikethrough ? " strike=\"sngStrike\"" : "")}{(st.CharacterSpacing != 0 ? $" spc=\"{I(st.CharacterSpacing * 50)}\"" : "")} dirty=\"0\"";
            var inner = $"<a:solidFill>{Clr(st.Color, opacity)}</a:solidFill><a:latin typeface=\"{X(st.FontFamily)}\"/><a:cs typeface=\"{X(st.FontFamily)}\"/>";
            if (lines[i].Length > 0) sb.Append($"<a:r><a:rPr {rPr}>{inner}</a:rPr><a:t>{X(lines[i])}</a:t></a:r>");
            sb.Append($"<a:endParaRPr {rPr}>{inner}</a:endParaRPr></a:p>");
        }
        return sb.Append("</p:txBody>").ToString();
    }

    /* ---------------- transitions ---------------- */

    const string McNs = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    const string P14Ns = "http://schemas.microsoft.com/office/powerpoint/2010/main";
    const string P15Ns = "http://schemas.microsoft.com/office/powerpoint/2012/main";
    const string P159Ns = "http://schemas.microsoft.com/office/powerpoint/2015/09/main";

    static string PushDir(string? from) => from switch { "left" => "r", "top" => "d", "bottom" => "u", _ => "l" };

    /// <summary>Closest native PowerPoint transition for each Visary transition: (namespace prefix, element xml).</summary>
    public static (string ns, string xml)? NativeTransition(TransitionSettings t)
    {
        var info = Transitions.TransitionCatalog.Get(t.Effect);
        var v = string.IsNullOrEmpty(t.Variant) ? info.DefaultVariant : t.Variant;
        return info.Id switch
        {
            "none" => null,
            "fade" or "silk" or "drift" or "focus" or "frost" or "cascade" => ("p", "<p:fade/>"),
            "glide" or "velocity" => ("p", $"<p:push dir=\"{PushDir(v)}\"/>"),
            "ribbon" => ("p", "<p:push dir=\"l\"/>"),
            "stack" => ("p", $"<p:cover dir=\"{PushDir(v)}\"/>"),
            "panels" => ("p", $"<p:pull dir=\"{(v == "down" ? "d" : "u")}\"/>"),
            "float" => ("p", "<p:pull dir=\"u\"/>"),
            "depth" => ("p", "<p:zoom dir=\"in\"/>"),
            "portal" => ("p14", "<p14:flythrough dir=\"in\"/>"),
            "orbit" => ("p14", $"<p14:gallery dir=\"{(v == "left" ? "r" : "l")}\"/>"),
            "sweep" => ("p", $"<p:wipe dir=\"{(v == "left" ? "l" : "r")}\"/>"),
            "feather" => ("p", $"<p:wipe dir=\"{v switch { "left" => "l", "down" => "d", "up" => "u", _ => "r" }}\"/>"),
            "aurora" or "spotlight" => ("p", "<p:circle/>"),
            "bloom" => ("p14", "<p14:flash/>"),
            "cinematic" => ("p", "<p:fade thruBlk=\"1\"/>"),
            "shutter" => ("p", $"<p:blinds dir=\"{(v == "vertical" ? "vert" : "horz")}\"/>"),
            "mosaic" => ("p", "<p:dissolve/>"),
            "morph" => ("p159", "<p159:morph option=\"byObject\"/>"),
            _ => ("p", "<p:fade/>"),
        };
    }

    public static string TransitionXml(TransitionSettings t)
    {
        var native = NativeTransition(t);
        var adv = $"{(t.AdvanceOnClick ? "" : " advClick=\"0\"")}{(t.AdvanceAfterSeconds is { } s ? $" advTm=\"{I(s * 1000)}\"" : "")}";
        if (native is null) return adv.Length > 0 ? $"<p:transition{adv}/>" : "";
        var (ns, xml) = native.Value;
        var spd = t.DurationMs < 600 ? "fast" : t.DurationMs < 1100 ? "med" : "slow";
        var dur = I(t.DurationMs);
        var decl = ns switch
        {
            "p15" => $"xmlns:p14=\"{P14Ns}\" xmlns:p15=\"{P15Ns}\"",
            "p159" => $"xmlns:p14=\"{P14Ns}\" xmlns:p159=\"{P159Ns}\"",
            _ => $"xmlns:p14=\"{P14Ns}\"",
        };
        var requires = ns == "p" ? "p14" : ns;
        var fallback = ns == "p" ? xml : "<p:fade/>";
        return $"<mc:AlternateContent xmlns:mc=\"{McNs}\"><mc:Choice {decl} Requires=\"{requires}\"><p:transition spd=\"{spd}\" p14:dur=\"{dur}\"{adv}>{xml}</p:transition></mc:Choice><mc:Fallback><p:transition spd=\"{spd}\"{adv}>{fallback}</p:transition></mc:Fallback></mc:AlternateContent>";
    }

    /* ---------------- theme ---------------- */

    static string ThemeXml(SlideTheme t)
    {
        string Acc(int i) => Color(t.Accents[i % t.Accents.Count]).rgb;
        var ns = "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"";
        const string ph = "<a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill>";
        return $"<a:theme {ns} name=\"{X(t.Name)}\"><a:themeElements>" +
            $"<a:clrScheme name=\"{X(t.Name)}\"><a:dk1><a:srgbClr val=\"{Color(t.Text).rgb}\"/></a:dk1><a:lt1><a:srgbClr val=\"FFFFFF\"/></a:lt1>" +
            $"<a:dk2><a:srgbClr val=\"{Color(t.TextMuted).rgb}\"/></a:dk2><a:lt2><a:srgbClr val=\"F3F2F1\"/></a:lt2>" +
            $"<a:accent1><a:srgbClr val=\"{Acc(0)}\"/></a:accent1><a:accent2><a:srgbClr val=\"{Acc(1)}\"/></a:accent2><a:accent3><a:srgbClr val=\"{Acc(2)}\"/></a:accent3>" +
            $"<a:accent4><a:srgbClr val=\"{Acc(3)}\"/></a:accent4><a:accent5><a:srgbClr val=\"{Acc(4)}\"/></a:accent5><a:accent6><a:srgbClr val=\"{Acc(5)}\"/></a:accent6>" +
            "<a:hlink><a:srgbClr val=\"0563C1\"/></a:hlink><a:folHlink><a:srgbClr val=\"954F72\"/></a:folHlink></a:clrScheme>" +
            $"<a:fontScheme name=\"{X(t.Name)}\"><a:majorFont><a:latin typeface=\"{X(t.HeadingFont)}\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:majorFont>" +
            $"<a:minorFont><a:latin typeface=\"{X(t.BodyFont)}\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:minorFont></a:fontScheme>" +
            "<a:fmtScheme name=\"Visary\"><a:fillStyleLst>" + ph + ph + ph + "</a:fillStyleLst>" +
            "<a:lnStyleLst><a:ln w=\"6350\">" + ph + "</a:ln><a:ln w=\"12700\">" + ph + "</a:ln><a:ln w=\"19050\">" + ph + "</a:ln></a:lnStyleLst>" +
            "<a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst>" +
            "<a:bgFillStyleLst>" + ph + ph + ph + "</a:bgFillStyleLst></a:fmtScheme></a:themeElements><a:objectDefaults/><a:extraClrSchemeLst/></a:theme>";
    }
}
