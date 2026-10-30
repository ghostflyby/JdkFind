#compdef jdkfind
# zsh completion for the jdkfind dotnet tool.
#
# Either copy this file to a directory on $fpath as `_jdkfind` (compinit
# registers it from the header line above), or source it from ~/.zshrc
# after compinit has run.

# Prints values for <field> from the installed JVMs, one per line. The
# listing runs with --no-probe so completion stays a fast local scan.
_jdkfind_values() {
    local pattern
    case $1 in
    vendor) pattern='"vendor": *"[^"]*"' ;;
    distribution) pattern='"distribution": *"[^"]*"' ;;
    arch) pattern='"architecture": *"[^"]*"' ;;
    release) pattern='"languageVersion": *[0-9]+' ;;
    version) pattern='"version": *"[^"]*"' ;;
    *) return 0 ;;
    esac

    # The completion runs for the literal `jdkfind` command (see compdef),
    # so the helpers call it by name: inside _arguments option-value actions
    # the $words array no longer carries the command line.
    command jdkfind list --json --no-probe 2>/dev/null |
        command grep -oE "$pattern" |
        command sed -e 's/^"[A-Za-z]*": *//' -e 's/^"//' -e 's/"$//' |
        command sort -u
}

# jdkfind accepts version prefixes of at most three all-digit segments
# (21, 21.0, 21.0.5); raw release strings can be longer or carry legacy
# suffixes, so trim every suggestion down to an acceptable prefix.
_jdkfind_version_values() {
    local value segment
    local -a parts trimmed out
    for value in ${(f)"$(_jdkfind_values version)"}; do
        parts=(${(s:.:)value})
        trimmed=()
        for segment in ${parts[1,3]}; do
            segment=${segment%%[!0-9]*}
            [[ -n $segment ]] || break
            trimmed+=($segment)
        done
        (($#trimmed)) && out+=("${(j:.:)trimmed}")
    done
    (($#out)) && printf '%s\n' ${(ou)out}
}

# Lists the extensionless executables in bin/ of the current selection, so
# 'jdkfind 21 jav<TAB>' completes against that exact installation. Any further
# arguments are selection filters forwarded to the CLI, so the listing is the
# installation the command will actually use.
_jdkfind_bin_tools() {
    local home
    local -a cmd=(command jdkfind)
    if (($#)); then
        [[ -n $1 ]] && cmd+=("$1")
        shift
    fi
    cmd+=(--no-probe "${@}")
    home=$("${cmd[@]}" 2>/dev/null)
    [[ -n $home && -d $home/bin ]] || return 0
    local -a bins=("$home"/bin/*(N:t))
    printf '%s\n' ${bins:#*.*}
}

# Completes an option's value from the installed JVMs.
_jdkfind_option_values() {
    local -a values
    values=(${(f)"$(_jdkfind_values "$1")"})
    (($#values)) || return 1
    _describe -t "$1-values" "$1" values
}

# Completes the positional words: the subcommands, version prefixes of the
# installed JVMs, and bin tools of the current selection. After '--' the
# child command's own line is completed (see the run-mode block below).
_jdkfind_operand() {
    local -i i skip=0 sub=0 ver=0 tool=0 dd=0 ddidx=0 shellgiven=0
    local w verword= subcmd= pending=
    local -a sel

    for ((i = 2; i < CURRENT; i++)); do
        w=${words[i]}
        if ((skip)); then
            skip=0
            sel+=("$pending" "$w")
            continue
        fi
        if [[ $w == -- ]]; then
            dd=1
            ddidx=$i
            continue
        fi
        if ((dd)); then
            continue
        fi
        case $w in
        --vendor=* | --distribution=* | --arch=* | --release=*)
            sel+=("$w")
            ;;
        --vendor | --distribution | --arch | --release)
            skip=1
            pending=$w
            ;;
        --jdk-only | --no-probe)
            sel+=("$w")
            ;;
        -*) ;;
        info | list | completions)
            sub=1
            subcmd=$w
            ;;
        [0-9]*)
            ver=1
            verword=$w
            ;;
        *) if [[ $subcmd == completions ]]; then shellgiven=1; else tool=1; fi ;;
        esac
    done

    # Run mode, after fish's java_home --exec completion: the words after '--'
    # are a standalone command line for the resolved installation. The command
    # word offers the executables of the selection's bin/; once it stands, the
    # words are re-anchored on it and _normal dispatches into that command's
    # own completion (sudo-style).
    if ((dd)); then
        if ((CURRENT == ddidx + 1)); then
            local -a bins
            bins=(${(f)"$(_jdkfind_bin_tools "$verword" "${sel[@]}")"})
            integer ret=1
            (($#bins)) && _describe -t run-bin 'executable of the selection' bins && ret=0
            return ret
        fi
        words=("${(@)words[ddidx+1,-1]}")
        CURRENT=$((CURRENT - ddidx))
        _normal
        return
    fi

    integer ret=1
    if [[ $subcmd == completions ]]; then
        # The shell argument mirrors what this build carries.
        if ((! shellgiven)); then
            local -a shells
            shells=(${(f)"$(command jdkfind completions 2>/dev/null)"})
            (($#shells)) && _describe -t shells shell shells && ret=0
        fi
    else
        if ((! sub)); then
            local -a subcmds=(
                'info:print one installation'"'"'s details'
                'list:list all matching installations'
                'completions:print a shell-completion script'
            )
            _describe -t commands command subcmds && ret=0
        fi
        if ((! ver)); then
            local -a versions
            versions=(${(f)"$(_jdkfind_version_values)"})
            (($#versions)) && _describe -t versions 'version prefix' versions && ret=0
        fi
        if ((! tool && ! sub)); then
            local -a bins
            bins=(${(f)"$(_jdkfind_bin_tools "$verword" "${sel[@]}")"})
            (($#bins)) && _describe -t bin-tools 'bin tool' bins && ret=0
        fi
    fi
    return ret
}

_jdkfind() {
    local -a opts
    opts=(
        '(-j --json)'{-j,--json}'[write JSON to stdout (list: array; others: single object)]'
        '--vendor[filter by vendor substring]:vendor:{_jdkfind_option_values vendor}'
        '--distribution[filter by distribution substring (foojay names)]:distribution:{_jdkfind_option_values distribution}'
        '--arch[filter by architecture substring]:arch:{_jdkfind_option_values arch}'
        '--release[filter by supported javac language level]:release:{_jdkfind_option_values release}'
        '--jdk-only[only installations that ship a compiler]'
        '--no-probe[skip executing each JVM for runtime properties]'
        '(-h --help)'{-h,--help}'[show help]'
        '--version[print version]'
        '*:operand:->operand'
    )

    local context state state_descr line
    typeset -A opt_args

    _arguments -S $opts && return

    [[ $state == operand ]] && _jdkfind_operand
    return
}

# Registers when this file is sourced from an interactive shell; when it is
# autoloaded from $fpath as `_jdkfind`, the #compdef header did it already.
if (($+functions[compdef])); then
    compdef _jdkfind jdkfind
fi
