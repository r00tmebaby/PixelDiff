using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;
using PixelDiff.Core.Baselines;
using PixelDiff.Core.Comparison;

namespace PixelDiff.App.ViewModels;

/// <summary>
/// One baseline in the suite list. Besides the descriptor read from disk it owns the images of
/// the latest run, which live only in memory until the baseline is approved or the app closes.
/// </summary>
public sealed partial class BaselineRowViewModel : ObservableObject, IDisposable
{
    private static readonly IBrush MatchBrush = new SolidColorBrush(Color.Parse("#3FB950"));
    private static readonly IBrush DiffersBrush = new SolidColorBrush(Color.Parse("#F85149"));
    private static readonly IBrush SizeMismatchBrush = new SolidColorBrush(Color.Parse("#D29922"));
    private static readonly IBrush NotRunBrush = new SolidColorBrush(Color.Parse("#6E7681"));

    private Mat? _candidate;
    private bool _disposed;

    /// <summary>Creates a row for a baseline that has not been compared yet.</summary>
    /// <param name="descriptor">Metadata of the baseline as stored on disk.</param>
    public BaselineRowViewModel(BaselineDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Descriptor = descriptor;
    }

    /// <summary>Metadata of the baseline as stored on disk.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetDescription))]
    public partial BaselineDescriptor Descriptor { get; set; }

    /// <summary>Outcome of the last run, or null when the baseline has not been run in this session.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusBrush))]
    public partial ComparisonResult? Result { get; set; }

    /// <summary>The baseline identifier, which is also its file name.</summary>
    public string Name => Descriptor.Name;

    /// <summary>One line describing what is captured, with the pixel size of the baseline.</summary>
    public string TargetDescription =>
        string.Create(CultureInfo.CurrentCulture, $"{Descriptor.Target}  {Descriptor.Width}x{Descriptor.Height}");

    /// <summary>Short status word for the list.</summary>
    public string StatusText => Result?.Outcome switch
    {
        ComparisonOutcome.Match => "Match",
        ComparisonOutcome.Differs => "Differs",
        ComparisonOutcome.SizeMismatch => "Size mismatch",
        _ => "Not run",
    };

    /// <summary>Colour paired with <see cref="StatusText"/>.</summary>
    public IBrush StatusBrush => Result?.Outcome switch
    {
        ComparisonOutcome.Match => MatchBrush,
        ComparisonOutcome.Differs => DiffersBrush,
        ComparisonOutcome.SizeMismatch => SizeMismatchBrush,
        _ => NotRunBrush,
    };

    /// <summary>The approved image prepared for display, or null until it has been loaded.</summary>
    public Bitmap? BaselineImage { get; private set; }

    /// <summary>The latest capture prepared for display, or null before the first run.</summary>
    public Bitmap? CandidateImage { get; private set; }

    /// <summary>The diff rendering of the latest run, or null when none exists.</summary>
    public Bitmap? DiffImage { get; private set; }

    /// <summary>True when a capture exists that could replace the baseline.</summary>
    public bool HasCandidate => _candidate is not null;

    /// <summary>The raw latest capture, owned by this row.</summary>
    internal Mat? Candidate => _candidate;

    /// <summary>Stores the display bitmap of the approved image, taking ownership of it.</summary>
    /// <param name="bitmap">The bitmap to adopt.</param>
    internal void SetBaselineImage(Bitmap bitmap)
    {
        if (_disposed || BaselineImage is not null)
        {
            // The row was removed or another load won the race, so this copy is surplus.
            bitmap.Dispose();
            return;
        }

        BaselineImage = bitmap;
    }

    /// <summary>Replaces the results of the previous run with a new one, taking ownership of its images.</summary>
    /// <param name="run">The finished run.</param>
    internal void ApplyRun(BaselineRunData run)
    {
        if (_disposed)
        {
            DisposeRun(run);
            return;
        }

        ReleaseRunImages();

        _candidate = run.Candidate;
        CandidateImage = run.CandidateBitmap;
        DiffImage = run.DiffBitmap;
        if (run.BaselineBitmap is not null)
        {
            SetBaselineImage(run.BaselineBitmap);
        }

        Result = run.Result;
        OnPropertyChanged(nameof(HasCandidate));
    }

    /// <summary>
    /// Makes the latest capture the displayed baseline once it has been written to disk. The status
    /// returns to not run because no comparison has been made against the new baseline yet.
    /// </summary>
    /// <param name="descriptor">The descriptor that was saved alongside the promoted image.</param>
    internal void CompleteApproval(BaselineDescriptor descriptor)
    {
        Bitmap? promoted = CandidateImage;
        CandidateImage = null;
        BaselineImage?.Dispose();
        BaselineImage = promoted;

        ReleaseRunImages();
        Descriptor = descriptor;
        Result = null;
        OnPropertyChanged(nameof(HasCandidate));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ReleaseRunImages();
        BaselineImage?.Dispose();
        BaselineImage = null;
    }

    /// <summary>Frees everything produced by a run except the baseline bitmap.</summary>
    private void ReleaseRunImages()
    {
        _candidate?.Dispose();
        _candidate = null;
        CandidateImage?.Dispose();
        CandidateImage = null;
        DiffImage?.Dispose();
        DiffImage = null;
    }

    private static void DisposeRun(BaselineRunData run)
    {
        run.Candidate.Dispose();
        run.CandidateBitmap.Dispose();
        run.DiffBitmap?.Dispose();
        run.BaselineBitmap?.Dispose();
    }
}
