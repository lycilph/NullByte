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

Write-Host "Checking formatting:"
$files | ForEach-Object { Write-Host "  $_" }

#dotnet format NullByte.slnx --verify-no-changes --no-restore --severity warn --include $files
dotnet format NullByte.slnx --no-restore --severity warn --include $files

if ($LASTEXITCODE -ne 0) {
    throw "Formatting verification failed."
}

Write-Host "Formatting verification passed."