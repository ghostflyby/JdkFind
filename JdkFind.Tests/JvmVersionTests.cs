namespace JdkFind.Tests;

public class JvmVersionTests
{
    [Theory]
    [InlineData("21.0.5", "21.0.5.0")]
    [InlineData("17.0.2", "17.0.2.0")]
    [InlineData("17.0.20.1", "17.0.20.1")]
    [InlineData("1.8.0_402", "1.8.0.402")]
    [InlineData("1.8.0_402-b10", "1.8.0.402")]
    [InlineData("21.0.5+9-LTS", "21.0.5.0")]
    [InlineData("25-ea", "25.0.0.0")]
    [InlineData("27", "27.0.0.0")]
    // GraalVM writes five components; the fifth has no place in System.Version and
    // keeps counting in the comparison tail.
    [InlineData("25.0.4.1.1", "25.0.4.1")]
    [InlineData("25.0.4.1.1+1-jvmci-25.4-b23", "25.0.4.1")]
    public void TryParse_ParsesCoreVersion(string javaVersion, string expectedCore)
    {
        Assert.True(JvmVersion.TryParse(javaVersion, out var version));

        Assert.Equal(new Version(expectedCore), version.Core);
        Assert.Equal(javaVersion, version.Original);
    }

    [Theory]
    [InlineData("25-ea")]
    [InlineData("24.0.1-ea+5")]
    [InlineData("25-ea+9")]
    public void TryParse_FlagsPreReleases(string javaVersion)
    {
        Assert.True(JvmVersion.TryParse(javaVersion, out var version));

        Assert.True(version.IsPreRelease);
    }

    [Theory]
    [InlineData("21.0.5")]
    [InlineData("1.8.0_402")]
    [InlineData("21.0.5+9")]
    [InlineData("21.0.5+9-LTS")]
    [InlineData("1.8.0_402-b10")]
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

    [Fact]
    public void Compare_TreatsThreeAndFourComponentNumbersAsEqual() =>
        Assert.Equal(0, Parse("17.0.2").CompareTo(Parse("17.0.2.0")));

    // "+9-LTS" is a GA build, not a pre-release: it must not sink below the bare
    // release or below an earlier build of the same release.
    [Fact]
    public void Compare_OrdersGaLtsBuildAboveItsBareRelease() =>
        Assert.True(Parse("21.0.5+9-LTS") > Parse("21.0.5"));

    [Fact]
    public void Compare_OrdersGaLtsBuildAboveEarlierBuild() =>
        Assert.True(Parse("21.0.5+9-LTS") > Parse("21.0.5+8"));

    [Fact]
    public void Compare_OrdersGaLtsBuildAbovePreReleaseOfSameCore() =>
        Assert.True(Parse("21.0.5+9-LTS") > Parse("21.0.5-ea"));

    [Fact]
    public void Compare_OrdersBuildNumbersNumerically() =>
        Assert.True(Parse("21.0.5+10") > Parse("21.0.5+9"));

    [Fact]
    public void Compare_OrdersBuildNumbersNumericallyAcrossTaggedAndPlain() =>
        Assert.True(Parse("21.0.5+10") > Parse("21.0.5+9-LTS"));

    [Fact]
    public void Compare_OrdersPreReleaseBuildNumbersNumerically() =>
        Assert.True(Parse("25-ea+10") > Parse("25-ea+9"));

    [Fact]
    public void Compare_OrdersBarePreReleaseBeforeItsBuild() =>
        Assert.True(Parse("25-ea") < Parse("25-ea+1"));

    // The fourth numeric component is not the build number: 21.0.12.1 outranks
    // 21.0.12+1, exactly as the JDK's own Runtime.Version orders them.
    [Fact]
    public void Compare_OrdersFourthComponentAboveBuildNumber() =>
        Assert.True(Parse("21.0.12.1") > Parse("21.0.12+1"));

    [Fact]
    public void Compare_OrdersLegacyBuildMarkersNumerically() =>
        Assert.True(Parse("1.8.0_402-b10") > Parse("1.8.0_402-b06"));

    [Fact]
    public void Compare_OrdersLegacyBuildMarkerAboveBareRelease() =>
        Assert.True(Parse("1.8.0_402-b10") > Parse("1.8.0_402"));

    // The legacy JDK 8 build marker only counts in the underscore notation; a
    // modern version follows JEP 223, where a dash opens a pre-release.
    [Fact]
    public void TryParse_ModernDashB_IsPreRelease()
    {
        var version = Parse("21.0.5-b10");

        Assert.True(version.IsPreRelease);
        Assert.True(version < Parse("21.0.5"));
    }

    [Fact]
    public void Compare_OrdersDottedBuildSuffixesNaturally() =>
        Assert.True(Parse("21.0.2+13.1") < Parse("21.0.2+13.2"));

    // GraalVM's five-component versions must stay parsed and ordered, not degrade
    // to Unknown. The fifth component outranks the build number, as in the JDK.
    [Fact]
    public void Compare_OrdersFifthComponentAboveBuildNumber() =>
        Assert.True(Parse("25.0.4.1.1") > Parse("25.0.4.1+1"));

    [Fact]
    public void Compare_OrdersFifthComponentAboveItsFourComponentPrefix() =>
        Assert.True(Parse("25.0.4.1.1") > Parse("25.0.4.1"));

    [Fact]
    public void Compare_OrdersFifthComponentBelowNextFourthComponent() =>
        Assert.True(Parse("25.0.4.1.1") < Parse("25.0.4.2"));

    [Fact]
    public void Compare_OrdersFifthComponentsNaturally() =>
        Assert.True(Parse("25.0.4.1.2") > Parse("25.0.4.1.1"));

    [Fact]
    public void Compare_TreatsTrailingZeroComponentsAsAbsent() =>
        Assert.Equal(0, Parse("25.0.4.1.0").CompareTo(Parse("25.0.4.1")));

    [Fact]
    public void Compare_OrdersGraalVmAboveEveryFourComponentRival() =>
        Assert.True(Parse("25.0.4.1.1") > Parse("21.0.12.1"));

    [Fact]
    public void TryParse_FiveComponentWithBuildAndTag_KeepsThemOrdered()
    {
        var version = Parse("25.0.4.1.1+1-jvmci-25.4-b23");

        Assert.False(version.IsPreRelease);
        Assert.True(version > Parse("25.0.4.1.1"));
        Assert.True(version < Parse("25.0.4.1.2"));
    }

    // Same build number, one form carries a tag: the plain form orders first.
    [Fact]
    public void Compare_OrdersPlainBuildBeforeItsTaggedForm() =>
        Assert.True(Parse("21.0.5+9") < Parse("21.0.5+9-LTS"));

    [Fact]
    public void Compare_OrdersUnparseableStringsNaturally()
    {
        Assert.True(JvmVersion.Unknown("alpha") < JvmVersion.Unknown("beta"));
        Assert.True(JvmVersion.Unknown("b9") < JvmVersion.Unknown("b10"));
    }

    [Fact]
    public void Compare_OrdersUnparseableStringsBeforeEveryKnownVersion() =>
        Assert.True(JvmVersion.Parse("not-a-version") < Parse("1.8.0_402"));

    [Fact]
    public void Equals_TreatsZeroFilledComponentsAsEqual()
    {
        Assert.Equal(Parse("17.0.2"), Parse("17.0.2.0"));
        Assert.True(Parse("17.0.2") == Parse("17.0.2.0"));
    }

    [Fact]
    public void Equals_DistinguishesBuildSuffixes()
    {
        Assert.NotEqual(Parse("21.0.5"), Parse("21.0.5+9"));
        Assert.True(Parse("21.0.5") != Parse("21.0.5+9"));
    }

    [Fact]
    public void Equals_FoldsTheSuffixCase() =>
        Assert.Equal(Parse("21.0.5+9-LTS"), Parse("21.0.5+9-lts"));

    [Fact]
    public void Equals_UnparseableValuesFoldTheSameWay()
    {
        Assert.Equal(JvmVersion.Unknown("B10"), JvmVersion.Unknown("b10"));
        Assert.Equal(JvmVersion.Unknown("b010"), JvmVersion.Unknown("b10"));
        Assert.NotEqual(JvmVersion.Unknown("b10"), JvmVersion.Unknown("b11"));
    }

    // CompareTo == 0 must imply equal hash codes, or the type breaks as a
    // dictionary or set key.
    [Fact]
    public void GetHashCode_AgreesWithEquality()
    {
        foreach (var (left, right) in new[]
                 {
                     ("17.0.2", "17.0.2.0"),
                     ("21.0.5+9-LTS", "21.0.5+9-lts"),
                     ("21.0.5+09", "21.0.5+9"),
                 })
        {
            var x = Parse(left);
            var y = Parse(right);

            Assert.Equal(0, x.CompareTo(y));
            Assert.Equal(x.GetHashCode(), y.GetHashCode());
        }

        Assert.Equal(JvmVersion.Unknown("B10").GetHashCode(), JvmVersion.Unknown("b10").GetHashCode());
    }

    [Fact]
    public void Core_UnparsedAndDefaultValues_ReportTheZeroVersion()
    {
        var zero = new Version(0, 0, 0, 0);

        Assert.Equal(zero, JvmVersion.Unknown("x").Core);
        Assert.Equal(zero, JvmVersion.Parse("not-a-version").Core);
        Assert.Equal(zero, default(JvmVersion).Core);
    }

    // A default value must not hand null out of its non-nullable members.
    [Fact]
    public void DefaultValue_ReportsEmptyStringMembers()
    {
        var version = default(JvmVersion);

        Assert.Equal(string.Empty, version.Original);
        Assert.Equal(string.Empty, version.ToString());
        Assert.False(version.IsPreRelease);
        Assert.Equal(0, version.CompareTo(JvmVersion.Unknown("")));
    }

    [Fact]
    public void ToString_ReturnsTheOriginalString()
    {
        Assert.Equal("21.0.5+9-LTS", Parse("21.0.5+9-LTS").ToString());
        Assert.Equal("25.0.4.1.1", Parse("25.0.4.1.1").ToString());
    }

    // The -b<digits> carve-out must not swallow the real JDK 8 early-access shape.
    [Fact]
    public void TryParse_LegacyEarlyAccess_StaysPreRelease()
    {
        var version = Parse("1.8.0_202-ea");

        Assert.True(version.IsPreRelease);
        Assert.True(version < Parse("1.8.0_202"));
    }

    [Fact]
    public void Parse_UnparseableInput_DegradesToUnknown()
    {
        var version = JvmVersion.Parse("unknown");

        Assert.Equal("unknown", version.Original);
        Assert.Equal(JvmVersion.Unknown("unknown"), version);
    }

    private static JvmVersion Parse(string javaVersion)
    {
        Assert.True(JvmVersion.TryParse(javaVersion, out var version));
        return version;
    }
}
