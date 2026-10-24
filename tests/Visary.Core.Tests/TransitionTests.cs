using Visary.Core.Model;
using Visary.Core.Transitions;

namespace Visary.Core.Tests;

public class TransitionTests
{
    const double W = 1920, H = 1080;

    static readonly Scene From = new([
        new("title", "Title", "TextElement", new RectD(100, 80, 1000, 160), 0, 1),
        new("a", null, "ShapeElement", new RectD(300, 400, 300, 300), 10, 1),
        new("gone", null, "ShapeElement", new RectD(1300, 600, 200, 200), 0, .8),
    ]);

    static readonly Scene To = new([
        new("title", "Title", "TextElement", new RectD(100, 60, 1200, 180), 0, 1),
        new("a", null, "ShapeElement", new RectD(1200, 300, 500, 500), 45, 1),
        new("new", null, "ImageElement", new RectD(200, 600, 400, 300), 0, 1),
    ]);

    public static IEnumerable<object?[]> Cases()
    {
        foreach (var t in TransitionCatalog.All)
        foreach (var v in t.Variants.Count > 0 ? t.Variants.Select(x => x.Id) : [""])
        foreach (var ease in Enum.GetValues<TransitionEase>())
        foreach (var intensity in new[] { TransitionIntensity.Subtle, TransitionIntensity.Bold })
        foreach (var hasFrom in new[] { true, false })
            yield return [t.Id, v, ease, intensity, hasFrom];
    }

    static TransitionPlan Plan(string id, string variant, TransitionEase ease, TransitionIntensity intensity, bool hasFrom) =>
        TransitionPlanner.Build(new TransitionSettings { Effect = id, Variant = variant, Ease = ease, Intensity = intensity, DurationMs = 1000 },
            hasFrom ? From : null, To, W, H);

    static double V(Actor a, Prop p, double t, double dflt) => a.Get(p)?.ValueAt(t) ?? dflt;

    [Theory]
    [MemberData(nameof(Cases))]
    public void Tracks_are_well_formed(string id, string variant, TransitionEase ease, TransitionIntensity intensity, bool hasFrom)
    {
        var plan = Plan(id, variant, ease, intensity, hasFrom);
        Assert.NotEmpty(plan.Actors);
        foreach (var a in plan.Actors)
        {
            if (!hasFrom) Assert.DoesNotContain(a.Source, new[] { ActorSource.OutgoingSlide, ActorSource.OutgoingBackground, ActorSource.OutgoingElement });
            if (a.Source is ActorSource.OutgoingElement) Assert.Contains(From.Elements, e => e.Id == a.ElementId);
            if (a.Source is ActorSource.IncomingElement) Assert.Contains(To.Elements, e => e.Id == a.ElementId);
            Assert.True(a.Bounds.Width > 0 && a.Bounds.Height > 0);
            Assert.Equal(a.Mask != null, a.Get(Prop.MaskProgress) != null);
            var rotations = a.Tracks.Count(t => t.Prop is Prop.RotateX or Prop.RotateY);
            Assert.True(rotations <= 1, "an actor can rotate around one 3D axis only");
            if (rotations == 1) Assert.Null(a.Get(Prop.RotateZ));
            foreach (var track in a.Tracks)
            {
                Assert.NotEmpty(track.Keys);
                Assert.Equal(0, track.Keys[0].Time);
                for (var i = 0; i < track.Keys.Count; i++)
                {
                    var k = track.Keys[i];
                    Assert.InRange(k.Time, 0, 1);
                    Assert.True(double.IsFinite(k.Value));
                    if (i > 0) Assert.True(k.Time > track.Keys[i - 1].Time, $"{id} {track.Prop} keys must strictly increase");
                }
                if (track.Prop is Prop.Opacity or Prop.ShadowOpacity) Assert.All(track.Keys, k => Assert.InRange(k.Value, -1e-9, 1 + 1e-9));
                if (track.Prop is Prop.Blur) Assert.All(track.Keys, k => Assert.True(k.Value >= -1e-9));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Ends_exactly_on_the_new_slide(string id, string variant, TransitionEase ease, TransitionIntensity intensity, bool hasFrom)
    {
        var plan = Plan(id, variant, ease, intensity, hasFrom);
        foreach (var a in plan.Actors)
        {
            switch (a.Source)
            {
                case ActorSource.IncomingSlide or ActorSource.IncomingBackground or ActorSource.IncomingElement:
                    var target = a.Source == ActorSource.IncomingElement ? To.Elements.First(e => e.Id == a.ElementId).Opacity : 1;
                    Assert.Equal(target, V(a, Prop.Opacity, 1, 1), 3);
                    Assert.Equal(0, V(a, Prop.TranslateX, 1, 0), 2);
                    Assert.Equal(0, V(a, Prop.TranslateY, 1, 0), 2);
                    Assert.Equal(1, V(a, Prop.Scale, 1, 1), 3);
                    Assert.Equal(1, V(a, Prop.ScaleX, 1, 1), 3);
                    Assert.Equal(1, V(a, Prop.ScaleY, 1, 1), 3);
                    Assert.Equal(0, V(a, Prop.Blur, 1, 0), 3);
                    Assert.Equal(0, V(a, Prop.Exposure, 1, 0), 3);
                    Assert.Equal(1, V(a, Prop.Saturation, 1, 1), 3);
                    Assert.Equal(0, V(a, Prop.RotateX, 1, 0), 3);
                    Assert.Equal(0, V(a, Prop.RotateY, 1, 0), 3);
                    Assert.Equal(a.BaseRotation, V(a, Prop.RotateZ, 1, a.BaseRotation), 3);
                    if (a.Mask != null) Assert.Equal(a.Mask.Invert ? 0 : 1, V(a, Prop.MaskProgress, 1, 1), 3);
                    break;
                case ActorSource.Color or ActorSource.LightBand or ActorSource.Glow:
                    // Overlays must be gone: transparent or slid fully off the slide.
                    var gone = V(a, Prop.Opacity, 1, 1) < 1e-3
                        || a.Bounds.Bottom + V(a, Prop.TranslateY, 1, 0) <= 1e-3
                        || a.Bounds.Y + V(a, Prop.TranslateY, 1, 0) >= H - 1e-3;
                    Assert.True(gone, $"{id}: overlay still visible at the end");
                    break;
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Starts_exactly_on_the_old_slide(string id, string variant, TransitionEase ease, TransitionIntensity intensity, bool hasFrom)
    {
        if (!hasFrom || id == "none") return;
        var plan = Plan(id, variant, ease, intensity, true);
        for (var i = 0; i < plan.Actors.Count; i++)
        {
            var a = plan.Actors[i];
            if (a.Source is ActorSource.Color or ActorSource.LightBand or ActorSource.Glow)
            {
                var hidden = V(a, Prop.Opacity, 0, 1) < 1e-3 || V(a, Prop.Scale, 0, 1) < 1e-3
                    || a.Bounds.Bottom + V(a, Prop.TranslateY, 0, 0) <= 1e-3 || a.Bounds.Y + V(a, Prop.TranslateY, 0, 0) >= H - 1e-3;
                Assert.True(hidden, $"{id}: overlay visible on the first frame");
                continue;
            }
            if (a.Source is not (ActorSource.IncomingSlide or ActorSource.IncomingBackground or ActorSource.IncomingElement)) continue;
            var invisible = V(a, Prop.Opacity, 0, 1) < 1e-3
                || (a.Mask != null && V(a, Prop.MaskProgress, 0, 1) < 1e-3)
                || Math.Abs(V(a, Prop.TranslateX, 0, 0)) >= a.Bounds.Width * .99 + Math.Min(a.Bounds.X, W - a.Bounds.Right)
                || Math.Abs(V(a, Prop.TranslateY, 0, 0)) >= a.Bounds.Height * .99 + Math.Min(a.Bounds.Y, H - a.Bounds.Bottom);
            if (invisible) continue;
            // Otherwise it must sit underneath outgoing actors that fully cover the slide.
            var cover = plan.Actors.Skip(i + 1)
                .Where(o => o.Source == ActorSource.OutgoingSlide && o.Mask == null
                    && V(o, Prop.Opacity, 0, 1) > .999 && Math.Abs(V(o, Prop.TranslateX, 0, 0)) < 1e-3 && Math.Abs(V(o, Prop.TranslateY, 0, 0)) < 1e-3
                    && V(o, Prop.Scale, 0, 1) >= .999 && V(o, Prop.Blur, 0, 0) < 1e-3)
                .Sum(o => o.Bounds.Width * o.Bounds.Height);
            Assert.True(cover >= W * H - 1, $"{id}/{variant}: incoming content shows on the first frame");
        }
    }

    [Fact]
    public void Outgoing_slide_is_untouched_on_the_first_frame()
    {
        foreach (var t in TransitionCatalog.All)
        {
            var plan = TransitionPlanner.Build(new TransitionSettings { Effect = t.Id }, From, To, W, H);
            foreach (var a in plan.Actors.Where(a => a.Source is ActorSource.OutgoingSlide or ActorSource.OutgoingBackground))
            {
                Assert.Equal(1, V(a, Prop.Opacity, 0, 1), 3);
                Assert.Equal(0, V(a, Prop.Blur, 0, 0), 3);
                Assert.Equal(1, V(a, Prop.Scale, 0, 1), 3);
                Assert.Equal(0, V(a, Prop.Exposure, 0, 0), 3);
                if (a.Mask != null) Assert.Equal(1, V(a, Prop.MaskProgress, 0, 1), 3);
            }
        }
    }

    [Fact]
    public void Morph_pairs_by_id_then_name_and_kind()
    {
        var from = new Scene([new("1", "Logo", "ImageElement", new RectD(0, 0, 10, 10), 0, 1), new("2", null, "TextElement", new RectD(0, 0, 10, 10), 0, 1)]);
        var to = new Scene([new("9", "Logo", "ImageElement", new RectD(5, 5, 20, 20), 0, 1), new("2", null, "ShapeElement", new RectD(0, 0, 10, 10), 0, 1)]);
        var pairs = TransitionPlanner.MatchForMorph(from, to);
        Assert.Single(pairs);
        Assert.Equal(("1", "9"), (pairs[0].from.Id, pairs[0].to.Id));
    }

    [Fact]
    public void Morph_moves_matched_objects_from_their_old_place()
    {
        var plan = TransitionPlanner.Build(new TransitionSettings { Effect = "morph" }, From, To, W, H);
        var a = plan.Actors.Single(x => x.Source == ActorSource.IncomingElement && x.ElementId == "a");
        var f = From.Elements.First(e => e.Id == "a").Bounds;
        var t = To.Elements.First(e => e.Id == "a").Bounds;
        Assert.Equal(f.CenterX - t.CenterX, V(a, Prop.TranslateX, 0, 0), 3);
        Assert.Equal(f.Width / t.Width, V(a, Prop.ScaleX, 0, 1), 3);
        Assert.Equal(10, V(a, Prop.RotateZ, 0, 0), 3);
        Assert.Equal(45, V(a, Prop.RotateZ, 1, 0), 3);
    }

    [Fact]
    public void Catalog_ids_are_unique_and_every_transition_has_a_description()
    {
        Assert.Equal(TransitionCatalog.All.Count, TransitionCatalog.All.Select(t => t.Id).Distinct().Count());
        Assert.All(TransitionCatalog.All, t => Assert.False(string.IsNullOrWhiteSpace(t.Description)));
        Assert.All(TransitionCatalog.All, t => Assert.Contains(t.Category, TransitionCatalog.Categories));
    }

    [Fact]
    public void Eases_hit_their_end_points()
    {
        foreach (var e in new[] { Ease.Linear, Ease.Smooth, Ease.Fluid, Ease.Snappy, Ease.Accelerate, Ease.Gentle, Ease.Exit })
        {
            Assert.Equal(0, e.Evaluate(0), 6);
            Assert.Equal(1, e.Evaluate(1), 6);
            Assert.InRange(e.Evaluate(.5), 0, 1);
        }
    }

    [Fact]
    public void Spring_settles_on_target_with_a_soft_overshoot()
    {
        var keys = TransitionPlanner.SpringKeys(0, 100, 0, 1).ToList();
        Assert.Equal(100, keys[^1].Value, 6);
        Assert.Contains(keys, k => k.Value > 100);
        Assert.True(keys.Max(k => k.Value) < 115);
    }
}
