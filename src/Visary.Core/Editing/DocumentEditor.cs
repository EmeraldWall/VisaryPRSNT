using System.Text.Json;
using Visary.Core.Documents;
using Visary.Core.Model;
using Visary.Core.Themes;
using Visary.Core.Transitions;

namespace Visary.Core.Editing;

[Flags]
public enum ChangeKind
{
    None = 0,
    /// <summary>Slide list or order changed.</summary>
    Slides = 1,
    /// <summary>Content of the current slide changed.</summary>
    Content = 2,
    Selection = 4,
    CurrentSlide = 8,
    /// <summary>Title, size, theme or settings.</summary>
    Document = 16,
    All = Slides | Content | Selection | CurrentSlide | Document,
}

public enum ZOrder { Front, Forward, Backward, Back }
public enum AlignKind { Left, Center, Right, Top, Middle, Bottom, DistributeHorizontally, DistributeVertically }

/// <summary>All document editing commands. The UI calls these and redraws on <see cref="Changed"/>.</summary>
public sealed class DocumentEditor
{
    Presentation _doc;
    int _index;
    List<string> _selection = [];

    public DocumentEditor(Presentation doc, string? path = null)
    {
        _doc = doc;
        FilePath = path;
    }

    public Presentation Doc => _doc;
    public History History { get; } = new();
    public string? FilePath { get; set; }
    public bool IsDirty { get; private set; }
    public int CurrentIndex => Math.Clamp(_index, 0, _doc.Slides.Count - 1);
    public Slide Current => _doc.Slides[CurrentIndex];
    public IReadOnlyList<string> Selection => _selection;
    public IEnumerable<SlideElement> SelectedElements => Current.Elements.Where(e => _selection.Contains(e.Id));
    public SlideTheme Theme => SlideThemes.Get(_doc.ThemeId);

    public event Action<ChangeKind>? Changed;

    void Raise(ChangeKind k) => Changed?.Invoke(k);

    public void Load(Presentation p, string? path)
    {
        _doc = p;
        FilePath = path;
        _index = 0;
        _selection = [];
        IsDirty = false;
        History.Clear();
        Raise(ChangeKind.All);
    }

    public void MarkSaved(string path)
    {
        FilePath = path;
        IsDirty = false;
        // Nothing in the slides changed, so views only refresh the title (AutoSave saves often).
        Raise(ChangeKind.None);
    }

    /// <summary>Applies a change with an undo step. Changes with the same coalesce key merge (typing, sliders).</summary>
    public void Mutate(Action<Presentation> change, ChangeKind kind = ChangeKind.Content, string? coalesce = null)
    {
        History.Record(_doc, coalesce);
        change(_doc);
        IsDirty = true;
        FixSelection();
        Raise(kind);
    }

    /// <summary>Changes during a drag: no undo step until <see cref="EndGesture"/>.</summary>
    public void BeginGesture() => History.BeginGesture(_doc);

    public void GestureUpdate(Action<Presentation> change)
    {
        change(_doc);
        IsDirty = true;
        Raise(ChangeKind.Content);
    }

    public void EndGesture()
    {
        History.EndGesture(_doc);
        Raise(ChangeKind.Content);
    }

    public void Undo() => Restore(History.Undo(_doc));
    public void Redo() => Restore(History.Redo(_doc));

    void Restore(Presentation? p)
    {
        if (p == null) return;
        var currentId = Current.Id;
        _doc = p;
        var i = _doc.IndexOf(currentId);
        _index = i >= 0 ? i : Math.Min(_index, _doc.Slides.Count - 1);
        IsDirty = true;
        FixSelection();
        Raise(ChangeKind.All);
    }

    void FixSelection()
    {
        if (_index >= _doc.Slides.Count) _index = _doc.Slides.Count - 1;
        var ids = Current.Elements.Select(e => e.Id).ToHashSet();
        _selection = _selection.Where(ids.Contains).ToList();
    }

    /* ---------------- selection and navigation ---------------- */

    public void GoTo(int index)
    {
        index = Math.Clamp(index, 0, _doc.Slides.Count - 1);
        if (index == _index && _selection.Count == 0) return;
        _index = index;
        _selection = [];
        Raise(ChangeKind.CurrentSlide | ChangeKind.Selection);
    }

    public void Select(IEnumerable<string> ids)
    {
        var valid = Current.Elements.Select(e => e.Id).ToHashSet();
        var next = ids.Where(valid.Contains).Distinct().ToList();
        if (next.SequenceEqual(_selection)) return;
        _selection = next;
        Raise(ChangeKind.Selection);
    }

    public void SelectAll() => Select(Current.Elements.Select(e => e.Id));
    public void ClearSelection() => Select([]);

    /* ---------------- slides ---------------- */

    public void AddSlide(string layoutId = "title-content")
    {
        var slide = Factory.CreateSlide(Theme, layoutId, _doc.Width, _doc.Height);
        slide.Transition = Current.Transition.Clone();
        var at = CurrentIndex + 1;
        Mutate(d => d.Slides.Insert(at, slide), ChangeKind.Slides);
        GoTo(at);
    }

    /// <summary>Duplicates keep object ids so Morph pairs them.</summary>
    public void DuplicateSlide(int? index = null)
    {
        var i = index ?? CurrentIndex;
        var copy = DocumentCloner.CloneSlide(_doc.Slides[i], keepIds: true);
        Mutate(d => d.Slides.Insert(i + 1, copy), ChangeKind.Slides);
        GoTo(i + 1);
    }

    public void DeleteSlide(int? index = null)
    {
        var i = index ?? CurrentIndex;
        Mutate(d =>
        {
            d.Slides.RemoveAt(i);
            if (d.Slides.Count == 0) d.Slides.Add(Factory.CreateSlide(Theme, "blank", d.Width, d.Height));
        }, ChangeKind.Slides | ChangeKind.CurrentSlide);
        _index = Math.Min(i, _doc.Slides.Count - 1);
        Raise(ChangeKind.CurrentSlide);
    }

    public void MoveSlide(int from, int to)
    {
        if (from == to || from < 0 || from >= _doc.Slides.Count) return;
        to = Math.Clamp(to, 0, _doc.Slides.Count - 1);
        var id = _doc.Slides[from].Id;
        Mutate(d =>
        {
            var s = d.Slides[from];
            d.Slides.RemoveAt(from);
            d.Slides.Insert(to, s);
        }, ChangeKind.Slides);
        _index = _doc.IndexOf(id);
        Raise(ChangeKind.CurrentSlide);
    }

    public void ToggleHidden(int? index = null)
    {
        var i = index ?? CurrentIndex;
        Mutate(d => d.Slides[i].Hidden = !d.Slides[i].Hidden, ChangeKind.Slides);
    }

    /* ---------------- objects ---------------- */

    (double x, double y) Cascade(double w, double h)
    {
        var n = Current.Elements.Count % 6;
        return ((_doc.Width - w) / 2 + n * 24, (_doc.Height - h) / 2 + n * 24);
    }

    public void Insert(SlideElement e, bool center = true)
    {
        if (center) (e.X, e.Y) = Cascade(e.Width, e.Height);
        Mutate(_ => Current.Elements.Add(e));
        Select([e.Id]);
    }

    public TextElement InsertTextBox(bool heading = false, string? text = null)
    {
        var t = Theme;
        var H = _doc.Height;
        var style = new TextStyle
        {
            FontFamily = heading ? t.HeadingFont : t.BodyFont, FontSize = Math.Round(H * (heading ? .06 : .035)),
            Color = t.Text, Bold = heading,
        };
        var e = Factory.Text(0, 0, _doc.Width * .4, H * .12, text ?? (heading ? "Heading" : "Click to add text"), style, heading ? TextRole.Title : TextRole.Body);
        if (text != null)
        {
            // Pasted text: size the box to the number of lines it holds.
            var lines = Math.Clamp(text.Split('\n').Length, 1, 20);
            e.Width = _doc.Width * .6;
            e.Height = Math.Min(H * .8, lines * style.FontSize * 1.4 + 2 * e.Padding);
        }
        Insert(e);
        return e;
    }

    public ShapeElement InsertShape(ShapeKind kind)
    {
        var s = _doc.Height * .24;
        var line = kind is ShapeKind.Line or ShapeKind.ArrowLine;
        var wide = kind is ShapeKind.ArrowRight or ShapeKind.ArrowLeft or ShapeKind.Chevron or ShapeKind.Parallelogram;
        var e = Factory.Shape(kind, 0, 0, line ? s * 1.6 : wide ? s * 1.4 : s, line ? 24 : s, Theme.Accents[0]);
        Insert(e);
        return e;
    }

    public ImageElement InsertImage(byte[] data, string contentType, int pixelWidth, int pixelHeight)
    {
        var id = Ids.New();
        _doc.Media[id] = new MediaItem { ContentType = contentType, Data = data };
        var k = Math.Min(Math.Min(_doc.Width * .6 / Math.Max(1, pixelWidth), _doc.Height * .6 / Math.Max(1, pixelHeight)), 1);
        var e = Factory.Image(id, 0, 0, Math.Max(20, pixelWidth * k), Math.Max(20, pixelHeight * k));
        Insert(e);
        return e;
    }

    public TableElement InsertTable(int rows, int cols)
    {
        var t = TableElement.Create(Math.Clamp(rows, 1, 30), Math.Clamp(cols, 1, 12));
        var th = Theme;
        t.Width = Math.Min(_doc.Width * .8, cols * _doc.Width * .14);
        t.Height = rows * _doc.Height * .075;
        t.Style.FontFamily = th.BodyFont;
        t.Style.FontSize = Math.Round(_doc.Height * .022);
        t.Style.Color = th.Text;
        t.HeaderFill = th.Accents[0];
        Insert(t);
        return t;
    }

    public IconElement InsertIcon(string glyph)
    {
        var s = _doc.Height * .18;
        var e = new IconElement { Glyph = glyph, Color = Theme.Accents[0], Width = s, Height = s };
        Insert(e);
        return e;
    }

    public void UpdateTable(string id, Action<TableElement> change, string? coalesce = null) =>
        Mutate(_ => { if (Current.Find(id) is TableElement t) { change(t); t.Normalize(); } }, ChangeKind.Content, coalesce);

    /* ---------------- groups and visibility ---------------- */

    /// <summary>The ids that select together with <paramref name="id"/> (its whole group).</summary>
    public IReadOnlyList<string> GroupOf(string id)
    {
        var e = Current.Find(id);
        if (e?.GroupId is not { } g) return [id];
        return Current.Elements.Where(x => x.GroupId == g).Select(x => x.Id).ToList();
    }

    public bool CanGroup => _selection.Count > 1;
    public bool CanUngroup => SelectedElements.Any(e => e.GroupId != null);

    public void Group()
    {
        if (!CanGroup) return;
        var g = Ids.New();
        UpdateSelected(e => e.GroupId = g);
    }

    public void Ungroup()
    {
        if (!CanUngroup) return;
        UpdateSelected(e => e.GroupId = null);
    }

    public void SetHidden(string id, bool hidden) =>
        Mutate(_ => { if (Current.Find(id) is { } e) e.Hidden = hidden; });

    public void SetLocked(string id, bool locked) =>
        Mutate(_ => { if (Current.Find(id) is { } e) e.Locked = locked; });

    public void Rename(string id, string? name) =>
        Mutate(_ => { if (Current.Find(id) is { } e) e.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(); }, ChangeKind.Content, $"rename-{id}");

    /// <summary>Moves one object a step up or down the stacking order (Selection pane arrows).</summary>
    public void MoveInStack(string id, int delta) => Mutate(_ =>
    {
        var list = Current.Elements;
        var i = list.FindIndex(e => e.Id == id);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
    });

    public void DeleteSelection()
    {
        if (_selection.Count == 0) return;
        var sel = _selection.ToHashSet();
        Mutate(_ =>
        {
            Current.Elements.RemoveAll(e => sel.Contains(e.Id));
            Current.Builds.RemoveAll(b => sel.Contains(b.ElementId));
        });
        ClearSelection();
    }

    public void UpdateSelected(Action<SlideElement> change, string? coalesce = null)
    {
        if (_selection.Count == 0) return;
        Mutate(_ => { foreach (var e in SelectedElements.ToList()) change(e); }, ChangeKind.Content, coalesce);
    }

    public void Nudge(double dx, double dy) =>
        UpdateSelected(e => { if (!e.Locked) { e.X += dx; e.Y += dy; } }, "nudge");

    public void Arrange(ZOrder order)
    {
        var sel = _selection.ToHashSet();
        if (sel.Count == 0) return;
        Mutate(_ =>
        {
            var els = Current.Elements;
            var picked = els.Where(e => sel.Contains(e.Id)).ToList();
            var rest = els.Where(e => !sel.Contains(e.Id)).ToList();
            switch (order)
            {
                case ZOrder.Front: els.Clear(); els.AddRange(rest); els.AddRange(picked); break;
                case ZOrder.Back: els.Clear(); els.AddRange(picked); els.AddRange(rest); break;
                default:
                    var step = order == ZOrder.Forward ? 1 : -1;
                    var idx = Enumerable.Range(0, els.Count).Where(i => sel.Contains(els[i].Id)).ToList();
                    if (step > 0) idx.Reverse();
                    foreach (var i in idx)
                    {
                        var j = i + step;
                        if (j < 0 || j >= els.Count || sel.Contains(els[j].Id)) continue;
                        (els[i], els[j]) = (els[j], els[i]);
                    }
                    break;
            }
        });
    }

    public void Align(AlignKind kind)
    {
        var els = SelectedElements.ToList();
        if (els.Count == 0) return;
        var toSlide = els.Count == 1;
        double minX = toSlide ? 0 : els.Min(e => e.X), minY = toSlide ? 0 : els.Min(e => e.Y);
        double maxX = toSlide ? _doc.Width : els.Max(e => e.X + e.Width), maxY = toSlide ? _doc.Height : els.Max(e => e.Y + e.Height);
        if (kind is AlignKind.DistributeHorizontally or AlignKind.DistributeVertically)
        {
            if (els.Count < 3) return;
            var h = kind == AlignKind.DistributeHorizontally;
            var sorted = els.OrderBy(e => h ? e.X : e.Y).ToList();
            var total = sorted.Sum(e => h ? e.Width : e.Height);
            var gap = ((h ? maxX - minX : maxY - minY) - total) / (sorted.Count - 1);
            Mutate(_ =>
            {
                var pos = h ? minX : minY;
                foreach (var e in sorted)
                {
                    if (h) e.X = pos; else e.Y = pos;
                    pos += (h ? e.Width : e.Height) + gap;
                }
            });
            return;
        }
        UpdateSelected(e =>
        {
            switch (kind)
            {
                case AlignKind.Left: e.X = minX; break;
                case AlignKind.Center: e.X = (minX + maxX) / 2 - e.Width / 2; break;
                case AlignKind.Right: e.X = maxX - e.Width; break;
                case AlignKind.Top: e.Y = minY; break;
                case AlignKind.Middle: e.Y = (minY + maxY) / 2 - e.Height / 2; break;
                case AlignKind.Bottom: e.Y = maxY - e.Height; break;
            }
        });
    }

    /* ---------------- clipboard ---------------- */

    sealed class ClipPayload
    {
        public List<SlideElement> Elements { get; set; } = [];
        public List<Build> Builds { get; set; } = [];
    }

    string? _clip;
    Dictionary<string, MediaItem> _clipMedia = [];
    int _pasteCount;

    public bool CanPaste => _clip != null;

    /// <summary>Raised after Copy or Cut with the plain text of the copied objects, so the app can fill the system clipboard.</summary>
    public event Action<string>? Copied;

    public void Copy()
    {
        if (!CopyCore()) return;
        var text = string.Join(Environment.NewLine + Environment.NewLine, SelectedElements.Select(PlainText).Where(t => t.Length > 0));
        Copied?.Invoke(text);
    }

    static string PlainText(SlideElement e) => e switch
    {
        ITextHost t => t.Text.Trim(),
        TableElement t => string.Join(Environment.NewLine, t.Rows.Select(r => string.Join("\t", r))),
        _ => "",
    };

    bool CopyCore()
    {
        var els = SelectedElements.ToList();
        if (els.Count == 0) return false;
        var ids = els.Select(e => e.Id).ToHashSet();
        _clip = JsonSerializer.Serialize(new ClipPayload { Elements = els, Builds = Current.Builds.Where(b => ids.Contains(b.ElementId)).ToList() }, Json.Options);
        _clipMedia = els.OfType<ImageElement>().Where(i => _doc.Media.ContainsKey(i.MediaId)).ToDictionary(i => i.MediaId, i => _doc.Media[i.MediaId]);
        _pasteCount = 0;
        return true;
    }

    public void Cut()
    {
        Copy();
        DeleteSelection();
    }

    public void Paste()
    {
        if (_clip == null) return;
        var payload = JsonSerializer.Deserialize<ClipPayload>(_clip, Json.Options)!;
        var offset = 24 * ++_pasteCount;
        var map = new Dictionary<string, string>();
        var groups = new Dictionary<string, string>();
        foreach (var e in payload.Elements)
        {
            if (e.GroupId is { } g) e.GroupId = groups.TryGetValue(g, out var ng) ? ng : groups[g] = Ids.New();
            var id = Ids.New();
            map[e.Id] = id;
            e.Id = id;
            e.X += offset;
            e.Y += offset;
        }
        foreach (var b in payload.Builds)
        {
            b.Id = Ids.New();
            b.ElementId = map.GetValueOrDefault(b.ElementId, b.ElementId);
        }
        foreach (var (k, v) in _clipMedia) _doc.Media.TryAdd(k, v);
        Mutate(_ =>
        {
            Current.Elements.AddRange(payload.Elements);
            Current.Builds.AddRange(payload.Builds);
        });
        Select(payload.Elements.Select(e => e.Id));
    }

    /// <summary>Duplicates without disturbing what is on the clipboard.</summary>
    public void DuplicateSelection()
    {
        var (clip, media, count) = (_clip, _clipMedia, _pasteCount);
        if (!CopyCore()) return;
        Paste();
        (_clip, _clipMedia, _pasteCount) = (clip, media, count);
    }

    /* ---------------- design ---------------- */

    public void ApplyTheme(string themeId)
    {
        var t = SlideThemes.Get(themeId);
        Mutate(d =>
        {
            d.ThemeId = t.Id;
            foreach (var s in d.Slides)
            {
                var title = Factory.Layouts.FirstOrDefault(l => l.Id == s.Layout)?.IsTitle ?? false;
                s.Background = Factory.ThemeBackground(t, title);
                foreach (var e in s.Elements)
                {
                    if (e is TableElement tb)
                    {
                        tb.HeaderFill = t.Accents[0];
                        tb.Style.FontFamily = t.BodyFont;
                        tb.Style.Color = t.Text;
                    }
                    if (e is IconElement ic) ic.Color = t.Accents[0];
                    if (e is TextElement te && te.Role != TextRole.None)
                    {
                        te.Style.FontFamily = te.Role == TextRole.Title ? t.HeadingFont : t.BodyFont;
                        te.Style.Color = te.Role == TextRole.Caption ? t.TextMuted
                            : title && te.Role is TextRole.Title or TextRole.Subtitle ? t.TitleText : t.Text;
                    }
                }
            }
        }, ChangeKind.All);
    }

    public void ResizeSlides(double width, double height)
    {
        double kx = width / _doc.Width, ky = height / _doc.Height, k = Math.Min(kx, ky);
        Mutate(d =>
        {
            d.Width = width;
            d.Height = height;
            foreach (var e in d.Slides.SelectMany(s => s.Elements))
            {
                double cx = (e.X + e.Width / 2) * kx, cy = (e.Y + e.Height / 2) * ky;
                e.Width *= k;
                e.Height *= k;
                e.X = cx - e.Width / 2;
                e.Y = cy - e.Height / 2;
                if (e is ITextHost th) th.Style.FontSize = Math.Max(6, Math.Round(th.Style.FontSize * k));
                if (e is TableElement tb) tb.Style.FontSize = Math.Max(6, Math.Round(tb.Style.FontSize * k));
            }
        }, ChangeKind.All);
    }

    public void SetBackground(Background bg, bool allSlides = false, string? coalesce = null) =>
        Mutate(d =>
        {
            if (allSlides) foreach (var s in d.Slides) s.Background = bg.Clone();
            else Current.Background = bg.Clone();
        }, allSlides ? ChangeKind.All : ChangeKind.Content, coalesce);

    /* ---------------- transitions and builds ---------------- */

    public void SetTransition(Action<TransitionSettings> change, string? coalesce = null) =>
        Mutate(_ => change(Current.Transition), ChangeKind.Content | ChangeKind.Slides, coalesce);

    public void ApplyEffect(string effectId)
    {
        var info = TransitionCatalog.Get(effectId);
        SetTransition(t =>
        {
            t.Effect = info.Id;
            t.Variant = info.DefaultVariant;
            t.DurationMs = info.DefaultDurationMs;
        });
    }

    public void ApplyTransitionToAll()
    {
        var t = Current.Transition;
        Mutate(d => { foreach (var s in d.Slides) s.Transition = t.Clone(); }, ChangeKind.Slides);
    }

    public void AddBuild(BuildKind kind, string effect)
    {
        var sel = _selection.ToList();
        if (sel.Count == 0) return;
        Mutate(_ =>
        {
            for (var i = 0; i < sel.Count; i++)
                Current.Builds.Add(new Build
                {
                    ElementId = sel[i], Kind = kind, Effect = effect,
                    Trigger = i == 0 ? BuildTrigger.OnClick : BuildTrigger.WithPrevious,
                    DurationMs = kind == BuildKind.Emphasis ? 900 : 650,
                });
        }, ChangeKind.Content | ChangeKind.Slides);
    }

    public void UpdateBuild(string id, Action<Build> change, string? coalesce = null) =>
        Mutate(_ => { var b = Current.Builds.FirstOrDefault(x => x.Id == id); if (b != null) change(b); }, ChangeKind.Content, coalesce);

    public void RemoveBuild(string id) => Mutate(_ => Current.Builds.RemoveAll(b => b.Id == id), ChangeKind.Content | ChangeKind.Slides);

    public void MoveBuild(string id, int delta) => Mutate(_ =>
    {
        var list = Current.Builds;
        var i = list.FindIndex(b => b.Id == id);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
    });

    /* ---------------- find and replace ---------------- */

    public IEnumerable<(int slide, string elementId)> Find(string text, bool matchCase)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        var cmp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        for (var i = 0; i < _doc.Slides.Count; i++)
            foreach (var e in _doc.Slides[i].Elements)
            {
                var hit = e switch
                {
                    ITextHost th => th.Text.Contains(text, cmp),
                    TableElement t => t.Rows.Any(r => r.Any(c => c.Contains(text, cmp))),
                    _ => false,
                };
                if (hit) yield return (i, e.Id);
            }
    }

    public int ReplaceAll(string find, string replace, bool matchCase)
    {
        var hits = Find(find, matchCase).Count();
        if (hits == 0) return 0;
        var cmp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        Mutate(d =>
        {
            foreach (var e in d.Slides.SelectMany(s => s.Elements))
            {
                if (e is ITextHost th) th.Text = th.Text.Replace(find, replace, cmp);
                if (e is TableElement t)
                    foreach (var row in t.Rows)
                        for (var c = 0; c < row.Count; c++) row[c] = row[c].Replace(find, replace, cmp);
            }
        }, ChangeKind.All);
        return hits;
    }
}
