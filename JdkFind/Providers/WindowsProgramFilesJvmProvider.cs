namespace JdkFind.Providers;

/// <summary>
///     Scans the vendor directories Windows installers use under <c>%ProgramFiles%</c>:
///     Java, Eclipse Adoptium, Microsoft, Zulu, Amazon Corretto and BellSoft.
/// </summary>
public sealed class WindowsProgramFilesJvmProvider(IEnumerable<string> prefixes) : ICommonPrefixesJvmProvider
{
    public string Name => "windows-programs";

    private readonly string[] prefixList = [.. prefixes.Where(p => !string.IsNullOrWhiteSpace(p))];

    private static readonly string[] SourceArray =
    [
        "Java",
        "Eclipse Adoptium",
        "Microsoft",
        "Zulu",
        "Amazon Corretto",
        "BellSoft"
    ];

    public WindowsProgramFilesJvmProvider() : this(ResolveDefaultPrefixes())
    {
    }

    IEnumerable<string> ICommonPrefixesJvmProvider.GetCommonPrefixes() => prefixList;

    private static IEnumerable<string> ResolveDefaultPrefixes()
    {
        if (!OperatingSystem.IsWindows())
            return [];

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrEmpty(programFiles))
            return [];

        return SourceArray.Select(vendor => Path.Combine(programFiles, vendor));
    }
}