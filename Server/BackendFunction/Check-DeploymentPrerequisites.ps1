# Read-only checks. Does not print keys, install software, or deploy functions.
$ErrorActionPreference = 'Stop'
$probeCli = Get-Command backend -ErrorAction SilentlyContinue
$probeConfigPath = Join-Path $env:APPDATA 'AFI/BFunc/config.json'
$probeConfigPresent = Test-Path -LiteralPath $probeConfigPath
$probeConfigKeyPresent = $false
$probeConfigProjectMatches = $false
$probeConfigFunctionMatches = $false
if ($probeConfigPresent) {
    $probeConfig = Get-Content -LiteralPath $probeConfigPath -Raw | ConvertFrom-Json
    $probeConfigKeyPresent = -not [string]::IsNullOrWhiteSpace($probeConfig.account.authKey)
    $probeConfigProjectMatches = $probeConfig.projectInfo.projectPath.TrimEnd('\','/') -eq $PSScriptRoot.TrimEnd('\','/') -and $probeConfig.projectInfo.csprojName -eq 'BackendFunction.csproj'
    $probeConfigFunctionMatches = $probeConfig.functionName -eq 'MatchSettlementProbe'
}
$probeUnitySettings = Join-Path $PSScriptRoot '../../Assets/TheBackend/Resources/TheBackendSettings.asset'
$probeUnityKeyPresent = $false
if (Test-Path -LiteralPath $probeUnitySettings) {
    $probeKeyLine = Select-String -LiteralPath $probeUnitySettings -Pattern '^\s*functionAuthKey:\s*(.*)$'
    $probeUnityKeyPresent = $null -ne $probeKeyLine -and -not [string]::IsNullOrWhiteSpace($probeKeyLine.Matches[0].Groups[1].Value.Trim('"',"'"))
}
[pscustomobject]@{
    BackendCliAvailable = $null -ne $probeCli
    CliConfigPresent = $probeConfigPresent
    CliAuthKeyConfigured = $probeConfigKeyPresent
    CliProjectMatches = $probeConfigProjectMatches
    CliFunctionMatches = $probeConfigFunctionMatches
    UnityFunctionKeyConfigured = $probeUnityKeyPresent
    ReadyForOfficialBuildAndHealthDeployment = ($null -ne $probeCli -and $probeConfigKeyPresent -and $probeConfigProjectMatches -and $probeConfigFunctionMatches -and $probeUnityKeyPresent)
} | ConvertTo-Json
