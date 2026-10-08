using System.Text.Json;
using OpenCvSharp;
using PixelDiff.Core.Baselines;
using PixelDiff.Core.Comparison;
using PixelDiff.Core.Models;
using Xunit;

namespace PixelDiff.Core.Tests;

public sealed class FileSystemBaselineStoreTests : IDisposable
{
    // A descriptor in the shape the store wrote before the derived region members were excluded.
    private const string LegacyDescriptorJson = """
        {
          "Name": "legacy",
          "Target": {
            "Kind": "ScreenRegion",
            "Region": {
              "X": -100, "Y": 20, "Width": 40, "Height": 30,
              "Right": -60, "Bottom": 50, "Area": 1200, "IsEmpty": false
            }
          },
          "Width": 40,
          "Height": 30,
          "CapturedAt": "2026-03-14T09:26:53+00:00",
          "Options": {
            "PerChannelTolerance": 5,
            "MaxChangedPixelRatio": 0.02,
            "IgnoreRegions": [
              {
                "X": 1, "Y": 2, "Width": 3, "Height": 4,
                "Right": 4, "Bottom": 6, "Area": 12, "IsEmpty": false
              }
            ],
            "MinimumRegionArea": 9
          },
          "Note": "TICKET-42"
        }
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "PixelDiffTests-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemBaselineStore _store;

    public FileSystemBaselineStoreTests()
    {
        _store = new FileSystemBaselineStore(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static BaselineDescriptor Describe(string name, Mat image) => new()
    {
        Name = name,
        Target = CaptureTarget.ForRegion(new CaptureRegion(-100, 20, image.Width, image.Height)),
        Width = image.Width,
        Height = image.Height,
        CapturedAt = new DateTimeOffset(2026, 3, 14, 9, 26, 53, TimeSpan.Zero),
        Options = new ComparisonOptions
        {
            PerChannelTolerance = 5,
            MaxChangedPixelRatio = 0.02,
            MinimumRegionArea = 9,
            IgnoreRegions = [new CaptureRegion(1, 2, 3, 4)],
        },
        Note = "TICKET-42",
    };

    [Fact]
    public void SaveThenLoadRoundTripsTheImageAndEveryDescriptorField()
    {
        using Mat image = TestImages.Grey(40, 30, 90);
        TestImages.Paint(image, new CaptureRegion(5, 5, 10, 10), 10, 20, 30);
        BaselineDescriptor descriptor = Describe("login", image);

        _store.Save(image, descriptor);
        using Baseline loaded = _store.Load("login");

        Assert.Equal(descriptor.Name, loaded.Descriptor.Name);
        Assert.Equal(descriptor.Target, loaded.Descriptor.Target);
        Assert.Equal(descriptor.Width, loaded.Descriptor.Width);
        Assert.Equal(descriptor.Height, loaded.Descriptor.Height);
        Assert.Equal(descriptor.CapturedAt, loaded.Descriptor.CapturedAt);
        Assert.Equal(descriptor.Note, loaded.Descriptor.Note);
        Assert.Equal(5, loaded.Descriptor.Options.PerChannelTolerance);
        Assert.Equal(0.02, loaded.Descriptor.Options.MaxChangedPixelRatio);
        Assert.Equal(9, loaded.Descriptor.Options.MinimumRegionArea);
        Assert.Equal(new CaptureRegion(1, 2, 3, 4), Assert.Single(loaded.Descriptor.Options.IgnoreRegions));

        using var difference = new Mat();
        Cv2.Absdiff(image, loaded.Image, difference);
        Assert.Equal(0, Cv2.CountNonZero(difference.Reshape(1)));
    }

    [Fact]
    public void SaveReplacesAnExistingBaseline()
    {
        using Mat first = TestImages.Grey(10, 10, 10);
        using Mat second = TestImages.Grey(12, 8, 200);
        _store.Save(first, Describe("page", first));

        _store.Save(second, Describe("page", second));
        using Baseline loaded = _store.Load("page");

        Assert.Equal(12, loaded.Image.Width);
        Assert.Equal(8, loaded.Image.Height);
        Assert.Empty(Directory.GetFiles(_store.BaselineDirectory, "*.tmp"));
    }

    [Fact]
    public void EnumerateNamesReturnsNamesSortedAndIgnoresStrayFiles()
    {
        using Mat image = TestImages.Grey(10, 10);
        foreach (string name in new[] { "charlie", "alpha", "bravo" })
        {
            _store.Save(image, Describe(name, image));
        }

        File.WriteAllText(Path.Combine(_store.BaselineDirectory, "notes.txt"), "stray");
        File.WriteAllText(Path.Combine(_store.BaselineDirectory, "bad name.json"), "{}");

        Assert.Equal(["alpha", "bravo", "charlie"], _store.EnumerateNames());
    }

    [Fact]
    public void EnumerateNamesIsEmptyForANewStore()
    {
        Assert.Empty(_store.EnumerateNames());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExistsIsFalseWhenOnlyOneOfTheTwoFilesIsPresent(bool removeImage)
    {
        using Mat image = TestImages.Grey(10, 10);
        _store.Save(image, Describe("half", image));
        Assert.True(_store.Exists("half"));

        File.Delete(removeImage ? _store.ImagePath("half") : _store.DescriptorPath("half"));

        Assert.False(_store.Exists("half"));
    }

    [Fact]
    public void ExistsIsFalseForAnInvalidName()
    {
        Assert.False(_store.Exists("..\\escape"));
    }

    [Fact]
    public void LoadThrowsBaselineNotFoundForAnUnknownName()
    {
        BaselineNotFoundException error = Assert.Throws<BaselineNotFoundException>(() => _store.Load("missing"));

        Assert.Equal("missing", error.Name);
    }

    [Fact]
    public void LoadThrowsInvalidDataWhenTheDescriptorIsMalformedJson()
    {
        using Mat image = TestImages.Grey(10, 10);
        _store.Save(image, Describe("broken", image));
        File.WriteAllText(_store.DescriptorPath("broken"), "{ this is not json");

        Assert.Throws<InvalidDataException>(() => _store.Load("broken"));
    }

    [Fact]
    public void LoadThrowsInvalidDataWhenTheDescriptorNamesADifferentBaseline()
    {
        using Mat image = TestImages.Grey(10, 10);
        _store.Save(image, Describe("original", image));
        File.Copy(_store.ImagePath("original"), _store.ImagePath("copy"));
        File.Copy(_store.DescriptorPath("original"), _store.DescriptorPath("copy"));

        Assert.Throws<InvalidDataException>(() => _store.Load("copy"));
    }

    [Fact]
    public void SaveRejectsADescriptorWhoseSizeContradictsTheImage()
    {
        using Mat image = TestImages.Grey(10, 10);
        BaselineDescriptor descriptor = Describe("sized", image) with { Width = 20 };

        Assert.Throws<ArgumentException>(() => _store.Save(image, descriptor));
        Assert.False(_store.Exists("sized"));
    }

    [Fact]
    public void DeleteReturnsTrueWhenItRemovedSomethingAndFalseOtherwise()
    {
        using Mat image = TestImages.Grey(10, 10);
        _store.Save(image, Describe("doomed", image));

        Assert.True(_store.Delete("doomed"));
        Assert.False(_store.Exists("doomed"));
        Assert.False(_store.Delete("doomed"));
    }

    [Fact]
    public void LoadDescriptorReturnsEveryFieldWithoutReadingTheImage()
    {
        using Mat image = TestImages.Grey(40, 30, 90);
        BaselineDescriptor descriptor = Describe("login", image);
        _store.Save(image, descriptor);

        // Nothing here is a readable PNG, so a method that decoded the image would fail.
        File.WriteAllBytes(_store.ImagePath("login"), [0, 1, 2, 3, 4, 5, 6, 7]);

        BaselineDescriptor loaded = _store.LoadDescriptor("login");

        Assert.Equal(descriptor.Name, loaded.Name);
        Assert.Equal(descriptor.Target, loaded.Target);
        Assert.Equal(descriptor.Width, loaded.Width);
        Assert.Equal(descriptor.Height, loaded.Height);
        Assert.Equal(descriptor.CapturedAt, loaded.CapturedAt);
        Assert.Equal(descriptor.Note, loaded.Note);
        Assert.Equal(descriptor.Options.PerChannelTolerance, loaded.Options.PerChannelTolerance);
        Assert.Equal(descriptor.Options.MaxChangedPixelRatio, loaded.Options.MaxChangedPixelRatio);
        Assert.Equal(descriptor.Options.MinimumRegionArea, loaded.Options.MinimumRegionArea);
        Assert.Equal(descriptor.Options.IgnoreRegions, loaded.Options.IgnoreRegions);
        Assert.Throws<ArgumentException>(() => _store.Load("login"));
    }

    [Fact]
    public void LoadDescriptorThrowsBaselineNotFoundForAnUnknownName()
    {
        BaselineNotFoundException error =
            Assert.Throws<BaselineNotFoundException>(() => _store.LoadDescriptor("missing"));

        Assert.Equal("missing", error.Name);
    }

    [Fact]
    public void LoadDescriptorThrowsBaselineNotFoundWhenOnlyTheDescriptorSurvives()
    {
        using Mat image = TestImages.Grey(10, 10);
        _store.Save(image, Describe("half", image));
        File.Delete(_store.ImagePath("half"));

        Assert.Throws<BaselineNotFoundException>(() => _store.LoadDescriptor("half"));
    }

    [Fact]
    public void LoadDescriptorThrowsInvalidDataWhenTheDescriptorIsMalformedJson()
    {
        using Mat image = TestImages.Grey(10, 10);
        _store.Save(image, Describe("broken", image));
        File.WriteAllText(_store.DescriptorPath("broken"), "{ this is not json");

        Assert.Throws<InvalidDataException>(() => _store.LoadDescriptor("broken"));
    }

    [Fact]
    public void LoadDescriptorThrowsInvalidDataWhenTheDescriptorNamesADifferentBaseline()
    {
        using Mat image = TestImages.Grey(10, 10);
        _store.Save(image, Describe("original", image));
        File.Copy(_store.ImagePath("original"), _store.ImagePath("copy"));
        File.Copy(_store.DescriptorPath("original"), _store.DescriptorPath("copy"));

        Assert.Throws<InvalidDataException>(() => _store.LoadDescriptor("copy"));
    }

    [Fact]
    public void LoadDescriptorRejectsAnInvalidName()
    {
        Assert.Throws<ArgumentException>(() => _store.LoadDescriptor("..\\escape"));
    }

    [Fact]
    public void ASavedDescriptorStoresOnlyTheFourValuesOfEachRegion()
    {
        using Mat image = TestImages.Grey(40, 30);
        _store.Save(image, Describe("shapes", image));

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(_store.DescriptorPath("shapes")));
        JsonElement target = document.RootElement.GetProperty("Target").GetProperty("Region");
        JsonElement ignored = document.RootElement.GetProperty("Options").GetProperty("IgnoreRegions")[0];

        Assert.Equal(["X", "Y", "Width", "Height"], target.EnumerateObject().Select(member => member.Name));
        Assert.Equal(["X", "Y", "Width", "Height"], ignored.EnumerateObject().Select(member => member.Name));
    }

    [Fact]
    public void ADescriptorStillCarryingTheDerivedRegionValuesKeepsLoading()
    {
        using Mat image = TestImages.Grey(40, 30);
        _store.Save(image, Describe("legacy", image));
        File.WriteAllText(_store.DescriptorPath("legacy"), LegacyDescriptorJson);

        using Baseline loaded = _store.Load("legacy");

        Assert.Equal(new CaptureRegion(-100, 20, 40, 30), loaded.Descriptor.Target.Region);
        Assert.Equal(new CaptureRegion(1, 2, 3, 4), Assert.Single(loaded.Descriptor.Options.IgnoreRegions));
        Assert.Equal(40, loaded.Descriptor.Width);
        Assert.Equal("TICKET-42", loaded.Descriptor.Note);
    }

    [Fact]
    public void APathTraversalNameIsRejectedBeforeAnyFileIsTouched()
    {
        string outside = Path.Combine(_root, "escape.png");
        File.WriteAllText(outside, "must survive");
        using Mat image = TestImages.Grey(10, 10);
        BaselineDescriptor descriptor = Describe("..\\escape", image);

        Assert.Throws<ArgumentException>(() => _store.Save(image, descriptor));
        Assert.Throws<ArgumentException>(() => _store.Load("..\\escape"));
        Assert.Throws<ArgumentException>(() => _store.Delete("..\\escape"));

        Assert.Equal("must survive", File.ReadAllText(outside));
    }
}
