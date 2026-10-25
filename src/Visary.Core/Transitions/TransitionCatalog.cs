namespace Visary.Core.Transitions;

public sealed record TransitionVariant(string Id, string Name);

public sealed record TransitionInfo(
    string Id,
    string Name,
    string Category,
    string Description,
    /// <summary>Segoe Fluent Icons glyph.</summary>
    string Glyph,
    int DefaultDurationMs,
    IReadOnlyList<TransitionVariant> Variants)
{
    public string DefaultVariant => Variants.Count > 0 ? Variants[0].Id : "";
}

public static class TransitionCatalog
{
    static readonly TransitionVariant[] FromSides =
        [new("right", "From right"), new("left", "From left"), new("bottom", "From bottom"), new("top", "From top")];
    static readonly TransitionVariant[] Corners =
        [new("center", "From centre"), new("top-left", "From top left"), new("top-right", "From top right"), new("bottom-left", "From bottom left"), new("bottom-right", "From bottom right")];

    public static readonly IReadOnlyList<string> Categories = ["Essentials", "Motion", "Depth", "Light", "Reveal", "Smart"];

    public static readonly IReadOnlyList<TransitionInfo> All =
    [
        new("none", "None", "Essentials", "Cut straight to the next slide.", "", 300, []),
        new("fade", "Fade", "Essentials", "A clean crossfade.", "", 700, []),
        new("silk", "Silk", "Essentials", "A crossfade that softens through a whisper of blur and a slow zoom.", "", 1000, []),
        new("drift", "Drift", "Essentials", "The new slide settles in with a slow, cinematic push in.", "", 1300,
            [new("right", "Drift left"), new("left", "Drift right"), new("none", "Straight in")]),

        new("glide", "Glide", "Motion", "The new slide glides over the old one, which sinks back in parallax.", "", 900, FromSides),
        new("velocity", "Velocity", "Motion", "A fast push with motion blur that resolves crisp.", "", 750, FromSides),
        new("stack", "Stack", "Motion", "The new slide lands on top like a card, the old one settles into the deck.", "", 950,
            [new("bottom", "From bottom"), new("right", "From right"), new("left", "From left"), new("top", "From top")]),
        new("ribbon", "Ribbon", "Motion", "Two halves of the new slide sweep in from opposite sides and meet.", "", 1000,
            [new("horizontal", "Top and bottom"), new("vertical", "Left and right")]),
        new("panels", "Panels", "Motion", "The old slide splits into panels that lift away one after another.", "", 1100,
            [new("alternate", "Alternate"), new("up", "Up"), new("down", "Down")]),
        new("parallax", "Parallax", "Motion", "Background and objects travel at different depths, like a camera dolly.", "\uE8AB", 1200, FromSides),
        new("float", "Float", "Motion", "The old slide lifts off and floats away, revealing the new one beneath.", "", 1100, []),

        new("depth", "Depth", "Depth", "The old slide recedes into the dark as the new one arrives from in front.", "", 1100, []),
        new("portal", "Portal", "Depth", "Fly through the old slide into the new one.", "", 1100, []),
        new("dive", "Dive", "Depth", "The camera dives into an object on the old slide and surfaces in the new one. Uses the object Morph would pair, or the largest one.", "\uE8A3", 1300, []),
        new("orbit", "Orbit", "Depth", "Slides swing past each other in perspective.", "", 1100, [new("right", "From right"), new("left", "From left")]),
        new("focus", "Focus pull", "Depth", "A camera rack focus: the old slide melts out of focus and the new one sharpens in.", "", 1200, []),
        new("frost", "Frost", "Depth", "The slide frosts over like glass, then clears on the new one.", "", 1200, []),

        new("sweep", "Light sweep", "Light", "A feathered edge of light sweeps across and leaves the new slide behind it.", "", 1200,
            [new("down-right", "Diagonal down"), new("right", "Left to right"), new("up-right", "Diagonal up"), new("left", "Right to left")]),
        new("aurora", "Aurora", "Light", "The new slide blooms open from a point, haloed in the theme colour.", "", 1300, Corners),
        new("bloom", "Bloom", "Light", "A soft burst of theme colour fills the screen and fades into the new slide.", "", 1200, []),
        new("spotlight", "Spotlight", "Light", "A spotlight closes on the old slide and opens on the new one.", "", 1300, []),
        new("cinematic", "Cinematic", "Light", "Film letterbox bars frame a slow crossfade.", "", 1500, [new("letterbox", "Letterbox"), new("blackout", "Blackout")]),

        new("feather", "Feather", "Reveal", "A wide, soft edged wipe.", "", 1000,
            [new("right", "From left"), new("left", "From right"), new("down", "From top"), new("up", "From bottom")]),
        new("shutter", "Shutter", "Reveal", "Slim strips open in a soft ripple.", "", 1100, [new("horizontal", "Horizontal"), new("vertical", "Vertical")]),
        new("mosaic", "Mosaic", "Reveal", "Tiles of the new slide surface in a wave.", "", 1300,
            [new("center", "From centre"), new("top-left", "From top left"), new("scatter", "Scatter")]),

        new("cascade", "Cascade", "Smart", "Objects leave and arrive one by one in reading order.", "", 1400,
            [new("rise", "Rise"), new("fall", "Fall"), new("sideways", "Sideways")]),
        new("morph", "Morph", "Smart", "Objects on both slides glide, resize and turn into place. Duplicate a slide, change it, then apply Morph.", "", 1300, []),
    ];

    public static TransitionInfo Get(string? id) => All.FirstOrDefault(t => t.Id == id) ?? All[0];
}
