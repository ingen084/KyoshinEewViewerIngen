#!/usr/bin/env pwsh
# スモークテスト: 実バイナリをテスト用プロファイルで起動し、
# メインウィンドウ表示のセンチネルファイルと正常終了を確認する。
# 対象バイナリは -p:IntegrationTestBuild=true でビルドされている必要がある。
param(
	[Parameter(Mandatory = $true)][string]$AppExePath,
	[Parameter(Mandatory = $true)][string]$ProfileTemplate,
	[string]$ExpectedVersion,
	[string]$WorkDir,
	[int]$TimeoutSec = 180
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$AppExePath = (Resolve-Path $AppExePath).Path
$ProfileTemplate = (Resolve-Path $ProfileTemplate).Path
if (-not $WorkDir) {
	$WorkDir = Join-Path $PSScriptRoot '../../tmp/integration/smoke'
}
New-Item -ItemType Directory -Force $WorkDir | Out-Null

$profileDir = Initialize-ProfileDir -WorkDir $WorkDir -ProfileTemplate $ProfileTemplate
$sentinelPath = Join-Path (Split-Path $AppExePath -Parent) 'kevi-startup-sentinel.json'
Remove-Item $sentinelPath -Force -ErrorAction SilentlyContinue

Write-Host "スモークテストを開始します: $AppExePath"
$proc = Start-Process -FilePath $AppExePath -ArgumentList @('-c', $profileDir, '--smoke-test') -PassThru

try {
	$sentinel = Wait-Sentinel -Path $sentinelPath -TimeoutSec $TimeoutSec
	Write-Host "センチネルを検出しました: Stage=$($sentinel.Stage), Version=$($sentinel.Version)"

	if ($sentinel.Stage -ne 'main-window-opened') {
		throw "予期しないStageです: $($sentinel.Stage)"
	}
	if ($ExpectedVersion -and $sentinel.Version -ne $ExpectedVersion) {
		throw "バージョンが一致しません (期待値: $ExpectedVersion, 実際: $($sentinel.Version))"
	}

	# スモークテストモードの自動終了を待つ
	if (-not $proc.WaitForExit(60000)) {
		throw 'アプリが自動終了しませんでした'
	}
	if ($proc.ExitCode -ne 0) {
		throw "アプリが異常終了しました (終了コード: $($proc.ExitCode))"
	}

	Write-Host "スモークテスト成功: バージョン $($sentinel.Version)"
}
catch {
	Show-AppLogs -ProfileDir $profileDir
	if (-not $proc.HasExited) {
		$proc.Kill($true)
	}
	throw
}
