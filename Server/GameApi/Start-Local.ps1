param([switch]$MigrateOnly)
$ErrorActionPreference = 'Stop'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$projectPath = Join-Path $PSScriptRoot 'GameApi.csproj'
if ($MigrateOnly) { dotnet run --project $projectPath -- --migrate-only }
else { dotnet run --project $projectPath }
exit $LASTEXITCODE
