namespace JdkFind.Tests;

public class JvmVersionTests
{
    [Theory]
    [InlineData("21.0.5", "21.0.5")]
    [InlineData("17.0.20.1", "17.0.20.1")]
    [InlineData("1.8.0_402", "1.8.0.402")]
    [InlineData("1.8.0_402-b10", "1.8.0.402")]
    [InlineData("25-ea", "25.0")]
    [InlineData("27", "27.0")]
    public void TryParse_ParsesCoreVersion(string javaVersion, string expectedCore)
    {
        Assert.True(JvmVersion.TryParse(javaVersion, out var version));

        Assert.Equal(new Version(expectedCore), version.Core);
        Assert.Equal(javaVersion, version.Original);
    }

    [Theory]
    [InlineData("25-ea")]
    [InlineData("24.0.1-ea+5")]
    public void TryParse_FlagsPreReleases(string javaVersion)
    {
        Assert.True(JvmVersion.TryParse(javaVersion, out var version));

        Assert.True(version.IsPreRelease);
    }

    [Theory]
    [InlineData("21.0.5")]
    [InlineData("1.8.0_402")]
    public void TryParse_ReleaseBuildsAreNotPreReleases(string javaVersion)
    {
        Assert.True(JvmVersion.TryParse(javaVersion, out var version));

        Assert.False(version.IsPreRelease);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("unknown")]
    public void TryParse_RejectsUnparseableValues(string? javaVersion) =>
        Assert.False(JvmVersion.TryParse(javaVersion, out _));

    [Fact]
    public void Compare_OrdersReleaseAfterPreReleaseOfSameNumber() =>
        Assert.True(Parse("25") > Parse("25-ea"));

    [Fact]
    public void Compare_OrdersFourComponentOverThree() =>
        Assert.True(Parse("21.0.12.1") > Parse("21.0.12"));

    [Fact]
    public void Compare_OrdersAcrossLegacyAndModernNotations() =>
        Assert.True(Parse("21.0.5") > Parse("1.8.0_402"));

    [Fact]
    public void Compare_TreatsEqualNumbersAsEqualRegardlessOfNotation() =>
        Assert.Equal(0, Parse("25-ea").CompareTo(Parse("25-ea")));

    private static JvmVersion Parse(string javaVersion)
    {
        Assert.True(JvmVersion.TryParse(javaVersion, out var version));
        return version;
    }
}
