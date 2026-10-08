using OpenCvSharp;
using PixelDiff.Capture.Windows;
using PixelDiff.Core.Baselines;

namespace PixelDiff.Cli;

/// <summary>
/// Replaces an approved image with a fresh capture of the same target, keeping the options and
/// note the baseline was created with.
/// </summary>
internal sealed class ApproveCommand
{
    private readonly FileSystemBaselineStore _store;
    private readonly IScreenCapture _capture;

    /// <summary>Creates the command over shared services.</summary>
    /// <param name="store">The suite that holds the baseline.</param>
    /// <param name="capture">The source of the fresh capture.</param>
    public ApproveCommand(FileSystemBaselineStore store, IScreenCapture capture)
    {
        _store = store;
        _capture = capture;
    }

    /// <summary>Recaptures and saves the named baseline.</summary>
    /// <param name="name">The baseline to approve.</param>
    /// <param name="output">Receives the confirmation line.</param>
    /// <returns>The process exit code.</returns>
    /// <exception cref="CliException">The baseline does not exist.</exception>
    public int Execute(string name, TextWriter output)
    {
        if (!_store.Exists(name))
        {
            throw CliException.Runtime($"Baseline '{name}' does not exist in {_store.RootDirectory}, so there is nothing to approve.");
        }

        BaselineDescriptor existing;
        using (Baseline current = _store.Load(name))
        {
            existing = current.Descriptor;
        }

        using Mat image = _capture.Capture(existing.Target);

        BaselineDescriptor updated = existing with
        {
            Width = image.Width,
            Height = image.Height,
            CapturedAt = DateTimeOffset.UtcNow,
        };

        _store.Save(image, updated);
        output.WriteLine($"Approved {name}: {updated.Width}x{updated.Height} from {updated.Target}");
        return ExitCodes.Success;
    }
}
