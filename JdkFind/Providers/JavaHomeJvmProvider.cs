namespace JdkFind.Providers;

/// <summary>Exposes the JDK referenced by the <c>JAVA_HOME</c> environment variable.</summary>
public sealed class JavaHomeJvmProvider(string? home) : IJvmProvider
{
    public string Name => "java-home";

    public JavaHomeJvmProvider() : this(Environment.GetEnvironmentVariable("JAVA_HOME"))
    {
    }

    public IEnumerable<string> GetJavaHomes()
    {
        if (string.IsNullOrWhiteSpace(home))
            return [];

        var javaHome = JavaHomeLayout.Probe(home);
        return javaHome != null ? [javaHome] : Array.Empty<string>();
    }
}
