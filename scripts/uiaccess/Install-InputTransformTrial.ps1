# 기본 호출은 변경 계획만 출력한다. -Apply는 사용자 승인 후 관리자 PowerShell에서만 실행한다.
[CmdletBinding()]
param([switch]$Apply)

$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$standardOutput = Join-Path $workspace 'src\Magnifier.App\bin\Debug\net9.0-windows'
$installDirectory = Join-Path $env:ProgramFiles 'MagnifierInputTransformTrial'
$certificateSubject = 'CN=Magnifier Input Transform Trial'
if (-not $Apply) {
    [pscustomobject]@{
        Workspace = $workspace
        BuildOutput = $standardOutput
        InstallDirectory = $installDirectory
        Certificate = "$certificateSubject / CurrentUser\My / 30일 / private key 비내보내기"
        Trust = '새 인증서의 공개 부분만 LocalMachine\Root에 등록'
        Manifest = '선택형 asInvoker/uiAccess=true; 진단 명령만 실행 가능'
        Restore = '설치 성공·실패와 무관하게 표준 출력은 uiAccess=false로 재빌드'
        Run = '자동 실행 없음; 설치 뒤 일반 사용자 세션에서 --diagnose-input-transform 실행'
    }
    return
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw '사용자 승인 후 관리자 PowerShell에서 실행해야 합니다. 이 스크립트는 자동 권한 상승을 하지 않습니다.'
}
if (@(Get-Process -Name Magnifier.App -ErrorAction SilentlyContinue).Count -gt 0) {
    throw 'Magnifier를 닫은 뒤 실행하세요. 실행 중인 앱을 종료하지 않습니다.'
}
if (Test-Path -LiteralPath $installDirectory) { throw "기존 설치를 덮어쓰지 않습니다: $installDirectory" }
if ((Get-Item -LiteralPath $env:ProgramFiles).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw 'Program Files가 재분석 지점이므로 설치하지 않습니다.'
}

Push-Location -LiteralPath $workspace
$logDirectory = Join-Path $workspace 'artifacts'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory ('uiaccess-install-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
Start-Transcript -Path $logPath | Out-Null
try {
    & dotnet build Magnifier.slnx --nologo -p:InputTransformTrial=true
    if ($LASTEXITCODE -ne 0) { throw 'UIAccess 실험본 빌드 실패' }
    New-Item -ItemType Directory -Path $installDirectory | Out-Null
    $certificate = New-SelfSignedCertificate -Type CodeSigningCert -Subject $certificateSubject `
        -CertStoreLocation 'Cert:\CurrentUser\My' -NotAfter (Get-Date).AddDays(30) `
        -KeyExportPolicy NonExportable -HashAlgorithm SHA256
    $marker = [ordered]@{
        Product = 'MagnifierInputTransformTrial'
        Thumbprint = $certificate.Thumbprint
        Subject = $certificateSubject
        OwnerSid = $identity.User.Value
        CreatedUtc = [DateTime]::UtcNow.ToString('o')
        Workspace = $workspace
    }
    $marker | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $installDirectory 'trial-install.json') -Encoding UTF8
    $publicCertificate = Join-Path $installDirectory 'trial-public.cer'
    [IO.File]::WriteAllBytes($publicCertificate, $certificate.RawData)
    Import-Certificate -FilePath $publicCertificate -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
    # EXE뿐 아니라 같은 표준 빌드의 DLL, deps, runtimeconfig 및 리소스를 함께 설치한다.
    Get-ChildItem -LiteralPath $standardOutput | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $installDirectory -Recurse
    }
    $installedExe = Join-Path $installDirectory 'Magnifier.App.exe'
    $signature = Set-AuthenticodeSignature -FilePath $installedExe -Certificate $certificate -HashAlgorithm SHA256
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
        throw '실험본 서명 검증 실패. trial-install.json을 보존했으므로 제거 스크립트로 정리할 수 있습니다.'
    }
    Write-Output "설치됨: $installedExe (자동 실행하지 않음)"
    Write-Output "제거 시 사용할 인증서: $($certificate.Thumbprint)"
}
finally {
    try {
        & dotnet build Magnifier.slnx --nologo -p:InputTransformTrial=false
        if ($LASTEXITCODE -ne 0) { throw '일반 표준 실행본 복원 빌드 실패. 앱을 실행하기 전에 재빌드가 필요합니다.' }
    }
    finally {
        Stop-Transcript | Out-Null
        Pop-Location
    }
}
