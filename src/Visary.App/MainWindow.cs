using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Visary.App.Editor;
using Visary.App.Shell;
using Visary.App.Show;
using Visary.Core.Documents;
using Visary.Core.Editing;
using Visary.Core.Model;
using Visary.Core.Themes;
using Windows.ApplicationModel.DataTransfer;
using VirtualKey = Windows.System.VirtualKey;

namespace Visary.App;

public sealed partial class MainWindow : Window
{
    readonly Grid Root = new();
    readonly Grid _titleBar = new() { Height = 40 };
    readonly TextBlock _title = new() { FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly TextBlock _status = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _zoomLabel = new() { FontSize = 12, Width = 44, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
    readonly Grid _workspace = new();
    readonly Grid _center = new();
    readonly TextBox _notes = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, PlaceholderText = "Click to add notes", BorderThickness = new Thickness(0), Height = 96 };
    readonly Border _notesHost = new() { BorderThickness = new Thickness(0, 1, 0, 0) };
    readonly Grid _backstageHost = new() { Visibility = Visibility.Collapsed };
    Ribbon _ribbon = null!;
    bool _syncingNotes;
    bool _closingConfirmed;
    readonly InfoBar _info = new() { IsOpen = false, IsClosable = true, Margin = new Thickness(16, 12, 16, 0), MaxWidth = 680, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center };
    readonly Grid _renderHost = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
    readonly ToggleSwitch _autoSave = new() { OnContent = null, OffContent = null, MinWidth = 0, Width = 44, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
    DispatcherQueueTimer? _infoTimer;
    DispatcherQueueTimer? _autoSaveTimer;
    SlideSorter _sorter = null!;
    bool _syncingAutoSave;
    const string ClipboardFormat = "VisaryPRSNT.Objects";
    SlideShowWindow? _showWindow;
    PresenterWindow? _presenter;

    public DocumentEditor Editor { get; } = new(Factory.CreatePresentation("office", sample: true));
    public SlideCanvas Canvas { get; private set; } = null!;
    public SidePane Side { get; private set; } = null!;
    public SlidesPane Slides { get; private set; } = null!;

    public MainWindow()
    {
        Content = Root;
        Title = "Visary PRSNT";
        Commands.Owner = this;
        Commands.Notify = Notify;
        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        try { AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Visary.ico")); } catch (Exception ex) { Log.Error("Icon", ex); }
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1500, 920));

        Palette.Apply(AppThemes.Get(Settings.Current.AppTheme));
        BuildLayout();
        ApplyBackdrop(Settings.Current.Mica);
        SetTitleBar(_titleBar.Children[1] as UIElement);
        ApplyCaptionColors();
        Palette.Changed += () => { Root.RequestedTheme = Palette.ElementTheme; ApplyCaptionColors(); };

        Editor.Changed += OnChanged;
        AppWindow.Closing += OnClosing;
        Editor.Copied += OnCopied;
        Root.Loaded += (_, _) =>
        {
            Dialogs.Root = Root.XamlRoot;
            OnChanged(ChangeKind.All);
            if (!Settings.Current.SeenTour && !Environment.GetCommandLineArgs().Contains("--selftest")) _ = TourAsync();
        };
        StartAutoRecover();
    }

    /* ---------------- layout ---------------- */

    void BuildLayout()
    {
        Root.RequestedTheme = Palette.ElementTheme;
        Root.Background = Palette.Pane;
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition());
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Title bar: quick access on the left, a drag region with the title, Present on the right.
        _titleBar.Background = Palette.TitleBar;
        _titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _titleBar.ColumnDefinitions.Add(new ColumnDefinition());
        _titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _title.Foreground = Palette.TitleBarText;
        var logo = new Border
        {
            Width = 22, Height = 22, CornerRadius = new CornerRadius(5), Background = new SolidColorBrush(ColorUtil.Parse("#D35230")),
            Child = new TextBlock { Text = "V", FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 },
            Margin = new Thickness(12, 0, 10, 0),
        };
        var quick = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { logo } };
        // Office style AutoSave: once the file has a location every change is saved a moment after it is made.
        var autoLabel = Ui.Text("AutoSave", 12, Palette.TitleBarText);
        autoLabel.Margin = new Thickness(0, 0, 6, 0);
        _autoSave.IsOn = Settings.Current.AutoSave;
        _autoSave.Toggled += async (_, _) =>
        {
            if (_syncingAutoSave) return;
            var on = _autoSave.IsOn;
            Settings.Current.AutoSave = on;
            if (on && Editor.FilePath == null && !await Commands.SaveAsync(Editor)) Settings.Current.AutoSave = false;
            Settings.Current.Save();
            OnChanged(ChangeKind.None);
            if (Settings.Current.AutoSave) ScheduleAutoSave();
        };
        ToolTipService.SetToolTip(_autoSave, "AutoSave saves your changes as you work, once the presentation is saved to a file");
        quick.Children.Add(autoLabel);
        quick.Children.Add(_autoSave);
        foreach (var (glyph, tip, action) in new (string, string, Action)[]
        {
            ("", "Save (Ctrl+S)", () => _ = Commands.SaveAsync(Editor)),
            ("", "Undo (Ctrl+Z)", Editor.Undo),
            ("", "Redo (Ctrl+Y)", Editor.Redo),
            ("", "Start from beginning (F5)", () => StartShow(0)),
        })
        {
            var b = new Button { Content = Ui.Icon(glyph, 14, Palette.TitleBarText), Background = Palette.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(8, 5, 8, 5) };
            ToolTipService.SetToolTip(b, tip);
            b.Click += (_, _) => action();
            quick.Children.Add(b);
        }
        _titleBar.Children.Add(quick);
        var drag = new Grid { Background = Palette.Transparent, Children = { _title }, Padding = new Thickness(16, 0, 16, 0) };
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(drag, 1);
        _titleBar.Children.Add(drag);
        var present = new Button
        {
            Content = Ui.Row(Ui.Icon("", 14), Ui.Text("Present", 12.5)),
            Background = Palette.PaneAlt, Foreground = Palette.Text, BorderBrush = Palette.Border, Padding = new Thickness(12, 4, 12, 4),
            Margin = new Thickness(0, 0, 150, 0), VerticalAlignment = VerticalAlignment.Center, CornerRadius = new CornerRadius(4),
        };
        ToolTipService.SetToolTip(present, "Present from the current slide (Shift+F5)");
        present.Click += (_, _) => StartShow(Editor.CurrentIndex);
        Grid.SetColumn(present, 2);
        _titleBar.Children.Add(present);
        Root.Children.Add(_titleBar);

        Side = new SidePane(Editor);
        Canvas = new SlideCanvas(Editor);
        Slides = new SlidesPane(Editor);
        _ribbon = new Ribbon(this);
        Grid.SetRow(_ribbon, 1);
        Root.Children.Add(_ribbon);
        Side.PreviewTransition = s => Canvas.PreviewTransitionAsync(s);
        Side.CancelPreview = Canvas.CancelPreview;
        Canvas.FormatRequested += () => Side.Mode = PaneMode.Format;
        Canvas.SelectionPaneRequested += () => Side.Mode = PaneMode.Selection;
        Canvas.PasteRequested += () => _ = PasteAsync();
        Side.PreviewBuilds = Canvas.PreviewBuildsAsync;

        _workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(212) });
        _workspace.ColumnDefinitions.Add(new ColumnDefinition());
        _workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _workspace.Children.Add(Slides);
        _center.RowDefinitions.Add(new RowDefinition());
        _center.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _center.Children.Add(Canvas);
        _notesHost.Child = _notes;
        _notesHost.BorderBrush = Palette.Border;
        _notesHost.Background = Palette.PaneAlt;
        _notes.Background = Palette.PaneAlt;
        Grid.SetRow(_notesHost, 1);
        _center.Children.Add(_notesHost);
        _notesHost.Visibility = Settings.Current.ShowNotes ? Visibility.Visible : Visibility.Collapsed;
        _notes.TextChanged += (_, _) =>
        {
            if (_syncingNotes) return;
            var text = _notes.Text;
            var id = Editor.Current.Id;
            Editor.Mutate(d => { var s = d.Slides.FirstOrDefault(x => x.Id == id); if (s != null) s.Notes = text; }, ChangeKind.None, $"notes-{id}");
        };
        Grid.SetColumn(_center, 1);
        _workspace.Children.Add(_center);
        Grid.SetColumn(Side, 2);
        _workspace.Children.Add(Side);
        _center.Children.Add(_info);
        _info.CloseButtonClick += (_, _) => _infoTimer?.Stop();

        _sorter = new SlideSorter(Editor) { Visibility = Visibility.Collapsed };
        _sorter.OpenRequested += () => SorterView = false;
        Grid.SetColumnSpan(_sorter, 3);
        _workspace.Children.Add(_sorter);

        // Slides are laid out here, under the workspace, when they are captured for PDF and picture export.
        Grid.SetRow(_renderHost, 2);
        Root.Children.Add(_renderHost);
        Commands.RenderHost = _renderHost;
        Grid.SetRow(_workspace, 2);
        Root.Children.Add(_workspace);

        // Status bar
        var statusBar = new Grid { Background = Palette.Pane, Height = 30, Padding = new Thickness(12, 0, 12, 0), BorderBrush = Palette.Border, BorderThickness = new Thickness(0, 1, 0, 0) };
        statusBar.ColumnDefinitions.Add(new ColumnDefinition());
        statusBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _status.Foreground = Palette.SubtleText;
        statusBar.Children.Add(_status);
        var zoom = new Slider { Width = 140, Minimum = 10, Maximum = 400, Value = 50, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) };
        zoom.ValueChanged += (_, e) =>
        {
            if (SorterView) _sorter.TileWidth = 120 + e.NewValue * 1.2;
            else if (Math.Abs(e.NewValue - Canvas.Zoom * 100) > .5) Canvas.SetZoom(e.NewValue / 100);
        };
        Canvas.ZoomChanged += () => { _zoomLabel.Text = $"{Math.Round(Canvas.Zoom * 100)}%"; zoom.Value = Canvas.Zoom * 100; };
        _zoomLabel.Foreground = Palette.SubtleText;
        var notesToggle = Ui.Small("", "Notes", () => SetNotesVisible(_notesHost.Visibility != Visibility.Visible));
        var fit = Ui.Small("", null, () => Canvas.SetZoom(null), "Fit slide to window");
        var play = Ui.Small("", null, () => StartShow(Editor.CurrentIndex), "Slide show");
        var normal = Ui.Small("\uE8A1", null, () => SorterView = false, "Normal");
        var sorter = Ui.Small("\uF0E2", null, () => SorterView = true, "Slide Sorter");
        var right = Ui.Row(notesToggle, normal, sorter, play, zoom, _zoomLabel, fit);
        Grid.SetColumn(right, 1);
        statusBar.Children.Add(right);
        Grid.SetRow(statusBar, 3);
        Root.Children.Add(statusBar);

        Grid.SetRow(_backstageHost, 1);
        Grid.SetRowSpan(_backstageHost, 3);
        Root.Children.Add(_backstageHost);

        Root.PreviewKeyDown += OnPreviewKey;
    }

    void ApplyCaptionColors()
    {
        var tb = AppWindow.TitleBar;
        var fg = ColorUtil.Parse(Palette.Current.TitleBarText);
        tb.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        tb.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        tb.ButtonForegroundColor = fg;
        tb.ButtonHoverForegroundColor = fg;
        tb.ButtonHoverBackgroundColor = ColorUtil.WithAlpha(fg, 40);
        tb.ButtonPressedBackgroundColor = ColorUtil.WithAlpha(fg, 70);
        tb.ButtonInactiveForegroundColor = ColorUtil.WithAlpha(fg, 150);
    }

    public void ApplyAppTheme(string id)
    {
        Palette.Apply(AppThemes.Get(id));
        Settings.Current.AppTheme = id;
        Settings.Current.Save();
    }

    public void SetNotesVisible(bool visible)
    {
        _notesHost.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        Settings.Current.ShowNotes = visible;
        Settings.Current.Save();
    }

    void OnChanged(ChangeKind k)
    {
        var dirty = Editor.IsDirty ? " (unsaved)" : "";
        var name = Editor.FilePath != null ? Path.GetFileNameWithoutExtension(Editor.FilePath) : Editor.Doc.Title;
        var saving = Settings.Current.AutoSave && Editor.FilePath != null;
        if (Editor.IsDirty && saving) dirty = "";
        _title.Text = $"{name}{dirty}{(saving ? "  (AutoSave on)" : "")} - Visary PRSNT";
        _syncingAutoSave = true;
        _autoSave.IsOn = saving;
        _syncingAutoSave = false;
        Title = _title.Text;
        _status.Text = $"Slide {Editor.CurrentIndex + 1} of {Editor.Doc.Slides.Count}     {Editor.Theme.Name}     {Core.Transitions.TransitionCatalog.Get(Editor.Current.Transition.Effect).Name} transition";
        if (k.HasFlag(ChangeKind.CurrentSlide) || k == ChangeKind.All || k.HasFlag(ChangeKind.Slides))
        {
            _syncingNotes = true;
            if (_notes.Text != Editor.Current.Notes) _notes.Text = Editor.Current.Notes;
            _syncingNotes = false;
        }
        _zoomLabel.Text = $"{Math.Round(Canvas.Zoom * 100)}%";
        if (Editor.IsDirty) ScheduleAutoSave();
    }

    void ScheduleAutoSave()
    {
        if (!Settings.Current.AutoSave || Editor.FilePath == null || !Editor.IsDirty) return;
        if (_autoSaveTimer == null)
        {
            _autoSaveTimer = DispatcherQueue.CreateTimer();
            _autoSaveTimer.Interval = TimeSpan.FromSeconds(2.5);
            _autoSaveTimer.IsRepeating = false;
            _autoSaveTimer.Tick += async (_, _) =>
            {
                if (Canvas.IsEditingText || _showWindow != null) { _autoSaveTimer.Start(); return; }
                if (Editor.FilePath is { } path && Editor.IsDirty) await Commands.SaveToAsync(Editor, path, quiet: true);
            };
        }
        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }

    /* ---------------- notifications, backdrop and tour ---------------- */

    /// <summary>Shows a short message in an InfoBar over the slide, closing itself after a few seconds.</summary>
    public void Notify(string title, string message, bool success)
    {
        _info.Title = title;
        _info.Message = message;
        _info.Severity = success ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        _info.IsOpen = true;
        _infoTimer ??= DispatcherQueue.CreateTimer();
        _infoTimer.Interval = TimeSpan.FromSeconds(success ? 5 : 10);
        _infoTimer.IsRepeating = false;
        _infoTimer.Tick -= CloseInfo;
        _infoTimer.Tick += CloseInfo;
        _infoTimer.Stop();
        _infoTimer.Start();
    }

    void CloseInfo(DispatcherQueueTimer t, object o) => _info.IsOpen = false;

    /// <summary>Mica lets the desktop colour tint the chrome on Windows 11. Windows 10 keeps solid colours.</summary>
    public void ApplyBackdrop(bool mica)
    {
        var on = mica && MicaController.IsSupported();
        SystemBackdrop = on ? new MicaBackdrop() : null;
        Palette.Translucent = on;
        Root.Background = on ? Palette.Transparent : Palette.Pane;
        Palette.Apply(Palette.Current);
        Settings.Current.Mica = mica;
        Settings.Current.Save();
    }

    /// <summary>A short first run tour with TeachingTips pointing at the parts that make Visary different.</summary>
    public async Task TourAsync()
    {
        await Task.Delay(900);
        var steps = new (FrameworkElement? target, string title, string text, Action? onShow)[]
        {
            (_ribbon.HeaderFor(Shell.RibbonTab.Transitions), "Transitions are the star here",
                "Silk, Dive, Parallax, Aurora and more: modern transitions built for elegance. Hover a tile to preview it live on your slide.",
                () => _ribbon.Tab = Shell.RibbonTab.Transitions),
            (Side, "Fine tune the feel", "Pick the motion (Smooth, Fluid, Spring) and intensity. Morph and Cascade animate each object on its own.", null),
            (Canvas, "Right click for quick tools", "The mini toolbar has bold, duplicate, group, hide and the Selection pane. Ctrl+G groups, F1 lists every shortcut.", null),
        };
        foreach (var (target, title, text, onShow) in steps)
        {
            onShow?.Invoke();
            var done = new TaskCompletionSource<bool>();
            var tip = new TeachingTip
            {
                Title = title, Subtitle = text, Target = target, IsLightDismissEnabled = false, PreferredPlacement = TeachingTipPlacementMode.Auto,
                ActionButtonContent = "Next", CloseButtonContent = "Skip tour", RequestedTheme = Palette.ElementTheme,
                IconSource = new FontIconSource { Glyph = "\uE8AB", FontFamily = Ui.IconFont },
            };
            if (title == steps[^1].title) tip.ActionButtonContent = "Done";
            tip.ActionButtonClick += (_, _) => { tip.IsOpen = false; done.TrySetResult(true); };
            tip.CloseButtonClick += (_, _) => done.TrySetResult(false);
            tip.Closed += (_, _) => done.TrySetResult(false);
            Root.Children.Add(tip);
            tip.IsOpen = true;
            var next = await done.Task;
            tip.IsOpen = false;
            Root.Children.Remove(tip);
            if (!next) break;
        }
        _ribbon.Tab = Shell.RibbonTab.Home;
        Settings.Current.SeenTour = true;
        Settings.Current.Save();
    }

    public async Task ShortcutsAsync()
    {
        var list = new (string keys, string what)[]
        {
            ("F5 / Shift+F5", "Present from the beginning / from this slide"), ("Ctrl+N, Ctrl+O, Ctrl+S", "New, open, save"), ("Ctrl+Shift+S", "Save as"),
            ("Ctrl+M", "New slide"), ("Ctrl+Z / Ctrl+Y", "Undo / redo"), ("Ctrl+C, Ctrl+X, Ctrl+V", "Copy, cut, paste (pictures and text from other apps too)"),
            ("Ctrl+D", "Duplicate"), ("Ctrl+G / Ctrl+Shift+G", "Group / ungroup"), ("Ctrl+A", "Select all"), ("Ctrl+B, Ctrl+I, Ctrl+U", "Bold, italic, underline"),
            ("Ctrl+] / Ctrl+[", "Bring forward / send backward (add Shift for front and back)"), ("Arrows (Shift for 10 px)", "Nudge the selection"),
            ("Alt+click", "Select one object inside a group"), ("Enter or F2", "Edit the selected text"), ("Ctrl+H", "Find and replace"), ("F1 or Ctrl+/", "This list"),
            ("In the show: Space, arrows, B, W, Esc", "Next and previous, black or white screen, end"),
        };
        var grid = new Grid { ColumnSpacing = 18, RowSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < list.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var keys = Ui.Text(list[i].keys, 12.5, Palette.Text, bold: true);
            var what = Ui.Text(list[i].what, 12.5, Palette.SubtleText, wrap: TextWrapping.Wrap);
            Grid.SetRow(keys, i);
            Grid.SetRow(what, i);
            Grid.SetColumn(what, 1);
            grid.Children.Add(keys);
            grid.Children.Add(what);
        }
        await Dialogs.ShowAsync(new ContentDialog
        {
            Title = "Keyboard shortcuts", Content = new ScrollViewer { Content = grid, MaxHeight = 520, Width = 520 }, CloseButtonText = "Close", RequestedTheme = Palette.ElementTheme,
        });
    }

    /* ---------------- views ---------------- */

    /// <summary>Switches between Normal view (thumbnails, slide, task pane) and Slide Sorter.</summary>
    public bool SorterView
    {
        get => _sorter.Visibility == Visibility.Visible;
        set
        {
            if (value == SorterView) return;
            Canvas.EndTextEdit(commit: true);
            var normal = value ? Visibility.Collapsed : Visibility.Visible;
            Slides.Visibility = normal;
            _center.Visibility = normal;
            Side.Visibility = normal;
            _sorter.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            if (value) _sorter.Rebuild();
        }
    }

    /* ---------------- system clipboard ---------------- */

    void OnCopied(string text)
    {
        try
        {
            // A private format marks our own objects; plain text lets other apps paste the words.
            var data = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            data.SetData(ClipboardFormat, Editor.Doc.Title);
            if (text.Length > 0) data.SetText(text);
            Clipboard.SetContent(data);
        }
        catch (Exception ex)
        {
            Log.Error("Clipboard copy", ex);
        }
    }

    /// <summary>Pastes our own objects, or a picture, files or text copied in another app.</summary>
    public async Task PasteAsync()
    {
        try
        {
            var view = Clipboard.GetContent();
            if (view.Contains(ClipboardFormat) && Editor.CanPaste)
            {
                Editor.Paste();
                return;
            }
            if (view.Contains(StandardDataFormats.StorageItems))
            {
                foreach (var item in await view.GetStorageItemsAsync())
                    if (item is Windows.Storage.StorageFile f) await Commands.InsertPictureFileAsync(Editor, f.Path);
                return;
            }
            if (view.Contains(StandardDataFormats.Bitmap))
            {
                var reference = await view.GetBitmapAsync();
                using var source = await reference.OpenReadAsync();
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(source);
                using var bitmap = await decoder.GetSoftwareBitmapAsync(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
                using var png = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, png);
                encoder.SetSoftwareBitmap(bitmap);
                await encoder.FlushAsync();
                var bytes = new byte[png.Size];
                png.Seek(0);
                using (var reader = new Windows.Storage.Streams.DataReader(png.GetInputStreamAt(0)))
                {
                    await reader.LoadAsync((uint)png.Size);
                    reader.ReadBytes(bytes);
                }
                Commands.InsertPictureBytes(Editor, bytes, "image/png");
                return;
            }
            if (view.Contains(StandardDataFormats.Text))
            {
                var text = (await view.GetTextAsync()).Replace("\r\n", "\n").Trim();
                if (text.Length > 0) Editor.InsertTextBox(text: text);
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Clipboard paste", ex);
        }
        Editor.Paste();
    }

    /* ---------------- keyboard ---------------- */

    static bool Down(VirtualKey k) => InputKeyboardSource.GetKeyStateForCurrentThread(k).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    bool TypingInControl()
    {
        var f = FocusManager.GetFocusedElement(Root.XamlRoot);
        return f is TextBox or NumberBox or AutoSuggestBox or PasswordBox or RichEditBox || (f is ComboBox c && c.IsEditable);
    }

    void OnPreviewKey(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = Down(VirtualKey.Control);
        var shift = Down(VirtualKey.Shift);
        var k = e.Key;
        bool Handle(Action a) { a(); e.Handled = true; return true; }

        if (k == VirtualKey.F5) { Handle(() => StartShow(shift ? Editor.CurrentIndex : 0)); return; }
        if (ctrl && k == VirtualKey.S) { Handle(() => _ = Commands.SaveAsync(Editor, saveAs: shift)); return; }
        if (ctrl && k == VirtualKey.O) { Handle(() => _ = OpenAsync()); return; }
        if (ctrl && k == VirtualKey.N) { Handle(() => _ = NewAsync(Factory.CreatePresentation())); return; }
        if (ctrl && k == VirtualKey.M) { Handle(() => Editor.AddSlide()); return; }
        if (ctrl && k == VirtualKey.H) { Handle(() => _ = FindReplaceAsync()); return; }
        if (k == VirtualKey.F1 || ctrl && k == (VirtualKey)191) { Handle(() => _ = ShortcutsAsync()); return; }
        if (_backstageHost.Visibility == Visibility.Visible)
        {
            if (k == VirtualKey.Escape) Handle(HideBackstage);
            return;
        }
        if (TypingInControl() || Canvas.IsEditingText) return;

        if (ctrl)
        {
            switch (k)
            {
                case VirtualKey.Z: Handle(shift ? Editor.Redo : Editor.Undo); break;
                case VirtualKey.Y: Handle(Editor.Redo); break;
                case VirtualKey.C: Handle(Editor.Copy); break;
                case VirtualKey.X: Handle(Editor.Cut); break;
                case VirtualKey.V: Handle(() => _ = PasteAsync()); break;
                case VirtualKey.G: Handle(shift ? Editor.Ungroup : Editor.Group); break;
                case VirtualKey.D: Handle(Editor.DuplicateSelection); break;
                case VirtualKey.A: Handle(Editor.SelectAll); break;
                case VirtualKey.B: Handle(() => ToggleText(s => s.Bold = !s.Bold)); break;
                case VirtualKey.I: Handle(() => ToggleText(s => s.Italic = !s.Italic)); break;
                case VirtualKey.U: Handle(() => ToggleText(s => s.Underline = !s.Underline)); break;
                case (VirtualKey)221: Handle(() => Editor.Arrange(shift ? ZOrder.Front : ZOrder.Forward)); break;
                case (VirtualKey)219: Handle(() => Editor.Arrange(shift ? ZOrder.Back : ZOrder.Backward)); break;
            }
            return;
        }
        var step = shift ? 10 : 1;
        switch (k)
        {
            case VirtualKey.Delete or VirtualKey.Back when Editor.Selection.Count > 0: Handle(Editor.DeleteSelection); break;
            case VirtualKey.Escape: Handle(Editor.ClearSelection); break;
            case VirtualKey.Left when Editor.Selection.Count > 0: Handle(() => Editor.Nudge(-step, 0)); break;
            case VirtualKey.Right when Editor.Selection.Count > 0: Handle(() => Editor.Nudge(step, 0)); break;
            case VirtualKey.Up when Editor.Selection.Count > 0: Handle(() => Editor.Nudge(0, -step)); break;
            case VirtualKey.Down when Editor.Selection.Count > 0: Handle(() => Editor.Nudge(0, step)); break;
            case VirtualKey.PageDown or VirtualKey.Down or VirtualKey.Right: Handle(() => Editor.GoTo(Editor.CurrentIndex + 1)); break;
            case VirtualKey.PageUp or VirtualKey.Up or VirtualKey.Left: Handle(() => Editor.GoTo(Editor.CurrentIndex - 1)); break;
            case VirtualKey.Enter or VirtualKey.F2 when Editor.Selection.Count == 1 && Editor.SelectedElements.First() is ITextHost:
                Handle(() => Canvas.BeginTextEdit(Editor.Selection[0])); break;
        }
    }

    void ToggleText(Action<TextStyle> f) => Editor.UpdateSelected(x => { if (x is ITextHost h) f(h.Style); });

    /* ---------------- documents ---------------- */

    async Task<bool> ConfirmDiscardAsync()
    {
        if (!Editor.IsDirty) return true;
        var r = await Dialogs.AskAsync("Save changes?", $"Do you want to save your changes to \"{Editor.Doc.Title}\"?", "Save", "Don't save");
        if (r == null) return false;
        if (r == true) return await Commands.SaveAsync(Editor);
        return true;
    }

    public async Task NewAsync(Presentation p)
    {
        if (!await ConfirmDiscardAsync()) return;
        Editor.Load(p, null);
        Canvas.SetZoom(null);
        HideBackstage();
    }

    public async Task OpenAsync()
    {
        var path = await Commands.PickOpenAsync(PresentationFile.Extension);
        if (path != null) OpenFile(path);
    }

    public async void OpenFile(string path)
    {
        try
        {
            if (!await ConfirmDiscardAsync()) return;
            var p = await Task.Run(() => PresentationFile.Load(path));
            Editor.Load(p, path);
            Settings.Current.AddRecent(path);
            Canvas.SetZoom(null);
            HideBackstage();
        }
        catch (Exception ex)
        {
            Log.Error("Open", ex);
            await Dialogs.MessageAsync("Could not open the file", ex.Message);
        }
    }

    async void OnClosing(AppWindow sender, AppWindowClosingEventArgs e)
    {
        if (_closingConfirmed || !Editor.IsDirty) return;
        e.Cancel = true;
        if (await ConfirmDiscardAsync())
        {
            _closingConfirmed = true;
            Close();
        }
    }

    void StartAutoRecover()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(90);
        timer.Tick += (_, _) =>
        {
            if (!Editor.IsDirty) return;
            try
            {
                var dir = Path.Combine(Settings.Folder, "AutoRecover");
                Directory.CreateDirectory(dir);
                var doc = Editor.Doc;
                var file = Path.Combine(dir, Commands.SafeName(doc.Title) + PresentationFile.Extension);
                _ = Task.Run(() => PresentationFile.Save(doc, file));
            }
            catch (Exception ex)
            {
                Log.Error("AutoRecover", ex);
            }
        };
        timer.Start();
    }

    public async Task FindReplaceAsync()
    {
        var find = new TextBox { PlaceholderText = "Find what" };
        var replace = new TextBox { PlaceholderText = "Replace with" };
        var matchCase = new CheckBox { Content = "Match case" };
        var info = new TextBlock { Foreground = Palette.SubtleText, TextWrapping = TextWrapping.Wrap };
        find.TextChanged += (_, _) => info.Text = $"{Editor.Find(find.Text, matchCase.IsChecked == true).Count()} objects contain this text.";
        var dialog = new ContentDialog
        {
            Title = "Find and replace",
            Content = new StackPanel { Spacing = 10, Width = 360, Children = { find, replace, matchCase, info } },
            PrimaryButtonText = "Replace all",
            SecondaryButtonText = "Find next",
            CloseButtonText = "Close",
            RequestedTheme = Palette.ElementTheme,
        };
        var r = await Dialogs.ShowAsync(dialog);
        if (r == ContentDialogResult.Primary)
        {
            var n = Editor.ReplaceAll(find.Text, replace.Text, matchCase.IsChecked == true);
            await Dialogs.MessageAsync("Find and replace", $"Replaced text in {n} objects.");
        }
        else if (r == ContentDialogResult.Secondary)
        {
            var hit = Editor.Find(find.Text, matchCase.IsChecked == true).FirstOrDefault(h => h.slide > Editor.CurrentIndex || h.slide == Editor.CurrentIndex && !Editor.Selection.Contains(h.elementId));
            if (hit == default) hit = Editor.Find(find.Text, matchCase.IsChecked == true).FirstOrDefault();
            if (hit != default)
            {
                Editor.GoTo(hit.slide);
                Editor.Select([hit.elementId]);
            }
        }
    }

    /* ---------------- backstage ---------------- */

    public void ShowBackstage(string page = "home")
    {
        _backstageHost.Children.Clear();
        _backstageHost.Children.Add(new Backstage(this, page));
        _backstageHost.Visibility = Visibility.Visible;
    }

    public void HideBackstage()
    {
        _backstageHost.Visibility = Visibility.Collapsed;
        _backstageHost.Children.Clear();
    }

    /* ---------------- slide show ---------------- */

    public void StartShow(int index, bool forcePresenter = false)
    {
        if (_showWindow != null) return;
        Canvas.EndTextEdit(commit: true);
        var show = new ShowController(Editor.Doc, index);
        DisplayArea? audience = null;
        try
        {
            var displays = DisplayArea.FindAll();
            var mine = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            for (var i = 0; i < displays.Count; i++)
                if (displays[i].DisplayId.Value != mine.DisplayId.Value) { audience = displays[i]; break; }
        }
        catch (Exception ex)
        {
            Log.Error("Displays", ex);
        }
        var usePresenter = forcePresenter || (audience != null && Settings.Current.PresenterView);
        _showWindow = new SlideShowWindow(show, usePresenter ? audience : null);
        if (usePresenter)
        {
            _presenter = new PresenterWindow(show);
            _presenter.Black += () => _showWindow?.Blank(Microsoft.UI.Colors.Black);
            _presenter.EndShow += () => _showWindow?.Close();
            _presenter.Closed += (_, _) => _presenter = null;
            _presenter.Activate();
        }
        _showWindow.Closing += () =>
        {
            var last = show.Index;
            _showWindow = null;
            _presenter?.Close();
            _presenter = null;
            DispatcherQueue.TryEnqueue(() => { Editor.GoTo(last); Activate(); });
        };
        _showWindow.Activate();
    }
}
