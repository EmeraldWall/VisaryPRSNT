using System.Text.Json.Serialization;

namespace Visary.Core.Model;

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
    public bool Intersects(RectD o) => X < o.Right && o.X < Right && Y < o.Bottom && o.Y < Bottom;
}

public sealed class Background
{
    public FillSpec Fill { get; set; } = FillSpec.Solid("#FFFFFF");
    public string? ImageMediaId { get; set; }
    /// <summary>0 to 1 darkening applied over a picture background.</summary>
    public double ImageDim { get; set; }
    public Background Clone() => new() { Fill = Fill.Clone(), ImageMediaId = ImageMediaId, ImageDim = ImageDim };
}

public enum TransitionEase { Smooth, Fluid, Snappy, Spring, Linear }
public enum TransitionIntensity { Subtle, Balanced, Bold }

public sealed class TransitionSettings
{
    public string Effect { get; set; } = "none";
    public string? Variant { get; set; }
    public int DurationMs { get; set; } = 900;
    public TransitionEase Ease { get; set; } = TransitionEase.Smooth;
    public TransitionIntensity Intensity { get; set; } = TransitionIntensity.Balanced;
    public bool AdvanceOnClick { get; set; } = true;
    /// <summary>Seconds before advancing automatically, or null for never.</summary>
    public double? AdvanceAfterSeconds { get; set; }
    public TransitionSettings Clone() => (TransitionSettings)MemberwiseClone();
}

public enum BuildKind { Entrance, Emphasis, Exit }
public enum BuildTrigger { OnClick, WithPrevious, AfterPrevious }

/// <summary>An object animation (PowerPoint calls these animations, Keynote calls them builds).</summary>
public sealed class Build
{
    public string Id { get; set; } = Ids.New();
    public string ElementId { get; set; } = "";
    public BuildKind Kind { get; set; } = BuildKind.Entrance;
    public string Effect { get; set; } = "fade";
    public BuildTrigger Trigger { get; set; } = BuildTrigger.OnClick;
    public int DelayMs { get; set; }
    public int DurationMs { get; set; } = 600;
}

public sealed class Slide
{
    public string Id { get; set; } = Ids.New();
    public List<SlideElement> Elements { get; set; } = [];
    public Background Background { get; set; } = new();
    public string Notes { get; set; } = "";
    public TransitionSettings Transition { get; set; } = new();
    public List<Build> Builds { get; set; } = [];
    public bool Hidden { get; set; }
    public string Layout { get; set; } = "blank";

    public SlideElement? Find(string id) => Elements.FirstOrDefault(e => e.Id == id);
}

public sealed class MediaItem
{
    public required string ContentType { get; init; }
    public required byte[] Data { get; init; }

    public string Extension => ContentType switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/gif" => ".gif",
        "image/bmp" => ".bmp",
        "image/webp" => ".webp",
        _ => ".bin",
    };

    public static string ContentTypeFromExtension(string ext) => ext.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".webp" => "image/webp",
        _ => "application/octet-stream",
    };
}

public sealed class Presentation
{
    public string Title { get; set; } = "Untitled presentation";
    public double Width { get; set; } = 1920;
    public double Height { get; set; } = 1080;
    public string ThemeId { get; set; } = "office";
    public bool Loop { get; set; }
    public bool ShowSlideNumbers { get; set; }
    public List<Slide> Slides { get; set; } = [];

    /// <summary>Pictures, stored next to the JSON inside the .vsry package.</summary>
    [JsonIgnore] public Dictionary<string, MediaItem> Media { get; set; } = [];

    public int IndexOf(string slideId) => Slides.FindIndex(s => s.Id == slideId);
}
