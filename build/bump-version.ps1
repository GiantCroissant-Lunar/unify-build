param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Version must be a semantic version such as 0.3.3 or 0.3.3-preview1."
}

$repoRoot = Split-Path -Parent $PSScriptRoot

function Write-Utf8NoBom {
    param(
        [string]$Path,
        [string]$Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Replace-FirstRegex {
    param(
        [string]$RelativePath,
        [string]$Pattern,
        [scriptblock]$Transform
    )

    $path = Join-Path $repoRoot $RelativePath
    $content = [System.IO.File]::ReadAllText($path)
    $regex = [System.Text.RegularExpressions.Regex]::new($Pattern)
    if (-not $regex.IsMatch($content)) {
        throw "Pattern not found in $RelativePath"
    }

    $updated = $regex.Replace(
        $content,
        [System.Text.RegularExpressions.MatchEvaluator]{ param($match) & $Transform $match },
        1
    )

    Write-Utf8NoBom -Path $path -Content $updated
    Write-Host "Updated $RelativePath"
}

Replace-FirstRegex 'GitVersion.yml' '(?m)^next-version:\s*.+$' { param($m) "next-version: $Version" }
Replace-FirstRegex 'dotnet/src/UnifyBuild.Nuke/UnifyBuild.Nuke.csproj' '(?m)^(\s*<Version>)[^<]+(</Version>\s*)$' { param($m) "$($m.Groups[1].Value)$Version$($m.Groups[2].Value)" }
Replace-FirstRegex 'dotnet/src/UnifyBuild.Tool/UnifyBuild.Tool.csproj' '(?m)^(\s*<Version>)[^<]+(</Version>\s*)$' { param($m) "$($m.Groups[1].Value)$Version$($m.Groups[2].Value)" }
Replace-FirstRegex 'unity/com.unifybuild.editor/package.json' '(?m)^(\s*"version"\s*:\s*")[^"]+(",?)(\r?)$' { param($m) "$($m.Groups[1].Value)$Version$($m.Groups[2].Value)$($m.Groups[3].Value)" }
Replace-FirstRegex '.config/dotnet-tools.json' '(?s)("unifybuild\.tool"\s*:\s*\{\s*"version"\s*:\s*")[^"]+("\s*,)' { param($m) "$($m.Groups[1].Value)$Version$($m.Groups[2].Value)" }
Replace-FirstRegex 'CHANGELOG.md' '(?m)^##\s+\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(\r?)$' { param($m) "## $Version$($m.Groups[1].Value)" }
Replace-FirstRegex 'unity/com.unifybuild.editor/CHANGELOG.md' '(?m)^##\s+\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(\r?)$' { param($m) "## $Version$($m.Groups[1].Value)" }

Write-Host "Version bump complete: $Version"
