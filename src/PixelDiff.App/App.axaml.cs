using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PixelDiff.App.Services;
using PixelDiff.App.ViewModels;
using PixelDiff.App.Views;
using PixelDiff.Capture.Windows;
using PixelDiff.Core.Baselines;
using PixelDiff.Core.Comparison;

namespace PixelDiff.App;

/// <summary>Application root. It builds the object graph by hand, since there are only a few dependencies.</summary>
public sealed class App : Application
{
    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var viewModel = new MainWindowViewModel(
                root => new FileSystemBaselineStore(root),
                new PixelImageComparer(),
                new WindowsScreenCapture(),
                new WindowFolderPicker(() => window),
                MainWindowViewModel.DefaultSuiteRoot());

            window.DataContext = viewModel;
            window.Closed += (_, _) => viewModel.Dispose();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
