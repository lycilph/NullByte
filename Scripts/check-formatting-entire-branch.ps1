param(
    [switch]$Fix
)

$ErrorActionPreference = "Stop"

if ($Fix) {
    Write-Host "Fixing formatting across all files in NullByte.slnx..."
} else {
    Write-Host "Checking formatting across all files in NullByte.slnx..."
}

$formatArgs = @(
    "format"
    "NullByte.slnx"
    "--no-restore"
    "--severity"
    "warn"
)

if (-not $Fix) {
    $formatArgs += "--verify-no-changes"
}

dotnet @formatArgs

if ($LASTEXITCODE -ne 0) {
    if ($Fix) {
        throw "Formatting failed."
    }
    throw "Formatting verification failed."
}

if ($Fix) {
    Write-Host "Formatting completed."
} else {
    Write-Host "Formatting verification passed."
}