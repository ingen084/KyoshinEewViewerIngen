#!/usr/bin/env pwsh
# UIテスト: 実バイナリの中で画面操作のシナリオを実行し、アプリが書き出した結果を確認する。
# 保存した内容を確認するために、同じプロファイルでもう一度起動する。
# 対象バイナリは -p:IntegrationTestBuild=true でビルドされている必要がある。
param(
	[Parameter(Mandatory = $true)][string]$AppExePath,
	[Parameter(Mandatory = $true)][string]$ProfileTemplate,
	[string]$WorkDir,
	[int]$TimeoutSec = 360
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

# プロファイルの Voicevox.Address と合わせる
$MockPort = 8621

$AppExePath = (Resolve-Path $AppExePath).Path
$ProfileTemplate = (Resolve-Path $ProfileTemplate).Path
if (-not $WorkDir) {
	$WorkDir = Join-Path $PSScriptRoot '../../tmp/integration/ui'
}
New-Item -ItemType Directory -Force $WorkDir | Out-Null
$WorkDir = (Resolve-Path $WorkDir).Path

$profileDir = Initialize-ProfileDir -WorkDir $WorkDir -ProfileTemplate $ProfileTemplate

# ファイルがないと、アプリはユーザーの設定ディレクトリのワークフローを読み書きする
Set-Content (Join-Path $profileDir 'workflows.json') '[]' -Encoding utf8NoBOM

$themesDir = Join-Path $profileDir 'Themes'
New-Item -ItemType Directory -Force $themesDir | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'profiles/ui-test-theme.json') $themesDir

$resultPath = Join-Path $profileDir 'kevi-ui-test-result.txt'

# アプリを起動してシナリオの完了を待ち、結果を確認する
function Invoke-UiTestRun([string[]]$ScenarioArgs) {
	Write-Host "UIテストを開始します: $AppExePath $ScenarioArgs"
	$proc = Start-Process -FilePath $AppExePath -ArgumentList (@('-c', $profileDir) + $ScenarioArgs) -PassThru

	try {
		if (-not $proc.WaitForExit($TimeoutSec * 1000)) {
			throw 'アプリが時間内に終了しませんでした'
		}

		# シナリオが始まらずに終了した場合も失敗にする
		if (-not (Test-Path $resultPath)) {
			throw "結果ファイルがありません: $resultPath"
		}
		$result = @(Get-Content $resultPath)
		$result | ForEach-Object { Write-Host $_ }
		if ($result[-1] -ne 'RESULT: PASSED') {
			throw "UIテストに失敗しました: $($result[-1])"
		}
		if ($proc.ExitCode -ne 0) {
			throw "アプリが異常終了しました (終了コード: $($proc.ExitCode))"
		}
	}
	catch {
		Show-AppLogs -ProfileDir $profileDir
		if (-not $proc.HasExited) {
			if (Test-Path $resultPath) {
				Get-Content $resultPath | ForEach-Object { Write-Host $_ }
			}
			$proc.Kill($true)
		}
		throw
	}
}

$mockServer = Start-MockServer -ServeDir (Join-Path $PSScriptRoot 'mock-voicevox') -Port $MockPort -LogDir $WorkDir -ReadyPath 'speakers'
try {
	Invoke-UiTestRun @('--ui-test')

	# 2回目が結果を書き出さずに終了したとき、1回目の結果で合格にならないようにする
	Move-Item $resultPath (Join-Path $profileDir 'kevi-ui-test-result-first-run.txt')

	Invoke-UiTestRun @('--ui-test', '--ui-test-after-restart')

	Write-Host 'UIテスト成功'
}
finally {
	if (-not $mockServer.HasExited) {
		$mockServer.Kill($true)
	}
}
