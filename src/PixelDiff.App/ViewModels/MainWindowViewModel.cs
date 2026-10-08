using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using PixelDiff.App.Services;
using PixelDiff.Capture.Windows;
using PixelDiff.Core.Baselines;
using PixelDiff.Core.Comparison;
using PixelDiff.Core.Imaging;
using PixelDiff.Core.Models;

namespace PixelDiff.App.ViewModels;

/// <summary>
/// State and behaviour of the main window. Captures, comparisons and disk access run on the
/// thread pool, and every update to bound state happens after the await that returns to the
/// UI thread, so the interface never blocks and never sees a half written result.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly Func<string, IBaselineStore> _storeFactory;
    private readonly IImageComparer _comparer;
    private readonly IScreenCapture _capture;
    private readonly IFolderPicker _folderPicker;

    private IBaselineStore? _store;

    /// <summary>Wires the view model to its collaborators.</summary>
    /// <param name="storeFactory">Opens a baseline store for a suite directory.</param>
    /// <param name="comparer">Compares a baseline with a fresh capture.</param>
    /// <param name="capture">Source of screenshots and the window list.</param>
    /// <param name="folderPicker">Dialog used to choose the suite directory.</param>
    /// <param name="suiteRoot">Suite directory used at start up.</param>
    public MainWindowViewModel(
        Func<string, IBaselineStore> storeFactory,
        IImageComparer comparer,
        IScreenCapture capture,
        IFolderPicker folderPicker,
        string suiteRoot)
    {
        ArgumentNullException.ThrowIfNull(storeFactory);
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(folderPicker);
        ArgumentException.ThrowIfNullOrWhiteSpace(suiteRoot);

        _storeFactory = storeFactory;
        _comparer = comparer;
        _capture = capture;
        _folderPicker = folderPicker;
        SuiteRoot = suiteRoot;
    }

    /// <summary>The view modes offered by the selector.</summary>
    public static IReadOnlyList<ImageViewMode> ViewModes { get; } = Enum.GetValues<ImageViewMode>();

    /// <summary>Directory that holds the suite, by default a folder in the user's documents.</summary>
    [ObservableProperty]
    public partial string SuiteRoot { get; set; }

    /// <summary>Baselines found in the suite directory.</summary>
    public ObservableCollection<BaselineRowViewModel> Rows { get; } = [];

    /// <summary>The row shown in the viewer.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApproveCommand))]
    [NotifyCanExecuteChangedFor(nameof(RequestDeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    public partial BaselineRowViewModel? SelectedRow { get; set; }

    /// <summary>Windows that can be chosen as a capture target.</summary>
    public ObservableCollection<CapturableWindow> Windows { get; } = [];

    /// <summary>The window the next new baseline will capture.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CaptureBaselineCommand))]
    public partial CapturableWindow? SelectedWindow { get; set; }

    /// <summary>Name typed for the next new baseline.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NameError))]
    [NotifyPropertyChangedFor(nameof(HasNameError))]
    [NotifyCanExecuteChangedFor(nameof(CaptureBaselineCommand))]
    public partial string NewBaselineName { get; set; } = string.Empty;

    /// <summary>Which image the viewer shows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentImage))]
    public partial ImageViewMode ViewMode { get; set; } = ImageViewMode.Baseline;

    /// <summary>Message describing the last action or the last error.</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Ready.";

    /// <summary>True while a capture, run or disk operation is in progress.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BrowseSuiteCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshSuiteCommand))]
    [NotifyCanExecuteChangedFor(nameof(CaptureBaselineCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApproveCommand))]
    [NotifyCanExecuteChangedFor(nameof(RequestDeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>True after the delete button was pressed and before the user confirms or cancels.</summary>
    [ObservableProperty]
    public partial bool IsDeleteArmed { get; set; }

    /// <summary>Why the typed name cannot be used, or null when it can.</summary>
    public string? NameError
    {
        get
        {
            if (string.IsNullOrEmpty(NewBaselineName))
            {
                return null;
            }

            string? problem = BaselineName.Describe(NewBaselineName);
            if (problem is not null)
            {
                return problem;
            }

            return _store?.Exists(NewBaselineName) == true
                ? $"A baseline named '{NewBaselineName}' already exists."
                : null;
        }
    }

    /// <summary>True when <see cref="NameError"/> has something to show.</summary>
    public bool HasNameError => NameError is not null;

    /// <summary>The image for the selected row and view mode, or null when there is nothing to show.</summary>
    public Bitmap? CurrentImage => SelectedRow is null
        ? null
        : ViewMode switch
        {
            ImageViewMode.Baseline => SelectedRow.BaselineImage,
            ImageViewMode.Candidate => SelectedRow.CandidateImage,
            ImageViewMode.Diff => SelectedRow.DiffImage,
            _ => null,
        };

    /// <summary>True when the selected row has a comparison result to describe.</summary>
    public bool HasResult => SelectedRow?.Result is not null;

    /// <summary>Verdict of the last comparison of the selected row.</summary>
    public string OutcomeText => SelectedRow?.StatusText ?? string.Empty;

    /// <summary>Changed pixels against the pixels that took part in the comparison.</summary>
    public string ChangedPixelsText => SelectedRow?.Result is { } result
        ? string.Create(
            CultureInfo.CurrentCulture,
            $"{result.ChangedPixelCount:N0} of {result.ComparedPixelCount:N0} pixels")
        : string.Empty;

    /// <summary>Share of compared pixels that changed, as a percentage.</summary>
    public string ChangedRatioText => SelectedRow?.Result is { } result
        ? result.ChangedPixelRatio.ToString("P3", CultureInfo.CurrentCulture)
        : string.Empty;

    /// <summary>Number of separate areas the changes were grouped into.</summary>
    public string ChangedAreasText => SelectedRow?.Result is { } result
        ? result.ChangedRegions.Count.ToString(CultureInfo.CurrentCulture)
        : string.Empty;

    /// <summary>Extra explanation supplied by the comparer, such as the sizes in a mismatch.</summary>
    public string DetailText => SelectedRow?.Result?.Detail ?? string.Empty;

    /// <summary>Builds the default suite location under the user's documents folder.</summary>
    /// <returns>An absolute path that may not exist yet.</returns>
    public static string DefaultSuiteRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PixelDiffSuite");

    /// <summary>
    /// Loads the suite and the window list. Called once when the window opens, because doing disk
    /// and window enumeration work in the constructor would delay the first frame.
    /// </summary>
    /// <returns>A task that completes when both lists are populated.</returns>
    public async Task InitializeAsync()
    {
        await RunBusyAsync(async () =>
        {
            await OpenSuiteAsync(SuiteRoot);
            await LoadWindowsAsync();
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (BaselineRowViewModel row in Rows)
        {
            row.Dispose();
        }

        Rows.Clear();
    }

    partial void OnSelectedRowChanged(BaselineRowViewModel? value)
    {
        IsDeleteArmed = false;
        RaiseViewerChanged();

        if (value is not null && value.BaselineImage is null)
        {
            _ = LoadBaselineImageAsync(value);
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseSuite))]
    private async Task BrowseSuiteAsync()
    {
        string? chosen = await _folderPicker.PickFolderAsync(SuiteRoot);
        if (chosen is null)
        {
            return;
        }

        await RunBusyAsync(() => OpenSuiteAsync(chosen));
    }

    [RelayCommand(CanExecute = nameof(CanUseSuite))]
    private async Task RefreshSuiteAsync()
    {
        await RunBusyAsync(async () =>
        {
            await OpenSuiteAsync(SuiteRoot);
            await LoadWindowsAsync();
        });
    }

    [RelayCommand]
    private async Task RefreshWindowsAsync()
    {
        await RunBusyAsync(LoadWindowsAsync);
    }

    [RelayCommand(CanExecute = nameof(CanCapture))]
    private async Task CaptureBaselineAsync()
    {
        IBaselineStore store = RequireStore();
        string name = NewBaselineName;

        // The title is used as a substring pattern, so the exact title of the chosen window is the
        // most specific pattern available to describe it.
        CaptureTarget target = CaptureTarget.ForWindow(SelectedWindow!.Title);

        await RunBusyAsync(async () =>
        {
            await Task.Run(() =>
            {
                using Mat image = _capture.Capture(target);
                store.Save(
                    image,
                    new BaselineDescriptor
                    {
                        Name = name,
                        Target = target,
                        Width = image.Width,
                        Height = image.Height,
                        CapturedAt = DateTimeOffset.Now,
                        Options = ComparisonOptions.Default,
                    });
            });

            NewBaselineName = string.Empty;
            await ReloadRowsAsync(name);
            StatusMessage = $"Captured baseline '{name}'.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunSelected))]
    private async Task RunSelectedAsync()
    {
        BaselineRowViewModel row = SelectedRow!;

        await RunBusyAsync(async () =>
        {
            await RunRowAsync(row);
            StatusMessage = $"{row.Name}: {row.StatusText}.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunAll))]
    private async Task RunAllAsync()
    {
        BaselineRowViewModel[] snapshot = [.. Rows];

        await RunBusyAsync(async () =>
        {
            int passed = 0;
            List<string> failures = [];

            foreach (BaselineRowViewModel row in snapshot)
            {
                StatusMessage = $"Running {row.Name}...";
                try
                {
                    await RunRowAsync(row);
                    if (row.Result?.Passed == true)
                    {
                        passed++;
                    }
                }
                catch (Exception error) when (IsExpected(error))
                {
                    // One window that cannot be captured must not stop the rest of the suite.
                    failures.Add($"{row.Name}: {error.Message}");
                }
            }

            StatusMessage = failures.Count == 0
                ? $"Ran {snapshot.Length} baselines, {passed} matched."
                : $"Ran {snapshot.Length} baselines, {passed} matched, {failures.Count} could not run. {failures[0]}";
        });
    }

    [RelayCommand(CanExecute = nameof(CanApprove))]
    private async Task ApproveAsync()
    {
        IBaselineStore store = RequireStore();
        BaselineRowViewModel row = SelectedRow!;
        Mat candidate = row.Candidate!;

        await RunBusyAsync(async () =>
        {
            BaselineDescriptor descriptor = row.Descriptor with
            {
                Width = candidate.Width,
                Height = candidate.Height,
                CapturedAt = DateTimeOffset.Now,
            };

            await Task.Run(() => store.Save(candidate, descriptor));

            row.CompleteApproval(descriptor);
            ViewMode = ImageViewMode.Baseline;
            StatusMessage = $"Approved the latest capture as the baseline '{row.Name}'.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void RequestDelete()
    {
        IsDeleteArmed = true;
    }

    [RelayCommand]
    private void CancelDelete()
    {
        IsDeleteArmed = false;
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        IBaselineStore store = RequireStore();
        BaselineRowViewModel row = SelectedRow!;
        IsDeleteArmed = false;

        await RunBusyAsync(async () =>
        {
            bool removed = await Task.Run(() => store.Delete(row.Name));

            SelectedRow = null;
            Rows.Remove(row);
            row.Dispose();
            RunAllCommand.NotifyCanExecuteChanged();
            StatusMessage = removed
                ? $"Deleted baseline '{row.Name}'."
                : $"Baseline '{row.Name}' was already gone.";
        });
    }

    private static bool IsExpected(Exception error) => error is
        ScreenCaptureException
        or BaselineNotFoundException
        or InvalidDataException
        or IOException
        or UnauthorizedAccessException
        or ArgumentException;

    private bool CanUseSuite() => !IsBusy;

    private bool CanCapture() => !IsBusy && SelectedWindow is not null && NameError is null
        && !string.IsNullOrEmpty(NewBaselineName);

    private bool CanRunSelected() => !IsBusy && SelectedRow is not null;

    private bool CanRunAll() => !IsBusy && Rows.Count > 0;

    private bool CanApprove() => !IsBusy && SelectedRow?.HasCandidate == true;

    private bool CanDelete() => !IsBusy && SelectedRow is not null;

    /// <summary>
    /// Runs an operation with the busy flag held and turns the failures a user can cause or fix,
    /// such as a minimised window or a missing file, into a status message instead of a crash.
    /// </summary>
    private async Task RunBusyAsync(Func<Task> operation)
    {
        IsBusy = true;
        try
        {
            await operation();
        }
        catch (Exception error) when (IsExpected(error))
        {
            StatusMessage = error.Message;
        }
        finally
        {
            IsBusy = false;
            RaiseViewerChanged();
        }
    }

    private IBaselineStore RequireStore() =>
        _store ?? throw new InvalidOperationException("No suite is open.");

    private async Task OpenSuiteAsync(string root)
    {
        IBaselineStore store = await Task.Run(() => _storeFactory(root));
        _store = store;
        SuiteRoot = root;
        OnPropertyChanged(nameof(NameError));
        OnPropertyChanged(nameof(HasNameError));
        await ReloadRowsAsync(SelectedRow?.Name);
        StatusMessage = $"Opened {Rows.Count} baselines from {root}.";
    }

    /// <summary>
    /// Rebuilds the row list from disk. Rows that still exist are kept so the results of earlier
    /// runs survive a refresh, and rows whose baseline vanished are disposed.
    /// </summary>
    private async Task ReloadRowsAsync(string? selectName)
    {
        IBaselineStore store = RequireStore();

        // Only the descriptors are read here. The list shows nothing but metadata, and decoding
        // every approved image to reach it would make opening a suite cost its total image bytes.
        // The image of a row is loaded when that row is selected or run.
        List<BaselineDescriptor> descriptors = await Task.Run(() =>
        {
            List<BaselineDescriptor> found = [];
            foreach (string name in store.EnumerateNames())
            {
                try
                {
                    found.Add(store.LoadDescriptor(name));
                }
                catch (Exception error) when (error is BaselineNotFoundException or InvalidDataException)
                {
                    // A damaged or concurrently removed baseline is left out of the list instead
                    // of making the whole suite unusable.
                }
            }

            return found;
        });

        Dictionary<string, BaselineRowViewModel> existing = Rows.ToDictionary(
            row => row.Name,
            StringComparer.OrdinalIgnoreCase);
        List<BaselineRowViewModel> next = [];
        foreach (BaselineDescriptor descriptor in descriptors)
        {
            if (existing.Remove(descriptor.Name, out BaselineRowViewModel? kept))
            {
                kept.Descriptor = descriptor;
                next.Add(kept);
            }
            else
            {
                next.Add(new BaselineRowViewModel(descriptor));
            }
        }

        BaselineRowViewModel? selected = next.FirstOrDefault(
            row => string.Equals(row.Name, selectName, StringComparison.OrdinalIgnoreCase));
        SelectedRow = null;
        Rows.Clear();
        foreach (BaselineRowViewModel row in next)
        {
            Rows.Add(row);
        }

        foreach (BaselineRowViewModel orphan in existing.Values)
        {
            orphan.Dispose();
        }

        SelectedRow = selected;
        RunAllCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadWindowsAsync()
    {
        string? previous = SelectedWindow?.Title;
        IReadOnlyList<CapturableWindow> windows = await Task.Run(_capture.EnumerateWindows);

        Windows.Clear();
        foreach (CapturableWindow window in windows)
        {
            Windows.Add(window);
        }

        SelectedWindow = Windows.FirstOrDefault(window => window.Title == previous);
    }

    private async Task RunRowAsync(BaselineRowViewModel row)
    {
        IBaselineStore store = RequireStore();
        bool needsBaselineBitmap = row.BaselineImage is null;

        BaselineRunData run = await Task.Run(() => ExecuteRun(store, row.Name, needsBaselineBitmap));
        row.ApplyRun(run);

        if (ReferenceEquals(row, SelectedRow))
        {
            RaiseViewerChanged();
        }
    }

    /// <summary>
    /// Captures and compares on a pool thread. Every image created here is released again when
    /// anything fails part way, so a failed run cannot leak a screenshot.
    /// </summary>
    private BaselineRunData ExecuteRun(IBaselineStore store, string name, bool needsBaselineBitmap)
    {
        using Baseline baseline = store.Load(name);
        Mat candidate = _capture.Capture(baseline.Descriptor.Target);
        Bitmap? candidateBitmap = null;
        Bitmap? diffBitmap = null;
        Bitmap? baselineBitmap = null;

        try
        {
            ComparisonResult result = _comparer.Compare(baseline.Image, candidate, baseline.Descriptor.Options);

            // A size mismatch has no pixel to pixel correspondence, so there is no diff to draw.
            if (result.Outcome != ComparisonOutcome.SizeMismatch)
            {
                using Mat diff = DiffVisualizer.Render(baseline.Image, candidate, baseline.Descriptor.Options);
                diffBitmap = MatImageSource.ToBitmap(diff);
            }

            candidateBitmap = MatImageSource.ToBitmap(candidate);
            if (needsBaselineBitmap)
            {
                baselineBitmap = MatImageSource.ToBitmap(baseline.Image);
            }

            return new BaselineRunData(result, candidate, candidateBitmap, diffBitmap, baselineBitmap);
        }
        catch
        {
            candidate.Dispose();
            candidateBitmap?.Dispose();
            diffBitmap?.Dispose();
            baselineBitmap?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Loads the approved image for a row that was just selected. It is started without being
    /// awaited, so it reports its own expected failures instead of leaving them unobserved.
    /// </summary>
    private async Task LoadBaselineImageAsync(BaselineRowViewModel row)
    {
        IBaselineStore? store = _store;
        if (store is null)
        {
            return;
        }

        try
        {
            Bitmap bitmap = await Task.Run(() =>
            {
                using Baseline baseline = store.Load(row.Name);
                return MatImageSource.ToBitmap(baseline.Image);
            });

            row.SetBaselineImage(bitmap);
            if (ReferenceEquals(row, SelectedRow))
            {
                RaiseViewerChanged();
            }
        }
        catch (Exception error) when (IsExpected(error))
        {
            StatusMessage = error.Message;
        }
    }

    private void RaiseViewerChanged()
    {
        OnPropertyChanged(nameof(CurrentImage));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(OutcomeText));
        OnPropertyChanged(nameof(ChangedPixelsText));
        OnPropertyChanged(nameof(ChangedRatioText));
        OnPropertyChanged(nameof(ChangedAreasText));
        OnPropertyChanged(nameof(DetailText));
        ApproveCommand.NotifyCanExecuteChanged();
    }
}
