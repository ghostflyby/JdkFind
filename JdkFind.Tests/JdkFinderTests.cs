using JdkFind.Providers;

namespace JdkFind.Tests;

public class JdkFinderTests : IDisposable
{
    private readonly TempDirectory temp = new();

    private sealed class StubJvmProvider(string name, params string[] homes) : IJvmProvider
    {
        public string Name { get; } = name;

        public IAsyncEnumerable<string> GetJavaHomesAsync(CancellationToken cancellationToken = default) =>
            homes.ToAsyncEnumerable();
    }

    [Fact]
    public async Task Locate_EnrichesMetadataFromReleaseFile()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");
        var options = new JdkFindOptions { Providers = [new StubJvmProvider("stub", jdk)] };

        var jvms = await JdkFinder.LocateAsync(options, TestContext.Current.CancellationToken).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        var jvm = Assert.Single(jvms);
        Assert.Equal("stub", jvm.Provider);
        Assert.Equal("21.0.5", jvm.Version);
        Assert.Equal(21, jvm.LanguageVersion);
        Assert.Equal("Test Vendor", jvm.Vendor);
        Assert.Equal("aarch64", jvm.Architecture);
        Assert.Equal(jdk, jvm.Home.FullName);
    }

    [Fact]
    public async Task Locate_DeduplicatesAcrossProvidersKeepingFirst()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");
        var options = new JdkFindOptions
        {
            Providers = [new StubJvmProvider("first", jdk), new StubJvmProvider("second", jdk)],
        };

        var jvms = await JdkFinder.LocateAsync(options, TestContext.Current.CancellationToken).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        var jvm = Assert.Single(jvms);
        Assert.Equal("first", jvm.Provider);
    }

    [Fact]
    public async Task Locate_CanDisableDeduplication()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");
        var options = new JdkFindOptions
        {
            Providers = [new StubJvmProvider("first", jdk), new StubJvmProvider("second", jdk)],
            DeduplicateHomes = false,
        };

        Assert.Equal(2, (await JdkFinder.LocateAsync(options, TestContext.Current.CancellationToken).ToListAsync(cancellationToken: TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task Locate_DeduplicatesCaseInsensitivelyOnMacOs()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsWindows())
            return;

        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "Jdk-21");
        var upper = jdk.ToUpperInvariant();
        if (upper == jdk)
            return;

        var options = new JdkFindOptions
        {
            Providers = [new StubJvmProvider("first", jdk), new StubJvmProvider("second", upper)],
        };

        Assert.Single(await JdkFinder.LocateAsync(options, TestContext.Current.CancellationToken).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FindAsync_FiltersByFeatureVersion()
    {
        var jdk8 = TestJdk.Create(temp.FullPath, "1.8.0_402", "jdks", "jdk8");
        TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk21");
        var options = new JdkFindOptions
        {
            Providers = [new StubJvmProvider("stub", jdk8, Path.Combine(temp.FullPath, "jdks", "jdk21"))],
        };

        var jvms = await JdkFinder.FindAsync(8, options, TestContext.Current.CancellationToken).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        var jvm = Assert.Single(jvms);
        Assert.Equal(jdk8, jvm.Home.FullName);
    }

    [Fact]
    public async Task GetNewest_PrefersHigherFeatureVersionThenPatch()
    {
        var jdk8 = TestJdk.Create(temp.FullPath, "1.8.0_402", "jdks", "jdk8");
        var jdk21 = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk21");
        var jdk21Older = TestJdk.Create(temp.FullPath, "21.0.1", "jdks", "jdk21-old");
        var options = new JdkFindOptions
        {
            Providers = [new StubJvmProvider("stub", jdk8, jdk21, jdk21Older)],
        };

        var newest = await JdkFinder.GetNewestAsync(options, TestContext.Current.CancellationToken);

        Assert.NotNull(newest);
        Assert.Equal(jdk21, newest.Home.FullName);
    }

    // The two GetDefault tests below mutate the process-wide JAVA_HOME. xunit.v3 runs
    // tests serially within a class and in parallel across classes, so the safety
    // boundary is "only this class touches the environment": other test classes must
    // not build a default JdkFindOptions (its parameterless JavaHomeJvmProvider reads
    // JAVA_HOME); inject providers or roots instead.

    [Fact]
    public async Task GetDefault_PrefersJavaHomeWhenResolvable()
    {
        var jdk8 = TestJdk.Create(temp.FullPath, "1.8.0_402", "jdks", "jdk8");
        TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk21");
        var options = new JdkFindOptions
        {
            Providers = [new StubJvmProvider("stub",
                jdk8, Path.Combine(temp.FullPath, "jdks", "jdk21"))],
        };

        var original = Environment.GetEnvironmentVariable("JAVA_HOME");
        Environment.SetEnvironmentVariable("JAVA_HOME", jdk8);
        try
        {
            var defaultJvm = await JdkFinder.GetDefaultAsync(options, TestContext.Current.CancellationToken);
            Assert.NotNull(defaultJvm);
            Assert.Equal(jdk8, defaultJvm.Home.FullName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JAVA_HOME", original);
        }
    }

    [Fact]
    public async Task GetDefault_FallsBackToNewestWithoutJavaHome()
    {
        TestJdk.Create(temp.FullPath, "1.8.0_402", "jdks", "jdk8");
        var jdk21 = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk21");
        var options = new JdkFindOptions
        {
            Providers = [new StubJvmProvider("stub",
                Path.Combine(temp.FullPath, "jdks", "jdk8"), jdk21)],
        };

        var original = Environment.GetEnvironmentVariable("JAVA_HOME");
        Environment.SetEnvironmentVariable("JAVA_HOME", null);
        try
        {
            var defaultJvm = await JdkFinder.GetDefaultAsync(options, TestContext.Current.CancellationToken);
            Assert.NotNull(defaultJvm);
            Assert.Equal(jdk21, defaultJvm.Home.FullName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JAVA_HOME", original);
        }
    }

    [Fact]
    public async Task Locate_MissingReleaseFile_IsSkipped()
    {
        var broken = Path.Combine(temp.FullPath, "broken");
        Directory.CreateDirectory(Path.Combine(broken, "bin"));
        await File.WriteAllTextAsync(Path.Combine(broken, "bin", JavaHomeLayout.JavaExecutableName), string.Empty, TestContext.Current.CancellationToken);
        var options = new JdkFindOptions { Providers = [new StubJvmProvider("stub", broken)] };

        Assert.Empty(await JdkFinder.LocateAsync(options, TestContext.Current.CancellationToken).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Locate_UnreadableReleaseFile_IsSkipped()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
            return; // Unix permission semantics; chmod has no effect when running as root.

        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "locked-jdk");
        File.SetUnixFileMode(Path.Combine(jdk, "release"), UnixFileMode.None);
        var options = new JdkFindOptions { Providers = [new StubJvmProvider("stub", jdk)] };

        // An access denial must not crash the scan; the candidate is simply skipped.
        Assert.Empty(await JdkFinder.LocateAsync(options, TestContext.Current.CancellationToken).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Scan_UnreadablePrefix_IsSkipped()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
            return;

        TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "good");
        var locked = Path.Combine(temp.FullPath, "locked-prefix");
        Directory.CreateDirectory(locked);
        File.SetUnixFileMode(locked, UnixFileMode.None);
        try
        {
            Assert.Empty(await new IntelliJJvmProvider(locked).GetHomesAsync());
        }
        finally
        {
            // Restore permissions, otherwise TempDirectory cleanup cannot delete recursively.
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    public void Dispose() => temp.Dispose();
}
