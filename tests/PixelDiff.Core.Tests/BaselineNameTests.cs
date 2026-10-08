using PixelDiff.Core.Baselines;
using Xunit;

namespace PixelDiff.Core.Tests;

public sealed class BaselineNameTests
{
    [Theory]
    [InlineData("login")]
    [InlineData("Login-Page_v2")]
    [InlineData("settings.dark.v1")]
    [InlineData("a")]
    [InlineData("console")]
    [InlineData("0123456789")]
    public void OrdinaryNamesAreAccepted(string name)
    {
        Assert.True(BaselineName.IsValid(name));
        BaselineName.Validate(name);
    }

    [Fact]
    public void ANameAtTheMaximumLengthIsAccepted()
    {
        Assert.True(BaselineName.IsValid(new string('a', BaselineName.MaximumLength)));
    }

    [Fact]
    public void ANameOverTheMaximumLengthIsRejected()
    {
        Assert.False(BaselineName.IsValid(new string('a', BaselineName.MaximumLength + 1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyAndWhitespaceNamesAreRejected(string? name)
    {
        Assert.False(BaselineName.IsValid(name));
    }

    [Theory]
    [InlineData(".hidden")]
    [InlineData("trailing.")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    public void LeadingAndTrailingDotsAndDashesAreRejected(string name)
    {
        Assert.False(BaselineName.IsValid(name));
    }

    [Fact]
    public void ConsecutiveDotsAreRejected()
    {
        Assert.False(BaselineName.IsValid("a..b"));
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("..\\escape")]
    [InlineData("../escape")]
    [InlineData("a b")]
    [InlineData("a:b")]
    [InlineData("a*b")]
    [InlineData("a?b")]
    [InlineData("café")]
    public void PathSeparatorsAndOtherDisallowedCharactersAreRejected(string name)
    {
        Assert.False(BaselineName.IsValid(name));
    }

    [Theory]
    [InlineData("con")]
    [InlineData("CON")]
    [InlineData("Nul")]
    [InlineData("com1")]
    [InlineData("LPT9")]
    [InlineData("con.v1")]
    [InlineData("aux.txt")]
    public void ReservedDeviceNamesAreRejectedRegardlessOfCaseOrExtension(string name)
    {
        Assert.False(BaselineName.IsValid(name));
    }

    [Theory]
    [InlineData("login")]
    [InlineData("")]
    [InlineData("a..b")]
    [InlineData("con")]
    public void DescribeReturnsNullExactlyWhenTheNameIsValid(string name)
    {
        Assert.Equal(BaselineName.IsValid(name), BaselineName.Describe(name) is null);
    }

    [Fact]
    public void ValidateThrowsWithTheSuppliedParameterName()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => BaselineName.Validate("a/b", "candidate"));

        Assert.Equal("candidate", error.ParamName);
    }
}
