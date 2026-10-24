using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Visary.App.Rendering;
using Visary.App.Shell;
using Visary.Core.Model;
using Visary.Core.Transitions;

namespace Visary.App.Transitions;

/// <summary>
/// Shows one slide at a time and plays transitions between them. Used by the slide show
/// and by the editor's transition preview. Sits at the slide's logical size; wrap it in a Viewbox.
/// </summary>
public sealed partial class TransitionStage : Grid
{
    readonly Presentation _pres;
    readonly Canvas _overlay = new() { IsHitTestVisible = false };
    readonly TransitionPlayer _player;
    CancellationTokenSource? _skip;
    int _generation;

    public SlideView? Current { get; private set; }
    public bool IsPlaying => _skip != null;

    /// <summary>Runs on each new view before it is captured, for example to hide objects that build in later.</summary>
    public Action<SlideView>? Prepare { get; set; }

    public TransitionStage(Presentation pres)
    {
        _pres = pres;
        Width = pres.Width;
        Height = pres.Height;
        Background = new SolidColorBrush(Microsoft.UI.Colors.Black);
        _overlay.Width = pres.Width;
        _overlay.Height = pres.Height;
        Children.Add(_overlay);
        _player = new TransitionPlayer(ElementCompositionPreview.GetElementVisual(this).Compositor);
    }

    /// <summary>Finishes the running transition immediately.</summary>
    public void Skip() => _skip?.Cancel();

    /// <summary>Shows <paramref name="slide"/>, animating with <paramref name="transition"/> (null cuts).</summary>
    public async Task ShowAsync(Slide slide, TransitionSettings? transition, string accent, Func<Slide, Scene>? sceneOf = null)
    {
        Skip();
        var gen = ++_generation;
        var view = new SlideView(_pres, slide, SlideViewMode.Show);
        // The new slide goes underneath the old one, so the first frame still shows the old slide.
        Children.Insert(0, view);
        Prepare?.Invoke(view);
        var old = Current;
        Current = view;

        if (old == null || transition == null || transition.Effect == "none")
        {
            if (old != null) Children.Remove(old);
            return;
        }

        await view.ReadyAsync();
        await NextFrameAsync();
        if (gen != _generation)
        {
            Children.Remove(old);
            return;
        }

        var plan = TransitionPlanner.Build(transition, (sceneOf ?? Scene.From)(old.Slide), (sceneOf ?? Scene.From)(slide), _pres.Width, _pres.Height, accent);
        _skip = new CancellationTokenSource();
        try
        {
            await _player.PlayAsync(plan, new Sources(old, view), _overlay, _skip.Token);
        }
        catch (Exception ex)
        {
            Log.Error($"Playing transition {transition.Effect}", ex);
        }
        finally
        {
            Children.Remove(old);
            if (gen == _generation)
            {
                _skip.Dispose();
                _skip = null;
            }
        }
    }

    /// <summary>Plays a transition between two slides once, for previews. Ends showing <paramref name="to"/>.</summary>
    public async Task PreviewAsync(Slide? from, Slide to, string accent)
    {
        Children.RemoveAll(c => c is SlideView);
        Current = null;
        if (from != null) await ShowAsync(from, null, accent);
        else
        {
            var black = new Slide { Background = new Background { Fill = FillSpec.Solid("#000000") } };
            await ShowAsync(black, null, accent);
        }
        await Current!.ReadyAsync();
        await ShowAsync(to, to.Transition, accent);
    }

    public static Task NextFrameAsync()
    {
        var tcs = new TaskCompletionSource();
        void Handler(object? s, object e)
        {
            CompositionTarget.Rendering -= Handler;
            tcs.TrySetResult();
        }
        CompositionTarget.Rendering += Handler;
        return tcs.Task;
    }

    sealed class Sources(SlideView from, SlideView to) : IActorSources
    {
        static Visual V(UIElement e) => ElementCompositionPreview.GetElementVisual(e);
        public Visual? Slide(bool incoming) => V(incoming ? to : from);
        public Visual? Background(bool incoming) => V((incoming ? to : from).BackgroundLayer);
        public Visual? Element(bool incoming, string id) =>
            (incoming ? to : from).Parts.TryGetValue(id, out var p) ? V(p.Content) : null;
    }
}

static class PanelExtensions
{
    public static void RemoveAll(this UIElementCollection children, Func<UIElement, bool> predicate)
    {
        for (var i = children.Count - 1; i >= 0; i--)
            if (predicate(children[i])) children.RemoveAt(i);
    }
}
