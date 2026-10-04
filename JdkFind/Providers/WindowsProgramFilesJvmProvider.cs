namespace JdkFind.Providers;

/// <summary>
///     Scans the vendor directories Windows installers use under
///     <c>%ProgramFiles%</c> and <c>%ProgramFiles(x86)%</c>: Java, Eclipse Adoptium,
///     legacy AdoptOpenJDK, Microsoft, Zulu, Amazon Corretto and BellSoft.
/// </summary>
public sealed class WindowsProgramFilesJvmProvider(IEnumerable<string> prefixes) : ICommonPrefixesJvmProvider
{
    /// <inheritdoc />
    public string Name => "windows-programs";

    private readonly string[] prefixList = [.. prefixes.Where(p => !string.IsNullOrWhiteSpace(p))];

    private static readonly string[] SourceArray =
    [
        "Java",
        "Eclipse Adoptium",
        "AdoptOpenJDK",
        "Microsoft",
        "Zulu",
        "Amazon Corretto",
        "BellSoft"
    ];

    /// <summary>Scans the vendor directories under <c>%ProgramFiles%</c>.</summary>
    public WindowsProgramFilesJvmProvider() : this(ResolveDefaultPrefixes())
    {
    }

    IEnumerable<string> ICommonPrefixesJvmProvider.GetCommonPrefixes() => prefixList;

    private static IEnumerable<string> ResolveDefaultPrefixes()
    {
        if (!OperatingSystem.IsWindows())
            return [];

        var roots = new List<string> { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) };
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(programFilesX86) &&
            roots.All(root => !string.Equals(root, programFilesX86, StringComparison.OrdinalIgnoreCase)))
            roots.Add(programFilesX86);

        return roots.Where(root => !string.IsNullOrEmpty(root))
            .SelectMany(root => SourceArray.Select(vendor => Path.Combine(root, vendor)));
    }
}