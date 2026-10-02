namespace JdkFind.Tests;

public class ReleaseFileTests : IDisposable
{
    private readonly TempDirectory temp = new();

    [Theory]
    [InlineData("21.0.5", 21)]
    [InlineData("17.0.2", 17)]
    [InlineData("1.8.0_402", 8)]
    [InlineData("1.8.0_402-b10", 8)]
    [InlineData("1.7.0_80", 7)]
    [InlineData("25-ea", 25)]
    [InlineData("11", 11)]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("unknown", null)]
    public void TryGetLanguageVersion_ExtractsFeatureVersion(string? javaVersion, int? expected) =>
        Assert.Equal(expected, ReleaseFile.TryGetLanguageVersion(javaVersion));

    [Fact]
    public void Parse_ReadsQuotedEntriesAndSkipsNoise()
    {
        var releasePath = Path.Combine(temp.FullPath, "release");
        File.WriteAllLines(releasePath,
        [
            "# a comment",
            string.Empty,
            "JAVA_VERSION=\"21.0.5\"",
            "IMPLEMENTOR=\"Say \\\"hi\\\"\"",
            "OS_ARCH=aarch64",
            "malformed-line",
        ]);

        var release = ReleaseFile.Parse(releasePath);

        Assert.Equal("21.0.5", release["JAVA_VERSION"]);
        Assert.Equal("Say \"hi\"", release["IMPLEMENTOR"]);
        Assert.Equal("aarch64", release["OS_ARCH"]);
        Assert.False(release.ContainsKey("malformed-line"));
    }

    public void Dispose() => temp.Dispose();
}
