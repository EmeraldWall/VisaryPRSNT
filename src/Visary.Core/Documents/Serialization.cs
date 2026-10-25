using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Visary.Core.Model;

namespace Visary.Core.Documents;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}

public static class DocumentCloner
{
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json.Options), Json.Options)!;

    /// <summary>Copies a slide. Keeping ids lets Morph pair objects between the original and the copy.</summary>
    public static Slide CloneSlide(Slide slide, bool keepIds)
    {
        var copy = Clone(slide);
        copy.Id = Ids.New();
        foreach (var b in copy.Builds) b.Id = Ids.New();
        if (!keepIds)
        {
            var map = new Dictionary<string, string>();
            foreach (var e in copy.Elements)
            {
                var id = Ids.New();
                map[e.Id] = id;
                e.Id = id;
            }
            foreach (var b in copy.Builds) b.ElementId = map.GetValueOrDefault(b.ElementId, b.ElementId);
        }
        return copy;
    }

    public static SlideElement CloneElement(SlideElement e, bool newId = true)
    {
        var c = Clone(e);
        if (newId) c.Id = Ids.New();
        return c;
    }
}

/// <summary>
/// The .vsry package: a zip with presentation.json plus media/&lt;id&gt;.&lt;ext&gt; files.
/// </summary>
public static class PresentationFile
{
    public const string Extension = ".vsry";
    const string DocEntry = "presentation.json";
    const int FormatVersion = 1;

    sealed class Envelope
    {
        public string Format { get; set; } = "visary-prsnt";
        public int Version { get; set; } = FormatVersion;
        public Presentation Presentation { get; set; } = new();
        public Dictionary<string, string> MediaTypes { get; set; } = [];
    }

    public static void Save(Presentation p, Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        var used = UsedMedia(p);
        var env = new Envelope { Presentation = p };
        foreach (var (id, item) in p.Media)
        {
            if (!used.Contains(id)) continue;
            env.MediaTypes[id] = item.ContentType;
            var entry = zip.CreateEntry($"media/{id}{item.Extension}", CompressionLevel.NoCompression);
            using var s = entry.Open();
            s.Write(item.Data);
        }
        var doc = zip.CreateEntry(DocEntry, CompressionLevel.Optimal);
        using (var ds = doc.Open()) JsonSerializer.Serialize(ds, env, Json.Options);
    }

    public static Presentation Load(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var docEntry = zip.GetEntry(DocEntry) ?? throw new InvalidDataException("This file is not a Visary presentation.");
        Envelope env;
        using (var ds = docEntry.Open())
            env = JsonSerializer.Deserialize<Envelope>(ds, Json.Options) ?? throw new InvalidDataException("The presentation is empty.");
        if (env.Format != "visary-prsnt") throw new InvalidDataException("This file is not a Visary presentation.");
        if (env.Version > FormatVersion) throw new InvalidDataException("This file was made by a newer version of Visary PRSNT.");
        var p = env.Presentation;
        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("media/", StringComparison.Ordinal)))
        {
            var id = Path.GetFileNameWithoutExtension(entry.Name);
            using var es = entry.Open();
            using var ms = new MemoryStream();
            es.CopyTo(ms);
            var type = env.MediaTypes.GetValueOrDefault(id) ?? MediaItem.ContentTypeFromExtension(Path.GetExtension(entry.Name));
            p.Media[id] = new MediaItem { ContentType = type, Data = ms.ToArray() };
        }
        Repair(p);
        return p;
    }

    public static void Save(Presentation p, string path)
    {
        // Write to a temporary file first so a failed save never destroys the previous copy.
        var tmp = path + ".tmp";
        using (var fs = File.Create(tmp)) Save(p, fs);
        File.Move(tmp, path, overwrite: true);
    }

    public static Presentation Load(string path)
    {
        using var fs = File.OpenRead(path);
        return Load(fs);
    }

    static HashSet<string> UsedMedia(Presentation p)
    {
        var set = new HashSet<string>();
        foreach (var s in p.Slides)
        {
            if (s.Background.ImageMediaId is { } bg) set.Add(bg);
            foreach (var e in s.Elements.OfType<ImageElement>()) set.Add(e.MediaId);
        }
        return set;
    }

    /// <summary>Fixes values a hand edited or damaged file could contain.</summary>
    public static void Repair(Presentation p)
    {
        if (p.Width < 100 || p.Height < 100) { p.Width = 1920; p.Height = 1080; }
        p.Slides ??= [];
        if (p.Slides.Count == 0) p.Slides.Add(new Slide());
        foreach (var s in p.Slides)
        {
            s.Elements ??= [];
            s.Builds ??= [];
            s.Background ??= new Background();
            s.Transition ??= new TransitionSettings();
            s.Transition.DurationMs = Math.Clamp(s.Transition.DurationMs, 100, 10000);
            s.Elements.RemoveAll(e => e is ImageElement img && !p.Media.ContainsKey(img.MediaId));
            foreach (var e in s.Elements)
            {
                e.Width = Math.Max(1, e.Width);
                e.Height = Math.Max(1, e.Height);
                e.Opacity = Math.Clamp(e.Opacity, 0, 1);
                if (e is TableElement t) t.Normalize();
            }
            var ids = s.Elements.Select(e => e.Id).ToHashSet();
            s.Builds.RemoveAll(b => !ids.Contains(b.ElementId));
            if (s.Background.ImageMediaId is { } bg && !p.Media.ContainsKey(bg)) s.Background.ImageMediaId = null;
        }
    }
}
