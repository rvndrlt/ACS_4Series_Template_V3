$ErrorActionPreference = "SilentlyContinue"

$commit = (git rev-parse --short HEAD 2>$null)
$branch = (git rev-parse --abbrev-ref HEAD 2>$null)
$date = (git log -1 --format=%cd --date=short 2>$null)

# Only update if git commands succeeded
if ([string]::IsNullOrWhiteSpace($commit)) {
    Write-Host "Git not available or not a repo - skipping version update"
    exit 0
}

$content = @"
namespace ACS_4Series_Template_V3
{
    public static class GitVersionInfo
    {
        public const string CommitHash = "$commit";
        public const string Branch = "$branch";
        public const string CommitDate = "$date";
    }
}
"@

$filePath = Join-Path -Path $PSScriptRoot -ChildPath "GitVersionInfo.cs"
$tempPath = "$filePath.tmp"

# Write to temp file first, then rename to avoid truncation race
Set-Content -Path $tempPath -Value $content -Force
if (Test-Path $tempPath) {
    if (Test-Path $filePath) { Remove-Item $filePath -Force }
    Move-Item $tempPath $filePath -Force
}