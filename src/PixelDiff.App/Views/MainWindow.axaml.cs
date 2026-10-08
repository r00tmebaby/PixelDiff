using Avalonia.Controls;
using PixelDiff.App.ViewModels;

namespace PixelDiff.App.Views;

/// <summary>Main window. All behaviour lives in the view model; this class only triggers initial loading.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Creates the window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}
