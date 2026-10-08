using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace PixelDiff.App.Services;

/// <summary>
/// Folder picker backed by the storage provider of a window. The window is resolved lazily
/// because the picker is created before the window it belongs to has been shown.
/// </summary>
public sealed class WindowFolderPicker : IFolderPicker
{
    private readonly Func<TopLevel?> _owner;

    /// <summary>Creates a picker that parents its dialog on the supplied window.</summary>
    /// <param name="owner">Returns the window that owns the dialog, or null when none is available.</param>
    public WindowFolderPicker(Func<TopLevel?> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <inheritdoc />
    public async Task<string?> PickFolderAsync(string? startDirectory)
    {
        IStorageProvider? storage = _owner()?.StorageProvider;
        if (storage is null || !storage.CanPickFolder)
        {
            return null;
        }

        IStorageFolder? start = string.IsNullOrWhiteSpace(startDirectory) || !Directory.Exists(startDirectory)
            ? null
            : await storage.TryGetFolderFromPathAsync(startDirectory);

        IReadOnlyList<IStorageFolder> picked = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose the suite folder",
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });

        return picked.Count == 0 ? null : picked[0].TryGetLocalPath();
    }
}
