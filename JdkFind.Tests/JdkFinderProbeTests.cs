using System.Runtime.Versioning;

namespace JdkFind.Tests;

public class JdkFinderProbeTests : IDisposable
{
    private readonly TempDirectory temp = new();

    [Fact]
    public void FromExecutable_MissingPath_ReturnsNull()
    {
        Assert.Null(new JdkFinder().FromExecutable(Path.Combine(temp.FullPath, "nowhere", "java")));
    }

    [Fact]
    public void FromExecutable_ProbeValuesWinOverRelease()
    {
        if (OperatingSystem.IsWindows())
            return; // The fake java executable is a POSIX shell script.

        var home = CreateFakeHome(temp.FullPath, "21.0.5", """
            cat >&2 <<'EOPROBE'
            java.version = 17.0.9
            java.vendor = Probe Vendor
            java.runtime.name = Probe Runtime
            java.runtime.version = 17.0.9+1
            java.vm.name = Probe VM
            java.vm.version = 17.0.9+1
            os.arch = testarch
            os.name = TestOS
            EOPROBE
            exit 0
            """);

        var jvm = new JdkFinder().FromExecutable(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName));

        Assert.NotNull(jvm);
        Assert.Null(jvm.StartFailure);
        // The binary's own word beats the release file's claim.
        Assert.Equal("17.0.9", jvm.Version.Original);
        Assert.Equal(17, jvm.LanguageVersion);
        Assert.Equal("Probe Vendor", jvm.VendorRaw);
        Assert.Equal("testarch", jvm.Architecture);
        Assert.Equal("TestOS", jvm.OsName);
        Assert.Equal("Probe Runtime", jvm.RuntimeName);
        Assert.Equal("Probe VM", jvm.VmName);
        Assert.Equal(home, jvm.Home.FullName);
        Assert.False(jvm.HasCompiler); // No javac in the fixture.
        Assert.Empty(jvm.Providers); // No provider reported this; the caller did.
    }

    [Fact]
    public void FromExecutable_RejectsShowSettings_ReportsStartFailure()
    {
        if (OperatingSystem.IsWindows())
            return; // The fake java executable is a POSIX shell script.

        // An ancient runtime rejecting the option is a start failure, not a
        // compatibility case: the release metadata still stands in.
        var home = CreateFakeHome(temp.FullPath, "21.0.5", "echo 'Unrecognized option: -XshowSettings:properties' >&2\nexit 1");

        var jvm = new JdkFinder().FromExecutable(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName));

        Assert.NotNull(jvm);
        Assert.Contains("exit code 1", jvm.StartFailure);
        Assert.Contains("Unrecognized option", jvm.StartFailure);
        Assert.Equal("21.0.5", jvm.Version.Original);
    }

    [Fact]
    public void FromExecutable_NonJavaBinary_ReturnsNull()
    {
        if (OperatingSystem.IsWindows())
            return; // The fake java executable is a POSIX shell script.

        var home = CreateFakeHome(temp.FullPath, string.Empty, "echo 'total garbage' >&2\nexit 1", includeRelease: false);

        Assert.Null(new JdkFinder().FromExecutable(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName)));
    }

    [Fact]
    public void FromExecutable_Timeout_ReportsStartFailure()
    {
        if (OperatingSystem.IsWindows())
            return; // The fake java executable is a POSIX shell script.

        var home = CreateFakeHome(temp.FullPath, "17.0.5", "sleep 30");

        var jvm = new JdkFinder().FromExecutable(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName), TimeSpan.FromMilliseconds(200));

        Assert.NotNull(jvm);
        Assert.Contains("timed out", jvm.StartFailure);
    }

    [Fact]
    public async Task FromExecutableAsync_PreCancelledToken_Throws()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new JdkFinder().FromExecutableAsync(Path.Combine(temp.FullPath, "any"), new CancellationToken(true)));
    }

    [Fact]
    public void FromExecutable_BlankPath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new JdkFinder().FromExecutable(null!));
        Assert.Throws<ArgumentException>(() => new JdkFinder().FromExecutable(string.Empty));
        Assert.Throws<ArgumentException>(() => new JdkFinder().FromExecutable("  "));
        Assert.Throws<ArgumentException>(() => new JdkFinder().FromHome("  "));
    }

    [Fact]
    public void FromHome_JreOnlyLayout_HomeIsJreDirectory()
    {
        if (OperatingSystem.IsWindows())
            return; // The fake java executable is a POSIX shell script.

        // A JDK 8 inner-JRE directory is itself a home; the factory keeps it.
        var jre = Path.Combine(temp.FullPath, "bundled-jdk", "jre");
        Directory.CreateDirectory(Path.Combine(jre, "bin"));
        var java = Path.Combine(jre, "bin", JavaHomeLayout.JavaExecutableName);
        File.WriteAllText(java, "#!/bin/sh\nexit 0\n"); // Runs but reports nothing: degrade, not fail.
        File.SetUnixFileMode(java, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.WriteAllText(Path.Combine(jre, "release"), "JAVA_VERSION=\"17.0.5\"\n");

        var jvm = new JdkFinder().FromHome(jre);

        Assert.NotNull(jvm);
        Assert.Equal(jre, jvm.Home.FullName);
        Assert.Null(jvm.StartFailure);
        Assert.Equal("17.0.5", jvm.Version.Original);
    }

    [Fact]
    public async Task FromHomeAsync_MissingDirectory_ReturnsNull()
    {
        Assert.Null(await new JdkFinder().FromHomeAsync(Path.Combine(temp.FullPath, "missing"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void FromHome_MissingDirectory_ReturnsNull()
    {
        Assert.Null(new JdkFinder().FromHome(Path.Combine(temp.FullPath, "missing")));
    }

    [Fact]
    public void EnvScrub_RemovesJvmDomainVariables()
    {
        var environment = new Dictionary<string, string?>
        {
            ["PATH"] = "/usr/bin",
            ["_JAVA_OPTIONS"] = "-Xbroken",
            ["JDK_JAVA_OPTIONS"] = "-Xbroken",
            ["JAVA_TOOL_OPTIONS"] = "-Xbroken",
            ["CLASSPATH"] = "/evil",
            ["LD_PRELOAD"] = "/evil.so",
            ["LD_LIBRARY_PATH"] = "/evil",
        };

        JvmRuntimeProbe.SanitizeEnvironment(environment);

        Assert.Equal(new[] { "PATH" }, environment.Keys);
    }

    /// <summary>Creates a home with a release file and a fake java executable that
    /// runs the given shell body. POSIX only.</summary>
    [UnsupportedOSPlatform("windows")]
    private static string CreateFakeHome(string root, string releaseVersion, string scriptBody, bool includeRelease = true)
    {
        var home = Path.Combine(root, "fake-jdk");
        Directory.CreateDirectory(Path.Combine(home, "bin"));
        var java = Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName);
        File.WriteAllText(java, $"#!/bin/sh\n{scriptBody}\n");
        File.SetUnixFileMode(java, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        if (includeRelease)
            File.WriteAllText(
                Path.Combine(home, "release"),
                $"""
                JAVA_VERSION="{releaseVersion}"
                IMPLEMENTOR="Release Vendor"
                """);
        return home;
    }

    public void Dispose() => temp.Dispose();
}
