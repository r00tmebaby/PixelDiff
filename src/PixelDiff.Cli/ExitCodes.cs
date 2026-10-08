namespace PixelDiff.Cli;

/// <summary>
/// Process exit codes. Build servers branch on these values, so they are part of the public
/// contract of the tool and are repeated in the help text.
/// </summary>
internal static class ExitCodes
{
    /// <summary>Every comparison matched, or a command that does not compare succeeded.</summary>
    public const int Success = 0;

    /// <summary>At least one comparison did not match its baseline.</summary>
    public const int ComparisonFailed = 1;

    /// <summary>The arguments were wrong or incomplete.</summary>
    public const int UsageError = 2;

    /// <summary>The command failed while running, for example a window could not be found.</summary>
    public const int RuntimeError = 3;
}
