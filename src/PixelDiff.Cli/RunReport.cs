using System.Text.Json;
using System.Text.Json.Serialization;
using PixelDiff.Core.Comparison;
using PixelDiff.Core.Models;

namespace PixelDiff.Cli;

/// <summary>
/// The machine readable result of one run, written as <c>report.json</c>. A build server reads
/// this file to decide what to publish, so property names and value shapes are a stable contract.
/// </summary>
public sealed class RunReport
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>When the run began, in UTC.</summary>
    public required DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>Absolute path of the suite that was run.</summary>
    public required string SuiteRoot { get; init; }

    /// <summary>Number of baselines compared.</summary>
    public int Total => Entries.Count;

    /// <summary>Number of baselines whose outcome was a match.</summary>
    public int Matched => Entries.Count(entry => entry.Outcome == RunOutcome.Match);

    /// <summary>Number of baselines that could not be compared at all.</summary>
    public int Errored => Entries.Count(entry => entry.Outcome == RunOutcome.Error);

    /// <summary>Number of baselines whose outcome was anything other than a match.</summary>
    public int Failed => Total - Matched;

    /// <summary>One entry per baseline, in the order they were compared.</summary>
    public required IReadOnlyList<RunReportEntry> Entries { get; init; }

    /// <summary>Serialises the report to indented JSON with enums written as strings.</summary>
    /// <returns>The JSON text.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);
}

/// <summary>The comparison outcome for a single baseline within a <see cref="RunReport"/>.</summary>
public sealed class RunReportEntry
{
    /// <summary>The baseline name.</summary>
    public required string Name { get; init; }

    /// <summary>The verdict for this baseline.</summary>
    public required RunOutcome Outcome { get; init; }

    /// <summary>Pixels that moved by more than the configured tolerance.</summary>
    public required long ChangedPixelCount { get; init; }

    /// <summary>Pixels examined, which excludes ignored rectangles.</summary>
    public required long ComparedPixelCount { get; init; }

    /// <summary>Pixels excluded by ignore rectangles.</summary>
    public required long IgnoredPixelCount { get; init; }

    /// <summary>Changed pixels as a fraction of those compared.</summary>
    public required double ChangedPixelRatio { get; init; }

    /// <summary>Bounding rectangles of the changed areas, largest first.</summary>
    public required IReadOnlyList<CaptureRegion> ChangedRegions { get; init; }

    /// <summary>
    /// Explanation for failures that need more than a number, otherwise absent. For
    /// <see cref="RunOutcome.Error"/> this carries the reason the baseline could not be compared.
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>Path of the candidate image relative to the report, or absent for a match.</summary>
    public string? CandidateImage { get; init; }

    /// <summary>Path of the diff image relative to the report, or absent when none could be drawn.</summary>
    public string? DiffImage { get; init; }
}
