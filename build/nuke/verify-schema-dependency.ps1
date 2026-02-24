# Verification script for task 4.2: Verify Pack depends on GenerateSchema
# This script checks that the dependency is correctly configured

Write-Host "Verifying Pack target dependency on GenerateSchema..." -ForegroundColor Cyan

# Check the build help output for dependency graph
$helpOutput = & ./build.ps1 --help 2>&1 | Out-String

# Look for the Pack target and its dependencies
if ($helpOutput -match "Pack\s+-> .*GenerateSchema") {
    Write-Host "✓ SUCCESS: Pack target depends on GenerateSchema" -ForegroundColor Green
    Write-Host "  Dependency graph shows: Pack -> Compile, GenerateSchema" -ForegroundColor Gray
    exit 0
} else {
    Write-Host "✗ FAILED: Pack target does not depend on GenerateSchema" -ForegroundColor Red
    Write-Host "  Expected: Pack -> Compile, GenerateSchema" -ForegroundColor Gray
    Write-Host "  Actual dependency graph:" -ForegroundColor Gray
    $helpOutput -split "`n" | Where-Object { $_ -match "Pack" } | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
    exit 1
}
