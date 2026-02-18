# Crestron CH5 Contract Generator
# This script allows you to generate and modify CH5 contracts without the Crestron Contract Editor

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("GenerateJson", "GenerateCSharp", "AddComponent", "ListComponents")]
    [string]$Action = "ListComponents",
    
    [Parameter(Mandatory=$false)]
    [string]$ContractPath = "..\Contract",
    
    [Parameter(Mandatory=$false)]
    [string]$JsonPath = "..\ACS_Contract.txt",
    
    [Parameter(Mandatory=$false)]
    [string]$ComponentName,
    
    [Parameter(Mandatory=$false)]
    [int]$StartSmartObjectId,
    
    [Parameter(Mandatory=$false)]
    [int]$Count = 1
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

# Helper function to get timestamp
function Get-ContractTimestamp {
    return (Get-Date).ToString("yyyy-MM-dd HH:mm:ss.fff")
}

# List all components in the contract
function Get-ContractComponents {
    param([string]$ContractFile)
    
    $contract = Get-Content $ContractFile -Raw | ConvertFrom-Json
    
    Write-Host "`nContract: $($contract.name)" -ForegroundColor Cyan
    Write-Host "Version: $($contract.version)"
    Write-Host "Timestamp: $($contract.timestamp)"
    Write-Host "`nComponents found in contract:" -ForegroundColor Yellow
    
    $components = @{}
    
    # Parse boolean states
    foreach ($smartObjId in $contract.signals.states.boolean.PSObject.Properties.Name) {
        $signals = $contract.signals.states.boolean.$smartObjId
        foreach ($joinNum in $signals.PSObject.Properties.Name) {
            $signalName = $signals.$joinNum
            if ($signalName -match "^(\w+)\[(\d+)\]\.") {
                $compName = $matches[1]
                $index = [int]$matches[2]
                if (-not $components.ContainsKey($compName)) {
                    $components[$compName] = @{
                        MinIndex = $index
                        MaxIndex = $index
                        SmartObjectIds = @($smartObjId)
                    }
                } else {
                    if ($index -lt $components[$compName].MinIndex) { $components[$compName].MinIndex = $index }
                    if ($index -gt $components[$compName].MaxIndex) { $components[$compName].MaxIndex = $index }
                    if ($components[$compName].SmartObjectIds -notcontains $smartObjId) {
                        $components[$compName].SmartObjectIds += $smartObjId
                    }
                }
            }
        }
    }
    
    foreach ($comp in $components.GetEnumerator() | Sort-Object { [int]$_.Value.SmartObjectIds[0] }) {
        $count = $comp.Value.MaxIndex - $comp.Value.MinIndex + 1
        $minSO = ($comp.Value.SmartObjectIds | ForEach-Object { [int]$_ } | Measure-Object -Minimum).Minimum
        $maxSO = ($comp.Value.SmartObjectIds | ForEach-Object { [int]$_ } | Measure-Object -Maximum).Maximum
        Write-Host "  $($comp.Key): $count instances (SmartObject IDs: $minSO - $maxSO)" -ForegroundColor Green
    }
    
    return $components
}

# Main execution
switch ($Action) {
    "ListComponents" {
        $jsonFile = Join-Path $scriptDir $JsonPath
        if (Test-Path $jsonFile) {
            Get-ContractComponents -ContractFile $jsonFile
        } else {
            Write-Host "Contract file not found: $jsonFile" -ForegroundColor Red
        }
    }
    
    default {
        Write-Host "Action '$Action' not yet implemented. See ContractModifier.cs for programmatic modifications." -ForegroundColor Yellow
    }
}

Write-Host "`nFor programmatic contract modifications, use the ContractModifier class in your C# code." -ForegroundColor Cyan

