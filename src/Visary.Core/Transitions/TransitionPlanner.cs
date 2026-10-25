using Visary.Core.Model;

namespace Visary.Core.Transitions;

/// <summary>
/// Turns transition settings plus the two slides into a <see cref="TransitionPlan"/>.
/// The plan is pure data, so every transition is testable without a GPU.
/// </summary>
public static class TransitionPlanner
{
    public static TransitionPlan Build(TransitionSettings settings, Scene? from, Scene to, double width, double height, string accent = "#C43E1C")
    {
        var info = TransitionCatalog.Get(settings.Effect);
        var variant = string.IsNullOrEmpty(settings.Variant) || info.Variants.All(v => v.Id != settings.Variant) ? info.DefaultVariant : settings.Variant!;
        var plan = new TransitionPlan
        {
            Effect = info.Id,
            DurationMs = info.Id == "none" ? 1 : Math.Clamp(settings.DurationMs, 100, 10000),
            Width = width,
            Height = height,
            Backdrop = "#000000",
        };
        var c = new Ctx(plan, settings, variant, from, to, accent);
        switch (info.Id)
        {
            case "none": c.In(); break;
            case "fade": Fade(c); break;
            case "silk": Silk(c); break;
            case "drift": Drift(c); break;
            case "glide": Glide(c); break;
            case "velocity": Velocity(c); break;
            case "stack": Stack(c); break;
            case "ribbon": Ribbon(c); break;
            case "panels": Panels(c); break;
            case "float": Float(c); break;
            case "depth": Depth(c); break;
            case "portal": Portal(c); break;
            case "orbit": Orbit(c); break;
            case "dive": Dive(c); break;
            case "parallax": Parallax(c); break;
            case "focus": Focus(c); break;
            case "frost": Frost(c); break;
            case "sweep": Sweep(c); break;
            case "aurora": Aurora(c); break;
            case "bloom": Bloom(c); break;
            case "spotlight": Spotlight(c); break;
            case "cinematic": Cinematic(c); break;
            case "feather": Feather(c); break;
            case "shutter": Shutter(c); break;
            case "mosaic": Mosaic(c); break;
            case "cascade": Cascade(c); break;
            case "morph": Morph(c); break;
            default: Fade(c); break;
        }
        return plan;
    }

    /* ------------------------------------------------------------ */

    sealed class Ctx(TransitionPlan plan, TransitionSettings s, string variant, Scene? from, Scene to, string accent)
    {
        public TransitionPlan Plan { get; } = plan;
        public string Variant { get; } = variant;
        public Scene? From { get; } = from;
        public Scene To { get; } = to;
        public string Accent { get; } = accent;
        public double W => Plan.Width;
        public double H => Plan.Height;
        public double Diagonal => Math.Sqrt(W * W + H * H);
        /// <summary>Intensity multiplier for distances, blur and scale changes.</summary>
        public double K { get; } = s.Intensity switch { TransitionIntensity.Subtle => .6, TransitionIntensity.Bold => 1.5, _ => 1 };
        public bool Spring { get; } = s.Ease == TransitionEase.Spring;
        /// <summary>The ease the user picked, used for the main motion.</summary>
        public Ease E { get; } = s.Ease switch
        {
            TransitionEase.Fluid => Ease.Fluid,
            TransitionEase.Snappy => Ease.Snappy,
            TransitionEase.Linear => Ease.Linear,
            TransitionEase.Spring => Ease.Fluid,
            _ => Ease.Smooth,
        };
        public bool HasFrom => From != null;

        public RectD Full => new(0, 0, W, H);

        public Actor Add(Actor a)
        {
            Plan.Actors.Add(a);
            return a;
        }

        public Actor Out(RectD? source = null, Mask? mask = null) =>
            Add(new Actor { Source = ActorSource.OutgoingSlide, Bounds = source ?? Full, SourceRect = source, Mask = mask });

        public Actor In(RectD? source = null, Mask? mask = null) =>
            Add(new Actor { Source = ActorSource.IncomingSlide, Bounds = source ?? Full, SourceRect = source, Mask = mask });

        /// <summary>Main motion: honours the Spring ease by sampling a damped spring.</summary>
        public Actor Motion(Actor a, Prop p, double from, double to, double t0 = 0, double t1 = 1)
        {
            if (!Spring) return a.Tween(p, from, to, t0, t1, E);
            return a.Replace(p, SpringKeys(from, to, t0, t1));
        }
    }

    /// <summary>A lightly damped spring (one soft overshoot), sampled into linear keys.</summary>
    internal static IEnumerable<Key> SpringKeys(double from, double to, double t0, double t1)
    {
        const int n = 24;
        yield return new Key(0, from, Ease.Linear);
        if (t0 > 0) yield return new Key(t0, from, Ease.Linear);
        for (var i = 1; i <= n; i++)
        {
            var x = (double)i / n;
            var zeta = .62;
            var omega = 9.0;
            var wd = omega * Math.Sqrt(1 - zeta * zeta);
            var tt = x;
            var env = Math.Exp(-zeta * omega * tt);
            var y = 1 - env * (Math.Cos(wd * tt) + zeta * omega / wd * Math.Sin(wd * tt));
            if (i == n) y = 1;
            yield return new Key(t0 + (t1 - t0) * x, from + (to - from) * y, Ease.Linear);
        }
        if (t1 < 1) yield return new Key(1, to, Ease.Linear);
    }

    static (double x, double y) Direction(string side) => side switch
    {
        "left" => (-1, 0),
        "top" => (0, -1),
        "bottom" => (0, 1),
        _ => (1, 0),
    };

    /* ---------------- Essentials ---------------- */

    static void Fade(Ctx c)
    {
        if (c.HasFrom) c.Out();
        c.In().Tween(Prop.Opacity, 0, 1, 0, 1, c.E);
    }

    // The outgoing slide never fades itself: the incoming one fades in on top, so there is no dip to black.
    static void Silk(Ctx c)
    {
        if (c.HasFrom)
            c.Out()
                .Tween(Prop.Blur, 0, 9 * c.K, 0, .8, Ease.Smooth)
                .Tween(Prop.Scale, 1, 1 + .02 * c.K, 0, 1, Ease.Smooth);
        c.In()
            .Tween(Prop.Opacity, 0, 1, .08, .92, Ease.Smooth)
            .Tween(Prop.Blur, 11 * c.K, 0, .05, 1, c.E)
            .Tween(Prop.Scale, 1 + .035 * c.K, 1, 0, 1, c.E);
    }

    static void Drift(Ctx c)
    {
        if (c.HasFrom) c.Out().Tween(Prop.Scale, 1, 1 - .02 * c.K, 0, 1, Ease.Smooth).Tween(Prop.Exposure, 0, -.3 * c.K, 0, 1, Ease.Smooth);
        var dx = c.Variant switch { "right" => 36, "left" => -36, _ => 0 } * c.K;
        var a = c.In()
            .Tween(Prop.Opacity, 0, 1, 0, .7, Ease.Smooth)
            .Tween(Prop.Scale, 1 + .08 * c.K, 1, 0, 1, Ease.Fluid);
        if (dx != 0) a.Tween(Prop.TranslateX, dx, 0, 0, 1, Ease.Fluid);
    }

    /* ---------------- Motion ---------------- */

    static void Glide(Ctx c)
    {
        var (vx, vy) = Direction(c.Variant);
        var dist = vx != 0 ? c.W : c.H;
        if (c.HasFrom)
        {
            var o = c.Out()
                .Tween(Prop.Exposure, 0, -.8 * c.K, 0, 1, c.E)
                .Tween(Prop.Scale, 1, 1 - .04 * c.K, 0, 1, c.E);
            c.Motion(o, Prop.TranslateX, 0, -vx * dist * .3 * c.K);
            c.Motion(o, Prop.TranslateY, 0, -vy * dist * .3 * c.K);
        }
        var i = c.In().Tween(Prop.ShadowOpacity, .55, .15, 0, 1, c.E);
        c.Motion(i, Prop.TranslateX, vx * dist, 0);
        c.Motion(i, Prop.TranslateY, vy * dist, 0);
    }

    static void Velocity(Ctx c)
    {
        var (vx, vy) = Direction(c.Variant);
        var dist = vx != 0 ? c.W : c.H;
        var blur = 18 * c.K;
        Key[] BlurKeys() => [new(0, 0, Ease.Linear), new(.5, blur, Ease.Smooth), new(1, 0, Ease.Smooth)];
        if (c.HasFrom)
            c.Out()
                .Tween(Prop.TranslateX, 0, -vx * dist, 0, 1, Ease.Smooth)
                .Tween(Prop.TranslateY, 0, -vy * dist, 0, 1, Ease.Smooth)
                .Replace(Prop.Blur, BlurKeys());
        c.In()
            .Tween(Prop.TranslateX, vx * dist, 0, 0, 1, Ease.Smooth)
            .Tween(Prop.TranslateY, vy * dist, 0, 0, 1, Ease.Smooth)
            .Replace(Prop.Blur, BlurKeys());
    }

    static void Stack(Ctx c)
    {
        var (vx, vy) = Direction(c.Variant);
        var dist = vx != 0 ? c.W : c.H;
        if (c.HasFrom)
            c.Out()
                .Tween(Prop.Scale, 1, 1 - .07 * c.K, 0, 1, Ease.Smooth)
                .Tween(Prop.Exposure, 0, -.9 * c.K, 0, 1, Ease.Smooth)
                .Tween(Prop.Blur, 0, 4 * c.K, 0, 1, Ease.Smooth);
        var i = c.In().Tween(Prop.ShadowOpacity, .6, .25, 0, 1, Ease.Smooth);
        c.Motion(i, Prop.TranslateX, vx * dist * 1.04, 0);
        c.Motion(i, Prop.TranslateY, vy * dist * 1.04, 0);
    }

    static void Ribbon(Ctx c)
    {
        if (c.HasFrom) c.Out().Tween(Prop.Exposure, 0, -.7 * c.K, 0, 1, Ease.Smooth).Tween(Prop.Scale, 1, 1 - .03 * c.K, 0, 1, Ease.Smooth);
        if (c.Variant == "vertical")
        {
            var left = c.In(new RectD(0, 0, c.W / 2, c.H)).Tween(Prop.ShadowOpacity, .45, 0, 0, 1, Ease.Smooth);
            var right = c.In(new RectD(c.W / 2, 0, c.W / 2, c.H)).Tween(Prop.ShadowOpacity, .45, 0, 0, 1, Ease.Smooth);
            c.Motion(left, Prop.TranslateY, -c.H, 0, 0, .8);
            c.Motion(right, Prop.TranslateY, c.H, 0, .2, 1);
            return;
        }
        var top = c.In(new RectD(0, 0, c.W, c.H / 2)).Tween(Prop.ShadowOpacity, .45, 0, 0, 1, Ease.Smooth);
        var bottom = c.In(new RectD(0, c.H / 2, c.W, c.H / 2)).Tween(Prop.ShadowOpacity, .45, 0, 0, 1, Ease.Smooth);
        c.Motion(top, Prop.TranslateX, c.W, 0, 0, .8);
        c.Motion(bottom, Prop.TranslateX, -c.W, 0, .2, 1);
    }

    static void Panels(Ctx c)
    {
        c.In()
            .Tween(Prop.Scale, 1 + .05 * c.K, 1, 0, 1, c.E)
            .Tween(Prop.Exposure, -.7 * c.K, 0, 0, 1, c.E);
        if (!c.HasFrom) return;
        const int n = 4;
        var pw = c.W / n;
        for (var i = 0; i < n; i++)
        {
            var dir = c.Variant switch { "up" => -1, "down" => 1, _ => i % 2 == 0 ? -1 : 1 };
            var t0 = i * .09;
            c.Out(new RectD(i * pw, 0, pw, c.H))
                .Tween(Prop.TranslateY, 0, dir * c.H * 1.08, t0, t0 + .62, Ease.Exit)
                .Tween(Prop.ShadowOpacity, 0, .5, t0, t0 + .2, Ease.Smooth);
        }
    }

    static void Float(Ctx c)
    {
        c.In()
            .Tween(Prop.Scale, 1 - .03 * c.K, 1, 0, 1, c.E)
            .Tween(Prop.Exposure, -.6 * c.K, 0, 0, 1, c.E);
        if (!c.HasFrom) return;
        var o = c.Out()
            .Tween(Prop.TranslateY, 0, -c.H * .09 * c.K, 0, .8, Ease.Smooth)
            .Tween(Prop.Scale, 1, 1 + .025 * c.K, 0, .8, Ease.Smooth)
            .Tween(Prop.RotateX, 0, 9 * c.K, 0, .8, Ease.Smooth)
            .Tween(Prop.ShadowOpacity, 0, .55, 0, .35, Ease.Smooth)
            .Tween(Prop.Opacity, 1, 0, .3, .82, Ease.Smooth);
        _ = o;
    }

    /* ---------------- Depth ---------------- */

    static void Depth(Ctx c)
    {
        if (c.HasFrom)
            c.Out()
                .Tween(Prop.Scale, 1, 1 - .18 * c.K, 0, .85, Ease.Smooth)
                .Tween(Prop.Exposure, 0, -1.2 * c.K, 0, .85, Ease.Smooth)
                .Tween(Prop.Blur, 0, 7 * c.K, 0, .85, Ease.Smooth);
        c.In()
            .Tween(Prop.Opacity, 0, 1, .22, .78, Ease.Smooth)
            .Tween(Prop.Scale, 1 + .12 * c.K, 1, .12, 1, c.E)
            .Tween(Prop.Blur, 10 * c.K, 0, .12, .9, c.E);
    }

    static void Portal(Ctx c)
    {
        // The new slide waits underneath while the old one rushes past the camera.
        c.In()
            .Tween(Prop.Scale, 1 - .1 * c.K, 1, .25, 1, Ease.Fluid)
            .Tween(Prop.Blur, 14 * c.K, 0, .25, .95, Ease.Fluid);
        if (c.HasFrom)
            c.Out()
                .Tween(Prop.Scale, 1, 1 + .9 * c.K, 0, .65, Ease.Accelerate)
                .Tween(Prop.Blur, 0, 24 * c.K, 0, .65, Ease.Accelerate)
                .Tween(Prop.Opacity, 1, 0, .18, .62, Ease.Smooth);
    }

    /// <summary>Zooms into a focal object of the old slide and emerges from the same spot into the new one.</summary>
    static void Dive(Ctx c)
    {
        var focus = c.HasFrom
            ? MatchForMorph(c.From!, c.To).Select(p => p.from).FirstOrDefault()
              ?? c.From!.Elements.OrderByDescending(e => e.Bounds.Width * e.Bounds.Height).FirstOrDefault(e => e.Bounds.Width * e.Bounds.Height < c.W * c.H * .6)
            : null;
        var fx = focus?.Bounds ?? new RectD(c.W * .4, c.H * .4, c.W * .2, c.H * .2);
        double px = Math.Clamp(fx.CenterX / c.W, 0, 1), py = Math.Clamp(fx.CenterY / c.H, 0, 1);
        var fill = Math.Clamp(Math.Min(c.W / Math.Max(1, fx.Width), c.H / Math.Max(1, fx.Height)), 1.6, 7) * Math.Min(1.2, c.K);
        if (c.HasFrom)
            c.Add(new Actor { Source = ActorSource.OutgoingSlide, Bounds = c.Full, PivotX = px, PivotY = py })
                .Tween(Prop.Scale, 1, fill, 0, .7, Ease.Accelerate)
                .Tween(Prop.Blur, 0, 14 * c.K, .2, .7, Ease.Accelerate)
                .Tween(Prop.Opacity, 1, 0, .35, .7, Ease.Smooth);
        var start = Math.Clamp(Math.Max(fx.Width / c.W, fx.Height / c.H), .12, .6);
        var a = c.Add(new Actor { Source = ActorSource.IncomingSlide, Bounds = c.Full, PivotX = px, PivotY = py })
            .Tween(Prop.Opacity, 0, 1, .32, .62, Ease.Smooth)
            .Tween(Prop.Blur, 10 * c.K, 0, .32, .95, Ease.Fluid);
        // Insert the incoming slide under the outgoing one so the dive happens in front of it.
        c.Plan.Actors.Remove(a);
        c.Plan.Actors.Insert(0, a);
        c.Motion(a, Prop.Scale, start, 1, .3, 1);
    }

    /// <summary>A push where backgrounds move slower than objects, and objects move at staggered depths.</summary>
    static void Parallax(Ctx c)
    {
        var (vx, vy) = Direction(c.Variant);
        var dist = vx != 0 ? c.W : c.H;
        if (c.HasFrom)
        {
            c.Add(new Actor { Source = ActorSource.OutgoingBackground, Bounds = c.Full })
                .Tween(Prop.TranslateX, 0, -vx * dist * .35, 0, 1, c.E)
                .Tween(Prop.TranslateY, 0, -vy * dist * .35, 0, 1, c.E)
                .Tween(Prop.Exposure, 0, -.4, 0, 1, c.E);
        }
        c.Add(new Actor { Source = ActorSource.IncomingBackground, Bounds = c.Full })
            .Tween(Prop.TranslateX, vx * dist * .35, 0, 0, 1, c.E)
            .Tween(Prop.TranslateY, vy * dist * .35, 0, 0, 1, c.E)
            .Tween(Prop.Opacity, 0, 1, 0, .35, Ease.Smooth);
        if (c.HasFrom)
        {
            var i = 0;
            foreach (var e in c.From!.Elements)
            {
                var depth = 1.05 + .25 * (i++ % 4) * c.K;
                c.Add(ElementActor(ActorSource.OutgoingElement, e))
                    .Tween(Prop.TranslateX, 0, -vx * dist * depth, 0, 1, c.E)
                    .Tween(Prop.TranslateY, 0, -vy * dist * depth, 0, 1, c.E)
                    .Tween(Prop.Opacity, e.Opacity, e.Opacity, 0, 1, Ease.Linear);
            }
        }
        var j = 0;
        foreach (var e in c.To.Elements)
        {
            var depth = 1.05 + .25 * (j++ % 4) * c.K;
            // Start fully off the slide whatever the object's position.
            var offX = vx > 0 ? c.W - e.Bounds.X : vx < 0 ? -e.Bounds.Right : 0;
            var offY = vy > 0 ? c.H - e.Bounds.Y : vy < 0 ? -e.Bounds.Bottom : 0;
            var a = c.Add(ElementActor(ActorSource.IncomingElement, e))
                .Tween(Prop.Opacity, e.Opacity, e.Opacity, 0, 1, Ease.Linear);
            c.Motion(a, Prop.TranslateX, vx * Math.Max(Math.Abs(offX), dist * depth), 0);
            c.Motion(a, Prop.TranslateY, vy * Math.Max(Math.Abs(offY), dist * depth), 0);
        }
    }

    static void Orbit(Ctx c)
    {
        var s = c.Variant == "left" ? -1 : 1;
        var angle = 38 * c.K;
        if (c.HasFrom)
            c.Out()
                .Tween(Prop.RotateY, 0, -s * angle, 0, .85, c.E)
                .Tween(Prop.TranslateX, 0, -s * c.W * .42 * c.K, 0, .85, c.E)
                .Tween(Prop.Exposure, 0, -.8, 0, .85, c.E)
                .Tween(Prop.Opacity, 1, 0, .35, .85, Ease.Smooth);
        c.In()
            .Tween(Prop.RotateY, s * angle, 0, .15, 1, c.E)
            .Tween(Prop.TranslateX, s * c.W * .42 * c.K, 0, .15, 1, c.E)
            .Tween(Prop.Opacity, 0, 1, .15, .6, Ease.Smooth)
            .Tween(Prop.ShadowOpacity, .5, .1, .15, 1, Ease.Smooth);
    }

    static void Focus(Ctx c)
    {
        if (c.HasFrom)
            c.Out()
                .Tween(Prop.Blur, 0, 28 * c.K, 0, .5, Ease.Gentle)
                .Tween(Prop.Exposure, 0, -.25, 0, .5, Ease.Gentle);
        c.In()
            .Tween(Prop.Opacity, 0, 1, .36, .62, Ease.Smooth)
            .Tween(Prop.Blur, 28 * c.K, 0, .42, 1, c.E)
            .Tween(Prop.Scale, 1.015, 1, .42, 1, c.E);
    }

    static void Frost(Ctx c)
    {
        var blur = 22 * c.K;
        if (c.HasFrom)
            c.Out()
                .Tween(Prop.Blur, 0, blur, 0, .45, Ease.Gentle)
                .Tween(Prop.Saturation, 1, .55, 0, .45, Ease.Gentle);
        c.In()
            .Tween(Prop.Opacity, 0, 1, .4, .55, Ease.Smooth)
            .Tween(Prop.Blur, blur, 0, .45, 1, c.E)
            .Tween(Prop.Saturation, .55, 1, .45, 1, c.E);
        c.Add(new Actor { Source = ActorSource.Color, Color = "#FFFFFF", Bounds = c.Full })
            .Replace(Prop.Opacity, [new(0, 0, Ease.Linear), new(.45, .42 * Math.Min(1, c.K), Ease.Gentle), new(.55, .42 * Math.Min(1, c.K), Ease.Linear), new(1, 0, c.E)]);
    }

    /* ---------------- Light ---------------- */

    /// <summary>Projection range of the slide on a direction, used to drive linear masks and light bands.</summary>
    static (double min, double max) Project(double w, double h, double angleDeg)
    {
        var a = angleDeg * Math.PI / 180;
        double cx = Math.Cos(a), cy = Math.Sin(a);
        double[] p = [0, w * cx, h * cy, w * cx + h * cy];
        return (p.Min(), p.Max());
    }

    static void Sweep(Ctx c)
    {
        var angle = c.Variant switch { "right" => 0d, "up-right" => -30d, "left" => 180d, _ => 30d };
        var feather = c.W * .32;
        if (c.HasFrom) c.Out().Tween(Prop.Exposure, 0, -.35, 0, 1, Ease.Smooth);
        c.In(mask: new Mask { Kind = MaskKind.Linear, Angle = angle, Feather = feather })
            .Tween(Prop.MaskProgress, 0, 1, 0, 1, c.E);

        // A band of light rides along the feathered edge.
        var (m0, m1) = Project(c.W, c.H, angle);
        var a = angle * Math.PI / 180;
        double dx = Math.Cos(a), dy = Math.Sin(a);
        var centerProj = c.W / 2 * dx + c.H / 2 * dy;
        double EdgeMid(double p) => m0 + p * (m1 - m0 + feather) - feather / 2;
        var bandW = c.W * .3;
        var bandH = c.Diagonal * 1.3;
        var startOff = EdgeMid(0) - centerProj;
        var endOff = EdgeMid(1) - centerProj;
        c.Add(new Actor
        {
            Source = ActorSource.LightBand,
            Color = "#FFF8EE",
            Bounds = new RectD(c.W / 2 - bandW / 2, c.H / 2 - bandH / 2, bandW, bandH),
            BaseRotation = angle,
        })
            .Tween(Prop.TranslateX, dx * startOff, dx * endOff, 0, 1, c.E)
            .Tween(Prop.TranslateY, dy * startOff, dy * endOff, 0, 1, c.E)
            .Replace(Prop.Opacity, [new(0, 0, Ease.Linear), new(.18, .75, Ease.Smooth), new(.75, .65, Ease.Linear), new(1, 0, Ease.Smooth)]);
    }

    static (double x, double y) Origin(Ctx c, string variant) => variant switch
    {
        "top-left" => (0, 0),
        "top-right" => (c.W, 0),
        "bottom-left" => (0, c.H),
        "bottom-right" => (c.W, c.H),
        _ => (c.W / 2, c.H / 2),
    };

    static void Aurora(Ctx c)
    {
        var (ox, oy) = Origin(c, c.Variant);
        if (c.HasFrom) c.Out().Tween(Prop.Exposure, 0, -.5, 0, 1, Ease.Smooth);
        c.In(mask: new Mask { Kind = MaskKind.Radial, CenterX = ox, CenterY = oy })
            .Tween(Prop.MaskProgress, 0, 1, 0, 1, c.E);
        var size = c.Diagonal * 1.25;
        c.Add(new Actor { Source = ActorSource.Glow, Color = c.Accent, Bounds = new RectD(ox - size / 2, oy - size / 2, size, size) })
            .Tween(Prop.Scale, .15, 1.4, 0, 1, Ease.Fluid)
            .Replace(Prop.Opacity, [new(0, 0, Ease.Linear), new(.3, .55 * Math.Min(1.2, c.K), Ease.Smooth), new(1, 0, Ease.Smooth)]);
    }

    static void Bloom(Ctx c)
    {
        if (c.HasFrom) c.Out().Tween(Prop.Scale, 1, 1.03, 0, .55, Ease.Smooth);
        c.In()
            .Tween(Prop.Opacity, 0, 1, .48, .56, Ease.Linear)
            .Tween(Prop.Scale, 1.04, 1, .5, 1, c.E);
        var size = c.Diagonal * 2.4;
        c.Add(new Actor { Source = ActorSource.Glow, Color = c.Accent, Bounds = new RectD(c.W / 2 - size / 2, c.H / 2 - size / 2, size, size) })
            .Replace(Prop.Scale, [new(0, 0, Ease.Linear), new(.52, 1, Ease.Smooth), new(1, 1.25, Ease.Fluid)])
            .Replace(Prop.Opacity, [new(0, 1, Ease.Linear), new(.52, 1, Ease.Linear), new(1, 0, c.E)]);
    }

    static void Spotlight(Ctx c)
    {
        if (c.HasFrom)
            c.Out(mask: new Mask { Kind = MaskKind.Radial, CenterX = c.W / 2, CenterY = c.H / 2 })
                .Tween(Prop.MaskProgress, 1, 0, 0, .48, Ease.Smooth);
        c.In(mask: new Mask { Kind = MaskKind.Radial, CenterX = c.W / 2, CenterY = c.H / 2 })
            .Tween(Prop.MaskProgress, 0, 1, .52, 1, c.E);
    }

    static void Cinematic(Ctx c)
    {
        var blackout = c.Variant == "blackout";
        var cover = blackout ? c.H / 2 : c.H * .13;
        if (c.HasFrom) c.Out().Tween(Prop.Scale, 1, 1.04, 0, .65, Ease.Smooth);
        c.In()
            .Tween(Prop.Opacity, 0, 1, blackout ? .49 : .32, blackout ? .5 : .68, Ease.Smooth)
            .Tween(Prop.Scale, 1.04, 1, .35, 1, c.E);
        Key[] Bar(double offscreen, double covering) =>
        [
            new(0, offscreen, Ease.Linear), new(.35, covering, Ease.Smooth), new(.65, covering, Ease.Linear), new(1, offscreen, Ease.Smooth),
        ];
        c.Add(new Actor { Source = ActorSource.Color, Color = "#000000", Bounds = new RectD(0, 0, c.W, c.H / 2) })
            .Replace(Prop.TranslateY, Bar(-c.H / 2, -c.H / 2 + cover));
        c.Add(new Actor { Source = ActorSource.Color, Color = "#000000", Bounds = new RectD(0, c.H / 2, c.W, c.H / 2) })
            .Replace(Prop.TranslateY, Bar(c.H / 2, c.H / 2 - cover));
    }

    /* ---------------- Reveal ---------------- */

    static void Feather(Ctx c)
    {
        var angle = c.Variant switch { "left" => 180d, "down" => 90d, "up" => -90d, _ => 0d };
        if (c.HasFrom) c.Out().Tween(Prop.Exposure, 0, -.25, 0, 1, Ease.Smooth);
        c.In(mask: new Mask { Kind = MaskKind.Linear, Angle = angle, Feather = (angle is 0 or 180 ? c.W : c.H) * .5 })
            .Tween(Prop.MaskProgress, 0, 1, 0, 1, c.E);
    }

    static void Shutter(Ctx c)
    {
        const int n = 7;
        var vertical = c.Variant == "vertical";
        if (c.HasFrom) c.Out().Tween(Prop.Exposure, 0, -.45, 0, 1, Ease.Smooth);
        for (var i = 0; i < n; i++)
        {
            var rect = vertical ? new RectD(i * c.W / n, 0, c.W / n, c.H) : new RectD(0, i * c.H / n, c.W, c.H / n);
            var angle = vertical ? (i % 2 == 0 ? 90d : -90d) : (i % 2 == 0 ? 0d : 180d);
            var length = vertical ? c.H : c.W;
            var t0 = i * .065;
            c.In(rect, new Mask { Kind = MaskKind.Linear, Angle = angle, Feather = length * .4 })
                .Tween(Prop.MaskProgress, 0, 1, t0, t0 + .58, c.E);
        }
    }

    static void Mosaic(Ctx c)
    {
        const int cols = 10;
        var rows = Math.Max(2, (int)Math.Round(cols * c.H / c.W));
        double tw = c.W / cols, th = c.H / rows;
        var (ox, oy) = c.Variant == "top-left" ? (0d, 0d) : (c.W / 2, c.H / 2);
        var maxDist = c.Variant == "top-left" ? c.Diagonal : c.Diagonal / 2;
        var rand = new Random(7);
        if (c.HasFrom) c.Out().Tween(Prop.Exposure, 0, -.7, 0, 1, Ease.Smooth).Tween(Prop.Scale, 1, .98, 0, 1, Ease.Smooth);
        for (var r = 0; r < rows; r++)
        for (var col = 0; col < cols; col++)
        {
            var rect = new RectD(col * tw, r * th, tw, th);
            var order = c.Variant == "scatter" ? rand.NextDouble() : Math.Sqrt(Math.Pow(rect.CenterX - ox, 2) + Math.Pow(rect.CenterY - oy, 2)) / maxDist;
            var t0 = Math.Clamp(order, 0, 1) * .55;
            c.In(rect)
                .Tween(Prop.Opacity, 0, 1, t0, t0 + .32, Ease.Smooth)
                .Tween(Prop.Scale, .55, 1, t0, t0 + .45, Ease.Fluid);
        }
    }

    /* ---------------- Smart ---------------- */

    static IEnumerable<SceneElement> ReadingOrder(IEnumerable<SceneElement> els) =>
        els.OrderBy(e => Math.Round(e.Bounds.Y / 40)).ThenBy(e => e.Bounds.X);

    static Actor ElementActor(ActorSource src, SceneElement e) =>
        new() { Source = src, ElementId = e.Id, Bounds = e.Bounds, BaseRotation = e.Rotation };

    static void Cascade(Ctx c)
    {
        double dx = 0, dy = 0;
        switch (c.Variant)
        {
            case "fall": dy = 1; break;
            case "sideways": dx = 1; break;
            default: dy = -1; break;
        }
        var outDist = 28 * c.K;
        var inDist = 40 * c.K;
        if (c.HasFrom)
        {
            c.Add(new Actor { Source = ActorSource.OutgoingBackground, Bounds = c.Full });
            var i = 0;
            foreach (var e in ReadingOrder(c.From!.Elements))
            {
                var t0 = Math.Min(.25, i++ * .045);
                c.Add(ElementActor(ActorSource.OutgoingElement, e))
                    .Tween(Prop.Opacity, e.Opacity, 0, t0, t0 + .32, Ease.Smooth)
                    .Tween(Prop.TranslateX, 0, -dx * outDist * 1.5, t0, t0 + .35, Ease.Gentle)
                    .Tween(Prop.TranslateY, 0, dy * outDist, t0, t0 + .35, Ease.Gentle)
                    .Tween(Prop.Blur, 0, 4 * c.K, t0, t0 + .35, Ease.Gentle);
            }
        }
        c.Add(new Actor { Source = ActorSource.IncomingBackground, Bounds = c.Full })
            .Tween(Prop.Opacity, 0, 1, c.HasFrom ? .22 : 0, c.HasFrom ? .65 : .4, Ease.Smooth);
        var j = 0;
        var list = ReadingOrder(c.To.Elements).ToList();
        var step = list.Count > 1 ? Math.Min(.07, .22 / (list.Count - 1)) : 0;
        foreach (var e in list)
        {
            var t0 = .36 + j++ * step;
            var a = c.Add(ElementActor(ActorSource.IncomingElement, e))
                .Tween(Prop.Opacity, 0, e.Opacity, t0, t0 + .32, Ease.Smooth)
                .Tween(Prop.Blur, 8 * c.K, 0, t0, t0 + .4, Ease.Fluid);
            if (c.Spring)
            {
                a.Replace(Prop.TranslateX, SpringKeys(dx * inDist * 1.5, 0, t0, Math.Min(1, t0 + .5)));
                a.Replace(Prop.TranslateY, SpringKeys(-dy * inDist, 0, t0, Math.Min(1, t0 + .5)));
            }
            else
            {
                a.Tween(Prop.TranslateX, dx * inDist * 1.5, 0, t0, Math.Min(1, t0 + .5), Ease.Fluid);
                a.Tween(Prop.TranslateY, -dy * inDist, 0, t0, Math.Min(1, t0 + .5), Ease.Fluid);
            }
        }
    }

    /// <summary>Pairs objects by id first, then by name, and only objects of the same kind.</summary>
    public static List<(SceneElement from, SceneElement to)> MatchForMorph(Scene from, Scene to)
    {
        var pairs = new List<(SceneElement, SceneElement)>();
        var used = new HashSet<string>();
        var byId = from.Elements.ToDictionary(e => e.Id);
        foreach (var t in to.Elements)
        {
            SceneElement? f = byId.GetValueOrDefault(t.Id);
            if (f == null || f.Kind != t.Kind || used.Contains(f.Id))
                f = t.Name is { Length: > 0 } ? from.Elements.FirstOrDefault(x => x.Name == t.Name && x.Kind == t.Kind && !used.Contains(x.Id)) : null;
            if (f == null) continue;
            used.Add(f.Id);
            pairs.Add((f, t));
        }
        return pairs;
    }

    static void Morph(Ctx c)
    {
        if (!c.HasFrom)
        {
            Fade(c);
            return;
        }
        c.Add(new Actor { Source = ActorSource.OutgoingBackground, Bounds = c.Full });
        c.Add(new Actor { Source = ActorSource.IncomingBackground, Bounds = c.Full }).Tween(Prop.Opacity, 0, 1, 0, 1, Ease.Smooth);
        var pairs = MatchForMorph(c.From!, c.To);
        var matchedFrom = pairs.Select(p => p.from.Id).ToHashSet();
        var matchedTo = pairs.Select(p => p.to.Id).ToHashSet();

        foreach (var e in c.From!.Elements.Where(e => !matchedFrom.Contains(e.Id)))
            c.Add(ElementActor(ActorSource.OutgoingElement, e))
                .Tween(Prop.Opacity, e.Opacity, 0, 0, .45, Ease.Smooth)
                .Tween(Prop.Scale, 1, .94, 0, .45, Ease.Smooth);

        foreach (var (f, t) in pairs)
        {
            double dx = f.Bounds.CenterX - t.Bounds.CenterX, dy = f.Bounds.CenterY - t.Bounds.CenterY;
            double sx = f.Bounds.Width / Math.Max(1, t.Bounds.Width), sy = f.Bounds.Height / Math.Max(1, t.Bounds.Height);
            // The old object rides the same path as it fades, so text and colour changes cross dissolve in motion.
            var ghost = c.Add(new Actor { Source = ActorSource.OutgoingElement, ElementId = f.Id, Bounds = f.Bounds, BaseRotation = f.Rotation })
                .Tween(Prop.Opacity, f.Opacity, 0, .1, .7, Ease.Smooth);
            c.Motion(ghost, Prop.TranslateX, 0, -dx);
            c.Motion(ghost, Prop.TranslateY, 0, -dy);
            c.Motion(ghost, Prop.ScaleX, 1, 1 / sx);
            c.Motion(ghost, Prop.ScaleY, 1, 1 / sy);
            c.Motion(ghost, Prop.RotateZ, f.Rotation, t.Rotation);

            var a = c.Add(new Actor { Source = ActorSource.IncomingElement, ElementId = t.Id, Bounds = t.Bounds, BaseRotation = t.Rotation })
                .Tween(Prop.Opacity, 0, t.Opacity, .05, .55, Ease.Smooth);
            c.Motion(a, Prop.TranslateX, dx, 0);
            c.Motion(a, Prop.TranslateY, dy, 0);
            c.Motion(a, Prop.ScaleX, sx, 1);
            c.Motion(a, Prop.ScaleY, sy, 1);
            c.Motion(a, Prop.RotateZ, f.Rotation, t.Rotation);
        }

        foreach (var e in c.To.Elements.Where(e => !matchedTo.Contains(e.Id)))
            c.Add(ElementActor(ActorSource.IncomingElement, e))
                .Tween(Prop.Opacity, 0, e.Opacity, .4, 1, Ease.Smooth)
                .Tween(Prop.Scale, .94, 1, .4, 1, Ease.Fluid);
    }
}
