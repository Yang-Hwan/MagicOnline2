param([ValidateRange(1024,65535)][int]$Port = 5082)
$ErrorActionPreference = 'Stop'
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
Push-Location -LiteralPath $PSScriptRoot
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    Write-Host "관리자 사이트: http://127.0.0.1:$Port/admin"
    Write-Host '개발 로그인: 관리자 ID만 입력하세요. 최초 접속이면 해당 ID로 개발용 관리자를 생성합니다.'
    # Keep admin binaries separate so a running game API cannot lock the build output.
    $adminOutput = Join-Path $PSScriptRoot 'bin/AdminServer'
    dotnet build (Join-Path $PSScriptRoot 'GameApi.csproj') --output $adminOutput
    if ($LASTEXITCODE -ne 0) { throw '관리자 서버 빌드에 실패했습니다.' }
    dotnet (Join-Path $adminOutput 'GameApi.dll') --urls "http://127.0.0.1:$Port"
    $serverExitCode = $LASTEXITCODE
}
finally {
    Pop-Location
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
}
exit $serverExitCode
