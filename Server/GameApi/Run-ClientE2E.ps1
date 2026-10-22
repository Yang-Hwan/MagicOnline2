param(
    [ValidateSet('rematch','disconnect','reconnect','restart','public','public-guard','isolation','corrupt','outage')][string]$Mode = 'rematch',
    [ValidateSet(0,9003,9004)][int]$HallId = 0
)
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$buildFile = Join-Path $workspace 'Logs\practice-test-build.json'
$build = Get-Content -LiteralPath $buildFile -Raw | ConvertFrom-Json
if ($build.result -ne 'Succeeded' -or !$build.startedUtc) {
    throw 'A successful current development player build is required.'
}
if (!(Test-Path -LiteralPath $build.executable)) { throw 'Test player not found.' }
$newerSources = @(Get-ChildItem -LiteralPath (Join-Path $workspace 'Assets\Scripts'),(Join-Path $workspace 'Assets\Editor') -Recurse -Filter '*.cs' |
    Where-Object { $_.LastWriteTimeUtc -gt ([datetime]$build.startedUtc).ToUniversalTime() })
if ($newerSources.Count) {
    # Unity Reimport can touch a source without changing it. Accept that only when
    # the actual player's portable PDB proves the same SHA256 was compiled.
    $symbolPath = Join-Path (Split-Path $build.executable) 'MagicOnline2-Test_Data\Managed\Assembly-CSharp.pdb'
    $symbolStream = $null; $symbolProvider = $null
    try {
        $symbolStream = [IO.File]::OpenRead($symbolPath)
        $symbolProvider = [System.Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($symbolStream)
        $metadata = $symbolProvider.GetMetadataReader(); $compiledHashes = @{}
        foreach ($handle in $metadata.Documents) {
            $document = $metadata.GetDocument($handle)
            if ($metadata.GetGuid($document.HashAlgorithm) -eq [guid]'8829d00f-11b8-4213-878b-770e8597ac16') {
                $compiledHashes[$metadata.GetString($document.Name)] = [Convert]::ToHexString($metadata.GetBlobBytes($document.Hash))
            }
        }
        foreach ($source in $newerSources) {
            if ($compiledHashes[$source.FullName] -ne (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash) {
                throw 'Changed or added source requires a new build.'
            }
        }
    }
    catch { throw 'A current build is required; newer source content could not be verified against player symbols.' }
    finally { if ($symbolProvider) { $symbolProvider.Dispose() }; if ($symbolStream) { $symbolStream.Dispose() } }
}
$health = Invoke-RestMethod 'http://127.0.0.1:5080/health'
if (!$health.Ok) { throw 'Local API is not ready.' }
$runId = [guid]::NewGuid().ToString('N')
$directory = Join-Path $workspace ('Logs\LocalApiE2E\' + $Mode + '-' + $runId)
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$hall = if ($Mode -in @('rematch','public','public-guard','restart','isolation','corrupt','outage')) { 9004 } else { 9003 }
if ($HallId) {
    if ($Mode -notin @('public','public-guard')) { throw 'HallId override is supported only for public scene validation.' }
    $hall = $HallId
}
$children = @()
# Credentials survive only in this orchestrator's memory and the A child's environment.
$restartLogin = 'e2e_' + [guid]::NewGuid().ToString('N').Substring(0,16)
$restartPassword = 'E2E-' + [guid]::NewGuid().ToString('N')
function Start-TestPlayer([string[]]$playerArguments, [bool]$restartOwner) {
    $oldLogin = $env:MAGIC_E2E_LOGIN; $oldPassword = $env:MAGIC_E2E_PASSWORD
    try {
        $env:MAGIC_E2E_LOGIN = if ($restartOwner) { $restartLogin } else { $null }
        $env:MAGIC_E2E_PASSWORD = if ($restartOwner) { $restartPassword } else { $null }
        Start-Process -FilePath $build.executable -ArgumentList $playerArguments -WindowStyle Hidden -PassThru
    }
    finally { $env:MAGIC_E2E_LOGIN = $oldLogin; $env:MAGIC_E2E_PASSWORD = $oldPassword }
}
function Read-Report([string]$path) {
    try { if (Test-Path -LiteralPath $path) { return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json } } catch { }
    return $null
}
try {
    foreach ($role in @('A','B')) {
        $reportPath = Join-Path $directory ($role + '.json')
        $logPath = Join-Path $directory ($role + '.log')
        $route = if ($Mode -in @('public','public-guard')) { '-local-api-public-test' } else { '-practice-match-test' }
        $arguments = @($route,'-local-api-e2e',$Mode,'-e2e-role',$role,'-e2e-hall',"$hall",'-e2e-output',$reportPath,
            '-logFile',$logPath,'-screen-fullscreen','0','-screen-width','960','-screen-height','540')
        if ($Mode -eq 'reconnect' -and $role -eq 'B') { $arguments += '-practice-match-recovery-test' }
        $children += Start-TestPlayer $arguments ($Mode -in @('restart','isolation','corrupt') -and $role -eq 'A')
        if ($role -eq 'A') {
            $hostDeadline = (Get-Date).AddSeconds(100)
            do {
                $hostReport = Read-Report $reportPath
                if ($hostReport.Error) { throw ('Host bootstrap failed: ' + $hostReport.Error) }
                if ((Test-Path -LiteralPath $logPath) -and
                    (Select-String -LiteralPath $logPath -Pattern '\[MatchConnection\] event=JoinedRoom ' -Quiet)) { break }
                if ((Get-Date) -gt $hostDeadline) { throw 'Host did not create its isolated room.' }
                Start-Sleep -Seconds 2
            } while ($true)
        }
    }
    Write-Output ('Client reports: ' + $directory)
    $resumed = $false
    $deadline = (Get-Date).AddSeconds(300)
    do {
        $a = Read-Report (Join-Path $directory 'A.json')
        $b = Read-Report (Join-Path $directory 'B.json')
        if ($a.Error -or $b.Error) { throw ('Client failed: A=' + $a.Error + ' B=' + $b.Error) }
        if ($Mode -in @('restart','isolation','corrupt') -and $a.StagedForRestart -and !$resumed -and $children[0].HasExited) {
            Copy-Item -LiteralPath (Join-Path $directory 'A.json') -Destination (Join-Path $directory 'A-before-restart.json')
            $resumeArguments = @('-practice-match-test','-local-api-e2e',$Mode,'-e2e-role','A','-e2e-resume','true',
                '-e2e-output',(Join-Path $directory 'A.json'),'-logFile',(Join-Path $directory 'A-resumed.log'),'-screen-fullscreen','0')
            $children += Start-TestPlayer $resumeArguments $true
            $resumed = $true
        }
        if ($a.Ok -and ($b.Ok -or ($Mode -eq 'disconnect' -and $b.ExitedForDisconnect))) {
            $errors = Get-ChildItem -LiteralPath $directory -Filter '*.log' |
                Select-String -Pattern '^(?:[A-Za-z0-9_]+\.)*[A-Za-z0-9_]*Exception:'
            if ($errors) { throw 'Unhandled client exceptions found; inspect per-client logs.' }
            [pscustomobject]@{ Mode=$Mode; Ok=$true; Directory=$directory; A=$a; B=$b } |
                ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $directory 'result.json')
            Write-Output ('PASS: ' + $Mode)
            break
        }
        if ((Get-Date) -gt $deadline) { throw 'Client integration timeout; inspect per-client logs.' }
        Start-Sleep -Seconds 3
    } while ($true)
}
finally {
    # Stop only test processes created by this invocation; never the Unity editor or another player.
    foreach ($child in $children) { if (!$child.HasExited) { Stop-Process -Id $child.Id -ErrorAction SilentlyContinue } }
}
