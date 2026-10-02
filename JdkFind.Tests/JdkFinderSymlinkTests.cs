namespace JdkFind.Tests;

public class JdkFinderSymlinkTests : IDisposable
{
    private readonly TempDirectory temp = new();

    private sealed class StubJvmProvider(string name, params string[] homes) : IJvmProvider
    {
        public string Name { get; } = name;

        public IEnumerable<string> GetJavaHomes() => homes;
    }

    [Fact]
    public void ResolveSymlinks_ExpandsRelativeLinkTargetsToSameCanonicalPath()
    {
        if (OperatingSystem.IsWindows())
            return; // Creating symbolic links requires privileges on Windows; POSIX only.

        var real = TestJdk.Create(temp.FullPath, "21.0.5", "real-jdk");
        var link = Path.Combine(temp.FullPath, "link-jdk");
        Directory.CreateSymbolicLink(link, Path.GetRelativePath(temp.FullPath, real));

        Assert.Equal(JdkFinder.ResolveSymlinks(real), JdkFinder.ResolveSymlinks(link));
    }

    [Fact]
    public void Locate_DeduplicatesSymbolicLinkAgainstTarget()
    {
        if (OperatingSystem.IsWindows())
            return;

        var real = TestJdk.Create(temp.FullPath, "21.0.5", "real-jdk");
        var link = Path.Combine(temp.FullPath, "link-jdk");
        Directory.CreateSymbolicLink(link, real);

        var options = new JdkFindOptions
        {
            Providers = [new StubJvmProvider("link", link), new StubJvmProvider("real", real)],
        };

        var jvm = Assert.Single(JdkFinder.Locate(options).ToList());
        Assert.Equal(["link", "real"], jvm.Providers);
    }

    [Fact]
    public void ResolveSymlinks_TerminatesOnLinkLoop()
    {
        if (OperatingSystem.IsWindows())
            return;

        var a = Path.Combine(temp.FullPath, "loop-a");
        var b = Path.Combine(temp.FullPath, "loop-b");
        Directory.CreateSymbolicLink(a, b);
        Directory.CreateSymbolicLink(b, a);

        // The 32-hop bound returns; the test completing is itself the proof there is no infinite loop.
        Assert.NotEmpty(JdkFinder.ResolveSymlinks(a));
    }

    [Fact]
    public void ResolveSymlinks_HandlesDanglingLinks()
    {
        if (OperatingSystem.IsWindows())
            return;

        var link = Path.Combine(temp.FullPath, "dangling");
        Directory.CreateSymbolicLink(link, Path.Combine(temp.FullPath, "missing"));

        Assert.NotEmpty(JdkFinder.ResolveSymlinks(link));
    }

    [Fact]
    public void ResolveSymlinks_CanonicalizesNonExistentPathsToo()
    {
        var missing = Path.Combine(temp.FullPath, "not-there", "jdk");

        // Prefix symlinks like macOS /var -> /private/var still expand; assert the tail is unchanged.
        Assert.EndsWith(Path.Combine("not-there", "jdk"), JdkFinder.ResolveSymlinks(missing));
    }

    public void Dispose() => temp.Dispose();
}
