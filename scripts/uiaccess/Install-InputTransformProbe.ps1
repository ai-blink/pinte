# 승인된 전용 설치 안에서만 입력 검증 실행본을 갱신한다. 기존 인증서를 사용하며 신뢰를 새로 등록하지 않는다.
[CmdletBinding()]
param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$installDirectory = Join-Path $env:ProgramFiles 'MagnifierInputTransformTrial'
$marker = Get-Content -LiteralPath (Join-Path $installDirectory 'trial-install.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if ($marker.Product -ne 'MagnifierInputTransformTrial' -or $marker.Subject -ne 'CN=Magnifier Input Transform Trial' -or
    $marker.OwnerSid -ne $identity.User.Value -or $marker.Thumbprint -notmatch '^[A-Fa-f0-9]{40}$') { throw '전용 설치 기록 불일치' }
$certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$($marker.Thumbprint)"
if ($certificate.Subject -ne $marker.Subject -or -not $certificate.HasPrivateKey) { throw '전용 서명 인증서 불일치' }
$probeDirectory = Join-Path $installDirectory 'probe'
if (-not $Apply) { [pscustomobject]@{Workspace=$workspace;ProbeDirectory=$probeDirectory;Certificate=$certificate.Subject}; return }
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw '관리자 PowerShell 필요' }
if (@(Get-Process -Name Magnifier.App,RelayProbe -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Magnifier 또는 프로브가 실행 중입니다.' }
foreach ($path in @($env:ProgramFiles, $installDirectory, $probeDirectory)) {
    if ((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw '재분석 지점에는 설치하지 않습니다.'
    }
}
Push-Location -LiteralPath $workspace
$logDirectory = Join-Path $workspace 'artifacts'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
Start-Transcript -Path (Join-Path $logDirectory ('uiaccess-probe-install-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')) | Out-Null
try {
    & dotnet build Magnifier.slnx --nologo -p:InputTransformTrial=false -tl:off
    if ($LASTEXITCODE -ne 0) { throw '표준 솔루션 빌드 실패' }
    & dotnet build scripts/probes/RelayProbe.csproj --nologo -t:Rebuild -p:InputTransformTrial=true -tl:off
    if ($LASTEXITCODE -ne 0) { throw 'OS 입력 검증 빌드 실패' }
    New-Item -ItemType Directory -Path $probeDirectory -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $workspace 'scripts\probes\bin\Debug\net9.0-windows') | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $probeDirectory -Recurse -Force
    }
    $probeExe = Join-Path $probeDirectory 'RelayProbe.exe'
    $manifestTool = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\mt.exe'
    $manifestOutput = Join-Path $logDirectory 'probe-installed.manifest'
    & $manifestTool '-nologo' ('-inputresource:' + $probeExe + ';#1') ('-out:' + $manifestOutput)
    if ($LASTEXITCODE -ne 0) { throw '실제 프로브 manifest 추출 실패' }
    [xml]$embeddedManifest = Get-Content -LiteralPath $manifestOutput -Raw -Encoding UTF8
    $executionLevel = $embeddedManifest.SelectSingleNode("//*[local-name()='requestedExecutionLevel']")
    if ($executionLevel.uiAccess -ne 'true' -or $executionLevel.level -ne 'asInvoker') { throw '실제 프로브 manifest가 UIAccess 설정과 다릅니다.' }
    $signature = Set-AuthenticodeSignature -FilePath $probeExe -Certificate $certificate -HashAlgorithm SHA256
    if ($signature.Status -ne 'Valid') { throw '프로브 서명 실패' }
    Write-Output "설치됨: $probeExe (자동 실행 없음)"
}
finally {
    try {
        & dotnet build scripts/probes/RelayProbe.csproj --nologo -t:Rebuild -p:InputTransformTrial=false -tl:off
        if ($LASTEXITCODE -ne 0) { throw '일반 프로브 복원 빌드 실패' }
    }
    finally { Stop-Transcript | Out-Null; Pop-Location }
}
