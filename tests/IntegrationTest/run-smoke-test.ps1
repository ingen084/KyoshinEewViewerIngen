#!/usr/bin/env pwsh
# スモークテスト: 実バイナリをテスト用プロファイルで起動し、
# メインウィンドウ表示のセンチネルファイル、正常終了、起動時ワークフローのテンプレート出力を確認する。
# 対象バイナリは -p:IntegrationTestBuild=true でビルドされている必要がある。
param(
	[Parameter(Mandatory = $true)][string]$AppExePath,
	[Parameter(Mandatory = $true)][string]$ProfileTemplate,
	[string]$WorkflowsTemplate = (Join-Path $PSScriptRoot 'profiles/workflows-smoke.json'),
	[string]$ExpectedVersion,
	[string]$WorkDir,
	[int]$TimeoutSec = 180
)

# トリミングで関数が消えるとテンプレートは例外になり、標準型のメンバーが消えると値が空になる
$expectedTemplateOutput = 'slice=5|index_of=2|zero=0|microsecond=9|nanosecond=100|days=2|guid_version=4'

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$AppExePath = (Resolve-Path $AppExePath).Path
$ProfileTemplate = (Resolve-Path $ProfileTemplate).Path
if (-not $WorkDir) {
	$WorkDir = Join-Path $PSScriptRoot '../../tmp/integration/smoke'
}
New-Item -ItemType Directory -Force $WorkDir | Out-Null

$profileDir = Initialize-ProfileDir -WorkDir $WorkDir -ProfileTemplate $ProfileTemplate

# macOSのログは実行をまたいで残るので、今回の出力を実行IDで見分ける
$runId = [guid]::NewGuid().ToString('N')
$workflows = (Get-Content $WorkflowsTemplate -Raw).Replace('__RUN_ID__', $runId)
Set-Content (Join-Path $profileDir 'workflows.json') $workflows -Encoding utf8NoBOM

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

	# workflows.json を読めなかった場合も黙って空になるので、出力がないことも失敗にする
	$templateOutput = Find-AppLogText -ProfileDir $profileDir -Prefix "TEMPLATE-CHECK|$runId|"
	if ($null -eq $templateOutput) {
		throw '起動時ワークフローのテンプレート出力がログにありません'
	}
	if ($templateOutput -ne $expectedTemplateOutput) {
		throw "テンプレート出力が一致しません (期待値: $expectedTemplateOutput, 実際: $templateOutput)"
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
