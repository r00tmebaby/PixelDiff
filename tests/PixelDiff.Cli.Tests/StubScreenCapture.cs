using OpenCvSharp;
using PixelDiff.Capture.Windows;
using PixelDiff.Core.Models;

namespace PixelDiff.Cli.Tests;

/// <summary>
/// Stands in for the real capture so the runner can be driven without a desktop session. Each
/// target either yields a canned image or throws, which is how a missing or minimised window
/// reaches the runner in practice. The stub keeps its own copy of every canned image, so a test
/// may dispose the image it handed over.
/// </summary>
internal sealed class StubScreenCapture : IScreenCapture, IDisposable
{
    private readonly Dictionary<string, Mat> _images = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _failures = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public CaptureRegion VirtualScreenBounds => new(0, 0, 1920, 1080);

    /// <summary>Number of times a capture was requested, across every target.</summary>
    public int CaptureCount { get; private set; }

    /// <summary>Makes a target yield a fresh copy of an image on every capture.</summary>
    /// <param name="target">The target to answer for.</param>
    /// <param name="image">The image to copy. It stays owned by the caller.</param>
    public void Returns(CaptureTarget target, Mat image) => _images[Key(target)] = image.Clone();

    /// <summary>Makes a target fail the way an absent or minimised window does.</summary>
    /// <param name="target">The target to answer for.</param>
    /// <param name="message">The failure message the report should carry.</param>
    public void Fails(CaptureTarget target, string message) => _failures[Key(target)] = message;

    /// <inheritdoc />
    public IReadOnlyList<CapturableWindow> EnumerateWindows() => [];

    /// <inheritdoc />
    public Mat CaptureScreenRegion(CaptureRegion region) => Capture(CaptureTarget.ForRegion(region));

    /// <inheritdoc />
    public Mat CaptureWindow(nint windowHandle, CaptureRegion? crop = null) =>
        throw new NotSupportedException("The stub is driven through capture targets.");

    /// <inheritdoc />
    public Mat Capture(CaptureTarget target)
    {
        CaptureCount++;
        string key = Key(target);

        if (_failures.TryGetValue(key, out string? message))
        {
            throw new ScreenCaptureException(message);
        }

        return _images.TryGetValue(key, out Mat? image)
            ? image.Clone()
            : throw new ScreenCaptureException($"No stub response for {target}.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (Mat image in _images.Values)
        {
            image.Dispose();
        }

        _images.Clear();
    }

    private static string Key(CaptureTarget target) =>
        $"{target.Kind}|{target.WindowTitlePattern}|{target.Region}";
}
