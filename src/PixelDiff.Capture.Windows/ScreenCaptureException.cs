namespace PixelDiff.Capture.Windows;

/// <summary>
/// Raised when a capture fails for a reason that belongs to the platform rather than to the
/// arguments, such as a window that closed while it was being captured or a device context the
/// system refused to hand out. Argument mistakes still surface as the usual argument exceptions.
/// </summary>
public sealed class ScreenCaptureException : Exception
{
    /// <summary>Initialises the exception with no message.</summary>
    public ScreenCaptureException()
    {
    }

    /// <summary>Initialises the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public ScreenCaptureException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises the exception with a message and the underlying cause.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The cause.</param>
    public ScreenCaptureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
