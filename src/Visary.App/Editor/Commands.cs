using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Visary.App.Rendering;
using Visary.App.Shell;
using Visary.Core.Documents;
using Visary.Core.Editing;
using Visary.Core.Export;
using Visary.Core.Model;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using NewPickers = Microsoft.Windows.Storage.Pickers;
using OldPickers = Windows.Storage.Pickers;

namespace Visary.App.Editor;

/// <summary>File and media commands that need Windows pickers.</summary>
public static class Commands
{
    public static Window? Owner { get; set; }

    /// <summary>A panel inside the window, under the workspace, where slides are laid out to be captured for export.</summary>
    public static Grid? RenderHost { get; set; }

    /// <summary>Raised to show a short notification bar in the main window (title, message, success).</summary>
    public static Action<string, string, bool>? Notify { get; set; }

    static void Init(object picker)
    {
        if (Owner != null) WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(Owner));
    }

    /*
     * Windows App SDK 1.8 adds Microsoft.Windows.Storage.Pickers, which take the window id directly and
     * work for elevated and unpackaged apps. The older Windows.Storage.Pickers stay as a fallback.
     */

    public static async Task<string?> PickOpenAsync(params string[] extensions)
    {
        if (Owner != null)
        {
            try
            {
                var picker = new NewPickers.FileOpenPicker(Owner.AppWindow.Id) { SuggestedStartLocation = NewPickers.PickerLocationId.DocumentsLibrary, ViewMode = NewPickers.PickerViewMode.List };
                foreach (var e in extensions) picker.FileTypeFilter.Add(e);
                var result = await picker.PickSingleFileAsync();
                return result?.Path;
            }
            catch (Exception ex)
            {
                Log.Error("Open picker", ex);
            }
        }
        var old = new OldPickers.FileOpenPicker { SuggestedStartLocation = OldPickers.PickerLocationId.DocumentsLibrary, ViewMode = OldPickers.PickerViewMode.List };
        foreach (var e in extensions) old.FileTypeFilter.Add(e);
        Init(old);
        return (await old.PickSingleFileAsync())?.Path;
    }

    public static async Task<string?> PickSaveAsync(string suggestedName, string label, string extension)
    {
        if (Owner != null)
        {
            try
            {
                var picker = new NewPickers.FileSavePicker(Owner.AppWindow.Id) { SuggestedStartLocation = NewPickers.PickerLocationId.DocumentsLibrary, SuggestedFileName = suggestedName, DefaultFileExtension = extension };
                picker.FileTypeChoices.Add(label, [extension]);
                var result = await picker.PickSaveFileAsync();
                return result?.Path;
            }
            catch (Exception ex)
            {
                Log.Error("Save picker", ex);
            }
        }
        var old = new OldPickers.FileSavePicker { SuggestedStartLocation = OldPickers.PickerLocationId.DocumentsLibrary, SuggestedFileName = suggestedName };
        old.FileTypeChoices.Add(label, [extension]);
        Init(old);
        return (await old.PickSaveFileAsync())?.Path;
    }

    public static async Task<string?> PickFolderAsync()
    {
        if (Owner != null)
        {
            try
            {
                var picker = new NewPickers.FolderPicker(Owner.AppWindow.Id) { SuggestedStartLocation = NewPickers.PickerLocationId.PicturesLibrary };
                var result = await picker.PickSingleFolderAsync();
                return result?.Path;
            }
            catch (Exception ex)
            {
                Log.Error("Folder picker", ex);
            }
        }
        var old = new OldPickers.FolderPicker { SuggestedStartLocation = OldPickers.PickerLocationId.PicturesLibrary };
        old.FileTypeFilter.Add("*");
        Init(old);
        return (await old.PickSingleFolderAsync())?.Path;
    }

    static readonly string[] PictureTypes = [".png", ".jpg", ".jpeg", ".gif", ".bmp"];

    public static async Task InsertPictureAsync(DocumentEditor ed)
    {
        var path = await PickOpenAsync(PictureTypes);
        if (path != null) await InsertPictureFileAsync(ed, path);
    }

    public static async Task InsertPictureFileAsync(DocumentEditor ed, string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!PictureTypes.Contains(ext)) return;
        var data = await File.ReadAllBytesAsync(path);
        if (!ImageInfo.TryGetSize(data, out var w, out var h)) (w, h) = (800, 600);
        ed.InsertImage(data, MediaItem.ContentTypeFromExtension(ext), w, h);
    }

    public static void InsertPictureBytes(DocumentEditor ed, byte[] data, string contentType)
    {
        if (!ImageInfo.TryGetSize(data, out var w, out var h)) (w, h) = (800, 600);
        ed.InsertImage(data, contentType, w, h);
    }

    public static async Task<(byte[] data, string type, int w, int h)?> PickPictureBytesAsync()
    {
        var path = await PickOpenAsync(PictureTypes);
        if (path == null) return null;
        var data = await File.ReadAllBytesAsync(path);
        if (!ImageInfo.TryGetSize(data, out var w, out var h)) (w, h) = (800, 600);
        return (data, MediaItem.ContentTypeFromExtension(Path.GetExtension(path)), w, h);
    }

    public static string SafeName(string title)
    {
        var name = string.Concat((title ?? "").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        return name.Length == 0 ? "Presentation" : name;
    }

    public static async Task<bool> SaveAsync(DocumentEditor ed, bool saveAs = false)
    {
        var path = ed.FilePath;
        if (saveAs || path == null)
        {
            path = await PickSaveAsync(SafeName(ed.Doc.Title), "Visary presentation", PresentationFile.Extension);
            if (path == null) return false;
        }
        return await SaveToAsync(ed, path, quiet: false);
    }

    /// <summary>Saves to a known path. AutoSave uses the quiet form, which reports failures in the notification bar.</summary>
    public static async Task<bool> SaveToAsync(DocumentEditor ed, string path, bool quiet)
    {
        try
        {
            var doc = ed.Doc;
            var json = DocumentCloner.Clone(doc);
            json.Media = new Dictionary<string, MediaItem>(doc.Media);
            await Task.Run(() => PresentationFile.Save(json, path));
            ed.MarkSaved(path);
            if (!quiet) Settings.Current.AddRecent(path);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Save", ex);
            if (quiet) Notify?.Invoke("AutoSave paused", ex.Message, false);
            else await Dialogs.MessageAsync("Could not save", ex.Message);
            return false;
        }
    }

    public static async Task ExportPptxAsync(DocumentEditor ed)
    {
        var path = await PickSaveAsync(SafeName(ed.Doc.Title), "PowerPoint presentation", ".pptx");
        if (path == null) return;
        try
        {
            await Task.Run(() => PptxExporter.Export(ed.Doc, path));
            Notify?.Invoke("Exported to PowerPoint", $"Saved {Path.GetFileName(path)}. Transitions were converted to their closest PowerPoint equivalents, and Morph stays Morph.", true);
        }
        catch (Exception ex)
        {
            Log.Error("Export pptx", ex);
            await Dialogs.MessageAsync("Export failed", ex.Message);
        }
    }

    /* ---------------- PDF and pictures ---------------- */

    /// <summary>Lays a slide out off screen and captures it with RenderTargetBitmap at the given pixel width.</summary>
    public static async Task<(byte[] bgra, int w, int h)> RenderSlideAsync(Presentation p, Slide s, int pixelWidth)
    {
        var host = RenderHost ?? throw new InvalidOperationException("The window is not ready to render slides.");
        var view = new SlideView(p, s, SlideViewMode.Show);
        var box = new Viewbox { Width = 960, Height = 960 * p.Height / p.Width, Child = view, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        host.Children.Add(box);
        try
        {
            await view.ReadyAsync();
            // One layout pass and a frame so text and pictures are composed before the capture.
            box.UpdateLayout();
            await Task.Delay(30);
            var pixelHeight = (int)Math.Round(pixelWidth * p.Height / p.Width);
            var rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(box, pixelWidth, pixelHeight);
            var pixels = await rtb.GetPixelsAsync();
            return (pixels.ToArray(), rtb.PixelWidth, rtb.PixelHeight);
        }
        finally
        {
            host.Children.Remove(box);
        }
    }

    static async Task<byte[]> EncodeAsync(byte[] bgra, int w, int h, bool jpeg)
    {
        using var stream = new InMemoryRandomAccessStream();
        BitmapEncoder enc;
        if (jpeg)
        {
            var options = new BitmapPropertySet { { "ImageQuality", new BitmapTypedValue(0.92, Windows.Foundation.PropertyType.Single) } };
            enc = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream, options);
        }
        else
        {
            enc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        }
        enc.SetPixelData(BitmapPixelFormat.Bgra8, jpeg ? BitmapAlphaMode.Ignore : BitmapAlphaMode.Premultiplied, (uint)w, (uint)h, 96, 96, bgra);
        await enc.FlushAsync();
        stream.Seek(0);
        var bytes = new byte[stream.Size];
        await stream.ReadAsync(bytes.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);
        return bytes;
    }

    static List<Slide> ExportSlides(Presentation p, bool includeHidden) => p.Slides.Where(s => includeHidden || !s.Hidden).ToList();

    public static async Task ExportPdfAsync(DocumentEditor ed, bool includeHidden = false, string? path = null)
    {
        path ??= await PickSaveAsync(SafeName(ed.Doc.Title), "PDF document", ".pdf");
        if (path == null) return;
        try
        {
            var p = ed.Doc;
            var pages = new List<PdfWriter.Page>();
            foreach (var s in ExportSlides(p, includeHidden))
            {
                var (bgra, w, h) = await RenderSlideAsync(p, s, 1920);
                pages.Add(new PdfWriter.Page(await EncodeAsync(bgra, w, h, jpeg: true), w, h));
            }
            // PDF pages use points; 960 points wide keeps 16:9 slides at a familiar 13.3 inch size.
            var pageWidth = 960.0;
            var pageHeight = pageWidth * p.Height / p.Width;
            await Task.Run(() =>
            {
                using var fs = File.Create(path);
                PdfWriter.Write(fs, pages, pageWidth, pageHeight, p.Title);
            });
            Notify?.Invoke("Exported to PDF", $"Saved {Path.GetFileName(path)} with {pages.Count} pages.", true);
        }
        catch (Exception ex)
        {
            Log.Error("Export pdf", ex);
            await Dialogs.MessageAsync("Export failed", ex.Message);
        }
    }

    public static async Task ExportImagesAsync(DocumentEditor ed, bool currentOnly, int pixelWidth = 1920, string? folder = null)
    {
        folder ??= await PickFolderAsync();
        if (folder == null) return;
        try
        {
            var p = ed.Doc;
            var slides = currentOnly ? [ed.Current] : ExportSlides(p, includeHidden: false);
            var name = SafeName(p.Title);
            var count = 0;
            foreach (var s in slides)
            {
                var (bgra, w, h) = await RenderSlideAsync(p, s, pixelWidth);
                var png = await EncodeAsync(bgra, w, h, jpeg: false);
                var index = p.Slides.IndexOf(s) + 1;
                await File.WriteAllBytesAsync(Path.Combine(folder, $"{name} - Slide {index}.png"), png);
                count++;
            }
            Notify?.Invoke("Exported pictures", $"Saved {count} PNG {(count == 1 ? "file" : "files")} to {folder}.", true);
        }
        catch (Exception ex)
        {
            Log.Error("Export pictures", ex);
            await Dialogs.MessageAsync("Export failed", ex.Message);
        }
    }
}
