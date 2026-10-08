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

        var executable = new JdkFinder().FromExecutable(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName));

        Assert.NotNull(executable);
        Assert.Null(executable.StartFailure);
        // The binary's own word beats the release file's claim.
        Assert.Equal("17.0.9", executable.Version.Original);
        Assert.Equal("Probe Vendor", executable.VendorRaw);
        Assert.Equal("testarch", executable.Architecture);
        Assert.Equal("TestOS", executable.OsName);
        Assert.Equal("Probe Runtime", executable.RuntimeName);
        Assert.Equal("Probe VM", executable.VmName);
        Assert.Equal(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName), executable.Path);
    }

    [Fact]
    public void FromExecutable_SymlinkedLauncher_ReportsTheRealHome()
    {
        if (OperatingSystem.IsWindows())
            return; // Symbolic link creation needs privileges.

        // A launcher reached through a symlink cannot be resolved by walking up
        // from its location — only its own reported java.home knows the home.
        var home = Path.Combine(temp.FullPath, "fake-jdk");
        var script = $"cat >&2 <<'EOPROPE'\njava.version = 21.0.5\njava.home = {home}\nEOPROPE\nexit 0\n";
        Assert.Equal(home, CreateFakeHome(temp.FullPath, "21.0.5", script));

        var link = Path.Combine(temp.FullPath, "linked-java");
        File.CreateSymbolicLink(link, Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName));

        var executable = new JdkFinder().FromExecutable(link);

        Assert.NotNull(executable);
        Assert.Equal("21.0.5", executable.Version.Original);
        Assert.Equal(home, executable.Home?.FullName); // Geometric derivation cannot see through the link.
        var installation = executable.Installation;
        Assert.NotNull(installation);
        Assert.Equal(home, installation.Home.FullName);
    }

    [Fact]
    public void FromExecutable_ExposesDerivedHome()
    {
        if (OperatingSystem.IsWindows())
            return; // The fake java executable is a POSIX shell script.

        var home = CreateFakeHome(temp.FullPath, "21.0.5", "exit 0");

        var executable = new JdkFinder().FromExecutable(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName));

        Assert.NotNull(executable);
        Assert.Equal(home, executable.Home?.FullName);
    }

    [Fact]
    public void FromExecutable_PromotesTheOuterJdkOverTheReportedInnerJre()
    {
        if (OperatingSystem.IsWindows())
            return; // The fake java executable is a POSIX shell script.

        // JDK 8 layout: java.home reports the inner JRE; the outer JDK owns javac.
        var outer = Path.Combine(temp.FullPath, "jdk1.8");
        var jre = Path.Combine(outer, "jre");
        Directory.CreateDirectory(Path.Combine(outer, "bin"));
        Directory.CreateDirectory(Path.Combine(jre, "bin"));
        File.WriteAllText(Path.Combine(outer, "release"), "JAVA_VERSION=\"1.8.0_402\"\n");
        File.WriteAllText(Path.Combine(outer, "bin", JavaHomeLayout.JavaExecutableName), string.Empty);
        File.WriteAllText(Path.Combine(outer, "bin", JavaHomeLayout.CompilerExecutableName), string.Empty);
        File.WriteAllText(Path.Combine(jre, "release"), "JAVA_VERSION=\"1.8.0_402\"\n");
        var java = Path.Combine(jre, "bin", JavaHomeLayout.JavaExecutableName);
        File.WriteAllText(java,
            $"#!/bin/sh\ncat >&2 <<'EOPROPE'\njava.version = 1.8.0_402\njava.home = {jre}\nEOPROPE\nexit 0\n");
        File.SetUnixFileMode(java, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var executable = new JdkFinder().FromExecutable(Path.Combine(jre, "bin", JavaHomeLayout.JavaExecutableName));

        Assert.NotNull(executable);
        Assert.Equal(outer, executable.Home?.FullName); // Promoted over the reported inner JRE.
        Assert.Equal("1.8.0_402", executable.Version.Original);
        var installation = executable.Installation;
        Assert.NotNull(installation);
        Assert.Equal(outer, installation.Home.FullName);
        Assert.Equal(
            Path.Combine(outer, "bin", JavaHomeLayout.CompilerExecutableName),
            installation.Resolve("javac")?.FullName); // javac is reachable again.
    }

    [Fact]
    public void FromExecutable_StandaloneJre_KeepsItsOwnHome()
    {
        if (OperatingSystem.IsWindows())
            return; // The fake java executable is a POSIX shell script.

        var home = CreateFakeHome(temp.FullPath, "17.0.5",
            $"cat >&2 <<'EOPROPE'\njava.version = 17.0.5\njava.home = {Path.Combine(temp.FullPath, "fake-jdk")}\nEOPROPE\nexit 0\n");

        var executable = new JdkFinder().FromExecutable(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName));

        Assert.NotNull(executable);
        Assert.Equal(home, executable.Home?.FullName); // The parent is not a home — no promotion.
    }

    [Fact]
    public void FromExecutable_StandaloneBinary_HasNoHome()
    {
        if (OperatingSystem.IsWindows())
            return; // The fake java executable is a POSIX shell script.

        var directory = Path.Combine(temp.FullPath, "standalone");
        Directory.CreateDirectory(directory);
        var java = Path.Combine(directory, JavaHomeLayout.JavaExecutableName);
        File.WriteAllText(java, "#!/bin/sh\ncat >&2 <<'EOPROBE'\njava.version = 21.0.5\nEOPROBE\nexit 0\n");
        File.SetUnixFileMode(java, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var executable = new JdkFinder().FromExecutable(java);

        Assert.NotNull(executable);
        Assert.Equal("21.0.5", executable.Version.Original);
        Assert.Null(executable.Home);
        Assert.Null(executable.Installation); // No recognizable home — nothing to derive.
    }

    [Fact]
    public void Installation_DerivesStandaloneInstallationWithoutSpawning()
    {
        if (OperatingSystem.IsWindows())
            return; // The fake java executable is a POSIX shell script.

        var home = CreateFakeHome(temp.FullPath, "17.0.5", "exit 0");

        var executable = new JdkFinder().FromExecutable(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName));
        Assert.NotNull(executable);

        var installation = executable.Installation;

        Assert.NotNull(installation);
        Assert.Equal(home, installation.Home.FullName);
        Assert.Same(executable, installation.Executable); // The derivation reuses this instance — no spawn.
        Assert.Equal("17.0.5", installation.Version.Original);
        Assert.False(installation.HasCompiler); // No javac in the fixture.
        Assert.Empty(installation.Providers);   // Discovery data cannot be restored.
    }

    [Fact]
    public void FromExecutable_RejectsShowSettings_ReportsStartFailure()
    {
        if (OperatingSystem.IsWindows())
            return; // The fake java executable is a POSIX shell script.

        // An ancient runtime rejecting the option is a start failure, not a
        // compatibility case: the release metadata still stands in.
        var home = CreateFakeHome(temp.FullPath, "21.0.5",
            "echo 'Unrecognized option: -XshowSettings:properties' >&2\nexit 1");

        var executable = new JdkFinder().FromExecutable(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName));

        Assert.NotNull(executable);
        Assert.Contains("exit code 1", executable.StartFailure);
        Assert.Contains("Unrecognized option", executable.StartFailure);
        Assert.Equal("21.0.5", executable.Version.Original);
    }

    [Fact]
    public void FromExecutable_NonJavaBinary_ReturnsNull()
    {
        if (OperatingSystem.IsWindows())
            return; // The fake java executable is a POSIX shell script.

        var home = CreateFakeHome(temp.FullPath, string.Empty, "echo 'total garbage' >&2\nexit 1",
            includeRelease: false);

        Assert.Null(new JdkFinder().FromExecutable(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName)));
    }

    [Fact]
    public void FromExecutable_Timeout_ReportsStartFailure()
    {
        if (OperatingSystem.IsWindows())
            return; // The fake java executable is a POSIX shell script.

        var home = CreateFakeHome(temp.FullPath, "17.0.5", "sleep 30");

        var executable = new JdkFinder().FromExecutable(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName),
            TimeSpan.FromMilliseconds(200));

        Assert.NotNull(executable);
        Assert.Contains("timed out", executable.StartFailure);
    }

    [Fact]
    public async Task FromExecutableAsync_PreCancelledToken_Throws()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new JdkFinder().FromExecutableAsync(Path.Combine(temp.FullPath, "any"), new CancellationToken(true)));
    }

    [Fact]
    public void FromExecutable_BlankPath_Throws()
    {
        // ReSharper disable once NullableWarningSuppressionIsUsed
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
        Assert.Null(jvm.Executable.StartFailure);
        Assert.Equal("17.0.5", jvm.Version.Original);
    }

    [Fact]
    public async Task FromHomeAsync_MissingDirectory_ReturnsNull()
    {
        Assert.Null(await new JdkFinder().FromHomeAsync(Path.Combine(temp.FullPath, "missing"),
            TestContext.Current.CancellationToken));
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
    private static string CreateFakeHome(string root, string releaseVersion, string scriptBody,
        bool includeRelease = true)
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
