using JdkFind.Providers;

namespace JdkFind.Tests;

public class JdkFinderTests : IDisposable
{
    private readonly TempDirectory temp = new();

    private sealed class StubJvmProvider(string name, params string[] homes) : IJvmProvider
    {
        public string Name { get; } = name;

        public IEnumerable<string> GetJavaHomes() => homes;
    }

    [Fact]
    public void Locate_EnrichesMetadataFromReleaseFile()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");
        var options = new JdkFindOptions { Providers = [new StubJvmProvider("stub", jdk)] };

        var jvms = JdkFinder.Locate(options).ToList();

        var jvm = Assert.Single(jvms);
        Assert.Equal(["stub"], jvm.Providers);
        Assert.Equal("21.0.5", jvm.Version.Original);
        Assert.Equal(new Version(21, 0, 5), jvm.Version.Core);
        Assert.Equal(21, jvm.LanguageVersion);
        Assert.Equal("Test Vendor", jvm.Vendor);
        Assert.Equal("aarch64", jvm.Architecture);
        Assert.Equal(jdk, jvm.Home.FullName);
    }

    [Fact]
    public void Locate_DeduplicatesAndMergesAllSources()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");
        var options = new JdkFindOptions
        {
            Providers = [new StubJvmProvider("first", jdk), new StubJvmProvider("second", jdk)],
        };

        var jvms = JdkFinder.Locate(options).ToList();

        var jvm = Assert.Single(jvms);
        Assert.Equal(["first", "second"], jvm.Providers);
    }

    [Fact]
    public void Locate_RepeatedCandidatesFromOneSource_RecordTheNameOnce()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");
        var options = new JdkFindOptions { Providers = [new StubJvmProvider("dup", jdk, jdk)] };

        var jvms = JdkFinder.Locate(options).ToList();

        var jvm = Assert.Single(jvms);
        Assert.Equal(["dup"], jvm.Providers);
    }

    [Fact]
    public void Locate_CanDisableDeduplication()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");
        var options = new JdkFindOptions
        {
            Providers = [new StubJvmProvider("first", jdk), new StubJvmProvider("second", jdk)],
            DeduplicateHomes = false,
        };

        var jvms = JdkFinder.Locate(options).ToList();

        // Each candidate stays its own entry with a single source — the merge logic
        // must not leak into the dedup-off branch.
        Assert.Equal(2, jvms.Count);
        Assert.All(jvms, jvm => Assert.Single(jvm.Providers));
    }

    [Fact]
    public void Locate_PreservesFirstDiscoveryOrderAcrossProviders()
    {
        var a = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "a");
        var b = TestJdk.Create(temp.FullPath, "17.0.2", "jdks", "b");
        var c = TestJdk.Create(temp.FullPath, "11", "jdks", "c");
        var options = new JdkFindOptions
        {
            // Interleaved and repeated candidates: p1=[a,b,a], p2=[b,c], p3=[a].
            Providers =
            [
                new StubJvmProvider("p1", a, b, a),
                new StubJvmProvider("p2", b, c),
                new StubJvmProvider("p3", a),
            ],
        };

        var jvms = JdkFinder.Locate(options).ToList();

        Assert.Equal([a, b, c], jvms.Select(jvm => jvm.Home.FullName).ToArray());
        Assert.Equal(["p1", "p3"], jvms[0].Providers);
        Assert.Equal(["p1", "p2"], jvms[1].Providers);
        Assert.Equal(["p2"], jvms[2].Providers);
    }

    [Fact]
    public void Locate_SameNameAcrossProviderInstances_RecordsTheNameOnce()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");
        var options = new JdkFindOptions
        {
            Providers = [new StubJvmProvider("x", jdk), new StubJvmProvider("x", jdk), new StubJvmProvider("y", jdk)],
        };

        var jvms = JdkFinder.Locate(options).ToList();

        var jvm = Assert.Single(jvms);
        Assert.Equal(["x", "y"], jvm.Providers);
    }

    [Fact]
    public void Locate_DeduplicatesCaseInsensitivelyOnMacOs()
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

        Assert.Single(JdkFinder.Locate(options).ToList());
    }

    [Fact]
    public void Find_FiltersByFeatureVersion()
    {
        var jdk8 = TestJdk.Create(temp.FullPath, "1.8.0_402", "jdks", "jdk8");
        TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk21");
        var options = new JdkFindOptions
        {
            Providers = [new StubJvmProvider("stub", jdk8, Path.Combine(temp.FullPath, "jdks", "jdk21"))],
        };

        var jvms = JdkFinder.Find(8, options).ToList();

        var jvm = Assert.Single(jvms);
        Assert.Equal(jdk8, jvm.Home.FullName);
    }

    [Fact]
    public void GetNewest_PrefersHigherFeatureVersionThenPatch()
    {
        var jdk8 = TestJdk.Create(temp.FullPath, "1.8.0_402", "jdks", "jdk8");
        var jdk21 = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk21");
        var jdk21Older = TestJdk.Create(temp.FullPath, "21.0.1", "jdks", "jdk21-old");
        var options = new JdkFindOptions
        {
            Providers = [new StubJvmProvider("stub", jdk8, jdk21, jdk21Older)],
        };

        var newest = JdkFinder.GetNewest(options);

        Assert.NotNull(newest);
        Assert.Equal(jdk21, newest.Home.FullName);
    }

    // The two GetDefault tests below mutate the process-wide JAVA_HOME. xunit.v3 runs
    // tests serially within a class and in parallel across classes, so the safety
    // boundary is "only this class touches the environment": other test classes must
    // not build a default JdkFindOptions (its parameterless JavaHomeJvmProvider reads
    // JAVA_HOME); inject providers or roots instead.

    [Fact]
    public void GetDefault_PrefersJavaHomeWhenResolvable()
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
            var defaultJvm = JdkFinder.GetDefault(options);
            Assert.NotNull(defaultJvm);
            Assert.Equal(jdk8, defaultJvm.Home.FullName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JAVA_HOME", original);
        }
    }

    [Fact]
    public void GetDefault_FallsBackToNewestWithoutJavaHome()
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
            var defaultJvm = JdkFinder.GetDefault(options);
            Assert.NotNull(defaultJvm);
            Assert.Equal(jdk21, defaultJvm.Home.FullName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JAVA_HOME", original);
        }
    }

    [Fact]
    public void Locate_ComputesKnownVendorAndDisplayName()
    {
        var jdk = TestJdk.CreateWithImplementor(temp.FullPath, "21.0.5", "Eclipse Adoptium", "jdks", "adoptium");
        var options = new JdkFindOptions
        {
            Providers = [new StubJvmProvider("stub", jdk)],
            ProbeRuntimeProperties = false, // 假 java 不可执行，探测会静默失败——本用例只验证 release 侧。
        };

        var jvm = Assert.Single(JdkFinder.Locate(options).ToList());

        Assert.Equal("Eclipse Adoptium", jvm.Vendor);
        Assert.Equal(JvmVendor.Adoptium, jvm.KnownVendor);
        Assert.Equal("Eclipse Temurin", jvm.VendorDisplayName);
    }

    [Fact]
    public void GetNewest_UnparseableVersion_SortsBeforeParsedVersions()
    {
        // An exotic JAVA_VERSION must not lose the JVM — it just sorts as unknown.
        var exotic = TestJdk.Create(temp.FullPath, "unknown", "jdks", "exotic");
        var jdk21 = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk21");
        var options = new JdkFindOptions
        {
            Providers = [new StubJvmProvider("stub", exotic, jdk21)],
        };

        var newest = JdkFinder.GetNewest(options);

        Assert.NotNull(newest);
        Assert.Equal(jdk21, newest.Home.FullName);
    }

    [Fact]
    public void Locate_MissingReleaseFile_IsSkipped()
    {
        var broken = Path.Combine(temp.FullPath, "broken");
        Directory.CreateDirectory(Path.Combine(broken, "bin"));
        File.WriteAllText(Path.Combine(broken, "bin", JavaHomeLayout.JavaExecutableName), string.Empty);
        var options = new JdkFindOptions { Providers = [new StubJvmProvider("stub", broken)] };

        Assert.Empty(JdkFinder.Locate(options).ToList());
    }

    [Fact]
    public void Locate_UnreadableReleaseFile_IsSkipped()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
            return; // Unix permission semantics; chmod has no effect when running as root.

        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "locked-jdk");
        File.SetUnixFileMode(Path.Combine(jdk, "release"), UnixFileMode.None);
        var options = new JdkFindOptions { Providers = [new StubJvmProvider("stub", jdk)] };

        // An access denial must not crash the scan; the candidate is simply skipped.
        Assert.Empty(JdkFinder.Locate(options).ToList());
    }

    [Fact]
    public void Scan_UnreadablePrefix_IsSkipped()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
            return;

        TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "good");
        var locked = Path.Combine(temp.FullPath, "locked-prefix");
        Directory.CreateDirectory(locked);
        File.SetUnixFileMode(locked, UnixFileMode.None);
        try
        {
            Assert.Empty(new IntelliJJvmProvider(locked).GetHomes());
        }
        finally
        {
            // Restore permissions, otherwise TempDirectory cleanup cannot delete recursively.
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    public void Dispose() => temp.Dispose();
}
