namespace PixelDiff.App.Services;

/// <summary>
/// Asks the user to choose a directory. It sits behind an interface so the view model does not
/// depend on a window and can be driven without a display.
/// </summary>
public interface IFolderPicker
{
    /// <summary>Shows the platform folder dialog.</summary>
    /// <param name="startDirectory">Directory the dialog opens in, when it exists.</param>
    /// <returns>The chosen absolute path, or null when the user cancelled.</returns>
    Task<string?> PickFolderAsync(string? startDirectory);
}
