namespace JdkFind.Cli;

/// <summary>
///     The shell wrappers the init command emits. Each wrapper defines a
///     jdkfind function that shadows the binary: 'jdkfind env [version]'
///     evaluates the env command's output in the current shell — exporting
///     JAVA_HOME — prepends the installation's bin directory to PATH and
///     removes the previously managed entry; every other invocation delegates
///     to the real binary. Enable with eval "jdkfind init &lt;shell&gt;"
///     (fish: source, PowerShell: Invoke-Expression).
/// </summary>
internal static class ShellInit
{
    private const string BashScript =
        """
        # Bash integration for jdkfind: 'jdkfind env [version]' exports JAVA_HOME
        # for the selected installation and prepends its bin directory to PATH in
        # the current shell. Enable with: eval "$(jdkfind init bash)"
        jdkfind() {
            case "${1:-}" in
            env)
                shift
                local output
                if ! output="$(command jdkfind env --format sh "$@")"
                then
                    return 1
                fi
                eval "$output"
                if [ -n "${JDKFIND_BIN:-}" ]
                then
                    # ENVIRON sidesteps awk's escape processing of -v values,
                    # which mangles backslash-bearing Windows paths.
                    export PATH="$(printf '%s' "$PATH" | awk 'BEGIN { RS = ":" } $0 != ENVIRON["JDKFIND_BIN"]' ORS = ":")"
                    export PATH="${PATH%:}"
                fi
                export JDKFIND_BIN="$JAVA_HOME/bin"
                export PATH="$JDKFIND_BIN:$PATH"
                ;;
            *)
                command jdkfind "$@"
                ;;
            esac
        }
        """;

    private const string ZshScript =
        """
        # Zsh integration for jdkfind: 'jdkfind env [version]' exports JAVA_HOME
        # for the selected installation and prepends its bin directory to PATH in
        # the current shell. Enable with: eval "$(jdkfind init zsh)"
        jdkfind() {
            case "${1:-}" in
            env)
                shift
                local output
                if ! output="$(command jdkfind env --format sh "$@")"
                then
                    return 1
                fi
                eval "$output"
                if [ -n "${JDKFIND_BIN:-}" ]
                then
                    export PATH="$JDKFIND_BIN:${(j.:.)${(s.:.)PATH}:#${JDKFIND_BIN}}"
                fi
                export JDKFIND_BIN="$JAVA_HOME/bin"
                export PATH="$JDKFIND_BIN:$PATH"
                ;;
            *)
                command jdkfind "$@"
                ;;
            esac
        }
        """;

    private const string FishScript =
        """
        # Fish integration for jdkfind: 'jdkfind env [version]' exports JAVA_HOME
        # for the selected installation and prepends its bin directory to PATH in
        # the current shell. Enable with: jdkfind init fish | source
        function jdkfind
            if test "$argv[1]" = "env"
                set -l args $argv[2..-1]
                if not set -l output (command jdkfind env --format fish $args)
                    return 1
                end
                eval "$output"
                if set -q JDKFIND_BIN
                    set PATH (string match -ve "$JDKFIND_BIN" -- $PATH)
                end
                set -x JDKFIND_BIN "$JAVA_HOME/bin"
                set -gx PATH "$JDKFIND_BIN" $PATH
            else
                command jdkfind $argv
            end
        end
        """;

    private const string PowerShellScript =
        """
        # PowerShell integration for jdkfind: 'jdkfind env [version]' exports
        # JAVA_HOME for the selected installation and prepends its bin directory
        # to PATH in the current session. Enable from $PROFILE with:
        #   jdkfind init powershell | Invoke-Expression
        function jdkfind {
            if ($args.Count -ge 1 -and $args[0] -eq 'env') {
                $rest = @($args | Select-Object -Skip 1)
                $output = @(& jdkfind.exe env --format powershell @rest)
                if ($LASTEXITCODE -ne 0) {
                    $output
                    return
                }
                Invoke-Expression ($output -join "`n")
                if ($env:JDKFIND_BIN) {
                    $env:PATH = ($env:PATH -split ';' | Where-Object { $_ -and $_ -ne $env:JDKFIND_BIN }) -join ';'
                }
                $env:JDKFIND_BIN = "$env:JAVA_HOME\bin"
                $env:PATH = "$env:JDKFIND_BIN;$env:PATH"
            }
            else {
                & jdkfind.exe @args
            }
        }
        """;

    private static readonly (string Shell, string Script)[] Registry =
    [
        ("bash", BashScript),
        ("zsh", ZshScript),
        ("fish", FishScript),
        ("powershell", PowerShellScript),
    ];

    /// <summary>The shells whose wrapper this build carries, in registry order.</summary>
    internal static IReadOnlyList<string> AvailableShells() =>
        Registry.Select(entry => entry.Shell).ToList();

    /// <summary>Returns the wrapper for a known shell.</summary>
    internal static string ReadScript(string shell) =>
        Registry.Where(entry => entry.Shell.Equals(shell, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Script)
            .Single();
}
