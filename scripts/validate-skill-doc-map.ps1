param()

$ErrorActionPreference = "Stop"

$repoRoot = (git rev-parse --show-toplevel).Trim()
Set-Location $repoRoot

$agentsPath = Join-Path $repoRoot "AGENTS.md"
$skillsRoot = Join-Path $repoRoot ".codex/skills"
$docsRoot = Join-Path $repoRoot "docs/agent"

function Fail {
    param([string]$Message)
    Write-Error $Message
    exit 2
}

function Parse-OwnershipTable {
    $text = Get-Content $agentsPath -Raw
    $marker = "## Skill Mapping"
    $index = $text.IndexOf($marker)
    if ($index -lt 0) {
        Fail "AGENTS.md is missing the '## Skill Mapping' section."
    }

    $section = $text.Substring($index) -split "`r?`n"
    $rows = @()
    $inTable = $false

    foreach ($line in $section) {
        if ($line -match '^\| Doc \| Owner \| Notes \|$') {
            $inTable = $true
            continue
        }

        if (-not $inTable) {
            continue
        }

        if ($line -match '^\|[-| ]+\|$') {
            continue
        }

        if (-not $line.StartsWith("|")) {
            break
        }

        $parts = $line.Trim('|').Split('|').ForEach({ $_.Trim() })
        if ($parts.Count -ne 3) {
            Fail "Malformed ownership row in AGENTS.md: $line"
        }

        $doc = $parts[0].Trim('`')
        $owner = $parts[1].Trim('`')
        $notes = $parts[2]

        $rows += [pscustomobject]@{
            Doc = $doc
            Owner = $owner
            Notes = $notes
        }
    }

    if ($rows.Count -eq 0) {
        Fail "AGENTS.md Skill Mapping table is empty."
    }

    return $rows
}

function Get-PrimaryDoc {
    param([string]$SkillPath)

    $lines = Get-Content $SkillPath
    $primaryIndex = [Array]::IndexOf($lines, "## Primary Doc")
    if ($primaryIndex -lt 0) {
        Fail "$SkillPath is missing a '## Primary Doc' section."
    }

    $docs = @()
    for ($i = $primaryIndex + 1; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        if ($line -match '^## ') {
            break
        }

        if ($line -match '^\s*-\s+\[.+\]\(([^)]+)\)\s*$') {
            $docs += $Matches[1]
        }
    }

    if ($docs.Count -ne 1) {
        Fail "$SkillPath must list exactly one primary doc, found $($docs.Count)."
    }

    return $docs[0]
}

function Resolve-RepoRelativeFromFile {
    param(
        [string]$BaseFile,
        [string]$RelativePath
    )

    $baseDirectory = Split-Path -Parent $BaseFile
    $combined = Join-Path $baseDirectory $RelativePath
    $resolved = [System.IO.Path]::GetFullPath($combined)
    return $resolved
}

function To-RepoRelativePath {
    param([string]$AbsolutePath)

    $repoUri = New-Object System.Uri(($repoRoot.TrimEnd('\') + '\'))
    $fileUri = New-Object System.Uri($AbsolutePath)
    $relativeUri = $repoUri.MakeRelativeUri($fileUri)
    return [System.Uri]::UnescapeDataString($relativeUri.ToString())
}

$ownershipRows = Parse-OwnershipTable
$ownershipByDoc = @{}
foreach ($row in $ownershipRows) {
    if ($ownershipByDoc.ContainsKey($row.Doc)) {
        Fail "Duplicate doc in AGENTS.md ownership table: $($row.Doc)"
    }
    $ownershipByDoc[$row.Doc] = $row
}

$docFiles = Get-ChildItem $docsRoot -Filter *.md | ForEach-Object { "docs/agent/$($_.Name)" }
foreach ($doc in $docFiles) {
    if (-not $ownershipByDoc.ContainsKey($doc)) {
        Fail "Doc missing from AGENTS.md ownership table: $doc"
    }
}

$skillFiles = Get-ChildItem $skillsRoot -Recurse -Filter SKILL.md |
    Where-Object { $_.Directory.Name -like "langoose-*" }

$primaryOwners = @{}
foreach ($skillFile in $skillFiles) {
    $skillName = $skillFile.Directory.Name
    $primaryDoc = Get-PrimaryDoc -SkillPath $skillFile.FullName

    $resolvedPrimaryDoc = Resolve-RepoRelativeFromFile -BaseFile $skillFile.FullName -RelativePath $primaryDoc
    if (-not (Test-Path $resolvedPrimaryDoc)) {
        Fail "$skillName primary doc does not exist: $primaryDoc"
    }

    $repoRelativePrimaryDoc = To-RepoRelativePath -AbsolutePath $resolvedPrimaryDoc

    if ($primaryOwners.ContainsKey($repoRelativePrimaryDoc)) {
        $existingOwner = $primaryOwners[$repoRelativePrimaryDoc]
        $routerPair = @($existingOwner, $skillName) -contains "langoose-dev"
        if (-not $routerPair) {
            Fail "Primary doc '$repoRelativePrimaryDoc' is owned by multiple skills: $existingOwner, $skillName"
        }
    }

    if ($skillName -ne "langoose-dev") {
        $primaryOwners[$repoRelativePrimaryDoc] = $skillName
    }

    if ($skillName -ne "langoose-dev") {
        $expected = $ownershipByDoc[$repoRelativePrimaryDoc]
        if (-not $expected) {
            Fail "$skillName primary doc is not listed in AGENTS.md ownership table: $repoRelativePrimaryDoc"
        }

        if ($expected.Owner -ne $skillName) {
            Fail "$skillName primary doc mismatch. AGENTS.md says '$($expected.Owner)' owns $repoRelativePrimaryDoc."
        }
    }
}

foreach ($row in $ownershipRows) {
    if ($row.Owner -eq "doc-only") {
        continue
    }

    if (-not $primaryOwners.ContainsKey($row.Doc)) {
        Fail "Owned doc has no matching skill primary doc: $($row.Doc) -> $($row.Owner)"
    }
}

$referenceFiles = Get-ChildItem $skillsRoot -Recurse -File |
    Where-Object { $_.FullName -match '[\\/]+references[\\/]' }

if ($referenceFiles.Count -gt 0) {
    $paths = $referenceFiles | ForEach-Object { $_.FullName.Replace($repoRoot + "\", "") }
    Fail "Reference files are no longer expected. Remove: $($paths -join ', ')"
}

Write-Output "Skill/doc ownership map is valid."
