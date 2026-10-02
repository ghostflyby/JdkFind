namespace JdkFind.Providers;

/// <summary>
///     Scans the vendor directories Windows installers use under <c>%ProgramFiles%</c>:
///     Java, Eclipse Adoptium, Microsoft, Zulu, Amazon Corretto and BellSoft.
/// </summary>
public sealed class WindowsProgramFilesJvmProvider(IEnumerable<string> prefixes) : ICommonPrefixesJvmProvider
{
    public string Name => "windows-programs";

    private readonly string[] prefixList = [.. prefixes.Where(p => !string.IsNullOrWhiteSpace(p))];

    public WindowsProgramFilesJvmProvider() : this(ResolveDefaultPrefixes()) { }

    IEnumerable<string> ICommonPrefixesJvmProvider.GetCommonPrefixes() => prefixList;

    string? ICommonPrefixesJvmProvider.GetJavaHome(string subDirectory) => JavaHomeLayout.Probe(subDirectory);

    private static IEnumerable<string> ResolveDefaultPrefixes()
    {
        if (!OperatingSystem.IsWindows())
            yield break;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrEmpty(programFiles))
            yield break;

        foreach (var vendor in new[] { "Java", "Eclipse Adoptium", "Microsoft", "Zulu", "Amazon Corretto", "BellSoft" })
            yield return Path.Combine(programFiles, vendor);
    }
}
