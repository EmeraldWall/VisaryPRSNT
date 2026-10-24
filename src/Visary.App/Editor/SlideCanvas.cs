using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Visary.App.Rendering;
using Visary.App.Shell;
using Visary.App.Transitions;
using Visary.Core.Editing;
using Visary.Core.Model;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;

namespace Visary.App.Editor;

/// <summary>The slide being edited, with selection handles, guides and inline text editing.</summary>
public sealed partial class SlideCanvas : Grid
{
    readonly DocumentEditor _ed;
    readonly ScrollViewer _scroll = new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollMode = ScrollMode.Auto,
        VerticalScrollMode = ScrollMode.Auto,
        ZoomMode = ZoomMode.Disabled,
    };
    readonly Grid _page = new() { Background = Palette.Transparent, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(36) };
    readonly Border _paper = new();
    readonly Canvas _scaled = new();
    readonly Canvas _adorners = new() { IsHitTestVisible = true };
    readonly Grid _previewHost = new() { Visibility = Visibility.Collapsed, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(200, 0, 0, 0)) };
    SlideView? _view;
    TextBox? _editor;
    string? _editingId;
    double _fit = .5;
    double? _zoom;
    Gesture? _gesture;
    const double Grid = 40;

    public bool ShowGridlines { get; set; }
    public bool SmartGuides { get; set; } = true;
    public double Zoom => _zoom ?? _fit;
    public event Action? ZoomChanged;

    abstract record Gesture;
    sealed record MoveGesture(Point Start, Dictionary<string, Point> Orig, RectD Box) : Gesture { public bool Moved { get; set; } }
    sealed record ResizeGesture(Point Start, SlideElement Orig, int Hx, int Hy) : Gesture;
    sealed record RotateGesture(string Id, Point Center) : Gesture;
    sealed record MarqueeGesture(Point Start, bool Additive) : Gesture;

    public SlideCanvas(DocumentEditor ed)
    {
        _ed = ed;
        Background = Palette.Canvas;
        _paper.Shadow = new ThemeShadow();
        _paper.Translation = new System.Numerics.Vector3(0, 0, 24);
        _page.Children.Add(_paper);
        _page.Children.Add(_scaled);
        _page.Children.Add(_adorners);
        _scroll.Content = _page;
        Children.Add(_scroll);
        Children.Add(_previewHost);

        _page.PointerPressed += OnPressed;
        _page.PointerMoved += OnMoved;
        _page.PointerReleased += OnReleased;
        _page.PointerCaptureLost += (_, _) => FinishGesture();
        _page.DoubleTapped += OnDoubleTapped;
        _page.RightTapped += OnRightTapped;
        _page.AllowDrop = true;
        _page.DragOver += (_, e) => { if (e.DataView.Contains(StandardDataFormats.StorageItems)) e.AcceptedOperation = DataPackageOperation.Copy; };
        _page.Drop += OnDrop;
        _scroll.PointerWheelChanged += OnWheel;
        _scroll.SizeChanged += (_, _) => { UpdateFit(); Layout(); };
        _ed.Changed += OnChanged;
        Rebuild();
    }

    void OnChanged(ChangeKind k)
    {
        if (k.HasFlag(ChangeKind.CurrentSlide) || k.HasFlag(ChangeKind.Document)) EndTextEdit(commit: false);
        if (k.HasFlag(ChangeKind.Content) || k.HasFlag(ChangeKind.CurrentSlide) || k.HasFlag(ChangeKind.Document) || k.HasFlag(ChangeKind.Slides)) Rebuild();
        else if (k.HasFlag(ChangeKind.Selection)) DrawAdorners();
    }

    /* ---------------- layout and zoom ---------------- */

    void UpdateFit()
    {
        var w = _scroll.ActualWidth - 72;
        var h = _scroll.ActualHeight - 72;
        if (w <= 0 || h <= 0) return;
        _fit = Math.Max(.05, Math.Min(w / _ed.Doc.Width, h / _ed.Doc.Height));
    }

    public void SetZoom(double? zoom)
    {
        _zoom = zoom is { } z ? Math.Clamp(z, .1, 4) : null;
        Layout();
        ZoomChanged?.Invoke();
    }

    void Layout()
    {
        var z = Zoom;
        _page.Width = _ed.Doc.Width * z;
        _page.Height = _ed.Doc.Height * z;
        _paper.Width = _page.Width;
        _paper.Height = _page.Height;
        _scaled.RenderTransform = new ScaleTransform { ScaleX = z, ScaleY = z };
        _adorners.Width = _page.Width;
        _adorners.Height = _page.Height;
        DrawAdorners();
    }

    public void Rebuild()
    {
        // Swap only the slide view so an open text editor keeps focus while the slide redraws.
        if (_view != null) _scaled.Children.Remove(_view);
        _view = new SlideView(_ed.Doc, _ed.Current, SlideViewMode.Edit, _editingId);
        _scaled.Children.Insert(0, _view);
        if (_editor != null && !_scaled.Children.Contains(_editor)) _scaled.Children.Add(_editor);
        _paper.Background = new SolidColorBrush(Microsoft.UI.Colors.White);
        UpdateFit();
        Layout();
    }

    void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (!ctrl) return;
        var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
        SetZoom(Zoom * (delta > 0 ? 1.1 : 1 / 1.1));
        e.Handled = true;
    }

    /* ---------------- adorners ---------------- */

    void DrawAdorners(List<(bool vertical, double pos)>? guides = null, Rect? marquee = null)
    {
        _adorners.Children.Clear();
        var z = Zoom;
        if (ShowGridlines)
        {
            var stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(50, 128, 128, 128));
            for (double x = Grid; x < _ed.Doc.Width; x += Grid)
                _adorners.Children.Add(new Line { X1 = x * z, X2 = x * z, Y1 = 0, Y2 = _page.Height, Stroke = stroke, StrokeThickness = 1, IsHitTestVisible = false });
            for (double y = Grid; y < _ed.Doc.Height; y += Grid)
                _adorners.Children.Add(new Line { X1 = 0, X2 = _page.Width, Y1 = y * z, Y2 = y * z, Stroke = stroke, StrokeThickness = 1, IsHitTestVisible = false });
        }
        var sel = _ed.SelectedElements.ToList();
        foreach (var e in sel)
        {
            var box = new Grid { Width = e.Width * z, Height = e.Height * z };
            Canvas.SetLeft(box, e.X * z);
            Canvas.SetTop(box, e.Y * z);
            if (e.Rotation != 0) box.RenderTransform = new RotateTransform { Angle = e.Rotation, CenterX = e.Width * z / 2, CenterY = e.Height * z / 2 };
            box.Children.Add(new Rectangle
            {
                Stroke = Palette.Selection, StrokeThickness = 1.5, IsHitTestVisible = false,
                StrokeDashArray = e.Locked || e.Id == _editingId ? new DoubleCollection { 3, 2 } : null,
            });
            if (sel.Count == 1 && !e.Locked && e.Id != _editingId)
            {
                for (var hx = -1; hx <= 1; hx++)
                for (var hy = -1; hy <= 1; hy++)
                {
                    if (hx == 0 && hy == 0) continue;
                    if ((e is ShapeElement { IsLine: true }) && hy != 0) continue;
                    var handle = new Rectangle
                    {
                        Width = 10, Height = 10, Fill = new SolidColorBrush(Microsoft.UI.Colors.White), Stroke = Palette.Selection, StrokeThickness = 1.5,
                        RadiusX = 2, RadiusY = 2,
                        HorizontalAlignment = hx < 0 ? HorizontalAlignment.Left : hx > 0 ? HorizontalAlignment.Right : HorizontalAlignment.Center,
                        VerticalAlignment = hy < 0 ? VerticalAlignment.Top : hy > 0 ? VerticalAlignment.Bottom : VerticalAlignment.Center,
                        Margin = new Thickness(hx < 0 ? -5 : 0, hy < 0 ? -5 : 0, hx > 0 ? -5 : 0, hy > 0 ? -5 : 0),
                    };
                    int cx = hx, cy = hy;
                    handle.PointerPressed += (s, args) => StartResize(args, e, cx, cy);
                    ProtectedCursorHelper.Set(handle, CursorFor(hx, hy, e.Rotation));
                    box.Children.Add(handle);
                }
                var rot = new Ellipse
                {
                    Width = 14, Height = 14, Fill = new SolidColorBrush(Microsoft.UI.Colors.White), Stroke = Palette.Selection, StrokeThickness = 1.5,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -34, 0, 0),
                };
                ToolTipService.SetToolTip(rot, "Rotate (hold Shift to snap)");
                rot.PointerPressed += (s, args) => StartRotate(args, e);
                box.Children.Add(new Line { X1 = 0, X2 = 0, Y1 = 0, Y2 = 20, Stroke = Palette.Selection, StrokeThickness = 1.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -20, 0, 0), IsHitTestVisible = false });
                box.Children.Add(rot);
            }
            _adorners.Children.Add(box);
        }
        if (guides != null)
            foreach (var (vertical, pos) in guides)
                _adorners.Children.Add(vertical
                    ? new Line { X1 = pos * z, X2 = pos * z, Y1 = 0, Y2 = _page.Height, Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 236, 72, 153)), StrokeThickness = 1, IsHitTestVisible = false }
                    : new Line { X1 = 0, X2 = _page.Width, Y1 = pos * z, Y2 = pos * z, Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 236, 72, 153)), StrokeThickness = 1, IsHitTestVisible = false });
        if (marquee is { } m)
        {
            var r = new Rectangle { Width = m.Width * z, Height = m.Height * z, Stroke = Palette.Selection, StrokeThickness = 1, Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(24, 196, 62, 28)), IsHitTestVisible = false };
            Canvas.SetLeft(r, m.X * z);
            Canvas.SetTop(r, m.Y * z);
            _adorners.Children.Add(r);
        }
    }

    static InputSystemCursorShape CursorFor(int hx, int hy, double rot)
    {
        var a = ((Math.Round((Math.Atan2(hy, hx) * 180 / Math.PI + rot) / 45) * 45 % 180) + 180) % 180;
        return a switch { 0 => InputSystemCursorShape.SizeWestEast, 45 => InputSystemCursorShape.SizeNorthwestSoutheast, 90 => InputSystemCursorShape.SizeNorthSouth, _ => InputSystemCursorShape.SizeNortheastSouthwest };
    }

    /* ---------------- pointer input ---------------- */

    Point Logical(PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(_page).Position;
        return new Point(p.X / Zoom, p.Y / Zoom);
    }

    static bool Contains(SlideElement e, Point p)
    {
        // Rotate the point into the object's frame for an exact hit test.
        var a = -e.Rotation * Math.PI / 180;
        double cx = e.X + e.Width / 2, cy = e.Y + e.Height / 2;
        double dx = p.X - cx, dy = p.Y - cy;
        double lx = dx * Math.Cos(a) - dy * Math.Sin(a), ly = dx * Math.Sin(a) + dy * Math.Cos(a);
        var pad = e is ShapeElement { IsLine: true } ? 10 : 2;
        return Math.Abs(lx) <= e.Width / 2 + pad && Math.Abs(ly) <= e.Height / 2 + pad;
    }

    SlideElement? HitTest(Point p) => _ed.Current.Elements.LastOrDefault(e => Contains(e, p));

    static bool KeyDown(Windows.System.VirtualKey k) => InputKeyboardSource.GetKeyStateForCurrentThread(k).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(_page).Properties.IsLeftButtonPressed) return;
        var p = Logical(e);
        var hit = HitTest(p);
        if (_editingId != null)
        {
            if (hit?.Id == _editingId) return;
            EndTextEdit(commit: true);
        }
        Focus(FocusState.Pointer);
        var shift = KeyDown(Windows.System.VirtualKey.Shift) || KeyDown(Windows.System.VirtualKey.Control);
        if (hit == null)
        {
            if (!shift) _ed.ClearSelection();
            _gesture = new MarqueeGesture(p, shift);
            _page.CapturePointer(e.Pointer);
            return;
        }
        var sel = _ed.Selection.ToList();
        if (shift)
        {
            if (sel.Contains(hit.Id)) sel.Remove(hit.Id); else sel.Add(hit.Id);
            _ed.Select(sel);
        }
        else if (!sel.Contains(hit.Id)) _ed.Select([hit.Id]);
        var movable = _ed.SelectedElements.Where(x => !x.Locked).ToList();
        if (movable.Count == 0) return;
        var box = Bounds(movable);
        _gesture = new MoveGesture(p, movable.ToDictionary(x => x.Id, x => new Point(x.X, x.Y)), box);
        _page.CapturePointer(e.Pointer);
    }

    static RectD Bounds(IEnumerable<SlideElement> els)
    {
        var list = els.ToList();
        double x = list.Min(e => e.X), y = list.Min(e => e.Y);
        return new RectD(x, y, list.Max(e => e.Right()) - x, list.Max(e => e.Bottom()) - y);
    }

    void StartResize(PointerRoutedEventArgs e, SlideElement el, int hx, int hy)
    {
        e.Handled = true;
        _ed.BeginGesture();
        _gesture = new ResizeGesture(Logical(e), Core.Documents.DocumentCloner.CloneElement(el, newId: false), hx, hy);
        _page.CapturePointer(e.Pointer);
    }

    void StartRotate(PointerRoutedEventArgs e, SlideElement el)
    {
        e.Handled = true;
        _ed.BeginGesture();
        _gesture = new RotateGesture(el.Id, new Point(el.X + el.Width / 2, el.Y + el.Height / 2));
        _page.CapturePointer(e.Pointer);
    }

    void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_gesture == null) return;
        var p = Logical(e);
        var shift = KeyDown(Windows.System.VirtualKey.Shift);
        switch (_gesture)
        {
            case MoveGesture g:
            {
                double dx = p.X - g.Start.X, dy = p.Y - g.Start.Y;
                if (!g.Moved && Math.Sqrt(dx * dx + dy * dy) * Zoom < 3) return;
                if (!g.Moved) { g.Moved = true; _ed.BeginGesture(); }
                if (shift) { if (Math.Abs(dx) > Math.Abs(dy)) dy = 0; else dx = 0; }
                var (sx, sy, guides) = Snap(new RectD(g.Box.X + dx, g.Box.Y + dy, g.Box.Width, g.Box.Height), g.Orig.Keys.ToHashSet());
                dx += sx;
                dy += sy;
                _ed.GestureUpdate(_ =>
                {
                    foreach (var el in _ed.Current.Elements)
                        if (g.Orig.TryGetValue(el.Id, out var o)) { el.X = Math.Round(o.X + dx); el.Y = Math.Round(o.Y + dy); }
                });
                DrawAdorners(guides);
                break;
            }
            case ResizeGesture g:
            {
                var o = g.Orig;
                var rad = o.Rotation * Math.PI / 180;
                double cos = Math.Cos(rad), sin = Math.Sin(rad);
                double gdx = p.X - g.Start.X, gdy = p.Y - g.Start.Y;
                double lx = gdx * cos + gdy * sin, ly = -gdx * sin + gdy * cos;
                double w = g.Hx == 0 ? o.Width : Math.Max(8, o.Width + g.Hx * lx);
                double h = g.Hy == 0 ? o.Height : Math.Max(8, o.Height + g.Hy * ly);
                if ((shift || o is ImageElement) && g.Hx != 0 && g.Hy != 0)
                {
                    var k = Math.Max(w / o.Width, h / o.Height);
                    w = o.Width * k;
                    h = o.Height * k;
                }
                double ox = g.Hx * (w - o.Width) / 2, oy = g.Hy * (h - o.Height) / 2;
                double cx = o.X + o.Width / 2 + ox * cos - oy * sin, cy = o.Y + o.Height / 2 + ox * sin + oy * cos;
                _ed.GestureUpdate(_ =>
                {
                    if (_ed.Current.Find(o.Id) is not { } el) return;
                    el.Width = Math.Round(w);
                    el.Height = Math.Round(h);
                    el.X = Math.Round(cx - w / 2);
                    el.Y = Math.Round(cy - h / 2);
                });
                break;
            }
            case RotateGesture g:
            {
                var deg = Math.Atan2(p.Y - g.Center.Y, p.X - g.Center.X) * 180 / Math.PI + 90;
                if (shift) deg = Math.Round(deg / 15) * 15;
                else foreach (var a in new[] { -90, 0, 90, 180, 270, 360 }) if (Math.Abs(deg - a) < 3) deg = a;
                deg = ((deg % 360) + 360) % 360;
                _ed.GestureUpdate(_ => { if (_ed.Current.Find(g.Id) is { } el) el.Rotation = Math.Round(deg); });
                break;
            }
            case MarqueeGesture g:
                DrawAdorners(marquee: new Rect(Math.Min(g.Start.X, p.X), Math.Min(g.Start.Y, p.Y), Math.Abs(p.X - g.Start.X), Math.Abs(p.Y - g.Start.Y)));
                break;
        }
    }

    void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_gesture is MarqueeGesture m)
        {
            var p = Logical(e);
            var r = new RectD(Math.Min(m.Start.X, p.X), Math.Min(m.Start.Y, p.Y), Math.Abs(p.X - m.Start.X), Math.Abs(p.Y - m.Start.Y));
            if (r.Width > 3 || r.Height > 3)
            {
                var hits = _ed.Current.Elements.Where(x => x.Bounds.Intersects(r)).Select(x => x.Id);
                _ed.Select(m.Additive ? _ed.Selection.Concat(hits) : hits);
            }
        }
        _page.ReleasePointerCapture(e.Pointer);
        FinishGesture();
    }

    void FinishGesture()
    {
        var g = _gesture;
        _gesture = null;
        if (g is ResizeGesture or RotateGesture || g is MoveGesture { Moved: true }) _ed.EndGesture();
        DrawAdorners();
    }

    (double dx, double dy, List<(bool, double)> guides) Snap(RectD box, HashSet<string> moving)
    {
        var guides = new List<(bool, double)>();
        if (ShowGridlines)
            return (Math.Round(box.X / Grid) * Grid - box.X, Math.Round(box.Y / Grid) * Grid - box.Y, guides);
        if (!SmartGuides) return (0, 0, guides);
        var thr = 6 / Zoom;
        var xs = new List<double> { 0, _ed.Doc.Width / 2, _ed.Doc.Width };
        var ys = new List<double> { 0, _ed.Doc.Height / 2, _ed.Doc.Height };
        foreach (var e in _ed.Current.Elements.Where(e => !moving.Contains(e.Id)))
        {
            xs.AddRange([e.X, e.X + e.Width / 2, e.X + e.Width]);
            ys.AddRange([e.Y, e.Y + e.Height / 2, e.Y + e.Height]);
        }
        static (double d, double t)? Pick(double[] vals, List<double> targets, double thr)
        {
            (double d, double t)? best = null;
            foreach (var v in vals)
            foreach (var t in targets)
            {
                var d = t - v;
                if (Math.Abs(d) < thr && (best == null || Math.Abs(d) < Math.Abs(best.Value.d))) best = (d, t);
            }
            return best;
        }
        var bx = Pick([box.X, box.CenterX, box.Right], xs, thr);
        var by = Pick([box.Y, box.CenterY, box.Bottom], ys, thr);
        if (bx is { } x) guides.Add((true, x.t));
        if (by is { } y) guides.Add((false, y.t));
        return (bx?.d ?? 0, by?.d ?? 0, guides);
    }

    /* ---------------- text editing ---------------- */

    void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var pt = e.GetPosition(_page);
        var hit = HitTest(new Point(pt.X / Zoom, pt.Y / Zoom));
        if (hit is ITextHost && hit is not ShapeElement { IsLine: true } && !hit.Locked) BeginTextEdit(hit.Id);
    }

    public void BeginTextEdit(string id)
    {
        if (_ed.Current.Find(id) is not { } el || el is not ITextHost th) return;
        EndTextEdit(commit: true);
        _ed.Select([id]);
        _editingId = id;
        if (_editor != null) _scaled.Children.Remove(_editor);
        var st = th.Style;
        var pad = el is TextElement t ? t.Padding : Math.Min(el.Width, el.Height) * .08;
        _editor = new TextBox
        {
            Text = th.Text,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Width = el.Width,
            MinHeight = el.Height,
            Padding = new Thickness(pad),
            FontFamily = new FontFamily(st.FontFamily),
            FontSize = Math.Max(1, st.FontSize),
            Foreground = ColorUtil.Brush(st.Color),
            FontWeight = st.Bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
            FontStyle = st.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
            TextAlignment = st.Align switch { TextAlign.Center => TextAlignment.Center, TextAlign.Right => TextAlignment.Right, TextAlign.Justify => TextAlignment.Justify, _ => TextAlignment.Left },
            BorderThickness = new Thickness(0),
            Background = Palette.Transparent,
            IsSpellCheckEnabled = true,
        };
        // Keep the box transparent in every state so the slide shows through while typing.
        foreach (var key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused" })
            _editor.Resources[key] = Palette.Transparent;
        foreach (var key in new[] { "TextControlForeground", "TextControlForegroundPointerOver", "TextControlForegroundFocused" })
            _editor.Resources[key] = ColorUtil.Brush(st.Color);
        Canvas.SetLeft(_editor, el.X);
        Canvas.SetTop(_editor, el.Y);
        if (el.Rotation != 0) _editor.RenderTransform = new RotateTransform { Angle = el.Rotation, CenterX = el.Width / 2, CenterY = el.Height / 2 };
        _editor.TextChanged += (_, _) =>
        {
            if (_editor == null || _editingId == null) return;
            var text = _editor.Text.Replace("\r\n", "\n").Replace('\r', '\n');
            var eid = _editingId;
            _ed.Mutate(_ => { if (_ed.Current.Find(eid) is ITextHost h) h.Text = text; }, ChangeKind.Content, $"type-{eid}");
        };
        _editor.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Escape)
            {
                args.Handled = true;
                EndTextEdit(commit: true);
            }
        };
        Rebuild();
        _editor.Loaded += (_, _) => { _editor?.Focus(FocusState.Programmatic); _editor?.SelectAll(); };
    }

    public bool IsEditingText => _editingId != null;

    public void EndTextEdit(bool commit)
    {
        if (_editingId == null) return;
        _ = commit; // Text is committed live while typing.
        _editingId = null;
        if (_editor != null) _scaled.Children.Remove(_editor);
        _editor = null;
        Rebuild();
    }

    /* ---------------- context menu and drop ---------------- */

    void OnRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var pt = e.GetPosition(_page);
        var hit = HitTest(new Point(pt.X / Zoom, pt.Y / Zoom));
        if (hit != null && !_ed.Selection.Contains(hit.Id)) _ed.Select([hit.Id]);
        var menu = new MenuFlyout();
        if (hit != null)
        {
            menu.Items.Add(Ui.Item("Cut", _ed.Cut, ""));
            menu.Items.Add(Ui.Item("Copy", _ed.Copy, ""));
            menu.Items.Add(Ui.Item("Paste", _ed.Paste, ""));
            menu.Items.Add(Ui.Item("Duplicate", _ed.DuplicateSelection, ""));
            menu.Items.Add(Ui.Item("Delete", _ed.DeleteSelection, ""));
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(Ui.Item("Bring to front", () => _ed.Arrange(ZOrder.Front), ""));
            menu.Items.Add(Ui.Item("Send to back", () => _ed.Arrange(ZOrder.Back), ""));
            menu.Items.Add(Ui.Item(hit.Locked ? "Unlock" : "Lock", () => _ed.UpdateSelected(x => x.Locked = !hit.Locked), ""));
            if (hit is ITextHost) menu.Items.Add(Ui.Item("Edit text", () => BeginTextEdit(hit.Id), ""));
        }
        else
        {
            menu.Items.Add(Ui.Item("Paste", _ed.Paste, ""));
            menu.Items.Add(Ui.Item("Select all", _ed.SelectAll, ""));
            menu.Items.Add(Ui.Item("New slide", () => _ed.AddSlide(), ""));
        }
        menu.ShowAt(_page, pt);
    }

    async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        foreach (var item in await e.DataView.GetStorageItemsAsync())
            if (item is StorageFile f) await Commands.InsertPictureFileAsync(_ed, f.Path);
    }

    /* ---------------- previews ---------------- */

    /// <summary>Plays the current slide's object animations on the canvas, then redraws it.</summary>
    public async Task PreviewBuildsAsync()
    {
        if (_view == null || _ed.Current.Builds.Count == 0) return;
        EndTextEdit(commit: true);
        var view = _view;
        BuildAnimator.ApplyInitialState(view);
        foreach (var step in Core.Transitions.BuildTimeline.Steps(_ed.Current.Builds))
        {
            await BuildAnimator.PlayStepAsync(view, step);
            await Task.Delay(250);
        }
        await Task.Delay(500);
        Rebuild();
    }

    public async Task PreviewTransitionAsync()
    {
        var i = _ed.CurrentIndex;
        var from = i > 0 ? _ed.Doc.Slides[i - 1] : null;
        var stage = new TransitionStage(_ed.Doc);
        _previewHost.Children.Clear();
        _previewHost.Children.Add(new Viewbox { Child = stage, Margin = new Thickness(36), Stretch = Stretch.Uniform });
        _previewHost.Visibility = Visibility.Visible;
        _previewHost.Tapped += Close;
        void Close(object s, TappedRoutedEventArgs a) => stage.Skip();
        try
        {
            await stage.PreviewAsync(from, _ed.Current, _ed.Theme.Accents[0]);
            await Task.Delay(400);
        }
        finally
        {
            _previewHost.Tapped -= Close;
            _previewHost.Visibility = Visibility.Collapsed;
            _previewHost.Children.Clear();
        }
    }
}

static class ElementExtensions
{
    public static double Right(this SlideElement e) => e.X + e.Width;
    public static double Bottom(this SlideElement e) => e.Y + e.Height;
}

/// <summary>Sets the pointer cursor on an element (ProtectedCursor is protected, so use reflection free subclass access).</summary>
static class ProtectedCursorHelper
{
    static readonly System.Reflection.PropertyInfo? Prop =
        typeof(UIElement).GetProperty("ProtectedCursor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    public static void Set(UIElement e, InputSystemCursorShape shape)
    {
        try
        {
            Prop?.SetValue(e, InputSystemCursor.Create(shape));
        }
        catch (Exception)
        {
            // Cursor hints are cosmetic.
        }
    }
}
