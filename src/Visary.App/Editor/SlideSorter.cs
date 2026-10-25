using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Visary.App.Rendering;
using Visary.App.Shell;
using Visary.Core.Editing;
using Visary.Core.Transitions;

namespace Visary.App.Editor;

/// <summary>The Slide Sorter view: every slide as a large tile, drag to reorder, double click to edit.</summary>
public sealed partial class SlideSorter : Grid
{
    readonly DocumentEditor _ed;
    readonly GridView _grid = new()
    {
        SelectionMode = ListViewSelectionMode.Single,
        CanDragItems = true,
        CanReorderItems = true,
        AllowDrop = true,
        Padding = new Thickness(28, 20, 28, 28),
    };
    readonly ObservableCollection<FrameworkElement> _items = [];
    bool _syncing;
    double _tileWidth = 240;

    /// <summary>Raised when a slide is opened from the sorter, to switch back to Normal view.</summary>
    public event Action? OpenRequested;

    public SlideSorter(DocumentEditor ed)
    {
        _ed = ed;
        Background = Palette.Canvas;
        _grid.ItemsSource = _items;
        Children.Add(_grid);
        _grid.SelectionChanged += (_, _) =>
        {
            if (_syncing || _grid.SelectedIndex < 0) return;
            _ed.GoTo(_grid.SelectedIndex);
        };
        _grid.DragItemsCompleted += (_, e) =>
        {
            if (e.Items.FirstOrDefault() is not FrameworkElement moved || moved.Tag is not string id) return;
            var to = _items.IndexOf(moved);
            var from = _ed.Doc.IndexOf(id);
            if (from >= 0 && to >= 0 && from != to) _ed.MoveSlide(from, to);
            else Rebuild();
        };
        _ed.Changed += k =>
        {
            if (Visibility != Visibility.Visible) return;
            if (k.HasFlag(ChangeKind.Slides) || k.HasFlag(ChangeKind.Document) || k.HasFlag(ChangeKind.Content)) Rebuild();
            else if (k.HasFlag(ChangeKind.CurrentSlide)) Sync();
        };
    }

    /// <summary>Tile width in pixels, changed by the zoom slider while the sorter is showing.</summary>
    public double TileWidth
    {
        get => _tileWidth;
        set { _tileWidth = Math.Clamp(value, 120, 520); Rebuild(); }
    }

    void Sync()
    {
        _syncing = true;
        _grid.SelectedIndex = _ed.CurrentIndex;
        if (_grid.SelectedItem != null) _grid.ScrollIntoView(_grid.SelectedItem);
        _syncing = false;
    }

    public void Rebuild()
    {
        _syncing = true;
        _items.Clear();
        for (var i = 0; i < _ed.Doc.Slides.Count; i++) _items.Add(Tile(i));
        _syncing = false;
        Sync();
    }

    FrameworkElement Tile(int index)
    {
        var slide = _ed.Doc.Slides[index];
        var h = _tileWidth * _ed.Doc.Height / _ed.Doc.Width;
        var frame = new Border
        {
            Child = new Viewbox { Width = _tileWidth, Height = h, Child = new SlideView(_ed.Doc, slide, SlideViewMode.Thumbnail) },
            BorderBrush = Palette.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Opacity = slide.Hidden ? .45 : 1,
        };
        var info = TransitionCatalog.Get(slide.Transition.Effect);
        var caption = new Grid { Margin = new Thickness(2, 6, 2, 0), ColumnSpacing = 6 };
        caption.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        caption.ColumnDefinitions.Add(new ColumnDefinition());
        caption.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        caption.Children.Add(Ui.Text((index + 1).ToString(), 12.5, Palette.Text, bold: true));
        if (slide.Transition.Effect != "none")
        {
            var t = Ui.Row(Ui.Icon(info.Glyph, 12, Palette.SubtleText), Ui.Text(info.Name, 11.5, Palette.SubtleText));
            Grid.SetColumn(t, 1);
            caption.Children.Add(t);
        }
        var extras = Ui.Row();
        if (slide.Builds.Count > 0) extras.Children.Add(Ui.Icon("", 12, Palette.SubtleText));
        if (slide.Hidden) extras.Children.Add(Ui.Icon("", 12, Palette.SubtleText));
        if (slide.Transition.AdvanceAfterSeconds is { } secs) extras.Children.Add(Ui.Text($"{secs:0.#} s", 11.5, Palette.SubtleText));
        Grid.SetColumn(extras, 2);
        caption.Children.Add(extras);
        var tile = new StackPanel { Tag = slide.Id, Padding = new Thickness(8), Children = { frame, caption } };
        tile.DoubleTapped += (_, _) =>
        {
            var i = _ed.Doc.IndexOf(slide.Id);
            if (i >= 0) _ed.GoTo(i);
            OpenRequested?.Invoke();
        };
        tile.RightTapped += (_, e) =>
        {
            var i = _ed.Doc.IndexOf(slide.Id);
            if (i < 0) return;
            _ed.GoTo(i);
            var menu = new MenuFlyout();
            menu.Items.Add(Ui.Item("Open in Normal view", () => OpenRequested?.Invoke(), ""));
            menu.Items.Add(Ui.Item("New slide", () => _ed.AddSlide(), ""));
            menu.Items.Add(Ui.Item("Duplicate slide", () => _ed.DuplicateSlide(i), ""));
            menu.Items.Add(Ui.Item(slide.Hidden ? "Show slide" : "Hide slide", () => _ed.ToggleHidden(i), ""));
            menu.Items.Add(Ui.Item("Delete slide", () => _ed.DeleteSlide(i), ""));
            menu.ShowAt(tile, e.GetPosition(tile));
        };
        return tile;
    }
}
