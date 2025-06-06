$commit = (git rev-parse --short HEAD) 
$branch = (git rev-parse --abbrev-ref HEAD)
$date = (git log -1 --format=%cd --date=short)

# Template for the GitVersionInfo.cs file
$template = @"
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

# Write to the file
$template | Out-File -FilePath "GitVersionInfo.cs" -Encoding UTF8 -Force

Write-Host "Updated GitVersionInfo.cs with commit $commit, branch $branch, date $date"
