using Avalonia.Media.Imaging;
using OpenCvSharp;
using PixelDiff.Core.Comparison;

namespace PixelDiff.App.ViewModels;

/// <summary>
/// Everything one comparison produced, assembled off the UI thread and handed to the row in a
/// single step. The instance owns its images until a row takes them over.
/// </summary>
/// <param name="Result">The comparison verdict.</param>
/// <param name="Candidate">The raw capture, kept so it can be promoted to a baseline.</param>
/// <param name="CandidateBitmap">The capture prepared for display.</param>
/// <param name="DiffBitmap">The diff rendering, or null when the sizes differ and no diff exists.</param>
/// <param name="BaselineBitmap">The baseline prepared for display, or null when the row already has one.</param>
internal sealed record BaselineRunData(
    ComparisonResult Result,
    Mat Candidate,
    Bitmap CandidateBitmap,
    Bitmap? DiffBitmap,
    Bitmap? BaselineBitmap);
