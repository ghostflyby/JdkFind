namespace JdkFind.Providers;

/// <summary>Exposes the JDK referenced by the <c>JAVA_HOME</c> environment variable.</summary>
public sealed class JavaHomeJvmProvider(string? home) : JvmProviderBase
{
    public override string Name => "java-home";

    public JavaHomeJvmProvider() : this(Environment.GetEnvironmentVariable("JAVA_HOME"))
    {
    }

    public override IEnumerable<string> GetJavaHomes()
    {
        if (!string.IsNullOrWhiteSpace(home) && Probe(home) is { } javaHome)
            yield return javaHome;
    }
}
