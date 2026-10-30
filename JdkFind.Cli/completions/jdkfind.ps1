# PowerShell completion for the jdkfind dotnet tool.
#
# Install by dot-sourcing this file from $PROFILE, e.g.:
#   . "$HOME/.local/share/jdkfind/jdkfind.ps1"

Register-ArgumentCompleter -Native -CommandName jdkfind -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)

    # Completed words before the cursor, without the command itself. The
    # word currently being completed shows up as the last element, unless
    # the cursor sits after a trailing space.
    $Elements = @($commandAst.CommandElements)
    $Tokens = [System.Collections.Generic.List[string]]::new()
    for ($i = 1; $i -lt $Elements.Count; $i++) {
        $Tokens.Add($Elements[$i].ToString())
    }
    if ($wordToComplete -ne '' -and $Tokens.Count -gt 0 -and
        $Tokens[$Tokens.Count - 1].EndsWith($wordToComplete)) {
        $Tokens.RemoveAt($Tokens.Count - 1)
    }

    # JSON property names for the option value completions below.
    $JsonProperty = @{
        vendor       = 'vendor'
        distribution = 'distribution'
        arch         = 'architecture'
        release      = 'languageVersion'
        version      = 'version'
    }

    # Lists values for a JSON property of the installed JVMs; the listing
    # runs with --no-probe so completion stays a fast local scan.
    function Get-JdkFindField([string]$Property) {
        if (-not (Get-Command jdkfind -ErrorAction Ignore)) { return @() }
        $json = & jdkfind list --json --no-probe 2>$null
        if (-not $json) { return @() }
        @(($json -join "`n") | ConvertFrom-Json) |
            ForEach-Object { $_.$Property } |
            Where-Object { $_ } |
            Sort-Object -Unique
    }

    # jdkfind accepts version prefixes of at most three all-digit segments;
    # raw release strings can be longer or carry legacy suffixes, so trim
    # every suggestion down to an acceptable prefix.
    function Get-JdkFindVersionPrefixes {
        foreach ($raw in (Get-JdkFindField version)) {
            $parts = $raw -split '\.'
            $take = [Math]::Min(3, $parts.Count)
            $segments = @()
            foreach ($part in $parts[0..($take - 1)]) {
                if ($part -notmatch '^([0-9]+)') { break }
                $segments += $Matches[1]
            }
            if ($segments.Count -gt 0) { $segments -join '.' }
        }
    }

    # Lists the executables in bin/ of the current selection, so
    # 'jdkfind 21 jav<TAB>' completes against that exact installation.
    function Get-JdkFindBinTools {
        if (-not (Get-Command jdkfind -ErrorAction Ignore)) { return @() }
        $homeArgs = @()
        if ($Version) { $homeArgs += $Version }
        $homeArgs += '--no-probe'
        # $HOME is a read-only automatic variable; do not reuse the name.
        $jdkHome = & jdkfind @homeArgs 2>$null
        if (-not $jdkHome) { return @() }
        $bin = Join-Path $jdkHome 'bin'
        if (-not (Test-Path -LiteralPath $bin)) { return @() }
        Get-ChildItem -LiteralPath $bin -File |
            Where-Object { $_.Extension -in '', '.exe' } |
            ForEach-Object { $_.BaseName } |
            Sort-Object -Unique
    }

    $results = [System.Collections.Generic.List[System.Management.Automation.CompletionResult]]::new()

    function AddResult([string]$Text, [string]$Type = 'ParameterValue', [string]$ToolTip = $Text) {
        if (-not $Text.StartsWith($wordToComplete, [System.StringComparison]::OrdinalIgnoreCase)) { return }
        # Vendor names such as 'Eclipse Adoptium' carry spaces.
        $completionText = if ($Text -match '\s') { '"{0}"' -f $Text } else { $Text }
        $results.Add([System.Management.Automation.CompletionResult]::new($completionText, $Text, $Type, $ToolTip))
    }

    # Grammar state from the completed words.
    $ValueOptions = '--vendor', '--distribution', '--arch', '--release'
    $Sub = $false
    $SubName = ''
    $ShellGiven = $false
    $Version = $null
    $Tool = $false
    $PastDd = $false
    $AfterDd = 0
    $Skip = $false
    foreach ($token in $Tokens) {
        if ($Skip) { $Skip = $false; continue }
        if ($token -eq '--') { $PastDd = $true; continue }
        if ($PastDd) { $AfterDd++; continue }
        if ($ValueOptions -contains $token) { $Skip = $true; continue }
        if ($token.StartsWith('-')) { continue }
        if ($token -eq 'info' -or $token -eq 'list' -or $token -eq 'completions') {
            $Sub = $true
            $SubName = $token
            continue
        }
        if ($SubName -eq 'completions') { $ShellGiven = $true; continue }
        if ($token -match '^[0-9]') { $Version = $token; continue }
        $Tool = $true
    }

    # Run mode, after fish's java_home --exec completion: the words after '--'
    # are a standalone command line for the resolved installation. The command
    # word offers the executables of the selection's bin/; once it stands, the
    # rest of the line is completed on the command's own terms — a nested
    # TabExpansion2 dispatches to that command's registered completer (files
    # as the fallback for commands without one). Note that PowerShell never
    # calls a -Native completer for a dash-prefixed word, so `jdkfind -- java
    # -ver` cannot delegate any more than bare `java -ver` completes — the
    # engine, not this script, decides that.
    if ($PastDd) {
        if ($AfterDd -eq 0) {
            foreach ($tool in (Get-JdkFindBinTools)) {
                AddResult $tool 'ParameterValue' 'Executable of the selection'
            }
            return $results
        }
        $run = @($Tokens | Select-Object -Last $AfterDd)
        $quoted = @($run | ForEach-Object { if ($_ -match '\s') { '"{0}"' -f $_ } else { $_ } })
        $line = ($quoted + $wordToComplete) -join ' '
        $nested = TabExpansion2 -inputScript $line -cursorColumn $line.Length
        return @($nested.CompletionMatches)
    }

    # An option value: either the word after the option or inline after '='.
    $field = $null
    if ($wordToComplete -match '^--(vendor|distribution|arch|release)=(.*)$') {
        $field = $Matches[1]
        $wordToComplete = $Matches[2]
    }
    elseif ($Tokens.Count -gt 0 -and $ValueOptions -contains $Tokens[$Tokens.Count - 1]) {
        $field = $Tokens[$Tokens.Count - 1] -replace '^--', ''
    }
    if ($null -ne $field) {
        foreach ($value in (Get-JdkFindField $JsonProperty[$field])) {
            AddResult $value 'ParameterValue' $value
        }
        return $results
    }

    if ($wordToComplete.StartsWith('-')) {
        $OptionTooltips = [ordered]@{
            '--json'         = 'Write JSON to stdout (list: array; others: single object)'
            '-j'             = 'Write JSON to stdout'
            '--vendor'       = 'Filter by vendor substring'
            '--distribution' = 'Filter by distribution substring (foojay names)'
            '--arch'         = 'Filter by architecture substring'
            '--release'      = 'Filter by supported javac language level'
            '--jdk-only'     = 'Only installations that ship a compiler'
            '--no-probe'     = 'Skip executing each JVM for runtime properties'
            '--help'         = 'Show help'
            '-h'             = 'Show help'
            '--version'      = 'Print version'
        }
        foreach ($name in $OptionTooltips.Keys) {
            AddResult $name 'ParameterName' $OptionTooltips[$name]
        }
        return $results
    }

    if ($SubName -eq 'completions') {
        # The shell argument mirrors what this build carries.
        if (-not $ShellGiven -and (Get-Command jdkfind -ErrorAction Ignore)) {
            foreach ($shell in @(& jdkfind completions 2>$null)) {
                AddResult $shell 'ParameterValue' 'Shell this build carries'
            }
        }
        return $results
    }

    if (-not $Sub) {
        AddResult 'info' 'Command' 'Print one installation''s details'
        AddResult 'list' 'Command' 'List all matching installations'
        AddResult 'completions' 'Command' 'Print a shell-completion script'
    }
    if ($null -eq $Version) {
        foreach ($prefix in (Get-JdkFindVersionPrefixes)) {
            AddResult $prefix 'ParameterValue' 'Version prefix'
        }
    }
    if (-not $Tool -and -not $Sub) {
        foreach ($toolName in (Get-JdkFindBinTools)) {
            AddResult $toolName 'ParameterValue' 'Bin tool of the selection'
        }
    }
    return $results
}
