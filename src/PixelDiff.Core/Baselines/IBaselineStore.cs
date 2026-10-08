using OpenCvSharp;

namespace PixelDiff.Core.Baselines;

/// <summary>
/// Persistence for approved images. An interface so the suite can later live in blob storage or
/// beside a build artifact without changing the review workflow.
/// </summary>
public interface IBaselineStore
{
    /// <summary>Lists the names of every stored baseline, ordered alphabetically.</summary>
    /// <returns>Baseline names, which may be empty but is never null.</returns>
    IReadOnlyList<string> EnumerateNames();

    /// <summary>Reports whether a baseline exists.</summary>
    /// <param name="name">The baseline name.</param>
    /// <returns>True when both the image and its descriptor are present.</returns>
    bool Exists(string name);

    /// <summary>
    /// Reads only the metadata of a baseline, without decoding its image. Listing a suite costs
    /// one descriptor per baseline this way, rather than the whole of every approved image.
    /// </summary>
    /// <param name="name">The baseline name.</param>
    /// <returns>The metadata, which holds no unmanaged resources and needs no disposal.</returns>
    /// <exception cref="BaselineNotFoundException">No baseline is stored under that name.</exception>
    BaselineDescriptor LoadDescriptor(string name);

    /// <summary>Reads a baseline and its metadata.</summary>
    /// <param name="name">The baseline name.</param>
    /// <returns>The baseline, which the caller must dispose.</returns>
    /// <exception cref="BaselineNotFoundException">No baseline is stored under that name.</exception>
    Baseline Load(string name);

    /// <summary>
    /// Writes a baseline, replacing any existing one with the same name. The image and descriptor
    /// are written together so a reader never sees one without the other.
    /// </summary>
    /// <param name="image">The image to approve.</param>
    /// <param name="descriptor">Metadata describing the image.</param>
    void Save(Mat image, BaselineDescriptor descriptor);

    /// <summary>Removes a baseline and its metadata.</summary>
    /// <param name="name">The baseline name.</param>
    /// <returns>True when something was removed, false when nothing was stored under that name.</returns>
    bool Delete(string name);
}
