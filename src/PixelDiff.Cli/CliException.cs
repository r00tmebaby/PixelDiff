namespace PixelDiff.Cli;

/// <summary>
/// A failure the tool anticipates and reports as a single line. The exit code is carried with the
/// exception so the entry point does not have to infer it from the message.
/// </summary>
internal sealed class CliException : Exception
{
    /// <summary>Creates an exception that maps to the given exit code.</summary>
    /// <param name="message">One line description shown on standard error.</param>
    /// <param name="exitCode">The process exit code to return.</param>
    public CliException(string message, int exitCode)
        : base(message)
    {
        ExitCode = exitCode;
    }

    /// <summary>The process exit code this failure maps to.</summary>
    public int ExitCode { get; }

    /// <summary>Creates a failure for incorrect or incomplete arguments.</summary>
    /// <param name="message">What was wrong with the arguments.</param>
    /// <returns>The exception to throw.</returns>
    public static CliException Usage(string message) => new(message, ExitCodes.UsageError);

    /// <summary>Creates a failure for a problem found while executing a command.</summary>
    /// <param name="message">What went wrong.</param>
    /// <returns>The exception to throw.</returns>
    public static CliException Runtime(string message) => new(message, ExitCodes.RuntimeError);
}
