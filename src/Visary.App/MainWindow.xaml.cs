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
using Windows.System;

namespace Visary.App;

public sealed partial class MainWindow : Window
{
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
    SlideShowWindow? _showWindow;
    PresenterWindow? _presenter;

    public DocumentEditor Editor { get; } = new(Factory.CreatePresentation("office", sample: true));
    public SlideCanvas Canvas { get; private set; } = null!;
    public SidePane Side { get; private set; } = null!;
    public SlidesPane Slides { get; private set; } = null!;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Visary PRSNT";
        Commands.Owner = this;
        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        try { AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Visary.ico")); } catch (Exception ex) { Log.Error("Icon", ex); }
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1500, 920));

        Palette.Apply(AppThemes.Get(Settings.Current.AppTheme));
        BuildLayout();
        SetTitleBar(_titleBar.Children[1] as UIElement);
        ApplyCaptionColors();
        Palette.Changed += () => { Root.RequestedTheme = Palette.ElementTheme; ApplyCaptionColors(); };

        Editor.Changed += OnChanged;
        AppWindow.Closing += OnClosing;
        Root.Loaded += (_, _) => { Dialogs.Root = Root.XamlRoot; OnChanged(ChangeKind.All); };
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
        Side.PreviewTransition = Canvas.PreviewTransitionAsync;
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
        Grid.SetRow(_workspace, 2);
        Root.Children.Add(_workspace);

        // Status bar
        var statusBar = new Grid { Background = Palette.Pane, Height = 30, Padding = new Thickness(12, 0, 12, 0), BorderBrush = Palette.Border, BorderThickness = new Thickness(0, 1, 0, 0) };
        statusBar.ColumnDefinitions.Add(new ColumnDefinition());
        statusBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _status.Foreground = Palette.SubtleText;
        statusBar.Children.Add(_status);
        var zoom = new Slider { Width = 140, Minimum = 10, Maximum = 400, Value = 50, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) };
        zoom.ValueChanged += (_, e) => { if (Math.Abs(e.NewValue - Canvas.Zoom * 100) > .5) Canvas.SetZoom(e.NewValue / 100); };
        Canvas.ZoomChanged += () => { _zoomLabel.Text = $"{Math.Round(Canvas.Zoom * 100)}%"; zoom.Value = Canvas.Zoom * 100; };
        _zoomLabel.Foreground = Palette.SubtleText;
        var notesToggle = Ui.Small("", "Notes", () => SetNotesVisible(_notesHost.Visibility != Visibility.Visible));
        var fit = Ui.Small("", null, () => Canvas.SetZoom(null), "Fit slide to window");
        var play = Ui.Small("", null, () => StartShow(Editor.CurrentIndex), "Slide show");
        var right = Ui.Row(notesToggle, play, zoom, _zoomLabel, fit);
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
        _title.Text = $"{name}{dirty} - Visary PRSNT";
        Title = _title.Text;
        _status.Text = $"Slide {Editor.CurrentIndex + 1} of {Editor.Doc.Slides.Count}     {Editor.Theme.Name}     {Core.Transitions.TransitionCatalog.Get(Editor.Current.Transition.Effect).Name} transition";
        if (k.HasFlag(ChangeKind.CurrentSlide) || k == ChangeKind.All || k.HasFlag(ChangeKind.Slides))
        {
            _syncingNotes = true;
            if (_notes.Text != Editor.Current.Notes) _notes.Text = Editor.Current.Notes;
            _syncingNotes = false;
        }
        _zoomLabel.Text = $"{Math.Round(Canvas.Zoom * 100)}%";
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
                case VirtualKey.V: Handle(Editor.Paste); break;
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
