using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using Visary.App.Rendering;
using Visary.App.Shell;
using Visary.Core.Model;
using Visary.Core.Transitions;

namespace Visary.App.Transitions;

/// <summary>Plays object animations on a rendered slide with Composition.</summary>
public static class BuildAnimator
{
    static Visual Prepare(ElementParts p, SlideElement e)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(p.BuildLayer, true);
        var v = ElementCompositionPreview.GetElementVisual(p.BuildLayer);
        v.CenterPoint = new Vector3((float)e.Width / 2, (float)e.Height / 2, 0);
        return v;
    }

    /// <summary>Hides objects whose first animation is an entrance.</summary>
    public static void ApplyInitialState(SlideView view)
    {
        foreach (var id in BuildTimeline.InitiallyHidden(view.Slide))
            if (view.Parts.TryGetValue(id, out var p) && view.Slide.Find(id) is { } e)
                Prepare(p, e).Opacity = 0;
    }

    /// <summary>Objects hidden until they build in. The transition planner leaves them out.</summary>
    public static Scene VisibleScene(Slide s)
    {
        var hidden = BuildTimeline.InitiallyHidden(s);
        return new Scene(Scene.From(s).Elements.Where(e => !hidden.Contains(e.Id)).ToList());
    }

    public static Task PlayStepAsync(SlideView view, BuildStep step, bool instant = false)
    {
        var compositor = ElementCompositionPreview.GetElementVisual(view).Compositor;
        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        foreach (var item in step.Items)
        {
            if (!view.Parts.TryGetValue(item.Build.ElementId, out var parts) || view.Slide.Find(item.Build.ElementId) is not { } el) continue;
            var visual = Prepare(parts, el);
            foreach (var track in BuildCatalog.Tracks(item.Build))
            {
                var anim = compositor.CreateScalarKeyFrameAnimation();
                foreach (var k in track.Keys)
                {
                    var ease = k.Ease == Ease.Linear
                        ? (CompositionEasingFunction)compositor.CreateLinearEasingFunction()
                        : compositor.CreateCubicBezierEasingFunction(new Vector2((float)k.Ease.X1, (float)k.Ease.Y1), new Vector2((float)k.Ease.X2, (float)k.Ease.Y2));
                    anim.InsertKeyFrame((float)k.Time, (float)k.Value, ease);
                }
                anim.Duration = TimeSpan.FromMilliseconds(instant ? 1 : Math.Max(1, item.Build.DurationMs));
                if (!instant)
                {
                    anim.DelayTime = TimeSpan.FromMilliseconds(item.StartMs + item.Build.DelayMs);
                    anim.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
                }
                try
                {
                    switch (track.Prop)
                    {
                        case Prop.Opacity: visual.StartAnimation("Opacity", anim); break;
                        case Prop.TranslateX: visual.StartAnimation("Translation.X", anim); break;
                        case Prop.TranslateY: visual.StartAnimation("Translation.Y", anim); break;
                        case Prop.Scale: visual.StartAnimation("Scale.X", anim); visual.StartAnimation("Scale.Y", anim); break;
                        case Prop.ScaleX: visual.StartAnimation("Scale.X", anim); break;
                        case Prop.ScaleY: visual.StartAnimation("Scale.Y", anim); break;
                        case Prop.RotateZ: visual.StartAnimation("RotationAngleInDegrees", anim); break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Build {item.Build.Effect}", ex);
                }
            }
        }
        batch.End();
        var tcs = new TaskCompletionSource();
        batch.Completed += (_, _) => tcs.TrySetResult();
        return tcs.Task;
    }
}
