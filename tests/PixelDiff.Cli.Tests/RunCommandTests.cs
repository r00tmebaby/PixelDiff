using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;
using PixelDiff.Core.Baselines;
using PixelDiff.Core.Comparison;
using PixelDiff.Core.Models;
using Xunit;

namespace PixelDiff.Cli.Tests;

public sealed class RunCommandTests : IDisposable
{
    // The runner writes enum values as names, so reading the report back needs the same converter.
    private static readonly JsonSerializerOptions ReaderOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _root = Path.Combine(Path.GetTempPath(), "PixelDiffCliTests-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemBaselineStore _store;
    private readonly StubScreenCapture _capture = new();
    private readonly StringWriter _output = new();
    private readonly Dictionary<string, CaptureTarget> _targets = new(StringComparer.Ordinal);
    private readonly string _runDirectory;

    public RunCommandTests()
    {
        _store = new FileSystemBaselineStore(_root);
        _runDirectory = Path.Combine(_root, "out");
    }

    public void Dispose()
    {
        _capture.Dispose();
        _output.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void EveryBaselineMatchingExitsZeroAndWritesAReport()
    {
        AddMatching("alpha");
        AddMatching("bravo");

        Assert.Equal(ExitCodes.Success, Run());

        RunReport report = ReadReport();
        Assert.Equal(2, report.Total);
        Assert.Equal(2, report.Matched);
        Assert.Equal(0, report.Errored);
        Assert.All(report.Entries, entry => Assert.Equal(RunOutcome.Match, entry.Outcome));
    }

    [Fact]
    public void ABaselineThatCannotBeCapturedDoesNotStopTheOnesAfterIt()
    {
        AddMatching("alpha");
        AddUncapturable("bravo", "Window 0x0 is minimised and cannot be captured.");
        AddMatching("charlie");

        int exitCode = Run();

        RunReport report = ReadReport();
        Assert.Equal(["alpha", "bravo", "charlie"], report.Entries.Select(entry => entry.Name));
        Assert.Equal(
            [RunOutcome.Match, RunOutcome.Error, RunOutcome.Match],
            report.Entries.Select(entry => entry.Outcome));
        Assert.Equal(3, _capture.CaptureCount);
        Assert.Equal(ExitCodes.RuntimeError, exitCode);
    }

    [Fact]
    public void AFailureIsRecordedAgainstItsBaselineWithTheReasonAndNoImages()
    {
        AddUncapturable("alpha", "No visible window has a title containing 'Sign in'.");

        Run();

        RunReportEntry entry = Assert.Single(ReadReport().Entries);
        Assert.Equal(RunOutcome.Error, entry.Outcome);
        Assert.Equal("No visible window has a title containing 'Sign in'.", entry.Detail);
        Assert.Null(entry.CandidateImage);
        Assert.Null(entry.DiffImage);
        Assert.Equal(0, entry.ChangedPixelCount);
        Assert.Equal(0, entry.ComparedPixelCount);
        Assert.Empty(entry.ChangedRegions);
    }

    [Fact]
    public void ADamagedDescriptorIsRecordedRatherThanAbortingTheRun()
    {
        AddMatching("alpha");
        AddMatching("bravo");
        File.WriteAllText(_store.DescriptorPath("alpha"), "{ this is not json");

        int exitCode = Run();

        RunReport report = ReadReport();
        Assert.Equal(RunOutcome.Error, report.Entries[0].Outcome);
        Assert.Equal(RunOutcome.Match, report.Entries[1].Outcome);
        Assert.Equal(ExitCodes.RuntimeError, exitCode);
    }

    [Fact]
    public void AMismatchOutranksABaselineThatCouldNotRunWhenChoosingTheExitCode()
    {
        AddDiffering("alpha");
        AddUncapturable("bravo", "gone");

        Assert.Equal(ExitCodes.ComparisonFailed, Run());

        RunReport report = ReadReport();
        Assert.Equal(2, report.Failed);
        Assert.Equal(1, report.Errored);
    }

    [Fact]
    public void OnlyFailuresToRunGiveTheRuntimeExitCode()
    {
        AddMatching("alpha");
        AddUncapturable("bravo", "gone");

        Assert.Equal(ExitCodes.RuntimeError, Run());
    }

    [Fact]
    public void AMismatchWritesTheCandidateAndDiffAndExitsOne()
    {
        AddDiffering("alpha");

        Assert.Equal(ExitCodes.ComparisonFailed, Run());

        RunReportEntry entry = Assert.Single(ReadReport().Entries);
        Assert.Equal(RunOutcome.Differs, entry.Outcome);
        Assert.Equal("alpha.candidate.png", entry.CandidateImage);
        Assert.Equal("alpha.diff.png", entry.DiffImage);
        Assert.True(File.Exists(Path.Combine(_runDirectory, "alpha.candidate.png")));
        Assert.True(File.Exists(Path.Combine(_runDirectory, "alpha.diff.png")));
    }

    [Fact]
    public void ASizeMismatchWritesTheCandidateButNoDiff()
    {
        using Mat stored = TestImage(40, 30, 10);
        using Mat captured = TestImage(44, 30, 10);
        Save("alpha", stored, captured);

        Assert.Equal(ExitCodes.ComparisonFailed, Run());

        RunReportEntry entry = Assert.Single(ReadReport().Entries);
        Assert.Equal(RunOutcome.SizeMismatch, entry.Outcome);
        Assert.Equal("alpha.candidate.png", entry.CandidateImage);
        Assert.Null(entry.DiffImage);
        Assert.False(File.Exists(Path.Combine(_runDirectory, "alpha.diff.png")));
    }

    [Fact]
    public void TheSummaryLineNamesTheBaselinesThatCouldNotRun()
    {
        AddMatching("alpha");
        AddUncapturable("bravo", "gone");

        Run();

        string text = _output.ToString();
        Assert.Contains("FAIL bravo: could not run. gone", text, StringComparison.Ordinal);
        Assert.Contains("1 could not run", text, StringComparison.Ordinal);
    }

    private static Mat TestImage(int width, int height, byte level) =>
        new(height, width, MatType.CV_8UC3, new Scalar(level, level, level));

    private int Run() =>
        new RunCommand(_store, new PixelImageComparer(), _capture).Execute(null, _runDirectory, _output);

    private RunReport ReadReport()
    {
        string path = Path.Combine(_runDirectory, "report.json");
        Assert.True(File.Exists(path), "the run must write a report even when a baseline fails");

        RunReport? report = JsonSerializer.Deserialize<RunReport>(File.ReadAllBytes(path), ReaderOptions);
        Assert.NotNull(report);
        return report;
    }

    private void AddMatching(string name)
    {
        using Mat image = TestImage(40, 30, 10);
        Save(name, image, image);
    }

    private void AddDiffering(string name)
    {
        using Mat stored = TestImage(40, 30, 10);
        using Mat captured = TestImage(40, 30, 200);
        Save(name, stored, captured);
    }

    private void AddUncapturable(string name, string message)
    {
        using Mat image = TestImage(40, 30, 10);
        CaptureTarget target = TargetFor(name);
        _store.Save(image, Describe(name, target, image));
        _capture.Fails(target, message);
    }

    private void Save(string name, Mat stored, Mat captured)
    {
        CaptureTarget target = TargetFor(name);
        _store.Save(stored, Describe(name, target, stored));
        _capture.Returns(target, captured);
    }

    // A distinct region per baseline, so the stub can answer each one differently.
    private CaptureTarget TargetFor(string name)
    {
        if (!_targets.TryGetValue(name, out CaptureTarget? target))
        {
            target = CaptureTarget.ForRegion(new CaptureRegion(_targets.Count * 100, 0, 40, 30));
            _targets.Add(name, target);
        }

        return target;
    }

    private static BaselineDescriptor Describe(string name, CaptureTarget target, Mat image) => new()
    {
        Name = name,
        Target = target,
        Width = image.Width,
        Height = image.Height,
        CapturedAt = new DateTimeOffset(2026, 3, 14, 9, 26, 53, TimeSpan.Zero),
        Options = new ComparisonOptions(),
    };
}
