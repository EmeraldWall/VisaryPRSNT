using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Visary.App.Editor;
using Visary.App.Shell;
using Visary.Core.Documents;
using Visary.Core.Export;
using Visary.Core.Model;
using Visary.Core.Transitions;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Visary.App;

/// <summary>
/// "VisaryPRSNT.exe --selftest &lt;folder&gt;" drives the real UI: every transition through the
/// GPU player, every pane, the show, save, reopen and export. Writes report.txt and
/// screenshots, then exits with code 1 if anything failed.
/// </summary>
public static class SelfTest
{
    static readonly List<string> Report = [];
    static int _failures;

    static void Ok(string s) => Report.Add($"PASS {s}");

    static void Fail(string s, Exception? ex = null)
    {
        _failures++;
        Report.Add($"FAIL {s}{(ex != null ? $": {ex.GetType().Name}: {ex.Message}" : "")}");
        if (ex != null) Log.Error($"Self test: {s}", ex);
    }

    static async Task Step(string name, Func<Task> body)
    {
        var errorsBefore = Log.Errors.Count;
        try
        {
            await body();
            if (Log.Errors.Count > errorsBefore) Fail($"{name} logged errors: {string.Join(" | ", Log.Errors.Skip(errorsBefore).Take(3))}");
            else Ok(name);
        }
        catch (Exception ex)
        {
            Fail(name, ex);
        }
    }

    public static async Task RunAsync(MainWindow w, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var started = DateTime.Now;
        await Task.Delay(1500);
        var ed = w.Editor;

        await Step("window loaded", async () => { await Shot(w, outDir, "01-editor"); });

        foreach (var theme in Core.Themes.AppThemes.All)
            await Step($"app theme {theme.Id}", async () => { w.ApplyAppTheme(theme.Id); await Task.Delay(120); });
        w.ApplyAppTheme("powerpoint");
        await Shot(w, outDir, "02-powerpoint-theme");

        await Step("insert every shape, text and a picture", async () =>
        {
            ed.GoTo(3);
            foreach (var k in Enum.GetValues<ShapeKind>()) ed.InsertShape(k);
            ed.InsertTextBox();
            ed.InsertTextBox(heading: true);
            ed.InsertImage(SamplePng(), "image/png", 64, 48);
            await Task.Delay(300);
        });

        foreach (var tab in Enum.GetValues<RibbonTab>())
            await Step($"ribbon tab {tab}", async () => { SetTab(w, tab); await Task.Delay(150); });

        foreach (var mode in Enum.GetValues<PaneMode>())
            await Step($"task pane {mode} for each kind of selection", async () =>
            {
                w.Side.Mode = mode;
                foreach (var e in ed.Current.Elements.Take(40).ToList())
                {
                    ed.Select([e.Id]);
                    await Task.Delay(15);
                }
                ed.ClearSelection();
                await Task.Delay(50);
            });

        await Step("text editing", async () =>
        {
            var t = ed.InsertTextBox();
            w.Canvas.BeginTextEdit(t.Id);
            await Task.Delay(200);
            w.Canvas.EndTextEdit(commit: true);
        });

        await Step("undo and redo", async () =>
        {
            for (var i = 0; i < 10; i++) ed.Undo();
            for (var i = 0; i < 5; i++) ed.Redo();
            await Task.Delay(100);
        });

        // Every transition and variant through the real Composition player.
        SetTab(w, RibbonTab.Transitions);
        ed.GoTo(1);
        var shotTaken = new HashSet<string>();
        foreach (var info in TransitionCatalog.All)
        foreach (var variant in info.Variants.Count > 0 ? info.Variants.Select(v => v.Id) : [""])
        {
            await Step($"transition {info.Id} {variant}", async () =>
            {
                ed.SetTransition(t => { t.Effect = info.Id; t.Variant = variant; t.DurationMs = 500; t.Ease = TransitionEase.Spring; });
                var preview = w.Canvas.PreviewTransitionAsync();
                if (shotTaken.Add(info.Id))
                {
                    await Task.Delay(250);
                    await Shot(w, outDir, $"tr-{info.Id}");
                }
                await preview;
            });
        }

        await Step("object animation preview", async () =>
        {
            ed.GoTo(1);
            ed.Select([ed.Current.Elements[0].Id]);
            foreach (var fx in BuildCatalog.All) ed.AddBuild(fx.Kind, fx.Id);
            foreach (var b in ed.Current.Builds) b.DurationMs = 120;
            await w.Canvas.PreviewBuildsAsync();
        });

        await Step("slide show through every slide", async () =>
        {
            w.StartShow(0, forcePresenter: true);
            await Task.Delay(1500);
            await Shot(w, outDir, "03-after-show-start");
            var controller = typeof(MainWindow).GetField("_showWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(w) as Show.SlideShowWindow;
            if (controller == null) throw new InvalidOperationException("Show window did not open");
            var show = (Show.ShowController)typeof(Show.SlideShowWindow).GetField("_show", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(controller)!;
            for (var i = 0; i < ed.Doc.Slides.Count * 4 + 30 && !show.AtEnd; i++)
            {
                show.Next();
                await Task.Delay(260);
            }
            if (!show.AtEnd) throw new InvalidOperationException($"Show stopped at slide {show.Index + 1}");
            show.Previous();
            await Task.Delay(300);
            controller.Close();
            await Task.Delay(500);
        });

        var file = Path.Combine(outDir, "roundtrip.vsry");
        await Step("save, reopen and export", async () =>
        {
            PresentationFile.Save(ed.Doc, file);
            var back = PresentationFile.Load(file);
            if (back.Slides.Count != ed.Doc.Slides.Count) throw new InvalidOperationException("Slide count changed after reopening");
            PptxExporter.Export(back, Path.Combine(outDir, "export.pptx"));
            await Task.Delay(50);
        });

        Report.Add($"Finished in {(DateTime.Now - started).TotalSeconds:0.0} s with {_failures} failure(s).");
        Report.AddRange(Log.Errors.Select(e => $"LOG {e}"));
        await File.WriteAllLinesAsync(Path.Combine(outDir, "report.txt"), Report);
        Environment.Exit(_failures == 0 ? 0 : 1);
    }

    static void SetTab(MainWindow w, RibbonTab tab)
    {
        var ribbon = (Ribbon?)typeof(MainWindow).GetField("_ribbon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(w);
        if (ribbon != null) ribbon.Tab = tab;
    }

    static async Task Shot(MainWindow w, string dir, string name)
    {
        try
        {
            if (w.Content is not UIElement root) return;
            var rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(root);
            var pixels = await rtb.GetPixelsAsync();
            using var stream = new InMemoryRandomAccessStream();
            var enc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            enc.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)rtb.PixelWidth, (uint)rtb.PixelHeight, 96, 96, pixels.ToArray());
            await enc.FlushAsync();
            stream.Seek(0);
            var bytes = new byte[stream.Size];
            await stream.ReadAsync(bytes.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);
            await File.WriteAllBytesAsync(Path.Combine(dir, name + ".png"), bytes);
        }
        catch (Exception ex)
        {
            Report.Add($"NOTE screenshot {name} failed: {ex.Message}");
        }
    }

    static byte[] SamplePng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAIAAAB7QOjdAAAAEElEQVR4nGP4z8DwnwEIAAz4AwHxNT4mAAAAAElFTkSuQmCC");
}
