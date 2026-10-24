using Microsoft.UI.Xaml;

namespace Visary.App;

public partial class App : Application
{
    public static MainWindow? Main { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            Shell.Log.Error("Unhandled", e.Exception);
            // Keep the app alive for recoverable UI errors; the log has the details.
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var argv = Environment.GetCommandLineArgs().Skip(1).ToArray();
        Main = new MainWindow();
        Main.Activate();
        var selfTest = Array.IndexOf(argv, "--selftest");
        if (selfTest >= 0)
        {
            var outDir = selfTest + 1 < argv.Length ? argv[selfTest + 1] : Path.Combine(Path.GetTempPath(), "visary-selftest");
            _ = SelfTest.RunAsync(Main, outDir);
            return;
        }
        var file = argv.FirstOrDefault(a => a.EndsWith(Core.Documents.PresentationFile.Extension, StringComparison.OrdinalIgnoreCase) && File.Exists(a));
        if (file != null) Main.OpenFile(file);
    }
}
