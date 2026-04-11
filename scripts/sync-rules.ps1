param(
    [Parameter(Position = 0)]
    [ValidateSet("to-codex", "to-claude", "reconcile", "status", "audit")]
    [string]$Mode,

    [Parameter(Position = 1)]
    [ValidateSet("claude", "codex", "agents")]
    [string]$Prefer
)

$ErrorActionPreference = "Stop"

$repoRoot = (git rev-parse --show-toplevel).Trim()
Set-Location $repoRoot

$agentsSkillMarker = "## Skill Index"
$guidanceMarker = "## Guidance Index"
$claudeSpecificMarker = "## Claude-Specific Notes"
$agentsPath = Join-Path $repoRoot "AGENTS.md"
$claudePath = Join-Path $repoRoot "CLAUDE.md"

function Trim-TrailingBlankLines {
    param([string]$Text)

    $trimmed = ($Text -replace "(\r?\n)+\z", "").TrimEnd()
    return ($trimmed -replace "`r`n", "`n")
}

function Get-AgentsShared {
    if (-not (Test-Path $agentsPath)) {
        throw "AGENTS.md not found"
    }

    $text = Get-Content $agentsPath -Raw
    $parts = $text -split [regex]::Escape($agentsSkillMarker), 2
    return (Trim-TrailingBlankLines $parts[0])
}

function Get-AgentsSkillIndex {
    if (-not (Test-Path $agentsPath)) {
        return ""
    }

    $text = Get-Content $agentsPath -Raw
    $index = $text.IndexOf($agentsSkillMarker)

    if ($index -lt 0) {
        return ""
    }

    return (Trim-TrailingBlankLines $text.Substring($index))
}

function Get-ClaudeShared {
    if (-not (Test-Path $claudePath)) {
        throw "CLAUDE.md not found"
    }

    $text = Get-Content $claudePath -Raw
    $index = $text.IndexOf($claudeSpecificMarker)
    if ($index -lt 0) {
        return (Trim-TrailingBlankLines $text)
    }

    return (Trim-TrailingBlankLines $text.Substring(0, $index))
}

function Write-Utf8NoBom {
    param(
        [string]$Path,
        [string]$Content
    )

    $normalized = $Content -replace "(?<!`r)`n", "`r`n"
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $normalized + "`r`n", $encoding)
}

function Sync-ToCodex {
    if (-not (Test-Path $claudePath)) {
        throw "CLAUDE.md not found"
    }

    $shared = Get-ClaudeShared
    $skillIndex = Get-AgentsSkillIndex

    if ([string]::IsNullOrWhiteSpace($skillIndex)) {
        Write-Warning "No Skill Index found in AGENTS.md, copying without it"
        Write-Utf8NoBom -Path $agentsPath -Content $shared
    }
    else {
        Write-Utf8NoBom -Path $agentsPath -Content ($shared + "`r`n`r`n" + $skillIndex)
    }

    Write-Output "Synced CLAUDE.md -> AGENTS.md"
    Audit
}

function Get-ClaudeSpecific {
    if (-not (Test-Path $claudePath)) {
        return ""
    }

    $text = Get-Content $claudePath -Raw
    $index = $text.IndexOf($claudeSpecificMarker)

    if ($index -lt 0) {
        return ""
    }

    return (Trim-TrailingBlankLines $text.Substring($index))
}

function Sync-ToClaude {
    $shared = Get-AgentsShared
    $claudeSpecific = Get-ClaudeSpecific

    if ([string]::IsNullOrWhiteSpace($claudeSpecific)) {
        Write-Utf8NoBom -Path $claudePath -Content $shared
    }
    else {
        Write-Utf8NoBom -Path $claudePath -Content ($shared + "`r`n`r`n" + $claudeSpecific)
    }

    Write-Output "Synced AGENTS.md -> CLAUDE.md"
    Audit
}

function Show-Status {
    $agentsShared = Get-AgentsShared
    $claudeShared = Get-ClaudeShared

    if ($agentsShared -ceq $claudeShared) {
        Write-Output "Shared rules are in sync"
        return
    }

    Write-Output "Shared rules differ"

    $agentLines = $agentsShared -split "`r?`n"
    $claudeLines = $claudeShared -split "`r?`n"
    $diff = Compare-Object $agentLines $claudeLines -PassThru
    if ($diff) {
        $diff | ForEach-Object { Write-Output $_ }
    }

    exit 2
}

function Test-IndexLinks {
    param(
        [string]$Path,
        [string]$Marker,
        [string]$Label
    )

    if (-not (Test-Path $Path)) {
        throw "$Path not found"
    }

    $text = Get-Content $Path -Raw
    $index = $text.IndexOf($Marker)

    if ($index -lt 0) {
        Write-Output "${Label}: marker '$Marker' not found in $Path"
        exit 2
    }

    $section = $text.Substring($index)

    $links = @()
    $mdMatches = [regex]::Matches($section, '\]\(([^)]+)\)')
    foreach ($m in $mdMatches) {
        $links += $m.Groups[1].Value
    }
    $btMatches = [regex]::Matches($section, '`([^`]+\.(?:md|yml|yaml|json|sh|ps1))`')
    foreach ($m in $btMatches) {
        $links += $m.Groups[1].Value
    }

    if ($links.Count -eq 0) {
        Write-Output "${Label}: no links found under $Marker"
        exit 2
    }

    $missing = @()
    foreach ($link in $links) {
        if ($link.StartsWith("http://") -or $link.StartsWith("https://") -or $link.StartsWith("#")) {
            continue
        }

        $fullPath = Join-Path $repoRoot $link
        if (-not (Test-Path $fullPath)) {
            $missing += $link
        }
    }

    if ($missing.Count -gt 0) {
        foreach ($link in $missing) {
            Write-Output "${Label}: missing $link"
        }

        exit 2
    }

    Write-Output "${Label}: all indexed links exist"
}

function Audit {
    Test-IndexLinks -Path $agentsPath -Marker $guidanceMarker -Label "AGENTS Guidance Index"
    Test-IndexLinks -Path $agentsPath -Marker $agentsSkillMarker -Label "AGENTS Skill Index"
    Test-IndexLinks -Path $claudePath -Marker $guidanceMarker -Label "CLAUDE Guidance Index"
}

function Reconcile {
    if (-not (Test-Path $agentsPath) -and -not (Test-Path $claudePath)) {
        throw "Neither AGENTS.md nor CLAUDE.md exists"
    }

    if (-not (Test-Path $agentsPath)) {
        Write-Output "AGENTS.md missing; syncing from CLAUDE.md"
        Sync-ToCodex
        return
    }

    if (-not (Test-Path $claudePath)) {
        Write-Output "CLAUDE.md missing; syncing from AGENTS.md"
        Sync-ToClaude
        return
    }

    $agentsShared = Get-AgentsShared
    $claudeShared = Get-ClaudeShared

    if ($agentsShared -ceq $claudeShared) {
        Write-Output "Shared rules already in sync"
        return
    }

    switch ($Prefer) {
        "claude" {
            Write-Output "Reconciling in favor of CLAUDE.md"
            Sync-ToCodex
            return
        }
        { $_ -in @("codex", "agents") } {
            Write-Output "Reconciling in favor of AGENTS.md"
            Sync-ToClaude
            return
        }
        default {
            Write-Output "Shared rules differ; specify a preference to resolve:"
            Write-Output "  scripts/sync-rules.ps1 reconcile claude"
            Write-Output "  scripts/sync-rules.ps1 reconcile codex"
            Write-Output ""

            $agentLines = $agentsShared -split "`r?`n"
            $claudeLines = $claudeShared -split "`r?`n"
            $diff = Compare-Object $agentLines $claudeLines -PassThru
            if ($diff) {
                $diff | ForEach-Object { Write-Output $_ }
            }

            exit 2
        }
    }
}

switch ($Mode) {
    "to-codex" { Sync-ToCodex }
    "to-claude" { Sync-ToClaude }
    "reconcile" { Reconcile }
    "status" { Show-Status }
    "audit" { Audit }
    default {
        throw "Usage: scripts/sync-rules.ps1 [to-codex|to-claude|reconcile|status|audit] [claude|codex]"
    }
}
