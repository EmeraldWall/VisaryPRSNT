using Visary.Core.Editing;
using Visary.Core.Model;

namespace Visary.Core.Tests;

public class EditorTests
{
    static DocumentEditor New() => new(Factory.CreatePresentation("office", sample: true));

    [Fact]
    public void Slide_commands_and_undo()
    {
        var ed = New();
        var count = ed.Doc.Slides.Count;
        ed.AddSlide("two-content");
        Assert.Equal(count + 1, ed.Doc.Slides.Count);
        Assert.Equal(1, ed.CurrentIndex);
        ed.DuplicateSlide();
        Assert.Equal(ed.Doc.Slides[1].Elements.Select(e => e.Id), ed.Doc.Slides[2].Elements.Select(e => e.Id));
        ed.MoveSlide(2, 0);
        Assert.Equal(0, ed.CurrentIndex);
        ed.DeleteSlide();
        ed.Undo();
        ed.Undo();
        ed.Undo();
        ed.Undo();
        Assert.Equal(count, ed.Doc.Slides.Count);
        ed.Redo();
        Assert.Equal(count + 1, ed.Doc.Slides.Count);
    }

    [Fact]
    public void Clipboard_z_order_and_align()
    {
        var ed = New();
        ed.GoTo(0);
        var a = ed.InsertShape(ShapeKind.Star5);
        var b = ed.InsertShape(ShapeKind.Ellipse);
        ed.Select([a.Id]);
        ed.Arrange(ZOrder.Front);
        Assert.Equal(a.Id, ed.Current.Elements[^1].Id);
        ed.Copy();
        ed.Paste();
        Assert.Single(ed.Selection);
        Assert.NotEqual(a.Id, ed.Selection[0]);
        var copy = ed.Current.Find(ed.Selection[0])!;
        Assert.Equal(a.X + 24, copy.X);
        ed.Select([a.Id, b.Id]);
        ed.Align(AlignKind.Left);
        Assert.Equal(ed.Current.Find(a.Id)!.X, ed.Current.Find(b.Id)!.X);
        ed.Select([a.Id]);
        ed.Align(AlignKind.Center);
        Assert.Equal(ed.Doc.Width / 2, ed.Current.Find(a.Id)!.Bounds.CenterX, 6);
    }

    [Fact]
    public void Gestures_make_one_undo_step()
    {
        var ed = New();
        var s = ed.InsertShape(ShapeKind.Rectangle);
        var x0 = s.X;
        ed.BeginGesture();
        for (var i = 0; i < 10; i++) ed.GestureUpdate(_ => ed.Current.Find(s.Id)!.X += 5);
        ed.EndGesture();
        Assert.Equal(x0 + 50, ed.Current.Find(s.Id)!.X);
        ed.Undo();
        Assert.Equal(x0, ed.Current.Find(s.Id)!.X);
    }

    [Fact]
    public void Theme_switch_restyles_titles_and_backgrounds()
    {
        var ed = New();
        ed.ApplyTheme("midnight");
        var title = (TextElement)ed.Doc.Slides[1].Elements[0];
        Assert.Equal("Bahnschrift", title.Style.FontFamily);
        Assert.Equal("#0F172A", ed.Doc.Slides[1].Background.Fill.Color);
    }

    [Fact]
    public void Builds_follow_their_objects()
    {
        var ed = New();
        var s = ed.InsertShape(ShapeKind.Heart);
        ed.AddBuild(BuildKind.Entrance, "pop");
        Assert.Single(ed.Current.Builds);
        ed.DeleteSelection();
        Assert.Empty(ed.Current.Builds);
        Assert.Null(ed.Current.Find(s.Id));
    }

    [Fact]
    public void Groups_select_together_and_paste_as_new_groups()
    {
        var ed = New();
        var a = ed.InsertShape(ShapeKind.Star5);
        var b = ed.InsertShape(ShapeKind.Ellipse);
        ed.Select([a.Id, b.Id]);
        ed.Group();
        Assert.Equal(new[] { a.Id, b.Id }, ed.GroupOf(a.Id));
        ed.Copy();
        ed.Paste();
        var pasted = ed.SelectedElements.ToList();
        Assert.Equal(2, pasted.Count);
        Assert.Single(pasted.Select(e => e.GroupId).Distinct());
        Assert.NotEqual(a.GroupId, pasted[0].GroupId);
        ed.Ungroup();
        Assert.All(ed.SelectedElements, e => Assert.Null(e.GroupId));
    }

    [Fact]
    public void Hidden_objects_leave_transitions_and_tables_find_text()
    {
        var ed = New();
        var t = ed.InsertTable(3, 3);
        ed.UpdateTable(t.Id, x => x.Rows[2][1] = "quarterly revenue");
        Assert.Contains(ed.Find("revenue", false), h => h.elementId == t.Id);
        ed.SetHidden(t.Id, true);
        Assert.DoesNotContain(Visary.Core.Transitions.Scene.From(ed.Current).Elements, e => e.Id == t.Id);
    }

    [Fact]
    public void Find_and_replace_across_slides()
    {
        var ed = New();
        Assert.NotEmpty(ed.Find("morph", false));
        var n = ed.ReplaceAll("Morph", "Glide", true);
        Assert.True(n > 0);
        Assert.Empty(ed.Find("Morph", true));
    }

    [Fact]
    public void Copy_reports_text_and_duplicate_keeps_the_clipboard()
    {
        var ed = New();
        ed.GoTo(1);
        var text = ed.Current.Elements.OfType<TextElement>().First(t => t.Text.Length > 0);
        string? copied = null;
        ed.Copied += t => copied = t;
        ed.Select([text.Id]);
        ed.Copy();
        Assert.Equal(text.Text.Trim(), copied);

        var other = ed.InsertShape(ShapeKind.Ellipse);
        copied = null;
        ed.DuplicateSelection();
        Assert.Null(copied);
        Assert.Equal(2, ed.Current.Elements.Count(e => e is ShapeElement { Kind: ShapeKind.Ellipse }));

        // Paste still gives the text that was copied, not the duplicated shape.
        var before = ed.Current.Elements.OfType<TextElement>().Count(t => t.Text == text.Text);
        ed.Paste();
        Assert.Equal(before + 1, ed.Current.Elements.OfType<TextElement>().Count(t => t.Text == text.Text));
        _ = other;
    }

    [Fact]
    public void Pasted_text_becomes_a_sized_text_box_and_saving_is_not_a_content_change()
    {
        var ed = New();
        var t = ed.InsertTextBox(text: "one\ntwo\nthree");
        Assert.Equal("one\ntwo\nthree", t.Text);
        Assert.True(t.Height > t.Style.FontSize * 3);
        ChangeKind? seen = null;
        ed.Changed += k => seen = k;
        ed.MarkSaved("x.vsry");
        Assert.Equal(ChangeKind.None, seen);
        Assert.False(ed.IsDirty);
    }
}
