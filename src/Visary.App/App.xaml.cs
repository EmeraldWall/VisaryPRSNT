using Microsoft.UI.Xaml;

namespace Visary.App;

public partial class App : Application
{
    public static MainWindow? Main { get; private set; }

    public App()
    {
        Shell.Log.Info($"Starting {string.Join(' ', Environment.GetCommandLineArgs())}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Shell.Log.Error("AppDomain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { Shell.Log.Error("Task", e.Exception); e.SetObserved(); };
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
        var control = Array.IndexOf(argv, "--smoke-control");
        if (control >= 0 && control + 1 < argv.Length)
        {
            var w = new Window { Content = new Microsoft.UI.Xaml.Controls.Grid { Padding = new Thickness(40), Children = { Shell.SmokeControls.Create(argv[control + 1]) } } };
            w.Activate();
            _ = Task.Delay(3500).ContinueWith(_ => Environment.Exit(0));
            return;
        }
        try
        {
            Main = new MainWindow();
            Shell.Log.Info("Main window created");
            Main.Activate();
            Shell.Log.Info("Main window activated");
        }
        catch (Exception ex)
        {
            Shell.Log.Error("Startup", ex);
            throw;
        }
        if (argv.Contains("--smoke"))
        {
            // Launch, render for a few seconds, exit. CI uses this to bisect startup problems.
            _ = Task.Delay(4000).ContinueWith(_ => Environment.Exit(0));
            return;
        }
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
