using OpenCvSharp;
using PixelDiff.Capture.Windows;
using PixelDiff.Core.Baselines;
using PixelDiff.Core.Comparison;
using PixelDiff.Core.Imaging;

namespace PixelDiff.Cli;

/// <summary>
/// Recaptures the target of each baseline, compares it with the approved image and writes the
/// evidence needed to review a failure.
/// </summary>
internal sealed class RunCommand
{
    private const string ReportFileName = "report.json";

    private readonly FileSystemBaselineStore _store;
    private readonly IImageComparer _comparer;
    private readonly IScreenCapture _capture;

    /// <summary>Creates the command over shared services.</summary>
    /// <param name="store">The suite to read baselines from.</param>
    /// <param name="comparer">The comparison strategy.</param>
    /// <param name="capture">The source of fresh captures.</param>
    public RunCommand(FileSystemBaselineStore store, IImageComparer comparer, IScreenCapture capture)
    {
        _store = store;
        _comparer = comparer;
        _capture = capture;
    }

    /// <summary>Runs the comparisons and writes output.</summary>
    /// <param name="name">A single baseline to run, or null for all of them.</param>
    /// <param name="outputDirectory">Where artifacts are written, or null to create a run directory.</param>
    /// <param name="output">Receives the per baseline lines and the summary.</param>
    /// <returns>The process exit code.</returns>
    public int Execute(string? name, string? outputDirectory, TextWriter output)
    {
        List<string> names = SelectNames(name);

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        string runDirectory = outputDirectory is null
            ? _store.CreateRunDirectory(startedAt)
            : Directory.CreateDirectory(outputDirectory).FullName;

        var entries = new List<RunReportEntry>(names.Count);
        foreach (string baselineName in names)
        {
            RunReportEntry entry = CompareOrRecordFailure(baselineName, runDirectory);
            entries.Add(entry);

            string verdict = entry.Outcome == RunOutcome.Match ? "PASS" : "FAIL";
            output.WriteLine($"{verdict} {entry.Name}: {Describe(entry)}");
        }

        var report = new RunReport { StartedAtUtc = startedAt, SuiteRoot = _store.RootDirectory, Entries = entries };
        string reportPath = Path.Combine(runDirectory, ReportFileName);
        File.WriteAllText(reportPath, report.ToJson());

        output.WriteLine(report.Errored == 0
            ? $"{report.Matched} of {report.Total} matched, {report.Failed} failed. Report: {reportPath}"
            : $"{report.Matched} of {report.Total} matched, {report.Failed} failed, {report.Errored} could not run. Report: {reportPath}");

        return ExitCodeFor(report);
    }

    /// <summary>
    /// Chooses the exit code once every baseline has been attempted. A comparison that did not
    /// match outranks a baseline that could not be run, because a visual change is the finding a
    /// build server is asked to act on and the run directory it then publishes also carries the
    /// baselines that failed outright. Success is reported only when neither happened.
    /// </summary>
    private static int ExitCodeFor(RunReport report)
    {
        int mismatched = report.Failed - report.Errored;
        if (mismatched > 0)
        {
            return ExitCodes.ComparisonFailed;
        }

        return report.Errored > 0 ? ExitCodes.RuntimeError : ExitCodes.Success;
    }

    private static string Describe(RunReportEntry entry) => entry.Outcome switch
    {
        RunOutcome.Match => "match",
        RunOutcome.SizeMismatch => $"size mismatch. {entry.Detail}",
        RunOutcome.Error => $"could not run. {entry.Detail}",
        _ => $"{entry.ChangedPixelCount} of {entry.ComparedPixelCount} pixels changed in {entry.ChangedRegions.Count} area(s)",
    };

    /// <summary>Maps a comparer verdict onto the wider vocabulary the report uses.</summary>
    private static RunOutcome ToRunOutcome(ComparisonOutcome outcome) => outcome switch
    {
        ComparisonOutcome.Match => RunOutcome.Match,
        ComparisonOutcome.SizeMismatch => RunOutcome.SizeMismatch,
        _ => RunOutcome.Differs,
    };

    /// <summary>
    /// Failures that belong to one baseline, such as a window that is not open or a descriptor
    /// that will not parse. They are recorded against that baseline so the rest of the suite still
    /// runs and the run still produces a report.
    /// </summary>
    private static bool BelongsToOneBaseline(Exception error) => error is
        ScreenCaptureException
        or BaselineNotFoundException
        or InvalidDataException
        or IOException
        or UnauthorizedAccessException
        or OpenCVException
        or ArgumentException
        or InvalidOperationException;

    private List<string> SelectNames(string? name)
    {
        if (name is null)
        {
            return [.. _store.EnumerateNames()];
        }

        if (!_store.Exists(name))
        {
            throw CliException.Runtime($"Baseline '{name}' does not exist in {_store.RootDirectory}.");
        }

        return [name];
    }

    private RunReportEntry CompareOrRecordFailure(string name, string runDirectory)
    {
        try
        {
            return Compare(name, runDirectory);
        }
        catch (Exception error) when (BelongsToOneBaseline(error))
        {
            return new RunReportEntry
            {
                Name = name,
                Outcome = RunOutcome.Error,
                ChangedPixelCount = 0,
                ComparedPixelCount = 0,
                IgnoredPixelCount = 0,
                ChangedPixelRatio = 0,
                ChangedRegions = [],
                Detail = error.Message,
            };
        }
    }

    private RunReportEntry Compare(string name, string runDirectory)
    {
        using Baseline baseline = _store.Load(name);
        using Mat candidate = _capture.Capture(baseline.Descriptor.Target);

        ComparisonResult result = _comparer.Compare(baseline.Image, candidate, baseline.Descriptor.Options);

        string? candidateFile = null;
        string? diffFile = null;

        if (!result.Passed)
        {
            candidateFile = $"{name}.candidate.png";
            File.WriteAllBytes(Path.Combine(runDirectory, candidateFile), ImageCodec.EncodePng(candidate));

            // A diff is only meaningful between images of equal size, so a size mismatch leaves just the candidate.
            if (result.Outcome != ComparisonOutcome.SizeMismatch)
            {
                using Mat diff = DiffVisualizer.Render(baseline.Image, candidate, baseline.Descriptor.Options);
                diffFile = $"{name}.diff.png";
                File.WriteAllBytes(Path.Combine(runDirectory, diffFile), ImageCodec.EncodePng(diff));
            }
        }

        return new RunReportEntry
        {
            Name = name,
            Outcome = ToRunOutcome(result.Outcome),
            ChangedPixelCount = result.ChangedPixelCount,
            ComparedPixelCount = result.ComparedPixelCount,
            IgnoredPixelCount = result.IgnoredPixelCount,
            ChangedPixelRatio = result.ChangedPixelRatio,
            ChangedRegions = result.ChangedRegions,
            Detail = result.Detail,
            CandidateImage = candidateFile,
            DiffImage = diffFile,
        };
    }
}
