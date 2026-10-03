using System.CommandLine;

namespace JdkFind.Cli;

/// <summary>
///     The assembled System.CommandLine tree for jdkfind, exposing the symbol
///     references that value mapping and dispatch wiring need.
/// </summary>
internal sealed class CommandTree
{
    internal required RootCommand Root { get; init; }

    internal required Command Info { get; init; }

    internal required Command List { get; init; }

    internal required Argument<string[]> Operands { get; init; }

    internal required Argument<string?> Version { get; init; }

    internal required Argument<string?> ListVersion { get; init; }

    internal required Option<bool> Json { get; init; }

    internal required Option<string> Vendor { get; init; }

    internal required Option<string> Distribution { get; init; }

    internal required Option<string> Architecture { get; init; }

    internal required Option<string> Release { get; init; }

    internal required Option<bool> JdkOnly { get; init; }

    internal required Option<bool> NoProbe { get; init; }

    internal required VersionOption VersionOption { get; init; }
}
