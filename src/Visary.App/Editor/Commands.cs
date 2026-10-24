using Microsoft.UI.Xaml;
using Visary.App.Shell;
using Visary.Core.Documents;
using Visary.Core.Editing;
using Visary.Core.Export;
using Visary.Core.Model;
using Windows.Storage.Pickers;

namespace Visary.App.Editor;

/// <summary>File and media commands that need Windows pickers.</summary>
public static class Commands
{
    public static Window? Owner { get; set; }

    static void Init(object picker)
    {
        if (Owner != null) WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(Owner));
    }

    public static async Task<string?> PickOpenAsync(params string[] extensions)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary, ViewMode = PickerViewMode.List };
        foreach (var e in extensions) picker.FileTypeFilter.Add(e);
        Init(picker);
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public static async Task<string?> PickSaveAsync(string suggestedName, string label, string extension)
    {
        var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary, SuggestedFileName = suggestedName };
        picker.FileTypeChoices.Add(label, [extension]);
        Init(picker);
        var file = await picker.PickSaveFileAsync();
        return file?.Path;
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
        try
        {
            await Task.Run(() => PresentationFile.Save(ed.Doc, path));
            ed.MarkSaved(path);
            Settings.Current.AddRecent(path);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Save", ex);
            await Dialogs.MessageAsync("Could not save", ex.Message);
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
            await Dialogs.MessageAsync("Exported", $"Saved {Path.GetFileName(path)}. Transitions were converted to their closest PowerPoint equivalents, and Morph stays Morph.");
        }
        catch (Exception ex)
        {
            Log.Error("Export pptx", ex);
            await Dialogs.MessageAsync("Export failed", ex.Message);
        }
    }
}
