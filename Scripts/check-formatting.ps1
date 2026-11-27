param(
    [switch]$fix
)

$ErrorActionPreference = "Stop"

git fetch origin master
if ($LASTEXITCODE -ne 0) {
    throw "Unable to fetch master."
}

$base = git merge-base origin/master HEAD
if ($LASTEXITCODE -ne 0) {
    throw "Unable to determine merge base."
}

$files = @(git diff --name-only $base HEAD -- '*.cs')

if ($files.Count -eq 0) {
    Write-Host "No changed C# files to check."
    exit 0
}

if ($Fix) {
    Write-Host "Fixing formatting:"
} else {
    Write-Host "Checking formatting:"
}

$files | ForEach-Object { Write-Host "  $_" }

$formatArgs = @(
    "format"
    "NullByte.slnx"
    "--no-restore"
    "--severity"
    "warn"
    "--include"
) + $files
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