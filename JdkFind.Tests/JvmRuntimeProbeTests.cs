namespace JdkFind.Tests;

public class JvmRuntimeProbeTests : IDisposable
{
    private readonly TempDirectory temp = new();

    [Fact]
    public void Probe_ReadsPropertiesFromTheJavaExecutable()
    {
        if (OperatingSystem.IsWindows())
            return; // The fake java executable is a POSIX shell script.

        var home = Path.Combine(temp.FullPath, "fake-jdk");
        Directory.CreateDirectory(Path.Combine(home, "bin"));
        var java = Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName);
        File.WriteAllText(java, """
            #!/bin/sh
            echo "    java.vendor = Probe Vendor" >&2
            echo "    java.version = 99.0" >&2
            echo "    java.runtime.name = Probe Runtime" >&2
            echo "    java.runtime.version = 99.0+1" >&2
            echo "    java.vm.name = Probe VM" >&2
            echo "    java.vm.version = 99.0+1" >&2
            echo "    os.arch = test" >&2
            """);
        File.SetUnixFileMode(java, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var info = JvmRuntimeProbe.Probe(home);

        Assert.NotNull(info);
        Assert.Equal("Probe Vendor", info.Vendor);
        Assert.Equal("99.0", info.JavaVersion);
        Assert.Equal("Probe Runtime", info.RuntimeName);
        Assert.Equal("99.0+1", info.RuntimeVersion);
        Assert.Equal("Probe VM", info.VmName);
        Assert.Equal("99.0+1", info.VmVersion);
        Assert.Equal("test", info.OsArch);
    }

    [Fact]
    public void Probe_MissingJava_ReturnsNull()
    {
        Assert.Null(JvmRuntimeProbe.Probe(Path.Combine(temp.FullPath, "empty")));
    }

    public void Dispose() => temp.Dispose();
}
