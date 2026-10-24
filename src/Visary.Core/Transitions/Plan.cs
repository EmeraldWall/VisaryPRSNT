using Visary.Core.Model;

namespace Visary.Core.Transitions;

/// <summary>Cubic bezier timing curve, the same model Windows Composition uses.</summary>
public readonly record struct Ease(double X1, double Y1, double X2, double Y2)
{
    public static readonly Ease Linear = new(0, 0, 1, 1);
    /// <summary>Balanced ease in and out.</summary>
    public static readonly Ease Smooth = new(.65, 0, .35, 1);
    /// <summary>Fast start, long graceful landing (Fluent "decelerate").</summary>
    public static readonly Ease Fluid = new(.1, .9, .2, 1);
    public static readonly Ease Snappy = new(.3, 0, 0, 1);
    public static readonly Ease Accelerate = new(.7, 0, 1, .5);
    public static readonly Ease Gentle = new(.4, 0, .2, 1);
    public static readonly Ease Exit = new(.55, 0, 1, .45);

    /// <summary>Evaluates the curve at progress x, used for tests and for sampling.</summary>
    public double Evaluate(double x)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;
        double lo = 0, hi = 1, t = x;
        for (var i = 0; i < 40; i++)
        {
            t = (lo + hi) / 2;
            var bx = Bez(t, X1, X2);
            if (Math.Abs(bx - x) < 1e-6) break;
            if (bx < x) lo = t; else hi = t;
        }
        return Bez(t, Y1, Y2);
    }

    static double Bez(double t, double a, double b) => 3 * (1 - t) * (1 - t) * t * a + 3 * (1 - t) * t * t * b + t * t * t;
}

public enum Prop
{
    Opacity,
    TranslateX,
    TranslateY,
    /// <summary>Uniform scale around the actor's pivot.</summary>
    Scale,
    ScaleX,
    ScaleY,
    /// <summary>2D rotation in degrees.</summary>
    RotateZ,
    /// <summary>3D rotation around the vertical axis, shown with perspective.</summary>
    RotateY,
    /// <summary>3D rotation around the horizontal axis, shown with perspective.</summary>
    RotateX,
    /// <summary>Gaussian blur radius in slide pixels.</summary>
    Blur,
    /// <summary>1 is normal, 0 is greyscale.</summary>
    Saturation,
    /// <summary>Exposure in stops, 0 is normal, negative darkens.</summary>
    Exposure,
    /// <summary>Opacity of the drop shadow under the actor.</summary>
    ShadowOpacity,
    /// <summary>Progress of the actor's mask from 0 (hidden) to 1 (fully shown).</summary>
    MaskProgress,
}

/// <summary>A value at a point in normalized time (0 to 1 of the whole transition). The ease shapes the segment arriving at this key.</summary>
public readonly record struct Key(double Time, double Value, Ease Ease);

public sealed class Track
{
    public required Prop Prop { get; init; }
    public List<Key> Keys { get; } = [];

    public double ValueAt(double time)
    {
        if (Keys.Count == 0) return 0;
        if (time <= Keys[0].Time) return Keys[0].Value;
        for (var i = 1; i < Keys.Count; i++)
        {
            var b = Keys[i];
            if (time <= b.Time)
            {
                var a = Keys[i - 1];
                var span = b.Time - a.Time;
                var p = span <= 0 ? 1 : b.Ease.Evaluate((time - a.Time) / span);
                return a.Value + (b.Value - a.Value) * p;
            }
        }
        return Keys[^1].Value;
    }
}

public enum ActorSource
{
    /// <summary>The whole outgoing slide (background and objects).</summary>
    OutgoingSlide,
    IncomingSlide,
    OutgoingBackground,
    IncomingBackground,
    /// <summary>One object of the outgoing slide, see <see cref="Actor.ElementId"/>.</summary>
    OutgoingElement,
    IncomingElement,
    /// <summary>A solid colour rectangle.</summary>
    Color,
    /// <summary>A soft band of light: transparent, colour, transparent across the actor's width.</summary>
    LightBand,
    /// <summary>A radial glow: colour in the centre fading to transparent.</summary>
    Glow,
}

public enum MaskKind { None, Linear, Radial }

/// <summary>A feathered reveal mask driven by the MaskProgress track.</summary>
public sealed class Mask
{
    public MaskKind Kind { get; init; }
    /// <summary>Direction the reveal travels, degrees (0 right, 90 down). Linear masks only.</summary>
    public double Angle { get; init; }
    /// <summary>Softness of the edge in actor pixels.</summary>
    public double Feather { get; init; }
    /// <summary>Radial centre in actor pixels.</summary>
    public double CenterX { get; init; }
    public double CenterY { get; init; }
    /// <summary>Radial masks: false grows a visible disc, true grows a transparent hole.</summary>
    public bool Invert { get; init; }
}

public sealed class Actor
{
    public required ActorSource Source { get; init; }
    public string? ElementId { get; init; }
    /// <summary>Where the actor sits on the slide, in slide pixels.</summary>
    public required RectD Bounds { get; init; }
    /// <summary>For tiles: the part of the source slide this actor shows. Null shows the whole source.</summary>
    public RectD? SourceRect { get; init; }
    /// <summary>Pivot for scale and rotation, relative to the actor (0.5, 0.5 is the centre).</summary>
    public double PivotX { get; init; } = .5;
    public double PivotY { get; init; } = .5;
    /// <summary>Resting 2D rotation (objects keep their own rotation).</summary>
    public double BaseRotation { get; init; }
    public string Color { get; init; } = "#FFFFFF";
    public Mask? Mask { get; init; }
    public bool HasEffects => Tracks.Any(t => t.Prop is Prop.Blur or Prop.Saturation or Prop.Exposure);
    public List<Track> Tracks { get; } = [];

    public Track? Get(Prop p) => Tracks.FirstOrDefault(t => t.Prop == p);

    public Actor Add(Prop prop, params Key[] keys)
    {
        var t = Get(prop) ?? new Track { Prop = prop };
        if (!Tracks.Contains(t)) Tracks.Add(t);
        t.Keys.AddRange(keys);
        t.Keys.Sort((a, b) => a.Time.CompareTo(b.Time));
        return this;
    }

    /// <summary>Animates from a to b between normalized times t0 and t1, holding a before t0 and b after t1.</summary>
    public Actor Tween(Prop prop, double a, double b, double t0, double t1, Ease ease)
    {
        t0 = Math.Clamp(t0, 0, 1);
        t1 = Math.Clamp(t1, 0, 1);
        var keys = new List<Key>();
        if (t1 <= t0)
        {
            // Instant switch at t0.
            if (t0 <= 0) keys.Add(new(0, b, Ease.Linear));
            else
            {
                keys.Add(new(0, a, Ease.Linear));
                keys.Add(new(Math.Max(0, t0 - 1e-3), a, Ease.Linear));
                keys.Add(new(t0, b, Ease.Linear));
            }
            if (t0 < 1) keys.Add(new(1, b, Ease.Linear));
        }
        else
        {
            keys.Add(new(0, a, Ease.Linear));
            if (t0 > 0) keys.Add(new(t0, a, Ease.Linear));
            keys.Add(new(t1, b, ease));
            if (t1 < 1) keys.Add(new(1, b, Ease.Linear));
        }
        Replace(prop, keys);
        return this;
    }

    /// <summary>Replaces a track with the given keys (sorted by time).</summary>
    public Actor Replace(Prop prop, IEnumerable<Key> keys)
    {
        var track = Get(prop);
        if (track != null) Tracks.Remove(track);
        return Add(prop, keys.ToArray());
    }

    public Actor Set(Prop prop, double value) => Tween(prop, value, value, 0, 1, Ease.Linear);
}

public sealed class TransitionPlan
{
    public required string Effect { get; init; }
    public required double DurationMs { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
    /// <summary>Painted behind every actor.</summary>
    public string Backdrop { get; init; } = "#000000";
    /// <summary>Perspective distance for 3D rotations, in slide pixels.</summary>
    public double Perspective { get; init; } = 2600;
    /// <summary>Painter's order: first actor is at the back.</summary>
    public List<Actor> Actors { get; } = [];
}

/// <summary>What the planner needs to know about a slide (no rendering involved).</summary>
public sealed record SceneElement(string Id, string? Name, string Kind, RectD Bounds, double Rotation, double Opacity);

public sealed record Scene(IReadOnlyList<SceneElement> Elements)
{
    public static Scene From(Slide s) => new(s.Elements.Select(e => new SceneElement(e.Id, e.Name, e.GetType().Name, e.Bounds, e.Rotation, e.Opacity)).ToList());
    public static readonly Scene Empty = new([]);
}
