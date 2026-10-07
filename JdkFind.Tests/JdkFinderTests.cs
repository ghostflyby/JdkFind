using System.Diagnostics;
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
        var finder = new JdkFinder { Providers = [new StubJvmProvider("stub", jdk)] };

        var jvms = finder.Locate().ToList();

        var jvm = Assert.Single(jvms);
        Assert.Equal(["stub"], jvm.Providers);
        Assert.Equal("21.0.5", jvm.Version.Original);
        Assert.Equal(new Version(21, 0, 5), jvm.Version.Core);
        Assert.Equal(21, jvm.LanguageVersion);
        Assert.Equal(JvmVendor.Unknown, jvm.Vendor);
        Assert.Equal(JvmDistribution.Unknown, jvm.Distribution);
        Assert.Equal("aarch64", jvm.Runtime.Architecture);
        Assert.Equal(jdk, jvm.Home.FullName);
    }

    [Fact]
    public void Locate_DeduplicatesAndMergesAllSources()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");
        var finder = new JdkFinder
        {
            Providers = [new StubJvmProvider("first", jdk), new StubJvmProvider("second", jdk)],
        };

        var jvms = finder.Locate().ToList();

        var jvm = Assert.Single(jvms);
        Assert.Equal(["first", "second"], jvm.Providers);
    }

    [Fact]
    public void Locate_RepeatedCandidatesFromOneSource_RecordTheNameOnce()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");
        var finder = new JdkFinder { Providers = [new StubJvmProvider("dup", jdk, jdk)] };

        var jvms = finder.Locate().ToList();

        var jvm = Assert.Single(jvms);
        Assert.Equal(["dup"], jvm.Providers);
    }

    [Fact]
    public void Locate_CanDisableDeduplication()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");
        var finder = new JdkFinder
        {
            Providers = [new StubJvmProvider("first", jdk), new StubJvmProvider("second", jdk)],
            DeduplicateHomes = false,
        };

        var jvms = finder.Locate().ToList();

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
        var finder = new JdkFinder
        {
            // Interleaved and repeated candidates: p1=[a,b,a], p2=[b,c], p3=[a].
            Providers =
            [
                new StubJvmProvider("p1", a, b, a),
                new StubJvmProvider("p2", b, c),
                new StubJvmProvider("p3", a),
            ],
        };

        var jvms = finder.Locate().ToList();

        Assert.Equal([a, b, c], jvms.Select(jvm => jvm.Home.FullName).ToArray());
        Assert.Equal(["p1", "p3"], jvms[0].Providers);
        Assert.Equal(["p1", "p2"], jvms[1].Providers);
        Assert.Equal(["p2"], jvms[2].Providers);
    }

    [Fact]
    public void Locate_SameNameAcrossProviderInstances_RecordsTheNameOnce()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");
        var finder = new JdkFinder
        {
            Providers = [new StubJvmProvider("x", jdk), new StubJvmProvider("x", jdk), new StubJvmProvider("y", jdk)],
        };

        var jvms = finder.Locate().ToList();

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

        var finder = new JdkFinder
        {
            Providers = [new StubJvmProvider("first", jdk), new StubJvmProvider("second", upper)],
        };

        Assert.Single(finder.Locate().ToList());
    }

    [Fact]
    public void Locate_UnparseableVersion_SortsAsUnknownWithoutLosingTheJvm()
    {
        // An exotic JAVA_VERSION must not lose the JVM — it just sorts as unknown.
        var exotic = TestJdk.Create(temp.FullPath, "unknown", "jdks", "exotic");
        var jdk21 = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk21");
        var finder = new JdkFinder
        {
            Providers = [new StubJvmProvider("stub", exotic, jdk21)],
        };

        var jvms = finder.Locate().ToList();

        Assert.Equal(2, jvms.Count);
        Assert.Equal("unknown", jvms.Single(jvm => jvm.Home.FullName == exotic).Version.Original);
    }

    [Fact]
    public void Locate_ComputesVendorAndDistribution()
    {
        var jdk = TestJdk.CreateWithImplementor(temp.FullPath, "21.0.5", "Eclipse Adoptium", "jdks", "adoptium");
        var finder = new JdkFinder
        {
            Providers = [new StubJvmProvider("stub", jdk)],
            ProbeRuntimeProperties =
                false, // The fake java is not executable, so probing fails silently — this test only verifies the release side.
        };

        var jvm = Assert.Single(finder.Locate().ToList());

        Assert.Equal("Eclipse Adoptium", jvm.VendorRaw);
        Assert.Equal(JvmVendor.Adoptium, jvm.Vendor);
        Assert.Equal(JvmDistribution.Temurin, jvm.Distribution);
    }

    [Fact]
    public void Locate_ProbesInstallationsConcurrently()
    {
        if (OperatingSystem.IsWindows())
            return; // The fake java executable is a POSIX shell script.

        var slow1 = TestJdk.CreateSlowProbe(temp.FullPath, "21.0.5", "jdks", "slow1");
        var slow2 = TestJdk.CreateSlowProbe(temp.FullPath, "17.0.2", "jdks", "slow2");
        var finder = new JdkFinder
        {
            Providers = [new StubJvmProvider("stub", slow1, slow2)],
        };

        var watch = Stopwatch.StartNew();
        var jvms = finder.Locate().ToList();
        watch.Stop();

        Assert.Equal(2, jvms.Count);
        // Sequential probing would take at least 2 × 1s; concurrent probing finishes
        // around 1s plus spawn overhead.
        Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(1600), $"probes did not overlap: {watch.Elapsed}");
    }

    [Fact]
    public void Locate_FlagsCompilerInstallations()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk");
        File.WriteAllText(Path.Combine(jdk, "bin", JavaHomeLayout.CompilerExecutableName), string.Empty);
        var runtime = TestJdk.Create(temp.FullPath, "1.8.0_402", "jdks", "jre");
        var finder = new JdkFinder
        {
            Providers = [new StubJvmProvider("stub", jdk, runtime)],
            ProbeRuntimeProperties = false,
        };

        var jvms = finder.Locate().ToList();

        Assert.Equal(2, jvms.Count);
        Assert.True(jvms[0].HasCompiler);
        Assert.False(jvms[1].HasCompiler);
    }

    [Fact]
    public void Locate_MissingReleaseFile_IsSkipped()
    {
        var broken = Path.Combine(temp.FullPath, "broken");
        Directory.CreateDirectory(Path.Combine(broken, "bin"));
        File.WriteAllText(Path.Combine(broken, "bin", JavaHomeLayout.JavaExecutableName), string.Empty);
        var finder = new JdkFinder { Providers = [new StubJvmProvider("stub", broken)] };

        Assert.Empty(finder.Locate().ToList());
    }

    [Fact]
    public void Locate_UnreadableReleaseFile_IsSkipped()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
            return; // Unix permission semantics; chmod has no effect when running as root.

        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "locked-jdk");
        File.SetUnixFileMode(Path.Combine(jdk, "release"), UnixFileMode.None);
        var finder = new JdkFinder { Providers = [new StubJvmProvider("stub", jdk)] };

        // An access denial must not crash the scan; the candidate is simply skipped.
        Assert.Empty(finder.Locate().ToList());
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

    [Fact]
    public void CreateDefaultProviders_IncludesEverySource()
    {
        string[] expected =
        [
            "java-home", "path", "macos", "unix", "flatpak", "homebrew",
            "windows-programs", "windows-registry", "intellij", "sdkman",
            "asdf", "gradle", "jabba", "scoop",
        ];

        Assert.Equal(expected, JdkFinder.Default.Providers.Select(p => p.Name).ToArray());
    }

    public void Dispose() => temp.Dispose();
}
