using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Visary.Core.Documents;
using Visary.Core.Export;
using Visary.Core.Model;
using Visary.Core.Transitions;

namespace Visary.Core.Tests;

public class DocumentTests
{
    // A 2 x 1 PNG.
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAIAAAB7QOjdAAAAEElEQVR4nGP4z8DwnwEIAAz4AwHxNT4mAAAAAElFTkSuQmCC");

    static Presentation Sample()
    {
        var p = Factory.CreatePresentation("aurora", sample: true);
        p.Media["img1"] = new MediaItem { ContentType = "image/png", Data = Png };
        p.Slides[0].Elements.Add(Factory.Image("img1", 100, 100, 400, 300));
        p.Slides[1].Background.ImageMediaId = "img1";
        p.Slides[2].Hidden = true;
        p.Slides[2].Builds.Add(new Build { ElementId = p.Slides[2].Elements[0].Id, Effect = "rise" });
        foreach (var k in Enum.GetValues<ShapeKind>()) p.Slides[3].Elements.Add(Factory.Shape(k, 10, 10, 100, 80, "#C43E1C"));
        var table = TableElement.Create(3, 4);
        table.X = 100; table.Y = 500; table.Width = 800; table.Height = 240;
        table.Rows[1][2] = "Cell & <text>";
        p.Slides[3].Elements.Add(table);
        p.Slides[3].Elements.Add(new IconElement { Glyph = "\uE721", X = 1000, Y = 100, Width = 160, Height = 160 });
        p.Slides[3].Elements[0].Hidden = true;
        p.ShowSlideNumbers = true;
        p.FooterText = "Visary & friends";
        return p;
    }

    [Fact]
    public void Package_round_trips_with_media()
    {
        var p = Sample();
        using var ms = new MemoryStream();
        PresentationFile.Save(p, ms);
        ms.Position = 0;
        var back = PresentationFile.Load(ms);
        Assert.Equal(p.Slides.Count, back.Slides.Count);
        Assert.Equal(Png, back.Media["img1"].Data);
        Assert.IsType<ImageElement>(back.Slides[0].Elements[^1]);
        Assert.Equal("cascade", back.Slides[1].Transition.Effect);
        Assert.Equal(p.Slides[2].Builds[0].ElementId, back.Slides[2].Builds[0].ElementId);
    }

    [Fact]
    public void Tables_icons_groups_and_hidden_flags_round_trip()
    {
        var p = Sample();
        p.Slides[3].Elements[1].GroupId = "g1";
        using var ms = new MemoryStream();
        PresentationFile.Save(p, ms);
        ms.Position = 0;
        var back = PresentationFile.Load(ms);
        var t = back.Slides[3].Elements.OfType<TableElement>().Single();
        Assert.Equal(4, t.ColumnCount);
        Assert.Equal("Cell & <text>", t.Rows[1][2]);
        Assert.Equal("\uE721", back.Slides[3].Elements.OfType<IconElement>().Single().Glyph);
        Assert.True(back.Slides[3].Elements[0].Hidden);
        Assert.Equal("g1", back.Slides[3].Elements[1].GroupId);
        Assert.Equal("Visary & friends", back.FooterText);
    }

    [Fact]
    public void Repair_drops_pictures_whose_media_is_missing()
    {
        var p = Sample();
        p.Media.Clear();
        PresentationFile.Repair(p);
        Assert.DoesNotContain(p.Slides[0].Elements, e => e is ImageElement);
        Assert.Null(p.Slides[1].Background.ImageMediaId);
    }

    [Fact]
    public void Rejects_files_that_are_not_presentations()
    {
        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });
        Assert.ThrowsAny<Exception>(() => PresentationFile.Load(ms));
    }

    [Fact]
    public void History_undoes_and_merges_gestures()
    {
        var h = new History();
        var p = Factory.CreatePresentation();
        h.Record(p);
        p.Title = "One";
        h.BeginGesture(p);
        for (var i = 0; i < 5; i++) { h.Record(p); p.Title = $"Drag {i}"; }
        h.EndGesture(p);
        var u1 = h.Undo(p)!;
        Assert.Equal("One", u1.Title);
        var u2 = h.Undo(u1)!;
        Assert.Equal("Untitled presentation", u2.Title);
        var r = h.Redo(u2)!;
        Assert.Equal("One", r.Title);
    }

    [Fact]
    public void Duplicated_slides_keep_ids_for_morph()
    {
        var s = Factory.CreateSlide(Themes.SlideThemes.Get("office"), "title", 1920, 1080);
        var keep = DocumentCloner.CloneSlide(s, keepIds: true);
        var fresh = DocumentCloner.CloneSlide(s, keepIds: false);
        Assert.NotEqual(s.Id, keep.Id);
        Assert.Equal(s.Elements.Select(e => e.Id), keep.Elements.Select(e => e.Id));
        Assert.Empty(s.Elements.Select(e => e.Id).Intersect(fresh.Elements.Select(e => e.Id)));
    }

    [Fact]
    public void Pptx_export_is_schema_valid()
    {
        var p = Sample();
        foreach (var (s, i) in p.Slides.Select((s, i) => (s, i)))
            s.Transition = new TransitionSettings { Effect = TransitionCatalog.All[i % TransitionCatalog.All.Count].Id, AdvanceAfterSeconds = i % 2 == 0 ? 3 : null };
        // One slide per transition so every mapping is validated.
        foreach (var t in TransitionCatalog.All)
        {
            var s = Factory.CreateSlide(Themes.SlideThemes.Get("office"), "title-content", p.Width, p.Height);
            s.Transition = new TransitionSettings { Effect = t.Id };
            p.Slides.Add(s);
        }
        using var ms = new MemoryStream();
        PptxExporter.Export(p, ms);
        ms.Position = 0;
        using var doc = PresentationDocument.Open(ms, false);
        var errors = new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Microsoft365).Validate(doc).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors.Take(10).Select(e => $"{e.Path?.XPath}: {e.Description}")));
        Assert.Equal(p.Slides.Count, doc.PresentationPart!.SlideParts.Count());
        var xml = doc.PresentationPart.SlideParts.Select(sp => sp.Slide.OuterXml).ToList();
        Assert.Contains(xml, x => x.Contains("p159:morph"));
        Assert.Contains(xml, x => x.Contains("name=\"!!"));
        Assert.Contains(xml, x => x.Contains("show=\"0\""));
        Assert.Contains(xml, x => x.Contains("<a:tbl>") && x.Contains("Cell &amp; &lt;text&gt;"));
        Assert.Contains(xml, x => x.Contains("hidden=\"1\""));
        Assert.Contains(xml, x => x.Contains("Visary &amp; friends"));
    }

    [Fact]
    public void Reads_png_dimensions()
    {
        Assert.True(ImageInfo.TryGetSize(Png, out var w, out var h));
        Assert.Equal((2, 1), (w, h));
    }

    [Fact]
    public void Every_shape_has_an_outline()
    {
        foreach (var k in Enum.GetValues<ShapeKind>())
        {
            var cmds = ShapeGeometry.Build(k, 100, 80);
            Assert.NotEmpty(cmds);
            Assert.Equal(PathOp.Move, cmds[0].Op);
            Assert.All(cmds, c => Assert.True(double.IsFinite(c.X) && double.IsFinite(c.Y)));
        }
        Assert.Equal(Enum.GetValues<ShapeKind>().Length, ShapeGeometry.Catalog.Count);
    }

    [Fact]
    public void Build_steps_group_triggers()
    {
        var builds = new[]
        {
            new Build { ElementId = "a", Trigger = BuildTrigger.AfterPrevious, DurationMs = 400 },
            new Build { ElementId = "b", Trigger = BuildTrigger.OnClick, DurationMs = 500 },
            new Build { ElementId = "c", Trigger = BuildTrigger.WithPrevious, DelayMs = 100, DurationMs = 300 },
            new Build { ElementId = "d", Trigger = BuildTrigger.AfterPrevious, DurationMs = 200 },
        };
        var steps = BuildTimeline.Steps(builds);
        Assert.Equal(2, steps.Count);
        Assert.True(steps[0].Automatic);
        Assert.False(steps[1].Automatic);
        Assert.Equal(new[] { 0, 0, 500 }, steps[1].Items.Select(i => i.StartMs));
        Assert.All(BuildCatalog.All, b => Assert.NotEmpty(BuildCatalog.Tracks(new Build { Kind = b.Kind, Effect = b.Id })));
    }
}

public class PdfTests
{
    static readonly byte[] Jpeg = Convert.FromBase64String("/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAYEBQYFBAYGBQYHBwYIChAKCgkJChQODwwQFxQYGBcUFhYaHSUfGhsjHBYWICwgIyYnKSopGR8tMC0oMCUoKSj/2wBDAQcHBwoIChMKChMoGhYaKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCj/wAARCAASACADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDl6KKK+RP1AKKKKACiiigAooooA//Z");

    [Fact]
    public void Writes_a_page_per_slide_with_a_valid_cross_reference_table()
    {
        using var ms = new MemoryStream();
        PdfWriter.Write(ms, [new(Jpeg, 32, 18), new(Jpeg, 32, 18)], 960, 540, "Deck (draft)");
        var bytes = ms.ToArray();
        var text = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.StartsWith("%PDF-1.4", text);
        Assert.Contains("/Count 2", text);
        Assert.Contains("(Deck \\(draft\\))", text);
        // Every xref offset must point at the start of its object.
        var start = int.Parse(text[(text.LastIndexOf("startxref") + 10)..].Split('\n')[0]);
        var lines = text[start..].Split('\n');
        var count = int.Parse(lines[1].Split(' ')[1]);
        for (var i = 1; i < count; i++)
        {
            var offset = int.Parse(lines[2 + i][..10]);
            Assert.StartsWith($"{i} 0 obj", text[offset..]);
        }
    }
}
