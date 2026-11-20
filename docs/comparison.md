# How comparison works

[Back to the README](../README.md)

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
