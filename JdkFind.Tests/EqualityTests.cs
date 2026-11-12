namespace JdkFind.Tests;

/// <summary>Pins the value semantics of the locator's immutable records.</summary>
public class EqualityTests
{
    private static Jvm CreateJvm(string home, IReadOnlyList<string>? providers = null, string version = "21.0.5") => new()
    {
        Home = new DirectoryInfo(home),
        Providers = providers ?? ["test"],
        Executable = CreateExecutable(home, version),
        Version = JvmVersion.Parse(version),
        LanguageVersion = 21,
        HasCompiler = true,
        VendorRaw = "release vendor",
        Vendor = JvmVendor.Unknown,
        Distribution = JvmDistribution.Unknown,
    };

    private static JavaExecutable CreateExecutable(string home, string version = "21.0.5") => new()
    {
        Path = Path.Combine(home, "bin", "java"),
        Home = new DirectoryInfo(home),
        Version = JvmVersion.Parse(version),
        VendorRaw = "probe vendor",
        Vendor = JvmVendor.Unknown,
        OsName = "Linux",
        Architecture = "x64",
        RuntimeName = "Runtime",
        RuntimeVersion = version + "+1",
        VmName = "VM",
        VmVersion = version + "+1",
    };

    [Fact]
    public void Jvm_UsesValueEqualityForDirectoryInfo()
    {
        var left = CreateJvm("/jvm/one");
        var right = CreateJvm("/jvm/one");

        Assert.NotSame(left.Home, right.Home);
        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Jvm_ProviderOrderIsPartOfEquality()
    {
        var left = CreateJvm("/jvm/one", ["a", "b"]);
        var right = CreateJvm("/jvm/one", ["b", "a"]);

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void Jvm_DifferentVersionIsNotEqual()
    {
        Assert.NotEqual(CreateJvm("/jvm/one", version: "21.0.5"), CreateJvm("/jvm/one", version: "21.0.6"));
    }

    [Fact]
    public void JavaExecutable_UsesValueEqualityForHome()
    {
        var left = CreateExecutable("/jvm/one");
        var right = CreateExecutable("/jvm/one");

        Assert.NotSame(left.Home, right.Home);
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void JvmInfo_UsesValueEqualityForHome()
    {
        var left = JvmInfo.From(CreateJvm("/jvm/one"));
        var right = JvmInfo.From(CreateJvm("/jvm/one"));

        Assert.NotSame(left.Home, right.Home);
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void JvmInfo_ProviderOrderIsPartOfEquality()
    {
        var left = JvmInfo.From(CreateJvm("/jvm/one", ["a", "b"]));
        var right = JvmInfo.From(CreateJvm("/jvm/one", ["b", "a"]));

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void PathCase_FollowsThePlatform()
    {
        var left = JvmInfo.From(CreateJvm("/jvm/One"));
        var right = JvmInfo.From(CreateJvm("/jvm/one"));

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            Assert.Equal(left, right);
        else
            Assert.NotEqual(left, right);
    }
}
