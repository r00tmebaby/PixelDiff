using Avalonia;

namespace PixelDiff.App;

/// <summary>Process entry point.</summary>
public static class Program
{
    /// <summary>Starts the desktop application.</summary>
    /// <param name="args">Command line arguments, passed through to the Avalonia lifetime.</param>
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Configures the application. Also used by the designer.</summary>
    /// <returns>The configured builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
