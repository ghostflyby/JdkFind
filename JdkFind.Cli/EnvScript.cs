namespace JdkFind.Cli;

/// <summary>
///     The env command's output: one shell statement exporting JAVA_HOME of
///     the selected installation. bash and zsh share the POSIX form; fish and
///     PowerShell have their own assignment syntax. PATH handling is left to
///     the wrapper the init command emits — the statements here are also
///     usable standalone with eval or Invoke-Expression.
/// </summary>
internal static class EnvScript
{
    internal static string For(Jvm jvm, string? format)
    {
        var shell = format ?? "sh";
        return shell switch
        {
            "fish" => $"set -gx JAVA_HOME '{EscapeFish(jvm.Home.FullName)}'\n",
            "powershell" => $"$env:JAVA_HOME = '{EscapePowerShell(jvm.Home.FullName)}'\n",
            _ => $"export JAVA_HOME='{EscapePosix(jvm.Home.FullName)}'\n",
        };
    }

    /// <summary>POSIX single quotes: only the quote itself needs the
    /// closer-quote-escaped-quote-opener dance; backslashes are literal.</summary>
    private static string EscapePosix(string value) => value.Replace("'", "'\\''");

    /// <summary>fish single quotes escape backslashes as well.</summary>
    private static string EscapeFish(string value) =>
        value.Replace("\\", "\\\\").Replace("'", "\\'");

    /// <summary>PowerShell single quotes double the quote; backslashes are literal.</summary>
    private static string EscapePowerShell(string value) => value.Replace("'", "''");
}
