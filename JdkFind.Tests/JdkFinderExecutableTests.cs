namespace JdkFind.Tests;

public sealed class JdkFinderExecutableTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void LocateExecutables_HomeBacked_ReturnsTheHomeJava()
    {
        var home = TestJdk.Create(_temp.FullPath, "21.0.6", "jdk");
        var finder = new JdkFinder
        {
            Providers = [new StubJvmProvider("stub", home)],
            ProbeRuntimeProperties = false,
        };

        var executable = Assert.Single(finder.LocateExecutables());

        Assert.Equal(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName), executable.Path);
        Assert.NotNull(executable.Home);
        Assert.Equal(home, executable.Home.FullName);
        Assert.Equal(JvmVersion.Parse("21.0.6"), executable.Version);
        Assert.Null(executable.StartFailure);
    }

    [Fact]
    public void LocateExecutables_StandaloneBinary_HasNoHomeAndIsStillProbed()
    {
        if (OperatingSystem.IsWindows())
            return;

        var directory = Path.Combine(_temp.FullPath, "standalone");
        var java = TestJdk.CreateStandaloneJava(directory, "21.0.6");
        var finder = new JdkFinder
        {
            ExecutableDirectories = [directory],
            ProbeRuntimeProperties = false,
        };

        var executable = Assert.Single(finder.LocateExecutables());

        Assert.Equal(java, executable.Path);
        Assert.Null(executable.Home);
        Assert.Equal(JvmVersion.Parse("21.0.6"), executable.Version);
        Assert.Null(executable.StartFailure);
    }

    [Fact]
    public void LocateExecutables_DeduplicatesHomeExecutableFoundThroughExecutableDirectory()
    {
        var home = TestJdk.Create(_temp.FullPath, "21.0.6", "jdk");
        var finder = new JdkFinder
        {
            Providers = [new StubJvmProvider("stub", home)],
            ExecutableDirectories = [Path.Combine(home, "bin")],
            ProbeRuntimeProperties = false,
        };

        var executable = Assert.Single(finder.LocateExecutables());

        Assert.Equal(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName), executable.Path);
        Assert.NotNull(executable.Home);
        Assert.Equal(home, executable.Home.FullName);
    }

    [Fact]
    public void LocateExecutables_DeduplicatesSymlinkedLauncherAgainstTheHomeExecutable()
    {
        if (OperatingSystem.IsWindows())
            return;

        var home = TestJdk.Create(_temp.FullPath, "21.0.6", "jdk");
        TestJdk.CreateStandaloneJava(Path.Combine(home, "bin"), "21.0.6");
        var links = Path.Combine(_temp.FullPath, "links");
        Directory.CreateDirectory(links);
        File.CreateSymbolicLink(
            Path.Combine(links, JavaHomeLayout.JavaExecutableName),
            Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName));

        var finder = new JdkFinder
        {
            Providers = [new StubJvmProvider("stub", home)],
            ExecutableDirectories = [links],
            ProbeRuntimeProperties = false,
        };

        var executable = Assert.Single(finder.LocateExecutables());

        Assert.Equal(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName), executable.Path);
    }

    [Fact]
    public void LocateExecutables_DeduplicatesSymlinkedDirectoryAgainstTheHomeExecutable()
    {
        if (OperatingSystem.IsWindows())
            return;

        var home = TestJdk.Create(_temp.FullPath, "21.0.6", "jdk");
        TestJdk.CreateStandaloneJava(Path.Combine(home, "bin"), "21.0.6");
        var links = Path.Combine(_temp.FullPath, "bin-link");
        Directory.CreateSymbolicLink(links, Path.Combine(home, "bin"));

        var finder = new JdkFinder
        {
            Providers = [new StubJvmProvider("stub", home)],
            ExecutableDirectories = [links],
            ProbeRuntimeProperties = false,
        };

        var executable = Assert.Single(finder.LocateExecutables());

        Assert.Equal(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName), executable.Path);
    }

    [Fact]
    public void LocateExecutables_SkipsMissingAndDuplicateDirectories()
    {
        if (OperatingSystem.IsWindows())
            return;

        var directory = Path.Combine(_temp.FullPath, "standalone");
        var java = TestJdk.CreateStandaloneJava(directory, "21.0.6");
        var finder = new JdkFinder
        {
            ExecutableDirectories =
            [
                Path.Combine(_temp.FullPath, "missing"),
                directory,
                directory,
            ],
            ProbeRuntimeProperties = false,
        };

        var executable = Assert.Single(finder.LocateExecutables());

        Assert.Equal(java, executable.Path);
    }

    [Fact]
    public void LocateExecutables_StandaloneBinaryWithoutJavaVersion_IsSkipped()
    {
        if (OperatingSystem.IsWindows())
            return;

        var directory = Path.Combine(_temp.FullPath, "not-java");
        Directory.CreateDirectory(directory);
        var java = Path.Combine(directory, JavaHomeLayout.JavaExecutableName);
        File.WriteAllText(java, "#!/bin/sh\nexit 0\n");
        File.SetUnixFileMode(java, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var finder = new JdkFinder
        {
            ExecutableDirectories = [directory],
            ProbeRuntimeProperties = false,
        };

        Assert.Empty(finder.LocateExecutables());
    }

    [Fact]
    public void LocateExecutables_NoSources_ReturnsEmpty()
    {
        Assert.Empty(new JdkFinder().LocateExecutables());
    }

    [Fact]
    public async Task LocateExecutablesAsync_MatchesTheSyncPipeline()
    {
        if (OperatingSystem.IsWindows())
            return;

        var home = TestJdk.Create(_temp.FullPath, "21.0.6", "jdk");
        var standalone = Path.Combine(_temp.FullPath, "standalone");
        var standaloneJava = TestJdk.CreateStandaloneJava(standalone, "17.0.12");
        var finder = new JdkFinder
        {
            Providers = [new StubJvmProvider("stub", home)],
            ExecutableDirectories = [standalone],
            ProbeRuntimeProperties = false,
        };

        var sync = finder.LocateExecutables().Select(executable => executable.Path).ToArray();
        var async = (await finder.LocateExecutablesAsync(TestContext.Current.CancellationToken))
            .Select(executable => executable.Path)
            .ToArray();

        Assert.Equal(
            [Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName), standaloneJava],
            sync);
        Assert.Equal(sync, async);
    }

    [Fact]
    public void LocateExecutables_SkipsABrokenCandidateAndKeepsScanning()
    {
        if (OperatingSystem.IsWindows())
            return;

        var broken = Path.Combine(_temp.FullPath, "broken");
        Directory.CreateDirectory(broken);
        var brokenJava = Path.Combine(broken, JavaHomeLayout.JavaExecutableName);
        File.WriteAllText(brokenJava, "#!/bin/sh\nexit 1\n");
        File.SetUnixFileMode(brokenJava,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var good = Path.Combine(_temp.FullPath, "good");
        var goodJava = TestJdk.CreateStandaloneJava(good, "21.0.6");
        var finder = new JdkFinder
        {
            ExecutableDirectories = [broken, good],
            ProbeRuntimeProperties = false,
        };

        var executable = Assert.Single(finder.LocateExecutables());

        Assert.Equal(goodJava, executable.Path);
    }

    [Fact]
    public async Task LocateExecutables_StandaloneCandidatesKeepDirectoryOrder()
    {
        if (OperatingSystem.IsWindows())
            return;

        var first = Path.Combine(_temp.FullPath, "first");
        var firstJava = TestJdk.CreateStandaloneJava(first, "21.0.6", slow: true);
        var second = Path.Combine(_temp.FullPath, "second");
        var secondJava = TestJdk.CreateStandaloneJava(second, "17.0.12");
        var finder = new JdkFinder
        {
            ExecutableDirectories = [first, second],
            ProbeRuntimeProperties = false,
        };

        var sync = finder.LocateExecutables().Select(executable => executable.Path).ToArray();
        var async = (await finder.LocateExecutablesAsync(TestContext.Current.CancellationToken))
            .Select(executable => executable.Path)
            .ToArray();

        Assert.Equal([firstJava, secondJava], sync);
        Assert.Equal(sync, async);
    }

    [Fact]
    public void LocateExecutables_ProbesHomeBackedExecutableByDefault()
    {
        if (OperatingSystem.IsWindows())
            return;

        var home = TestJdk.CreateSlowProbe(_temp.FullPath, "21.0.6", "jdk");
        var finder = new JdkFinder { Providers = [new StubJvmProvider("stub", home)] };

        var executable = Assert.Single(finder.LocateExecutables());

        Assert.Equal(JvmVersion.Parse("99.0"), executable.Version);
        Assert.Null(executable.StartFailure);
    }

    [Fact]
    public async Task LocateExecutablesAsync_PreCancelledToken_Throws()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new JdkFinder().LocateExecutablesAsync(new CancellationToken(true)));
    }

    private sealed class StubJvmProvider(string name, params string[] homes) : IJvmProvider
    {
        public string Name => name;

        public IEnumerable<string> GetJavaHomes() => homes;
    }
}
