using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Visary.App.Shell;
using Visary.Core.Model;
using VirtualKey = Windows.System.VirtualKey;

namespace Visary.App.Show;

/// <summary>A focusable root so the show window receives keyboard input.</summary>
public sealed partial class FocusRoot : UserControl
{
    public FocusRoot()
    {
        IsTabStop = true;
        UseSystemFocusVisuals = false;
    }
}

/// <summary>The audience window: full screen, black letterbox, keyboard and mouse navigation.</summary>
public sealed partial class SlideShowWindow : Window
{
    readonly ShowController _show;
    readonly FocusRoot _root = new();
    readonly Grid _grid = new() { Background = new SolidColorBrush(Microsoft.UI.Colors.Black) };
    readonly Border _blank = new() { Visibility = Visibility.Collapsed };
    readonly TextBlock _end = new() { Text = "End of slide show. Click or press any key to exit.", Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray), FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 30, 0, 0), Visibility = Visibility.Collapsed };
    readonly StackPanel _controls = new() { Orientation = Orientation.Horizontal, Spacing = 4, Padding = new Thickness(6), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(18), Opacity = 0, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Windows.UI.Color.FromArgb(190, 24, 24, 26)) };
    readonly Ellipse _laser = new() { Width = 16, Height = 16, Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 40, 40)), Visibility = Visibility.Collapsed, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    readonly DispatcherQueueTimer _idle;
    string _digits = "";
    bool _closed;

    public event Action? Closing;

    public SlideShowWindow(ShowController show, DisplayArea? display)
    {
        _show = show;
        Title = $"{show.Presentation.Title} - Slide Show";
        _grid.Children.Add(new Viewbox { Child = show.Stage, Stretch = Stretch.Uniform });
        _grid.Children.Add(_blank);
        _grid.Children.Add(new Grid { Children = { _end }, Background = new SolidColorBrush(Microsoft.UI.Colors.Black), Visibility = Visibility.Collapsed });
        _grid.Children.Add(_laser);
        _controls.Children.Add(Btn("", "Previous", show.Previous));
        _controls.Children.Add(Btn("", "Next", show.Next));
        _controls.Children.Add(Btn("", "Laser pointer (L)", ToggleLaser));
        _controls.Children.Add(Btn("", "Black screen (B)", () => Blank(Microsoft.UI.Colors.Black)));
        _controls.Children.Add(Btn("", "End show (Esc)", Close));
        _grid.Children.Add(_controls);
        _root.Content = _grid;
        Content = _root;

        _idle = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _idle.Interval = TimeSpan.FromSeconds(2.5);
        _idle.IsRepeating = false;
        _idle.Tick += (_, _) => _controls.Opacity = 0;
        _root.KeyDown += OnKey;
        _root.Tapped += (_, e) =>
        {
            if (e.OriginalSource is FrameworkElement f && IsInside(f, _controls)) return;
            if (_show.ClickAdvances || _show.AtEnd) _show.Next();
        };
        _root.RightTapped += (_, _) => _show.Previous();
        _root.PointerMoved += (_, e) =>
        {
            _controls.Opacity = 1;
            _idle.Stop();
            _idle.Start();
            if (_laser.Visibility == Visibility.Visible)
            {
                var p = e.GetCurrentPoint(_grid).Position;
                _laser.Margin = new Thickness(p.X - 8, p.Y - 8, 0, 0);
            }
        };

        _show.StateChanged += OnState;
        _show.EndRequested += Close;
        Closed += (_, _) =>
        {
            _closed = true;
            _show.Stop();
            Closing?.Invoke();
        };

        if (display != null) AppWindow.Move(new Windows.Graphics.PointInt32(display.OuterBounds.X + 40, display.OuterBounds.Y + 40));
        AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        _root.Loaded += async (_, _) =>
        {
            _root.Focus(FocusState.Programmatic);
            await _show.StartAsync();
        };
    }

    static bool IsInside(DependencyObject child, DependencyObject parent)
    {
        for (var d = child; d != null; d = VisualTreeHelper.GetParent(d)) if (d == parent) return true;
        return false;
    }

    static Button Btn(string glyph, string tip, Action a)
    {
        var b = new Button { Content = Ui.Icon(glyph, 14, new SolidColorBrush(Microsoft.UI.Colors.White)), Background = Palette.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(10, 6, 10, 6) };
        ToolTipService.SetToolTip(b, tip);
        b.Click += (_, _) => a();
        return b;
    }

    void OnState()
    {
        if (_closed) return;
        var endLayer = (Grid)_end.Parent;
        endLayer.Visibility = _show.AtEnd ? Visibility.Visible : Visibility.Collapsed;
        _end.Visibility = endLayer.Visibility;
    }

    void ToggleLaser() => _laser.Visibility = _laser.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    public void Blank(Windows.UI.Color c)
    {
        if (_blank.Visibility == Visibility.Visible && _blank.Background is SolidColorBrush b && b.Color == c) _blank.Visibility = Visibility.Collapsed;
        else
        {
            _blank.Background = new SolidColorBrush(c);
            _blank.Visibility = Visibility.Visible;
        }
    }

    void OnKey(object sender, KeyRoutedEventArgs e)
    {
        var k = e.Key;
        e.Handled = true;
        if (k is >= VirtualKey.Number0 and <= VirtualKey.Number9) { _digits += (char)('0' + (k - VirtualKey.Number0)); return; }
        if (k is >= VirtualKey.NumberPad0 and <= VirtualKey.NumberPad9) { _digits += (char)('0' + (k - VirtualKey.NumberPad0)); return; }
        if (k == VirtualKey.Enter && _digits.Length > 0)
        {
            if (int.TryParse(_digits, out var n)) _show.GoTo(n - 1);
            _digits = "";
            return;
        }
        _digits = "";
        if (_blank.Visibility == Visibility.Visible && k is not (VirtualKey.B or VirtualKey.W))
        {
            _blank.Visibility = Visibility.Collapsed;
            return;
        }
        switch (k)
        {
            case VirtualKey.Right or VirtualKey.Down or VirtualKey.PageDown or VirtualKey.Space or VirtualKey.Enter or VirtualKey.N:
                _show.Next(); break;
            case VirtualKey.Left or VirtualKey.Up or VirtualKey.PageUp or VirtualKey.Back or VirtualKey.P:
                _show.Previous(); break;
            case VirtualKey.Home: _show.GoTo(0); break;
            case VirtualKey.End: _show.GoTo(_show.Presentation.Slides.Count - 1); break;
            case VirtualKey.B: Blank(Microsoft.UI.Colors.Black); break;
            case VirtualKey.W: Blank(Microsoft.UI.Colors.White); break;
            case VirtualKey.L: ToggleLaser(); break;
            case VirtualKey.Escape: Close(); break;
            default: e.Handled = false; break;
        }
    }
}
