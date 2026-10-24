using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Visary.App.Rendering;
using Visary.App.Shell;

namespace Visary.App.Show;

/// <summary>The speaker's window: current and next slide, notes, timer and controls.</summary>
public sealed partial class PresenterWindow : Window
{
    readonly ShowController _show;
    readonly Border _current = new() { CornerRadius = new CornerRadius(6) };
    readonly Border _next = new() { CornerRadius = new CornerRadius(4) };
    readonly TextBlock _notes = new() { TextWrapping = TextWrapping.Wrap, FontSize = 22, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), LineHeight = 32 };
    readonly TextBlock _timer = new() { FontSize = 30, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) };
    readonly TextBlock _clock = new() { FontSize = 18, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGray), VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _count = new() { FontSize = 15, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGray), VerticalAlignment = VerticalAlignment.Center };
    readonly DispatcherQueueTimer _tick;
    DateTime _start = DateTime.Now;
    TimeSpan? _paused;

    public PresenterWindow(ShowController show)
    {
        _show = show;
        Title = $"Presenter view - {show.Presentation.Title}";
        var dark = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 23, 27));
        var panel = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 34, 40));

        var root = new Grid { Background = dark, Padding = new Thickness(20), RowSpacing = 16, RequestedTheme = ElementTheme.Dark };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var pause = new Button { Content = "Pause" };
        pause.Click += (_, _) =>
        {
            if (_paused is { } p) { _start = DateTime.Now - p; _paused = null; pause.Content = "Pause"; }
            else { _paused = DateTime.Now - _start; pause.Content = "Resume"; }
        };
        var reset = new Button { Content = "Reset" };
        reset.Click += (_, _) => { _start = DateTime.Now; if (_paused != null) _paused = TimeSpan.Zero; };
        var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Children = { _timer, pause, reset, _clock, _count } };
        root.Children.Add(top);

        var main = new Grid { ColumnSpacing = 20 };
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        main.Children.Add(new Viewbox { Child = _current, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Top });
        var side = new Grid { RowSpacing = 10 };
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        side.RowDefinitions.Add(new RowDefinition());
        var smaller = new Button { Content = "A-" };
        smaller.Click += (_, _) => _notes.FontSize = Math.Max(12, _notes.FontSize - 2);
        var larger = new Button { Content = "A+" };
        larger.Click += (_, _) => _notes.FontSize = Math.Min(60, _notes.FontSize + 2);
        side.Children.Add(Label("NEXT"));
        var nextBox = new Viewbox { Child = _next, Stretch = Stretch.Uniform, MaxHeight = 260, HorizontalAlignment = HorizontalAlignment.Left };
        Grid.SetRow(nextBox, 1);
        side.Children.Add(nextBox);
        var notesHead = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Label("NOTES"), smaller, larger } };
        Grid.SetRow(notesHead, 2);
        side.Children.Add(notesHead);
        var notesScroll = new ScrollViewer { Content = _notes, Padding = new Thickness(14), Background = panel, CornerRadius = new CornerRadius(6) };
        Grid.SetRow(notesScroll, 3);
        side.Children.Add(notesScroll);
        Grid.SetColumn(side, 1);
        main.Children.Add(side);
        Grid.SetRow(main, 1);
        root.Children.Add(main);

        var nav = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        nav.Children.Add(NavButton("Previous", show.Previous));
        nav.Children.Add(NavButton("Next", show.Next, primary: true));
        nav.Children.Add(NavButton("Black screen", () => Black?.Invoke()));
        nav.Children.Add(NavButton("End show", () => EndShow?.Invoke()));
        Grid.SetRow(nav, 2);
        root.Children.Add(nav);
        Content = root;

        root.KeyDown += (_, e) =>
        {
            if (e.Key is Windows.System.VirtualKey.Right or Windows.System.VirtualKey.Space or Windows.System.VirtualKey.PageDown) { show.Next(); e.Handled = true; }
            if (e.Key is Windows.System.VirtualKey.Left or Windows.System.VirtualKey.PageUp) { show.Previous(); e.Handled = true; }
        };

        _tick = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _tick.Interval = TimeSpan.FromMilliseconds(500);
        _tick.Tick += (_, _) => UpdateClock();
        _tick.Start();
        show.StateChanged += Refresh;
        Closed += (_, _) => { _tick.Stop(); show.StateChanged -= Refresh; };
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));
        Refresh();
    }

    public event Action? Black;
    public event Action? EndShow;

    static TextBlock Label(string t) => new() { Text = t, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray), CharacterSpacing = 80, VerticalAlignment = VerticalAlignment.Center };

    static Button NavButton(string text, Action a, bool primary = false)
    {
        var b = new Button { Content = text, Padding = new Thickness(18, 8, 18, 8) };
        if (primary) b.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        b.Click += (_, _) => a();
        return b;
    }

    void UpdateClock()
    {
        var elapsed = _paused ?? DateTime.Now - _start;
        _timer.Text = elapsed.TotalHours >= 1 ? elapsed.ToString(@"h\:mm\:ss") : elapsed.ToString(@"mm\:ss");
        _clock.Text = DateTime.Now.ToShortTimeString();
    }

    void Refresh()
    {
        var p = _show.Presentation;
        var slide = p.Slides[_show.Index];
        _current.Child = new SlideView(p, slide, SlideViewMode.Thumbnail);
        var next = _show.NextSlideIndex();
        _next.Child = next >= 0
            ? new SlideView(p, p.Slides[next], SlideViewMode.Thumbnail)
            : new Grid { Width = p.Width, Height = p.Height, Background = new SolidColorBrush(Microsoft.UI.Colors.Black), Children = { new TextBlock { Text = "End of show", FontSize = 80, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } } };
        _notes.Text = string.IsNullOrWhiteSpace(slide.Notes) ? "No notes for this slide." : slide.Notes;
        _count.Text = $"Slide {_show.Index + 1} of {p.Slides.Count}" + (_show.StepCount > 0 ? $"   Step {_show.Step} of {_show.StepCount}" : "") + (_show.AtEnd ? "   End" : "");
        UpdateClock();
    }
}
