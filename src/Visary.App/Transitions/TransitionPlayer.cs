using System.Numerics;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Visary.App.Shell;
using Visary.Core.Transitions;
using Windows.UI;

namespace Visary.App.Transitions;

/// <summary>Supplies live visuals for the actors of a plan.</summary>
public interface IActorSources
{
    Visual? Slide(bool incoming);
    Visual? Background(bool incoming);
    Visual? Element(bool incoming, string id);
}

/// <summary>
/// Plays a <see cref="TransitionPlan"/> with Windows Composition. Everything runs on the GPU
/// compositor thread, so transitions stay smooth even while the UI thread is busy.
/// </summary>
public sealed class TransitionPlayer(Compositor compositor)
{
    readonly Compositor _c = compositor;
    CompositionEffectFactory? _effects;
    readonly Dictionary<Ease, CompositionEasingFunction> _eases = [];

    CompositionEffectFactory Effects => _effects ??= _c.CreateEffectFactory(
        new ExposureEffect
        {
            Name = "Expo",
            Exposure = 0f,
            Source = new SaturationEffect
            {
                Name = "Sat",
                Saturation = 1f,
                Source = new GaussianBlurEffect
                {
                    Name = "Blur",
                    BlurAmount = 0f,
                    BorderMode = EffectBorderMode.Hard,
                    Optimization = EffectOptimization.Balanced,
                    Source = new CompositionEffectSourceParameter("src"),
                },
            },
        },
        ["Blur.BlurAmount", "Sat.Saturation", "Expo.Exposure"]);

    CompositionEasingFunction Easing(Ease e)
    {
        if (_eases.TryGetValue(e, out var f)) return f;
        f = e == Ease.Linear
            ? _c.CreateLinearEasingFunction()
            : _c.CreateCubicBezierEasingFunction(new Vector2((float)e.X1, (float)e.Y1), new Vector2((float)e.X2, (float)e.Y2));
        _eases[e] = f;
        return f;
    }

    /// <summary>
    /// Plays the plan on top of <paramref name="host"/> (an element covering the slide at logical size).
    /// Completes when the animation ends or <paramref name="skip"/> fires. The overlay is removed afterwards.
    /// </summary>
    public async Task PlayAsync(TransitionPlan plan, IActorSources sources, UIElement host, CancellationToken skip)
    {
        var W = (float)plan.Width;
        var H = (float)plan.Height;
        var duration = TimeSpan.FromMilliseconds(Math.Max(1, plan.DurationMs));
        var root = _c.CreateContainerVisual();
        root.Size = new Vector2(W, H);
        // Glows and light bands are larger than the slide; keep every effect inside the slide.
        root.Clip = _c.CreateInsetClip();
        var center = new Vector3(W / 2, H / 2, 0);
        var perspective = Matrix4x4.Identity;
        perspective.M34 = -1f / (float)plan.Perspective;
        root.TransformMatrix = Matrix4x4.CreateTranslation(-center) * perspective * Matrix4x4.CreateTranslation(center);

        var backdrop = _c.CreateSpriteVisual();
        backdrop.Size = root.Size;
        backdrop.Brush = _c.CreateColorBrush(ColorUtil.Parse(plan.Backdrop));
        root.Children.InsertAtTop(backdrop);

        var starts = new List<Action>();
        foreach (var actor in plan.Actors)
        {
            try
            {
                var sprite = BuildActor(actor, plan, sources, duration, starts);
                if (sprite != null) root.Children.InsertAtTop(sprite);
            }
            catch (Exception ex)
            {
                Log.Error($"Transition '{plan.Effect}' actor {actor.Source}", ex);
            }
        }

        ElementCompositionPreview.SetElementChildVisual(host, root);
        var done = new TaskCompletionSource();
        var batch = _c.CreateScopedBatch(CompositionBatchTypes.Animation);
        foreach (var start in starts)
        {
            try
            {
                start();
            }
            catch (Exception ex)
            {
                Log.Error($"Transition '{plan.Effect}' start", ex);
            }
        }
        batch.End();
        batch.Completed += (_, _) => done.TrySetResult();
        using (skip.Register(() => done.TrySetResult()))
        {
            // Never hang on a lost completion: give the batch a generous deadline.
            await Task.WhenAny(done.Task, Task.Delay(duration + TimeSpan.FromSeconds(2)));
        }
        ElementCompositionPreview.SetElementChildVisual(host, null);
        root.Children.RemoveAll();
        root.Dispose();
    }

    SpriteVisual? BuildActor(Actor a, TransitionPlan plan, IActorSources sources, TimeSpan duration, List<Action> starts)
    {
        var b = a.Bounds;
        var sprite = _c.CreateSpriteVisual();
        sprite.Size = new Vector2((float)b.Width, (float)b.Height);
        sprite.Offset = new Vector3((float)b.X, (float)b.Y, 0);
        sprite.CenterPoint = new Vector3((float)(b.Width * a.PivotX), (float)(b.Height * a.PivotY), 0);

        CompositionBrush? brush = a.Source switch
        {
            ActorSource.OutgoingSlide or ActorSource.IncomingSlide =>
                Surface(sources.Slide(a.Source == ActorSource.IncomingSlide), a.SourceRect is { } r ? new Vector2((float)r.X, (float)r.Y) : Vector2.Zero, sprite.Size),
            ActorSource.OutgoingBackground or ActorSource.IncomingBackground =>
                Surface(sources.Background(a.Source == ActorSource.IncomingBackground), Vector2.Zero, sprite.Size),
            ActorSource.OutgoingElement or ActorSource.IncomingElement when a.ElementId != null =>
                Surface(sources.Element(a.Source == ActorSource.IncomingElement, a.ElementId), Vector2.Zero, sprite.Size),
            ActorSource.Color => _c.CreateColorBrush(ColorUtil.Parse(a.Color)),
            ActorSource.LightBand => LightBand(ColorUtil.Parse(a.Color)),
            ActorSource.Glow => Glow(ColorUtil.Parse(a.Color)),
            _ => null,
        };
        if (brush == null) return null;

        CompositionEffectBrush? effect = null;
        if (a.HasEffects)
        {
            effect = Effects.CreateBrush();
            effect.SetSourceParameter("src", brush);
            brush = effect;
        }

        if (a.Mask is { Kind: not MaskKind.None } mask && a.Get(Prop.MaskProgress) is { } progress)
        {
            var mb = _c.CreateMaskBrush();
            mb.Source = brush;
            mb.Mask = MaskBrush(mask, progress, sprite.Size, duration, starts);
            brush = mb;
        }
        sprite.Brush = brush;

        // Rotation: one axis per actor. 3D axes rotate in the parent's perspective.
        var rotTrack = a.Get(Prop.RotateY) ?? a.Get(Prop.RotateX) ?? a.Get(Prop.RotateZ);
        sprite.RotationAxis = rotTrack?.Prop switch
        {
            Prop.RotateY => new Vector3(0, 1, 0),
            Prop.RotateX => new Vector3(1, 0, 0),
            _ => new Vector3(0, 0, 1),
        };
        sprite.RotationAngleInDegrees = (float)(rotTrack?.ValueAt(0) ?? a.BaseRotation);

        DropShadow? shadow = null;
        if (a.Get(Prop.ShadowOpacity) is { } sh)
        {
            shadow = _c.CreateDropShadow();
            shadow.BlurRadius = 56;
            shadow.Offset = new Vector3(0, 22, 0);
            shadow.Color = Microsoft.UI.Colors.Black;
            shadow.Opacity = (float)sh.ValueAt(0);
            sprite.Shadow = shadow;
        }

        var hasScaleXY = a.Get(Prop.ScaleX) != null || a.Get(Prop.ScaleY) != null;
        foreach (var t in a.Tracks)
        {
            switch (t.Prop)
            {
                case Prop.Opacity:
                    sprite.Opacity = Clamp(t.ValueAt(0), 0, 1);
                    Start(sprite, "Opacity", Scalar(t, duration, v => Clamp(v, 0, 1)), starts);
                    break;
                case Prop.TranslateX:
                    sprite.Offset = sprite.Offset with { X = (float)(b.X + t.ValueAt(0)) };
                    Start(sprite, "Offset.X", Scalar(t, duration, v => (float)(b.X + v)), starts);
                    break;
                case Prop.TranslateY:
                    sprite.Offset = sprite.Offset with { Y = (float)(b.Y + t.ValueAt(0)) };
                    Start(sprite, "Offset.Y", Scalar(t, duration, v => (float)(b.Y + v)), starts);
                    break;
                case Prop.Scale when !hasScaleXY:
                {
                    var v0 = (float)t.ValueAt(0);
                    sprite.Scale = new Vector3(v0, v0, 1);
                    var anim = Scalar(t, duration, v => (float)v);
                    Start(sprite, "Scale.X", anim, starts);
                    Start(sprite, "Scale.Y", anim, starts);
                    break;
                }
                case Prop.ScaleX:
                    sprite.Scale = sprite.Scale with { X = (float)t.ValueAt(0) };
                    Start(sprite, "Scale.X", Scalar(t, duration, v => (float)v), starts);
                    break;
                case Prop.ScaleY:
                    sprite.Scale = sprite.Scale with { Y = (float)t.ValueAt(0) };
                    Start(sprite, "Scale.Y", Scalar(t, duration, v => (float)v), starts);
                    break;
                case Prop.RotateX or Prop.RotateY or Prop.RotateZ when t == rotTrack:
                    Start(sprite, "RotationAngleInDegrees", Scalar(t, duration, v => (float)v), starts);
                    break;
                case Prop.Blur when effect != null:
                    effect.Properties.InsertScalar("Blur.BlurAmount", Clamp(t.ValueAt(0), 0, 250));
                    Start(effect, "Blur.BlurAmount", Scalar(t, duration, v => Clamp(v, 0, 250)), starts);
                    break;
                case Prop.Saturation when effect != null:
                    Start(effect, "Sat.Saturation", Scalar(t, duration, v => Clamp(v, 0, 2)), starts);
                    break;
                case Prop.Exposure when effect != null:
                    Start(effect, "Expo.Exposure", Scalar(t, duration, v => Clamp(v, -2, 2)), starts);
                    break;
                case Prop.ShadowOpacity when shadow != null:
                    Start(shadow, "Opacity", Scalar(t, duration, v => Clamp(v, 0, 1)), starts);
                    break;
            }
        }
        return sprite;
    }

    static float Clamp(double v, double min, double max) => (float)Math.Clamp(v, min, max);

    static void Start(CompositionObject target, string property, CompositionAnimation anim, List<Action> starts) =>
        starts.Add(() => target.StartAnimation(property, anim));

    ScalarKeyFrameAnimation Scalar(Track t, TimeSpan duration, Func<double, float> map)
    {
        var a = _c.CreateScalarKeyFrameAnimation();
        foreach (var k in t.Keys) a.InsertKeyFrame((float)k.Time, map(k.Value), Easing(k.Ease));
        a.Duration = duration;
        return a;
    }

    CompositionSurfaceBrush? Surface(Visual? source, Vector2 offset, Vector2 size)
    {
        if (source == null) return null;
        var surface = _c.CreateVisualSurface();
        surface.SourceVisual = source;
        surface.SourceOffset = offset;
        surface.SourceSize = size;
        var brush = _c.CreateSurfaceBrush(surface);
        brush.Stretch = CompositionStretch.Fill;
        return brush;
    }

    CompositionBrush LightBand(Color color)
    {
        var g = _c.CreateLinearGradientBrush();
        g.StartPoint = new Vector2(0, .5f);
        g.EndPoint = new Vector2(1, .5f);
        var clear = ColorUtil.WithAlpha(color, 0);
        g.ColorStops.Add(_c.CreateColorGradientStop(0, clear));
        g.ColorStops.Add(_c.CreateColorGradientStop(.5f, color));
        g.ColorStops.Add(_c.CreateColorGradientStop(1, clear));
        return g;
    }

    CompositionBrush Glow(Color color)
    {
        var g = _c.CreateRadialGradientBrush();
        g.EllipseCenter = new Vector2(.5f, .5f);
        g.EllipseRadius = new Vector2(.5f, .5f);
        g.ColorStops.Add(_c.CreateColorGradientStop(0, color));
        g.ColorStops.Add(_c.CreateColorGradientStop(.55f, ColorUtil.WithAlpha(color, 230)));
        g.ColorStops.Add(_c.CreateColorGradientStop(1, ColorUtil.WithAlpha(color, 0)));
        return g;
    }

    /// <summary>A feathered alpha mask whose edge is driven by the MaskProgress track.</summary>
    CompositionBrush MaskBrush(Visary.Core.Transitions.Mask mask, Track progress, Vector2 size, TimeSpan duration, List<Action> starts)
    {
        var opaque = Microsoft.UI.Colors.White;
        var clear = ColorUtil.WithAlpha(opaque, 0);
        if (mask.Kind == MaskKind.Linear)
        {
            var g = _c.CreateLinearGradientBrush();
            g.MappingMode = CompositionMappingMode.Absolute;
            g.ColorStops.Add(_c.CreateColorGradientStop(0, opaque));
            g.ColorStops.Add(_c.CreateColorGradientStop(1, clear));
            var angle = mask.Angle * Math.PI / 180;
            var d = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            double[] proj = [0, size.X * d.X, size.Y * d.Y, size.X * d.X + size.Y * d.Y];
            double m0 = proj.Min(), m1 = proj.Max(), f = Math.Max(1, mask.Feather);
            (Vector2 start, Vector2 end) Edge(double p)
            {
                var e = m0 + p * (m1 - m0 + f);
                return (d * (float)(e - f), d * (float)e);
            }
            var (s0, e0) = Edge(progress.ValueAt(0));
            g.StartPoint = s0;
            g.EndPoint = e0;
            var sa = _c.CreateVector2KeyFrameAnimation();
            var ea = _c.CreateVector2KeyFrameAnimation();
            foreach (var k in progress.Keys)
            {
                var (s, e) = Edge(k.Value);
                sa.InsertKeyFrame((float)k.Time, s, Easing(k.Ease));
                ea.InsertKeyFrame((float)k.Time, e, Easing(k.Ease));
            }
            sa.Duration = ea.Duration = duration;
            Start(g, "StartPoint", sa, starts);
            Start(g, "EndPoint", ea, starts);
            return g;
        }
        else
        {
            var g = _c.CreateRadialGradientBrush();
            g.MappingMode = CompositionMappingMode.Absolute;
            g.EllipseCenter = new Vector2((float)mask.CenterX, (float)mask.CenterY);
            var inside = mask.Invert ? clear : opaque;
            var outside = mask.Invert ? opaque : clear;
            g.ColorStops.Add(_c.CreateColorGradientStop(0, inside));
            g.ColorStops.Add(_c.CreateColorGradientStop(.75f, inside));
            g.ColorStops.Add(_c.CreateColorGradientStop(1, outside));
            double cx = mask.CenterX, cy = mask.CenterY;
            var far = new[] { (0d, 0d), (size.X, 0d), (0d, size.Y), (size.X, size.Y) }.Max(p => Math.Sqrt(Math.Pow(p.Item1 - cx, 2) + Math.Pow(p.Item2 - cy, 2)));
            Vector2 Radius(double p)
            {
                var r = (float)Math.Max(.5, p * far / .75);
                return new Vector2(r, r);
            }
            g.EllipseRadius = Radius(progress.ValueAt(0));
            var ra = _c.CreateVector2KeyFrameAnimation();
            foreach (var k in progress.Keys) ra.InsertKeyFrame((float)k.Time, Radius(k.Value), Easing(k.Ease));
            ra.Duration = duration;
            Start(g, "EllipseRadius", ra, starts);
            return g;
        }
    }
}
