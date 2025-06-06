$commit = (git rev-parse --short HEAD)
$branch = (git rev-parse --abbrev-ref HEAD)
$date = (git log -1 --format=%cd --date=short)

$content = @"
// This file is auto-generated. Do not edit manually.
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
Set-Content -Path $filePath -Value $content -Force