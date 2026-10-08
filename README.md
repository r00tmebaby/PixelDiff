# PixelDiff

PixelDiff is a visual regression testing tool for Windows. You point it at a window or a rectangle of the screen, capture it once and approve that image as the baseline. Later you recapture and compare, so an unintended change to the interface is caught and can fail a build.

It is meant for people testing the look of a desktop application, a web page in a browser window or a dialog. It is not a screen recorder or a media tool. It compares still images of deterministic interface rendering.

There are two executables built from the same libraries: a desktop application for capturing, reviewing and approving baselines, and a command line runner (`pixeldiff`) for unattended runs on a build server.

## Requirements

- Windows. Capture uses GDI and DWM, and the projects that reference it target `net10.0-windows`.
- The .NET SDK pinned in `global.json`, currently 10.0.401. `rollForward` is set to `latestFeature`, so a newer 10.0 feature band is accepted.

The pin exists because every project targets `net10.0`, and because `global.json` also selects the Microsoft.Testing.Platform runner for `dotnet test`, which is what the xUnit v3 test project expects. An older SDK fails at restore or build time rather than producing a different result.

## Build, test and run

From the repository root:

```
dotnet build
dotnet test
```

Do not pass `--nologo` to `dotnet test`. Under the Microsoft.Testing.Platform runner the option is rejected and the run reports zero tests with exit code 5. Run a subset with a filter, for example:

```
dotnet test --filter-class "*PixelImageComparerTests"
```

Launch the interface:

```
dotnet run --project src/PixelDiff.App
```

Run the command line tool:

```
dotnet run --project src/PixelDiff.Cli -- help
dotnet run --project src/PixelDiff.Cli -- run --suite path\to\suite
```

For a build server, publish the runner once and call the executable directly:

```
dotnet publish src/PixelDiff.Cli -c Release -o artifacts/cli
artifacts\cli\pixeldiff.exe run --suite path\to\suite
```

## Workflow

1. Open the interface. It opens the suite at `Documents\PixelDiffSuite`; use Browse to choose another folder, ideally one inside your repository.
2. Pick a target. The window list shows top level windows that have a title and are neither hidden nor minimised, because a minimised window has no surface to render and cannot be captured. Use Refresh after opening the application you want to test.
3. Enter a baseline name and choose Capture baseline. The window is captured and saved as the approved image.
4. Later, choose Run selected or Run all. Each baseline is recaptured and compared. The row shows Match, Differs or a size mismatch, with the changed pixel count, the changed ratio and the number of changed areas.
5. Review the result by switching the viewer between the baseline, the latest capture and the diff. The diff dims the capture, paints changed pixels magenta and draws a box around each changed area.
6. If the change is intended, choose Approve candidate. This replaces the baseline with the latest capture and keeps the target, tolerances and note. If it is not intended, fix the application and run again.

Baselines are meant to be committed to version control. A baseline is a PNG plus a small JSON file, so an approved visual change appears in a pull request as a changed image, reviewed like any code change. Run output under `runs/` is evidence for a single run and normally belongs in `.gitignore`.

The interface keeps results in memory and does not write a report. The command line runner writes `report.json` and the candidate and diff images.

## How a baseline is stored

A suite is a directory. The store creates a `baselines` folder inside it and each baseline is two files sharing a name:

```
suite/
  baselines/
    login-dialog.png
    login-dialog.json
  runs/
    20261008-093000/
      report.json
      login-dialog.candidate.png
      login-dialog.diff.png
```

The PNG is lossless and holds the approved image as three channel colour. The JSON descriptor records:

- `Name`, which must match the file name.
- `Target`, how to capture the image again: `Kind` (`Window` or `ScreenRegion`), a `Region`, and for windows a `WindowTitlePattern`. For a window target `Region` is an optional crop relative to the window's top left corner.
- `Width` and `Height` of the stored image.
- `CapturedAt`, when the baseline was approved.
- `Options`, the comparison settings for this baseline.
- `Note`, optional free text such as a ticket reference.

A descriptor written by the store looks like this:

```
{
  "Name": "login-dialog",
  "Target": {
    "Kind": "Window",
    "Region": {
      "X": 0,
      "Y": 0,
      "Width": 320,
      "Height": 120
    },
    "WindowTitlePattern": "Sign in"
  },
  "Width": 320,
  "Height": 120,
  "CapturedAt": "2026-10-08T09:30:00+00:00",
  "Options": {
    "PerChannelTolerance": 12,
    "MaxChangedPixelRatio": 0.001,
    "IgnoreRegions": [
      {
        "X": 240,
        "Y": 8,
        "Width": 72,
        "Height": 16
      }
    ],
    "MinimumRegionArea": 16
  },
  "Note": "Approved for TICKET-123"
}
```

A region is written as exactly `X`, `Y`, `Width` and `Height`. Its right edge, bottom edge, area and emptiness are derived from those four and are deliberately not stored, so a hand edited file cannot end up holding a width and an area that disagree. A descriptor written by an earlier version that still carries those names keeps loading; the extra members are ignored.

Names may contain ASCII letters, digits, dot, dash and underscore, up to 100 characters. They cannot start or end with a dot or dash, contain two consecutive dots, or be a reserved Windows device name such as `CON`. A name that breaks a rule is rejected rather than rewritten, so two names can never collapse onto one file.

A baseline is saved by encoding and staging both files before either is moved into place, so a failure while writing leaves the previous pair untouched. The two moves are not atomic with respect to each other, so a crash in the gap between them can leave a new image beside an old descriptor; loading then fails on the size check rather than accepting the mismatch. A baseline exists only when both files are present.

## How comparison works

A fresh capture is compared with the baseline pixel by pixel. If the two images differ in size, no pixel comparison is attempted and the result is a size mismatch. Otherwise the comparison goes through these steps.

1. For every pixel, take the absolute difference of each colour channel.
2. Keep the largest of the three channel differences.
3. Mark the pixel as changed if that value is strictly greater than `PerChannelTolerance`.
4. Clear pixels inside the ignore regions.
5. Divide the number of changed pixels by the number of pixels still compared. If the ratio is at most `MaxChangedPixelRatio`, the baseline matches.

Taking the maximum across channels, instead of converting to grayscale first, matters because grayscale averages the channels. A change from one colour to another of similar brightness, such as a red status badge turning green, can leave the grayscale value almost unchanged and pass unnoticed. With the maximum, any single channel that moves by more than the tolerance is enough. The alpha channel is ignored.

| Setting | Default | Meaning |
| --- | --- | --- |
| `PerChannelTolerance` | 8 | Largest per channel difference, 0 to 255, that is still treated as unchanged. Absorbs font anti aliasing differences. A difference equal to the tolerance is unchanged; one above it is changed. |
| `MaxChangedPixelRatio` | 0 | Fraction of compared pixels allowed to change, 0 to 1. Zero means any changed pixel fails. |
| `IgnoreRegions` | none | Rectangles, in image coordinates from the top left corner, excluded from comparison. |
| `MinimumRegionArea` | 16 | Smallest pixel area of a changed group that is reported as a region. |

`MinimumRegionArea` only affects the boxes shown to a reviewer and listed in the report. It never changes whether a baseline passes. Nearby changed pixels are grouped into one box before the area is measured, so a changed line of text is one box rather than one per letter.

Ignore regions exist for content that legitimately differs on every run, such as a clock, a frame counter, a caret or a build number. Pixels inside a region are removed from both the changed count and the compared count, so they cannot fail a run and they do not dilute the ratio. Overlapping regions are counted once. A region that lies entirely outside the image is rejected when the descriptor is loaded.

To tune a flaky baseline:

1. Run it and open the diff. The boxes show where the instability is.
2. If the boxes cover dynamic content, add an ignore region around it. This is the preferred fix because it keeps the rest of the image strict.
3. If the diff is scattered single pixels along text edges, raise `PerChannelTolerance` gradually. Values from about 4 to 12 absorb anti aliasing without hiding layout movement.
4. If a small, steady amount of change is acceptable, set `MaxChangedPixelRatio` to a small value. For example 0.001 allows one pixel in a thousand to change.
5. Leave `MinimumRegionArea` alone unless the review boxes are cluttered; it does not affect the verdict.

The interface creates every baseline with the default settings and has no editor for tolerances, ignore regions or the note. Edit the JSON descriptor to change them and commit the change. The runner reads the descriptor on every run.

## Command line

```
pixeldiff run     [--suite <dir>] [--name <baseline>] [--output <dir>]
pixeldiff list    [--suite <dir>]
pixeldiff approve --name <baseline> [--suite <dir>]
pixeldiff help
```

| Command | Behaviour |
| --- | --- |
| `run` | Recaptures every baseline in the suite, or only `--name`, compares each one and writes `report.json`. For each baseline that does not match it also writes `<name>.candidate.png`, and `<name>.diff.png` unless the sizes differ. Prints one PASS or FAIL line per baseline and a summary. |
| `list` | Prints each baseline with its size and capture target, then a count. |
| `approve` | Recaptures one existing baseline and stores the result as the new approved image, keeping its target, options and note. Updates the recorded size and time. |
| `help` | Prints usage. |

| Option | Applies to | Meaning |
| --- | --- | --- |
| `--suite <dir>` | run, list, approve | Suite directory. Defaults to the current directory. It must already exist. |
| `--name <baseline>` | run, approve | Restrict to one baseline. Required for `approve`. |
| `--output <dir>` | run | Where to write artifacts. Defaults to `runs/<yyyyMMdd-HHmmss>` (UTC) under the suite. |

Each option takes exactly one non empty value and may be given once. Command names are case insensitive.

`report.json` holds the start time, the suite path, the totals, and one entry per baseline with its outcome (`Match`, `Differs`, `SizeMismatch` or `Error`), changed, compared and ignored pixel counts, the changed ratio, the changed regions, an optional detail message and the relative file names of the candidate and diff images.

`Error` means that baseline could not be compared at all, because its descriptor would not load or its target could not be captured. The reason is in the entry detail, the counts are zero and no images are written. The remaining baselines still run.

### Exit codes

| Code | Meaning | Suggested build server reaction |
| --- | --- | --- |
| 0 | Every comparison matched, or a command that does not compare succeeded. | Pass. |
| 1 | At least one comparison did not match. | Fail the build and publish the run directory so the diffs can be reviewed. |
| 2 | The arguments were wrong or incomplete. | Fix the pipeline configuration. |
| 3 | The command failed before it could run, or every failure in the run was a baseline that could not be captured or read rather than a comparison that did not match. | Treat as an infrastructure or environment failure. |

A `run` always attempts every baseline and always writes `report.json`. A baseline that cannot be captured or read is recorded as `Error` and the run carries on. The exit code is chosen at the end: 1 if any comparison did not match, otherwise 3 if any baseline could not be run, otherwise 0. A mismatch outranks a failure to run because a visual change is the finding to act on, and the published run directory carries both.

## Continuous integration

The runner needs an interactive Windows desktop session with the target application open and visible, because it captures what is on screen. A service session without a desktop cannot capture.

A minimal pipeline step, shown as a script:

```
artifacts\cli\pixeldiff.exe run --suite tests\visual --output build\visual
set code=%ERRORLEVEL%
if %code%==1 echo Visual changes detected. See build\visual for diffs.
exit /b %code%
```

Publish the output directory as a build artifact whether or not the step passes. Exit code 1 means the images differ, and the reviewer then either fixes the application or approves the new image locally and commits it.

The first baseline for a target has to be created in the interface. The runner cannot create one: `approve` only refreshes a baseline that already exists, and no command captures a new target. This is a deliberate current limitation, not an oversight. Creating a baseline needs a person to choose the target and confirm that what is on screen is correct, and that decision is not something an unattended run should make. Once a baseline exists, `pixeldiff approve --name <baseline>` can refresh it from the command line.

## Project layout

```
src/
  PixelDiff.Core/             Comparison, diff rendering, baseline store
  PixelDiff.Capture.Windows/  Screen and window capture
  PixelDiff.Cli/              The pixeldiff runner
  PixelDiff.App/              Avalonia desktop interface
tests/
  PixelDiff.Core.Tests/       xUnit v3 tests for Core
  PixelDiff.Cli.Tests/        xUnit v3 tests for the runner
```

- `PixelDiff.Core` holds the comparison maths, the diff image renderer, the capture target and region models, and the file system baseline store. It targets plain `net10.0` and references only the managed OpenCvSharp binding, so it carries no Windows dependency and can be tested and reused on any host. The native OpenCV runtime is added by the executables.
- `PixelDiff.Capture.Windows` captures the desktop and windows through GDI, `PrintWindow` and DWM, and lists capturable windows. It is separate from Core because it depends on user32, gdi32 and System.Drawing, which are Windows only. Both executables reach it through the `IScreenCapture` interface.
- `PixelDiff.Cli` is the command line runner, built as `pixeldiff.exe`.
- `PixelDiff.App` is the desktop interface, built as `PixelDiff.exe`, using Avalonia and CommunityToolkit.Mvvm.
- `PixelDiff.Core.Tests` covers baseline names, regions, the change mask, the comparer, diff rendering, image encoding and the baseline store.
- `PixelDiff.Cli.Tests` covers the runner: the report, what happens to the rest of a suite when one baseline cannot be captured, and the exit codes. It replaces the capture interface with a stub, so it needs no desktop session. There are no tests for capture itself or for the interface, because those do.

## Limitations

- Screen region capture copies the composited desktop. A window that overlaps the region appears in the image. Window capture uses `PrintWindow` and is not affected by windows on top.
- Window targets match by case insensitive substring of the title, and the first visible match in z-order is used. A window whose title changes, such as a browser tab that shows the page title, can stop matching. The interface stores the full title of the selected window as the pattern, so edit `WindowTitlePattern` in the descriptor to a stable fragment. If two windows match, the one higher in z-order is captured.
- Both executables declare per monitor DPI awareness (PerMonitorV2) in their manifests. Without it, Windows gives the capture code scaled coordinates on a display with scaling above 100 percent, so window rectangles and captured pixels disagree and a capture can come back at the wrong size, which would be reported as a size mismatch. Baselines are in physical pixels, so a baseline taken at one display scale will not match a capture taken at another. Capture and run on machines with the same scaling and resolution.
- A minimised window raises an error instead of producing a blank image. It is left out of the window list for that reason, and if a baseline's window is minimised when the runner reaches it, that baseline is recorded as `Error` and the rest of the suite still runs.
- The interface draws images at 96 DPI without scaling to fit. On a scaled display an image is not shown at exactly one image pixel per physical pixel, so judge small differences from the diff and the reported counts rather than by eye.
- The interface can only create window baselines. A screen region baseline requires a hand written descriptor with `Kind` set to `ScreenRegion` and a `Region`.
- Comparison is exact pixel arithmetic and suits deterministic interface rendering. It is not a perceptual comparison and is not suited to video, animation or photographs.
- Image sizes must match. A resized window or a changed display scale gives a size mismatch with no diff image.

## Contributing

The build is strict.

- Warnings are errors, with analysers at the `latest-recommended` level.
- Code style from `.editorconfig` is enforced during the build, so a style violation fails it. Examples are file scoped namespaces and `using` directives outside the namespace. Files use LF line endings and four space indentation for C#.
- Documentation comments are required on public members, because the documentation file is generated and a missing comment is a warning.
- Package versions are managed centrally in `Directory.Packages.props`. A `PackageReference` in a project file must not carry a `Version`; add or change the version in the central file.

Run `dotnet build` and `dotnet test` before submitting a change. Both must finish with no warnings and no failures.
