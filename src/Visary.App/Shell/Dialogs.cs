using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Visary.App.Shell;

public static class Dialogs
{
    public static XamlRoot? Root { get; set; }
    static bool _open;

    static ContentDialog Make(string title, object content) => new()
    {
        Title = title,
        Content = content,
        XamlRoot = Root,
        RequestedTheme = Palette.ElementTheme,
        DefaultButton = ContentDialogButton.Primary,
    };

    public static async Task MessageAsync(string title, string message)
    {
        if (Root == null || _open) return;
        var d = Make(title, new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        d.CloseButtonText = "OK";
        await ShowAsync(d);
    }

    /// <summary>Returns true for the primary button, false for the secondary, null for cancel.</summary>
    public static async Task<bool?> AskAsync(string title, string message, string primary, string? secondary, string cancel = "Cancel")
    {
        if (Root == null || _open) return null;
        var d = Make(title, new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        d.PrimaryButtonText = primary;
        if (secondary != null) d.SecondaryButtonText = secondary;
        d.CloseButtonText = cancel;
        var r = await ShowAsync(d);
        return r switch { ContentDialogResult.Primary => true, ContentDialogResult.Secondary => false, _ => null };
    }

    public static async Task<ContentDialogResult> ShowAsync(ContentDialog d)
    {
        if (Root == null || _open) return ContentDialogResult.None;
        _open = true;
        try
        {
            d.XamlRoot = Root;
            return await d.ShowAsync();
        }
        finally
        {
            _open = false;
        }
    }
}
