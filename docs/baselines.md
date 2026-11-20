# How a baseline is stored

[Back to the README](../README.md)

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
