using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Visary.App.Rendering;
using Visary.App.Shell;
using Visary.Core.Editing;
using Visary.Core.Model;
using Visary.Core.Transitions;

namespace Visary.App.Editor;

/// <summary>Slide thumbnails on the left, with drag to reorder.</summary>
public sealed partial class SlidesPane : Grid
{
    readonly DocumentEditor _ed;
    readonly ListView _list = new()
    {
        SelectionMode = ListViewSelectionMode.Single,
        CanDragItems = true,
        CanReorderItems = true,
        AllowDrop = true,
        Padding = new Thickness(0, 6, 0, 24),
    };
    readonly ObservableCollection<FrameworkElement> _items = [];
    readonly DispatcherQueueTimer _refresh;
    bool _syncing;
    const double ThumbWidth = 168;

    public SlidesPane(DocumentEditor ed)
    {
        _ed = ed;
        Background = Palette.Pane;
        BorderBrush = Palette.Border;
        BorderThickness = new Thickness(0, 0, 1, 0);
        _list.ItemsSource = _items;
        Children.Add(_list);
        _list.SelectionChanged += (_, _) =>
        {
            if (_syncing || _list.SelectedIndex < 0) return;
            _ed.GoTo(_list.SelectedIndex);
        };
        _list.DragItemsCompleted += (_, e) =>
        {
            if (e.Items.FirstOrDefault() is not FrameworkElement moved || moved.Tag is not string id) return;
            var to = _items.IndexOf(moved);
            var from = _ed.Doc.IndexOf(id);
            if (from >= 0 && to >= 0 && from != to) _ed.MoveSlide(from, to);
            else Rebuild();
        };
        _refresh = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _refresh.Interval = TimeSpan.FromMilliseconds(220);
        _refresh.IsRepeating = false;
        _refresh.Tick += (_, _) => RefreshCurrent();
        _ed.Changed += OnChanged;
        Rebuild();
    }

    void OnChanged(ChangeKind k)
    {
        if (k.HasFlag(ChangeKind.Slides) || k.HasFlag(ChangeKind.Document)) Rebuild();
        else if (k.HasFlag(ChangeKind.Content)) _refresh.Start();
        if (k.HasFlag(ChangeKind.CurrentSlide)) SyncSelection();
    }

    void SyncSelection()
    {
        _syncing = true;
        _list.SelectedIndex = _ed.CurrentIndex;
        if (_list.SelectedItem != null) _list.ScrollIntoView(_list.SelectedItem);
        _syncing = false;
    }

    public void Rebuild()
    {
        _syncing = true;
        _items.Clear();
        for (var i = 0; i < _ed.Doc.Slides.Count; i++) _items.Add(Item(i));
        _syncing = false;
        SyncSelection();
    }

    void RefreshCurrent()
    {
        var i = _ed.CurrentIndex;
        if (i >= _items.Count) return;
        _syncing = true;
        _items[i] = Item(i);
        _list.SelectedIndex = i;
        _syncing = false;
    }

    FrameworkElement Item(int index)
    {
        var slide = _ed.Doc.Slides[index];
        var thumb = new Viewbox { Width = ThumbWidth, Height = ThumbWidth * _ed.Doc.Height / _ed.Doc.Width, Child = new SlideView(_ed.Doc, slide, SlideViewMode.Thumbnail), Stretch = Stretch.Uniform };
        var frame = new Border
        {
            Child = thumb, BorderBrush = Palette.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3),
            Opacity = slide.Hidden ? .45 : 1,
        };
        var badges = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 0) };
        if (slide.Transition.Effect != "none")
        {
            var info = TransitionCatalog.Get(slide.Transition.Effect);
            var star = Ui.Icon(info.Glyph, 11, Palette.SubtleText);
            ToolTipService.SetToolTip(star, $"Transition: {info.Name}");
            badges.Children.Add(star);
        }
        if (slide.Builds.Count > 0) badges.Children.Add(Ui.Icon("", 11, Palette.SubtleText));
        var number = new StackPanel
        {
            Width = 22,
            Children =
            {
                new TextBlock { Text = (index + 1).ToString(), FontSize = 12, Foreground = Palette.SubtleText, HorizontalAlignment = HorizontalAlignment.Right, TextDecorations = slide.Hidden ? Windows.UI.Text.TextDecorations.Strikethrough : Windows.UI.Text.TextDecorations.None },
                badges,
            },
        };
        var grid = new Grid { Tag = slide.Id, Padding = new Thickness(0, 4, 6, 4), ColumnSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(number);
        Grid.SetColumn(frame, 1);
        grid.Children.Add(frame);
        grid.RightTapped += (_, e) =>
        {
            var i = _ed.Doc.IndexOf(slide.Id);
            if (i < 0) return;
            _ed.GoTo(i);
            var menu = new MenuFlyout();
            menu.Items.Add(Ui.Item("New slide", () => _ed.AddSlide(), ""));
            menu.Items.Add(Ui.Item("Duplicate slide", () => _ed.DuplicateSlide(i), ""));
            menu.Items.Add(Ui.Item("Delete slide", () => _ed.DeleteSlide(i), ""));
            menu.Items.Add(Ui.Item(slide.Hidden ? "Show slide" : "Hide slide", () => _ed.ToggleHidden(i), ""));
            menu.ShowAt(grid, e.GetPosition(grid));
        };
        return grid;
    }
}
