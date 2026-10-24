using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Visary.App.Shell;

/// <summary>Diagnostics: "--smoke-control name" shows a single control so CI can find controls that crash.</summary>
public static class SmokeControls
{
    public static UIElement Create(string name) => name switch
    {
        "colorbutton" => Ui.ColorButton("#C43E1C", _ => { }, ["#FFFFFF", "#000000"]),
        "combo" => Ui.Combo([(1, "One"), (2, "Two")], 1, _ => { }),
        "editablecombo" => new ComboBox { IsEditable = true, Width = 170, ItemsSource = Core.Themes.SlideThemes.Fonts.ToList(), Text = "Segoe UI" },
        "numberbox" => Ui.Number(12, 0, 100, _ => { }),
        "slider" => Ui.Slider(5, 0, 10, _ => { }),
        "menu" => Ui.Menu("", "Menu", new MenuFlyout { Items = { Ui.Item("Item", () => { }) } }),
        "splitbutton" => new SplitButton { Content = "Split", Flyout = new MenuFlyout() },
        "toggle" => Ui.Toggle("", "Bold", true, _ => { }),
        "check" => Ui.Check("Check", true, _ => { }),
        "dropdown" => new DropDownButton { Content = "Plain drop down", Flyout = new Flyout { Content = new TextBlock { Text = "x" } } },
        "colorpicker" => new ColorPicker(),
        "expander" => new Expander { Header = "Header", Content = new TextBlock { Text = "Body" } },
        "group" => Ui.Group("Group", Ui.Large("", "Large", () => { })),
        "section" => Ui.Section("Section", Ui.Text("Body")),
        "accentbutton" => new Button { Content = "Accent", Style = (Style)Application.Current.Resources["AccentButtonStyle"] },
        _ => new TextBlock { Text = "Unknown control " + name },
    };
}
