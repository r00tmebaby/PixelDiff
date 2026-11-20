# Using the desktop app

[Back to the README](../README.md)

1. Open the interface. It opens the suite at `Documents\PixelDiffSuite`; use Browse to choose another folder, ideally one inside your repository.
2. Pick a target. The window list shows top level windows that have a title and are neither hidden nor minimised, because a minimised window has no surface to render and cannot be captured. Use Refresh after opening the application you want to test.
3. Enter a baseline name and choose Capture baseline. The window is captured and saved as the approved image.
4. Later, choose Run selected or Run all. Each baseline is recaptured and compared. The row shows Match, Differs or a size mismatch, with the changed pixel count, the changed ratio and the number of changed areas.
5. Review the result by switching the viewer between the baseline, the latest capture and the diff. The diff dims the capture, paints changed pixels magenta and draws a box around each changed area.
6. If the change is intended, choose Approve candidate. This replaces the baseline with the latest capture and keeps the target, tolerances and note. If it is not intended, fix the application and run again.

Baselines are meant to be committed to version control. A baseline is a PNG plus a small JSON file, so an approved visual change appears in a pull request as a changed image, reviewed like any code change. Run output under `runs/` is evidence for a single run and normally belongs in `.gitignore`.

The interface keeps results in memory and does not write a report. The command line runner writes `report.json` and the candidate and diff images.
