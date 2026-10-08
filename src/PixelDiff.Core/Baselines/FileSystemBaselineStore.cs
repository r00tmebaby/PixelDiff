using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;
using PixelDiff.Core.Imaging;

namespace PixelDiff.Core.Baselines;

/// <summary>
/// Keeps baselines as a PNG and a JSON descriptor side by side under a single directory. Plain
/// files on disk mean a suite can be committed to version control, so a change to an approved
/// image shows up in review like any other change.
/// </summary>
public sealed class FileSystemBaselineStore : IBaselineStore
{
    private const string ImageExtension = ".png";
    private const string DescriptorExtension = ".json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _baselineDirectory;

    /// <summary>Opens a store rooted at a directory, creating it when absent.</summary>
    /// <param name="rootDirectory">Directory that holds the suite.</param>
    /// <exception cref="ArgumentException">The path is empty.</exception>
    public FileSystemBaselineStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        RootDirectory = Path.GetFullPath(rootDirectory);
        _baselineDirectory = Path.Combine(RootDirectory, "baselines");
        Directory.CreateDirectory(_baselineDirectory);
    }

    /// <summary>Absolute path of the directory holding the suite.</summary>
    public string RootDirectory { get; }

    /// <summary>Absolute path of the directory holding approved images.</summary>
    public string BaselineDirectory => _baselineDirectory;

    /// <inheritdoc />
    public IReadOnlyList<string> EnumerateNames()
    {
        if (!Directory.Exists(_baselineDirectory))
        {
            return [];
        }

        List<string> names = Directory
            .EnumerateFiles(_baselineDirectory, "*" + DescriptorExtension)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => BaselineName.IsValid(name))
            .Select(name => name!)
            .ToList();

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    /// <inheritdoc />
    public bool Exists(string name)
    {
        if (!BaselineName.IsValid(name))
        {
            return false;
        }

        return File.Exists(ImagePath(name)) && File.Exists(DescriptorPath(name));
    }

    /// <inheritdoc />
    /// <remarks>
    /// The image file still has to be present, because a descriptor without one is not a baseline
    /// and a caller that lists a suite must not offer something that cannot then be run.
    /// </remarks>
    /// <exception cref="ArgumentException">The name breaks a naming rule.</exception>
    /// <exception cref="InvalidDataException">The descriptor on disk is unreadable or inconsistent.</exception>
    public BaselineDescriptor LoadDescriptor(string name)
    {
        BaselineName.Validate(name);

        string descriptorPath = DescriptorPath(name);
        if (!File.Exists(descriptorPath) || !File.Exists(ImagePath(name)))
        {
            throw new BaselineNotFoundException(name);
        }

        return ReadDescriptor(name, descriptorPath);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">The name breaks a naming rule.</exception>
    /// <exception cref="InvalidDataException">The descriptor on disk is unreadable or inconsistent.</exception>
    public Baseline Load(string name)
    {
        BaselineDescriptor descriptor = LoadDescriptor(name);
        Mat image = ImageCodec.DecodeBgr(File.ReadAllBytes(ImagePath(name)));

        try
        {
            return new Baseline(image, descriptor);
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public void Save(Mat image, BaselineDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(descriptor);

        descriptor.Validate();

        if (image.Width != descriptor.Width || image.Height != descriptor.Height)
        {
            throw new ArgumentException(
                $"Descriptor records {descriptor.Width}x{descriptor.Height} but the image is {image.Width}x{image.Height}.",
                nameof(descriptor));
        }

        Directory.CreateDirectory(_baselineDirectory);

        byte[] encodedImage = ImageCodec.EncodePng(image);
        byte[] encodedDescriptor = JsonSerializer.SerializeToUtf8Bytes(descriptor, SerializerOptions);

        // Both files are encoded and staged before either is moved into place, so a failure while
        // encoding or writing leaves the previous pair untouched. The two moves themselves are not
        // atomic with respect to each other: a crash between them pairs the new image with the old
        // descriptor, which the size check in Load then reports rather than silently accepting.
        string stagedImage = ImagePath(descriptor.Name) + ".tmp";
        string stagedDescriptor = DescriptorPath(descriptor.Name) + ".tmp";

        try
        {
            File.WriteAllBytes(stagedImage, encodedImage);
            File.WriteAllBytes(stagedDescriptor, encodedDescriptor);

            File.Move(stagedImage, ImagePath(descriptor.Name), overwrite: true);
            File.Move(stagedDescriptor, DescriptorPath(descriptor.Name), overwrite: true);
        }
        finally
        {
            Discard(stagedImage);
            Discard(stagedDescriptor);
        }
    }

    /// <inheritdoc />
    public bool Delete(string name)
    {
        BaselineName.Validate(name);

        bool removed = false;
        foreach (string path in new[] { ImagePath(name), DescriptorPath(name) })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                removed = true;
            }
        }

        return removed;
    }

    /// <summary>
    /// Creates and returns the directory for one run's output. Runs are kept apart so a failure can
    /// be reviewed after a later run has already happened.
    /// </summary>
    /// <param name="startedAt">When the run began, used to name the directory.</param>
    /// <returns>Absolute path of the created directory.</returns>
    public string CreateRunDirectory(DateTimeOffset startedAt)
    {
        string stamp = startedAt.UtcDateTime.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        string path = Path.Combine(RootDirectory, "runs", stamp);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Absolute path of the image file for a baseline.</summary>
    /// <param name="name">The baseline name.</param>
    /// <returns>The path, whether or not the file exists.</returns>
    public string ImagePath(string name) => Path.Combine(_baselineDirectory, name + ImageExtension);

    /// <summary>Absolute path of the descriptor file for a baseline.</summary>
    /// <param name="name">The baseline name.</param>
    /// <returns>The path, whether or not the file exists.</returns>
    public string DescriptorPath(string name) => Path.Combine(_baselineDirectory, name + DescriptorExtension);

    private static BaselineDescriptor ReadDescriptor(string name, string path)
    {
        BaselineDescriptor? descriptor;
        try
        {
            descriptor = JsonSerializer.Deserialize<BaselineDescriptor>(File.ReadAllBytes(path), SerializerOptions);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"The descriptor for baseline '{name}' is not valid JSON.", error);
        }

        if (descriptor is null)
        {
            throw new InvalidDataException($"The descriptor for baseline '{name}' is empty.");
        }

        if (!string.Equals(descriptor.Name, name, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The descriptor at '{path}' names baseline '{descriptor.Name}' but was loaded as '{name}'.");
        }

        descriptor.Validate();
        return descriptor;
    }

    private static void Discard(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
