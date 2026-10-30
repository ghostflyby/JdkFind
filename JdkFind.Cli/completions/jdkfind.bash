# bash completion for the jdkfind dotnet tool.
#
# Source this file from ~/.bashrc, or copy it to
# ~/.local/share/bash-completion/completions/jdkfind.

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

    "${COMP_WORDS[0]}" list --json --no-probe 2>/dev/null |
        grep -oE "$pattern" |
        sed -e 's/^"[A-Za-z]*": *//' -e 's/^"//' -e 's/"$//' |
        sort -u
}

# jdkfind accepts version prefixes of at most three all-digit segments
# (21, 21.0, 21.0.5); raw release strings can be longer or carry legacy
# suffixes, so trim every suggestion down to an acceptable prefix.
_jdkfind_version_values() {
    local value segment joined
    local -a parts trimmed out=()
    while IFS= read -r value; do
        [[ -n $value ]] || continue
        IFS=. read -r -a parts <<<"$value"
        trimmed=()
        for segment in "${parts[@]:0:3}"; do
            segment="${segment%%[!0-9]*}"
            [[ -n $segment ]] || break
            trimmed+=("$segment")
        done
        if [[ ${#trimmed[@]} -gt 0 ]]; then
            joined="${trimmed[0]}"
            for segment in "${trimmed[@]:1}"; do
                joined+=".$segment"
            done
            out+=("$joined")
        fi
    done < <(_jdkfind_values version)
    printf '%s\n' "${out[@]}" | sort -u
}

# Lists the extensionless executables in bin/ of the current selection, so
# 'jdkfind 21 jav<TAB>' completes against that exact installation.
_jdkfind_bin_tools() {
    local home tool
    local -a cmd=("${COMP_WORDS[0]}")
    [[ -n ${1-} ]] && cmd+=("$1")
    cmd+=(--no-probe)
    home="$(command "${cmd[@]}" 2>/dev/null)" || return 0
    [[ -d $home/bin ]] || return 0
    for tool in "$home"/bin/*; do
        [[ -f $tool ]] || continue
        tool="${tool##*/}"
        [[ $tool == *.* ]] || printf '%s\n' "$tool"
    done
}

# Completes option values, prefix-filtered and escaped where needed
# (vendor names such as "Eclipse Adoptium" carry spaces).
_jdkfind_complete_values() {
    local field=$1 cur=$2 value
    local -a reply=()
    while IFS= read -r value; do
        [[ -n $value ]] || continue
        [[ $value == "$cur"* ]] || continue
        reply+=("$(printf '%q' "$value")")
    done < <(_jdkfind_values "$field")
    COMPREPLY=("${reply[@]}")
}

_jdkfind() {
    local cur prev i w verword='' subcmd='' sub=0 ver=0 tool=0 dd=0 ddidx=0 skip=0 shellgiven=0
    COMPREPLY=()
    cur="${COMP_WORDS[COMP_CWORD]}"
    prev="${COMP_WORDS[COMP_CWORD - 1]}"

    # An option value: either the word after the option or inline after '='.
    case $cur in
    --vendor=* | --distribution=* | --arch=* | --release=*)
        local field="${cur%%=*}"
        _jdkfind_complete_values "${field#--}" "${cur#*=}"
        return 0
        ;;
    esac
    case $prev in
    --vendor | --distribution | --arch | --release)
        _jdkfind_complete_values "${prev#--}" "$cur"
        return 0
        ;;
    esac

    # Grammar state from the completed words.
    for ((i = 1; i < COMP_CWORD; i++)); do
        w="${COMP_WORDS[i]}"
        if ((skip)); then
            skip=0
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
        --vendor=* | --distribution=* | --arch=* | --release=*) ;;
        --vendor | --distribution | --arch | --release) skip=1 ;;
        -*) ;;
        info | list | completions)
            sub=1
            subcmd=$w
            ;;
        [0-9]*)
            ver=1
            verword=$w
            ;;
        *) [[ $subcmd == completions ]] && shellgiven=1 || tool=1 ;;
        esac
    done

    # Run mode, after fish's java_home --exec completion: the word being
    # completed after '--' is a standalone command line for the resolved
    # installation. Its first word offers the executables of the selection's
    # bin/; once the first word stands, the remaining words are handed to that
    # command's own bash completion when one is registered (files otherwise).
    if ((dd)); then
        if ((COMP_CWORD == ddidx + 1)); then
            while IFS= read -r w; do
                [[ $w == "$cur"* ]] || continue
                COMPREPLY+=("$w")
            done < <(_jdkfind_bin_tools "$verword")
            return 0
        fi

        local run_cmd="${COMP_WORDS[ddidx + 1]}" spec func
        spec="$(complete -p "$run_cmd" 2>/dev/null)" || spec=
        if [[ $spec == *" -F "* ]] && [[ $spec =~ \ -F\ ([^ ]+) ]] &&
            declare -F "${BASH_REMATCH[1]}" >/dev/null; then
            func="${BASH_REMATCH[1]}"
            COMP_WORDS=("${COMP_WORDS[@]:ddidx+1}")
            COMP_CWORD=$((COMP_CWORD - ddidx - 1))
            "$func"
        else
            while IFS= read -r w; do
                COMPREPLY+=("$w")
            done < <(compgen -f -- "$cur")
        fi
        return 0
    fi

    if [[ $cur == -* ]]; then
        while IFS= read -r w; do
            COMPREPLY+=("$w")
        done < <(compgen -W \
            "--json -j --vendor --distribution --arch --release --jdk-only --no-probe --help -h --version" \
            -- "$cur")
        return 0
    fi

    # Positionals: the subcommands, version prefixes of the installed JVMs,
    # and bin tools of the current selection; after 'completions' the shell
    # names mirror what this build carries.
    local -a candidates=()
    if [[ $subcmd == completions ]]; then
        if ((! shellgiven)); then
            while IFS= read -r w; do
                [[ -n $w ]] && candidates+=("$w")
            done < <("${COMP_WORDS[0]}" completions 2>/dev/null)
        fi
    else
        ((sub)) || candidates+=(info list completions)
        if ((! ver)); then
            while IFS= read -r w; do
                [[ -n $w ]] && candidates+=("$w")
            done < <(_jdkfind_version_values)
        fi
        if ((! tool && ! sub)); then
            while IFS= read -r w; do
                [[ -n $w ]] && candidates+=("$w")
            done < <(_jdkfind_bin_tools "$verword")
        fi
    fi
    # compgen emits one candidate per line; loop-and-append instead of
    # mapfile, which macOS's bash 3.2 lacks.
    while IFS= read -r w; do
        COMPREPLY+=("$w")
    done < <(compgen -W "${candidates[*]}" -- "$cur")
}

complete -F _jdkfind jdkfind
