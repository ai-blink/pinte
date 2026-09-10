# 사용자가 직접 실행하는 진입점. 빌드·설치·자동 입력은 실행하지 않는다.
[CmdletBinding()]
param(
    [ValidateSet('App', 'InputTransformManual', 'InputTransformDiagnostic')]
    [string]$Mode = 'App',
    [switch]$Administrator,
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$trialDirectory = Join-Path $env:ProgramFiles 'MagnifierInputTransformTrial'
$arguments = @()
switch ($Mode) {
    'App' {
        $executable = Join-Path $workspace 'src\Magnifier.App\bin\Debug\net9.0-windows\Magnifier.App.exe'
    }
    'InputTransformManual' {
        $executable = Join-Path $trialDirectory 'probe\RelayProbe.exe'
        $arguments = @('--input-transform-manual')
    }
    'InputTransformDiagnostic' {
        $executable = Join-Path $trialDirectory 'Magnifier.App.exe'
        $arguments = @('--diagnose-input-transform')
    }
}
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "실행본이 없습니다: $executable"
}
if ($Mode -ne 'App') {
    $marker = Get-Content -LiteralPath (Join-Path $trialDirectory 'trial-install.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $signature = Get-AuthenticodeSignature -FilePath $executable
    if ($marker.Product -ne 'MagnifierInputTransformTrial' -or $signature.Status -ne 'Valid' -or
        $signature.SignerCertificate.Thumbprint -ne $marker.Thumbprint) {
        throw '승인된 UIAccess 실험본의 설치 기록 또는 서명이 다릅니다.'
    }
}
$running = @(Get-Process -Name Magnifier.App,RelayProbe -ErrorAction SilentlyContinue)
$configuration = [ordered]@{
    Mode = $Mode
    Executable = $executable
    Arguments = $arguments
    AdministratorRequested = [bool]$Administrator
    Sha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
    RunningProcessIds = @($running | ForEach-Object { $_.Id })
    Diagnostics = Join-Path $env:LOCALAPPDATA 'Magnifier\diagnostics'
}
if ($CheckOnly) { [pscustomobject]$configuration; return }
if ($running.Count -gt 0) {
    throw 'Magnifier 또는 검증 창이 이미 실행 중입니다. 원래 화면으로 돌아간 뒤 앱을 닫고 다시 실행하세요.'
}
$launch = @{
    FilePath = $executable
    WorkingDirectory = Split-Path -Parent $executable
    WindowStyle = 'Normal'
    PassThru = $true
}
if ($arguments.Count -gt 0) { $launch.ArgumentList = $arguments }
if ($Administrator) { $launch.Verb = 'RunAs' }
$process = Start-Process @launch
$configuration.ProcessId = $process.Id
$configuration.StartedAt = (Get-Date).ToString('o')
# 이는 실행 요청 기록이다. 실제 상승 토큰이나 대상 입력 반응의 성공 판정이 아니다.
$folder = Join-Path $workspace 'artifacts\manual-validation'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$configuration | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (
    Join-Path $folder ('launch-' + $process.Id + '.json')) -Encoding UTF8
[pscustomobject]$configuration
