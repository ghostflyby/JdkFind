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
    public void Jvm_ValueEqualityCoversEveryStoredProperty()
    {
        var jvm = CreateJvm("/jvm/one");

        Assert.NotEqual(jvm, jvm with { Home = new DirectoryInfo("/jvm/two") });
        Assert.NotEqual(jvm, jvm with { Providers = ["other"] });
        Assert.NotEqual(jvm, jvm with { Executable = jvm.Executable with { Path = "/other/java" } });
        Assert.NotEqual(jvm, jvm with { Version = JvmVersion.Parse("21.0.6") });
        Assert.NotEqual(jvm, jvm with { LanguageVersion = 17 });
        Assert.NotEqual(jvm, jvm with { HasCompiler = false });
        Assert.NotEqual(jvm, jvm with { VendorRaw = "other" });
        Assert.NotEqual(jvm, jvm with { Vendor = JvmVendor.Azul });
        Assert.NotEqual(jvm, jvm with { Distribution = JvmDistribution.Zulu });
    }

    [Fact]
    public void JavaExecutable_ValueEqualityCoversEveryStoredProperty()
    {
        var executable = CreateExecutable("/jvm/one");

        Assert.NotEqual(executable, executable with { Path = "/other/java" });
        Assert.NotEqual(executable, executable with { Home = new DirectoryInfo("/jvm/two") });
        Assert.NotEqual(executable, executable with { StartFailure = "failed" });
        Assert.NotEqual(executable, executable with { Version = JvmVersion.Parse("21.0.6") });
        Assert.NotEqual(executable, executable with { VendorRaw = "other" });
        Assert.NotEqual(executable, executable with { Vendor = JvmVendor.Azul });
        Assert.NotEqual(executable, executable with { OsName = "other" });
        Assert.NotEqual(executable, executable with { Architecture = "other" });
        Assert.NotEqual(executable, executable with { RuntimeName = "other" });
        Assert.NotEqual(executable, executable with { RuntimeVersion = "other" });
        Assert.NotEqual(executable, executable with { VmName = "other" });
        Assert.NotEqual(executable, executable with { VmVersion = "other" });
    }

    [Fact]
    public void JvmInfo_ValueEqualityCoversEveryStoredProperty()
    {
        var info = JvmInfo.From(CreateJvm("/jvm/one"));

        Assert.NotEqual(info, info with { Home = new DirectoryInfo("/jvm/two") });
        Assert.NotEqual(info, info with { Version = JvmVersion.Parse("21.0.6") });
        Assert.NotEqual(info, info with { LanguageVersion = 17 });
        Assert.NotEqual(info, info with { HasCompiler = false });
        Assert.NotEqual(info, info with { Vendor = JvmVendor.Azul });
        Assert.NotEqual(info, info with { Distribution = JvmDistribution.Zulu });
        Assert.NotEqual(info, info with { VendorRaw = "other" });
        Assert.NotEqual(info, info with { RuntimeName = "other" });
        Assert.NotEqual(info, info with { RuntimeVersion = "other" });
        Assert.NotEqual(info, info with { VmName = "other" });
        Assert.NotEqual(info, info with { VmVersion = "other" });
        Assert.NotEqual(info, info with { Architecture = "other" });
        Assert.NotEqual(info, info with { OsName = "other" });
        Assert.NotEqual(info, info with { Providers = ["other"] });
    }

    [Fact]
    public void JavaExecutable_NullHomeIsEqualAndHashes()
    {
        var left = CreateExecutable("/jvm/one") with { Home = null };
        var right = CreateExecutable("/jvm/one") with { Home = null };

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.NotEqual(left, CreateExecutable("/jvm/one"));
    }

    [Fact]
    public void ProviderSequences_HandleNullAndEmpty()
    {
        var empty = JvmInfo.From(CreateJvm("/jvm/one", []));
        var emptyAgain = JvmInfo.From(CreateJvm("/jvm/one", []));

        Assert.Equal(empty, emptyAgain);
        Assert.Equal(empty.GetHashCode(), emptyAgain.GetHashCode());
        Assert.NotEqual(empty, JvmInfo.From(CreateJvm("/jvm/one", ["test"])));

        var nullProviders = JvmInfo.From(CreateJvm("/jvm/one")) with { Providers = null! };
        var nullProvidersAgain = JvmInfo.From(CreateJvm("/jvm/one")) with { Providers = null! };

        Assert.Equal(nullProviders, nullProvidersAgain);
        Assert.Equal(nullProviders.GetHashCode(), nullProvidersAgain.GetHashCode());
    }

    [Fact]
    public void ProviderSequence_NullElementDoesNotBreakHashing()
    {
        var info = JvmInfo.From(CreateJvm("/jvm/one")) with
        {
            Providers = new string?[] { null }!,
        };

        Assert.NotEqual(info, JvmInfo.From(CreateJvm("/jvm/one")));
        _ = info.GetHashCode();
    }

    [Fact]
    public void JavaExecutablePathCase_FollowsThePlatform()
    {
        var left = CreateExecutable("/jvm/one");
        var right = left with { Path = Path.Combine("/jvm/One", "bin", "java") };

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            Assert.Equal(left, right);
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }
        else
        {
            Assert.NotEqual(left, right);
        }
    }

    [Fact]
    public void PathCase_FollowsThePlatform()
    {
        var left = JvmInfo.From(CreateJvm("/jvm/One"));
        var right = JvmInfo.From(CreateJvm("/jvm/one"));

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            Assert.Equal(left, right);
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }
        else
        {
            Assert.NotEqual(left, right);
        }
    }
}
