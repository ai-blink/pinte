# 이 스크립트가 소유한 고정 설치 폴더와 전용 인증서만 제거한다.
[CmdletBinding()]
param([switch]$Apply)

$ErrorActionPreference = 'Stop'
$installDirectory = [IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'MagnifierInputTransformTrial'))
$expectedDirectory = [IO.Path]::GetFullPath($env:ProgramFiles).TrimEnd('\') + '\MagnifierInputTransformTrial'
if ($installDirectory -ne $expectedDirectory) { throw '제거 대상 경로가 지정된 설치 폴더와 다릅니다.' }
if (-not (Test-Path -LiteralPath $installDirectory)) { Write-Output '설치 폴더 없음'; return }
$directory = Get-Item -LiteralPath $installDirectory
if ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw '재분석 지점은 제거하지 않습니다.' }
if ((Get-Item -LiteralPath $env:ProgramFiles).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw 'Program Files가 재분석 지점이므로 제거하지 않습니다.'
}
$marker = Get-Content -LiteralPath (Join-Path $installDirectory 'trial-install.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($marker.Product -ne 'MagnifierInputTransformTrial' -or
    $marker.Subject -ne 'CN=Magnifier Input Transform Trial' -or $marker.Thumbprint -notmatch '^[A-Fa-f0-9]{40}$') {
    throw '전용 설치 기록이 아니므로 제거하지 않습니다.'
}
$certificatePaths = @("Cert:\LocalMachine\Root\$($marker.Thumbprint)", "Cert:\CurrentUser\My\$($marker.Thumbprint)")
if ($marker.OwnerSid -ne [Security.Principal.WindowsIdentity]::GetCurrent().User.Value) {
    throw '설치 때 인증서를 생성한 계정으로 제거해야 합니다. 다른 계정의 인증서를 남긴 채 기록을 지우지 않습니다.'
}
foreach ($certificatePath in $certificatePaths) {
    if ((Test-Path -LiteralPath $certificatePath) -and (Get-Item -LiteralPath $certificatePath).Subject -ne $marker.Subject) {
        throw '인증서가 전용 설치 기록과 다릅니다.'
    }
}
if (-not $Apply) {
    [pscustomobject]@{ RemoveDirectory = $installDirectory; RemoveCertificates = $certificatePaths }
    return
}
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw '제거는 사용자 승인 후 관리자 PowerShell에서 실행해야 합니다.'
}
if (@(Get-Process -Name Magnifier.App,RelayProbe -ErrorAction SilentlyContinue).Count -gt 0) {
    throw 'Magnifier와 검증 창을 닫은 뒤 제거하세요. 실행 중인 앱을 종료하지 않습니다.'
}
if (@(Get-ChildItem -LiteralPath $installDirectory -Recurse -Force | Where-Object {
    $_.Attributes -band [IO.FileAttributes]::ReparsePoint
}).Count -gt 0) { throw '설치 폴더 내부에 재분석 지점이 있어 제거하지 않습니다.' }
foreach ($certificatePath in $certificatePaths) {
    if (Test-Path -LiteralPath $certificatePath) {
        if ($certificatePath.StartsWith('Cert:\CurrentUser\My\', [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $certificatePath -DeleteKey -Force
        }
        else { Remove-Item -LiteralPath $certificatePath -Force }
    }
}
Remove-Item -LiteralPath $installDirectory -Recurse -Force
Write-Output '전용 실험본과 전용 인증서를 제거했습니다.'
