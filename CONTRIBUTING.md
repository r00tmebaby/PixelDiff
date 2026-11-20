# Contributing

How to build, test and change PixelDiff. For what the tool does and how to use it, see the [README](README.md).

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

## Build rules

The build is strict.

- Warnings are errors, with analysers at the `latest-recommended` level.
- Code style from `.editorconfig` is enforced during the build, so a style violation fails it. Examples are file scoped namespaces and `using` directives outside the namespace. Files use LF line endings and four space indentation for C#.
- Documentation comments are required on public members, because the documentation file is generated and a missing comment is a warning.
- Package versions are managed centrally in `Directory.Packages.props`. A `PackageReference` in a project file must not carry a `Version`; add or change the version in the central file.

Run `dotnet build` and `dotnet test` before submitting a change. Both must finish with no warnings and no failures.
