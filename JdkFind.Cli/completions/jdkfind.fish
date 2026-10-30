# fish completion for the jdkfind dotnet tool.
# Copy this file to ~/.config/fish/completions/jdkfind.fish.

# Prints the completed positional tokens of the current command line:
# options and their values are stripped, and scanning stops at '--'.
function __jdkfind_tokens
    set -l tokens (commandline -opc)
    set -l skip 0
    for token in $tokens[2..-1]
        if test $skip -eq 1
            set skip 0
            continue
        end
        switch $token
            case --
                return 0
            case --vendor --distribution --arch --release
                set skip 1
            case '--*'
                # --opt=value carries its value inline
                continue
            case '-*'
                continue
            case '*'
                echo $token
        end
    end
end

function __jdkfind_past_dd
    contains -- -- (commandline -opc)
end

function __jdkfind_wants_subcommand
    not __jdkfind_tokens | string match --quiet --regex '^(info|list|completions)$'
end

function __jdkfind_seen_completions
    __jdkfind_tokens | string match --quiet completions
end

function __jdkfind_wants_version
    not __jdkfind_tokens | string match --quiet --regex '^[0-9]'
end

# A tool positional is offered only while none was given and no subcommand
# appeared (the info/list/completions grammar has no tool slot).
function __jdkfind_wants_tool
    if __jdkfind_tokens | string match --quiet --invert --regex '^(info|list|completions|[0-9].*)$'
        return 1
    end
    if __jdkfind_tokens | string match --quiet --regex '^(info|list|completions)$'
        return 1
    end
    return 0
end

# The shell argument of 'completions': the last positional is the subcommand.
function __jdkfind_wants_shell
    set -l tokens (__jdkfind_tokens)
    test (count $tokens) -gt 0
    and test "$tokens[-1]" = completions
end

# Prints values for <field> from the installed JVMs, one per line. The
# listing runs with --no-probe so completion stays a fast local scan.
function __jdkfind_values
    set -l pattern
    switch $argv[1]
        case vendor
            set pattern '"vendor": *"[^"]*"'
        case distribution
            set pattern '"distribution": *"[^"]*"'
        case arch
            set pattern '"architecture": *"[^"]*"'
        case release
            set pattern '"languageVersion": *[0-9]+'
        case version
            set pattern '"version": *"[^"]*"'
        case '*'
            return 0
    end
    command jdkfind list --json --no-probe 2>/dev/null |
        string match --all --regex "$pattern" |
        string replace --regex '^"[A-Za-z]+": *' '' |
        string replace --all '"' '' |
        sort -u
end

# jdkfind accepts version prefixes of at most three all-digit segments;
# raw release strings can be longer or carry legacy suffixes, so trim every
# suggestion down to an acceptable prefix.
function __jdkfind_version_values
    set -l out
    for value in (__jdkfind_values version)
        set -l parts (string split . -- $value)
        set -l trimmed
        for segment in $parts[1..3]
            if not string match --quiet --regex '^[0-9]+$' -- $segment
                break
            end
            set --append trimmed $segment
        end
        if test (count $trimmed) -gt 0
            set --append out (string join . -- $trimmed)
        end
    end
    if test (count $out) -eq 0
        return 0
    end
    printf '%s\n' $out | sort -u
end

# Lists the extensionless executables in bin/ of the current selection, so
# 'jdkfind 21 jav<TAB>' completes against that exact installation.
function __jdkfind_tools
    set -l verword ''
    for token in (__jdkfind_tokens)
        if string match --quiet --regex '^[0-9]' -- $token
            set verword $token
            break
        end
    end
    set -l home ''
    if test -n "$verword"
        set home (command jdkfind $verword --no-probe 2>/dev/null)
    else
        set home (command jdkfind --no-probe 2>/dev/null)
    end
    if test -n "$home"; and test -d "$home/bin"
        for file in $home/bin/*
            test -f $file; or continue
            string match --quiet '*.*' -- (basename $file); and continue
            basename $file
        end
    end
end

complete -c jdkfind --no-files

complete -c jdkfind -s j -l json -d 'Write JSON to stdout (list: array; others: single object)' -n 'not __jdkfind_past_dd'
complete -c jdkfind -l vendor -d 'Filter by vendor substring' -x -a '(__jdkfind_values vendor)' -n 'not __jdkfind_past_dd'
complete -c jdkfind -l distribution -d 'Filter by distribution substring (foojay names)' -x -a '(__jdkfind_values distribution)' -n 'not __jdkfind_past_dd'
complete -c jdkfind -l arch -d 'Filter by architecture substring' -x -a '(__jdkfind_values arch)' -n 'not __jdkfind_past_dd'
complete -c jdkfind -l release -d 'Filter by supported javac language level' -x -a '(__jdkfind_values release)' -n 'not __jdkfind_past_dd'
complete -c jdkfind -l jdk-only -d 'Only installations that ship a compiler' -n 'not __jdkfind_past_dd'
complete -c jdkfind -l no-probe -d 'Skip executing each JVM for runtime properties' -n 'not __jdkfind_past_dd'
complete -c jdkfind -s h -l help -d 'Show help' -n 'not __jdkfind_past_dd'
complete -c jdkfind -l version -d 'Print version' -n 'not __jdkfind_past_dd'

# After '--' jdkfind runs the child command verbatim; no jdkfind words apply.
complete -c jdkfind -n 'not __jdkfind_past_dd; and __jdkfind_wants_subcommand' -a info -d 'Print one installation\'s details'
complete -c jdkfind -n 'not __jdkfind_past_dd; and __jdkfind_wants_subcommand' -a list -d 'List all matching installations'
complete -c jdkfind -n 'not __jdkfind_past_dd; and __jdkfind_wants_subcommand' -a completions -d 'Print a shell-completion script'
complete -c jdkfind -n 'not __jdkfind_past_dd; and not __jdkfind_seen_completions; and __jdkfind_wants_version' -a '(__jdkfind_version_values)' -d 'Version prefix'
complete -c jdkfind -n 'not __jdkfind_past_dd; and __jdkfind_wants_tool' -a '(__jdkfind_tools)' -d 'Bin tool of the selection'
complete -c jdkfind -n 'not __jdkfind_past_dd; and __jdkfind_wants_shell' -a '(command jdkfind completions 2>/dev/null)' -d 'Shell this build carries'
