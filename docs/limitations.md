# Limitations

[Back to the README](../README.md)

- Screen region capture copies the composited desktop. A window that overlaps the region appears in the image. Window capture uses `PrintWindow` and is not affected by windows on top.
- Window targets match by case insensitive substring of the title, and the first visible match in z-order is used. A window whose title changes, such as a browser tab that shows the page title, can stop matching. The interface stores the full title of the selected window as the pattern, so edit `WindowTitlePattern` in the descriptor to a stable fragment. If two windows match, the one higher in z-order is captured.
- Both executables declare per monitor DPI awareness (PerMonitorV2) in their manifests. Without it, Windows gives the capture code scaled coordinates on a display with scaling above 100 percent, so window rectangles and captured pixels disagree and a capture can come back at the wrong size, which would be reported as a size mismatch. Baselines are in physical pixels, so a baseline taken at one display scale will not match a capture taken at another. Capture and run on machines with the same scaling and resolution.
- A minimised window raises an error instead of producing a blank image. It is left out of the window list for that reason, and if a baseline's window is minimised when the runner reaches it, that baseline is recorded as `Error` and the rest of the suite still runs.
- The interface draws images at 96 DPI without scaling to fit. On a scaled display an image is not shown at exactly one image pixel per physical pixel, so judge small differences from the diff and the reported counts rather than by eye.
- The interface can only create window baselines. A screen region baseline requires a hand written descriptor with `Kind` set to `ScreenRegion` and a `Region`.
- Comparison is exact pixel arithmetic and suits deterministic interface rendering. It is not a perceptual comparison and is not suited to video, animation or photographs.
- Image sizes must match. A resized window or a changed display scale gives a size mismatch with no diff image.
