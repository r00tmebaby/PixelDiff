using PixelDiff.Core.Baselines;

namespace PixelDiff.Cli;

/// <summary>Prints every baseline in the suite with its recorded size and capture target.</summary>
internal sealed class ListCommand
{
    private readonly FileSystemBaselineStore _store;

    /// <summary>Creates the command over a suite.</summary>
    /// <param name="store">The suite to list.</param>
    public ListCommand(FileSystemBaselineStore store)
    {
        _store = store;
    }

    /// <summary>Writes one line per baseline.</summary>
    /// <param name="output">Receives the listing.</param>
    /// <returns>The process exit code.</returns>
    public int Execute(TextWriter output)
    {
        IReadOnlyList<string> names = _store.EnumerateNames();

        foreach (string name in names)
        {
            using Baseline baseline = _store.Load(name);
            BaselineDescriptor descriptor = baseline.Descriptor;
            output.WriteLine($"{name}\t{descriptor.Width}x{descriptor.Height}\t{descriptor.Target}");
        }

        output.WriteLine($"{names.Count} baseline(s) in {_store.RootDirectory}");
        return ExitCodes.Success;
    }
}
