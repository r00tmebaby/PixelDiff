# Command line and continuous integration

[Back to the README](../README.md)

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
