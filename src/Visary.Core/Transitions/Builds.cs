using Visary.Core.Model;

namespace Visary.Core.Transitions;

public sealed record BuildEffect(string Id, string Name, BuildKind Kind);

/// <summary>Object animations. They run on live objects, so they use transform and opacity only.</summary>
public static class BuildCatalog
{
    public static readonly IReadOnlyList<BuildEffect> All =
    [
        new("fade", "Fade", BuildKind.Entrance),
        new("rise", "Rise", BuildKind.Entrance),
        new("drop", "Drop", BuildKind.Entrance),
        new("zoom", "Zoom", BuildKind.Entrance),
        new("pop", "Pop", BuildKind.Entrance),
        new("slide-left", "Slide from left", BuildKind.Entrance),
        new("slide-right", "Slide from right", BuildKind.Entrance),
        new("unfold", "Unfold", BuildKind.Entrance),
        new("pulse", "Pulse", BuildKind.Emphasis),
        new("breathe", "Breathe", BuildKind.Emphasis),
        new("nudge", "Nudge", BuildKind.Emphasis),
        new("spin", "Spin", BuildKind.Emphasis),
        new("grow", "Grow", BuildKind.Emphasis),
        new("fade", "Fade out", BuildKind.Exit),
        new("sink", "Sink", BuildKind.Exit),
        new("zoom", "Zoom out", BuildKind.Exit),
        new("slide-away", "Slide away", BuildKind.Exit),
        new("drift-up", "Drift up", BuildKind.Exit),
    ];

    public static IEnumerable<BuildEffect> For(BuildKind kind) => All.Where(b => b.Kind == kind);

    /// <summary>Tracks in the build's own normalized time. Values are relative to the object's resting state.</summary>
    public static List<Track> Tracks(Build b, double baseRotation = 0)
    {
        var a = new Actor { Source = ActorSource.IncomingElement, Bounds = default };
        switch (b.Kind, b.Effect)
        {
            case (BuildKind.Entrance, "rise"): a.Tween(Prop.Opacity, 0, 1, 0, .7, Ease.Smooth).Tween(Prop.TranslateY, 50, 0, 0, 1, Ease.Fluid); break;
            case (BuildKind.Entrance, "drop"): a.Tween(Prop.Opacity, 0, 1, 0, .7, Ease.Smooth).Tween(Prop.TranslateY, -50, 0, 0, 1, Ease.Fluid); break;
            case (BuildKind.Entrance, "zoom"): a.Tween(Prop.Opacity, 0, 1, 0, .7, Ease.Smooth).Tween(Prop.Scale, .82, 1, 0, 1, Ease.Fluid); break;
            case (BuildKind.Entrance, "pop"):
                a.Tween(Prop.Opacity, 0, 1, 0, .4, Ease.Smooth).Replace(Prop.Scale, TransitionPlanner.SpringKeys(.55, 1, 0, 1)); break;
            case (BuildKind.Entrance, "slide-left"): a.Tween(Prop.Opacity, 0, 1, 0, .6, Ease.Smooth).Tween(Prop.TranslateX, -90, 0, 0, 1, Ease.Fluid); break;
            case (BuildKind.Entrance, "slide-right"): a.Tween(Prop.Opacity, 0, 1, 0, .6, Ease.Smooth).Tween(Prop.TranslateX, 90, 0, 0, 1, Ease.Fluid); break;
            case (BuildKind.Entrance, "unfold"): a.Tween(Prop.Opacity, 0, 1, 0, .5, Ease.Smooth).Tween(Prop.ScaleY, .1, 1, 0, 1, Ease.Fluid); break;
            case (BuildKind.Entrance, _): a.Tween(Prop.Opacity, 0, 1, 0, 1, Ease.Smooth); break;

            case (BuildKind.Emphasis, "pulse"): a.Replace(Prop.Scale, [new(0, 1, Ease.Linear), new(.45, 1.07, Ease.Smooth), new(1, 1, Ease.Smooth)]); break;
            case (BuildKind.Emphasis, "breathe"): a.Replace(Prop.Opacity, [new(0, 1, Ease.Linear), new(.5, .45, Ease.Smooth), new(1, 1, Ease.Smooth)]); break;
            case (BuildKind.Emphasis, "nudge"):
                a.Replace(Prop.TranslateX, [new(0, 0, Ease.Linear), new(.25, 14, Ease.Smooth), new(.55, -10, Ease.Smooth), new(.8, 5, Ease.Smooth), new(1, 0, Ease.Smooth)]); break;
            case (BuildKind.Emphasis, "spin"): a.Tween(Prop.RotateZ, baseRotation, baseRotation + 360, 0, 1, Ease.Smooth); break;
            case (BuildKind.Emphasis, "grow"): a.Tween(Prop.Scale, 1, 1.18, 0, 1, Ease.Fluid); break;
            case (BuildKind.Emphasis, _): a.Replace(Prop.Scale, [new(0, 1, Ease.Linear), new(.5, 1.05, Ease.Smooth), new(1, 1, Ease.Smooth)]); break;

            case (BuildKind.Exit, "sink"): a.Tween(Prop.Opacity, 1, 0, .2, 1, Ease.Smooth).Tween(Prop.TranslateY, 0, 50, 0, 1, Ease.Exit); break;
            case (BuildKind.Exit, "zoom"): a.Tween(Prop.Opacity, 1, 0, .2, 1, Ease.Smooth).Tween(Prop.Scale, 1, .82, 0, 1, Ease.Exit); break;
            case (BuildKind.Exit, "slide-away"): a.Tween(Prop.Opacity, 1, 0, .3, 1, Ease.Smooth).Tween(Prop.TranslateX, 0, 110, 0, 1, Ease.Exit); break;
            case (BuildKind.Exit, "drift-up"): a.Tween(Prop.Opacity, 1, 0, .1, 1, Ease.Smooth).Tween(Prop.TranslateY, 0, -40, 0, 1, Ease.Gentle).Tween(Prop.Scale, 1, 1.04, 0, 1, Ease.Gentle); break;
            default: a.Tween(Prop.Opacity, 1, 0, 0, 1, Ease.Smooth); break;
        }
        return a.Tracks;
    }
}

public sealed record TimedBuild(Build Build, int StartMs);

/// <summary>A group of builds started by one click (or automatically when the slide appears).</summary>
public sealed record BuildStep(bool Automatic, IReadOnlyList<TimedBuild> Items)
{
    public int DurationMs => Items.Count == 0 ? 0 : Items.Max(i => i.StartMs + i.Build.DelayMs + i.Build.DurationMs);
}

public static class BuildTimeline
{
    public static List<BuildStep> Steps(IEnumerable<Build> builds)
    {
        var steps = new List<BuildStep>();
        List<TimedBuild>? current = null;
        var auto = false;
        int prevStart = 0, prevEnd = 0;
        foreach (var b in builds)
        {
            if (b.Trigger == BuildTrigger.OnClick || current == null)
            {
                if (current != null) steps.Add(new BuildStep(auto, current));
                current = [];
                auto = b.Trigger != BuildTrigger.OnClick;
                prevStart = prevEnd = 0;
            }
            var start = b.Trigger switch
            {
                BuildTrigger.WithPrevious => prevStart,
                BuildTrigger.AfterPrevious => prevEnd,
                _ => 0,
            };
            current.Add(new TimedBuild(b, start));
            prevStart = start;
            prevEnd = Math.Max(prevEnd, start + b.DelayMs + b.DurationMs);
        }
        if (current != null) steps.Add(new BuildStep(auto, current));
        return steps;
    }

    /// <summary>Objects whose first build is an entrance start hidden.</summary>
    public static HashSet<string> InitiallyHidden(Slide s)
    {
        var seen = new HashSet<string>();
        var hidden = new HashSet<string>();
        foreach (var b in s.Builds)
        {
            if (!seen.Add(b.ElementId)) continue;
            if (b.Kind == BuildKind.Entrance) hidden.Add(b.ElementId);
        }
        return hidden;
    }
}
