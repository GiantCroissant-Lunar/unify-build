# Dogfood verification script for unify-build
$ErrorActionPreference = "Stop"

# Get version from GitVersion
$version = gitversion /showvariable MajorMinorPatch 2>$null
if (-not $version) {
    $version = "0.0.0"
}

$ArtifactDir = "build\_artifacts\$version\nuget"

if (-not (Test-Path $ArtifactDir)) {
    throw "FAIL: Artifact directory $ArtifactDir does not exist"
}

$packages = Get-ChildItem -Path $ArtifactDir -Filter "*.nupkg"
if ($packages.Count -eq 0) {
    throw "FAIL: No .nupkg files found in $ArtifactDir"
}

Write-Host "SUCCESS: Found $($packages.Count) NuGet package(s) in $ArtifactDir"
$packages | ForEach-Object { Write-Host "  - $($_.Name)" }
