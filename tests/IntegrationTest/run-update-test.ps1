#!/usr/bin/env pwsh
# 自動アップデート結合テスト:
# モックのGitHub Releases APIサーバーを立て、実バイナリを --auto-update-test で起動し、
# 更新検出→ダウンロード→検証→自己置換→再起動の完了(ダミー新バージョンのセンチネル)を確認する。
# 対象バイナリは -p:IntegrationTestBuild=true でビルドされている必要がある。
param(
	[Parameter(Mandatory = $true)][string]$AppExePath,
	[Parameter(Mandatory = $true)][string]$ProfileTemplate,
	[Parameter(Mandatory = $true)][string]$NewVersionZip,
	[Parameter(Mandatory = $true)][string]$AssetName,
	[Parameter(Mandatory = $true)][string]$ExpectedVersion,
	[int]$Port = 8620,
	[string]$WorkDir,
	[int]$TimeoutSec = 300
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$AppExePath = (Resolve-Path $AppExePath).Path
$ProfileTemplate = (Resolve-Path $ProfileTemplate).Path
$NewVersionZip = (Resolve-Path $NewVersionZip).Path
if (-not $WorkDir) {
	$WorkDir = Join-Path $PSScriptRoot '../../tmp/integration/update'
}
New-Item -ItemType Directory -Force $WorkDir | Out-Null

$profileDir = Initialize-ProfileDir -WorkDir $WorkDir -ProfileTemplate $ProfileTemplate
$sentinelPath = Join-Path (Split-Path $AppExePath -Parent) 'kevi-startup-sentinel.json'
Remove-Item $sentinelPath -Force -ErrorAction SilentlyContinue

# モックサーバーのコンテンツを生成
$serveDir = Join-Path $WorkDir 'serve'
if (Test-Path $serveDir) {
	Remove-Item $serveDir -Recurse -Force
}
New-Item -ItemType Directory -Force $serveDir | Out-Null
Copy-Item $NewVersionZip (Join-Path $serveDir $AssetName)

$hash = (Get-FileHash -Algorithm SHA256 $NewVersionZip).Hash.ToLowerInvariant()
$size = (Get-Item $NewVersionZip).Length
$baseUrl = "http://127.0.0.1:$Port"

$release = [ordered]@{
	name = "v$ExpectedVersion"
	tag_name = $ExpectedVersion
	body = '結合テスト用リリース'
	url = "$baseUrl/releases.json"
	draft = $false
	prerelease = $false
	created_at = '2026-01-01T00:00:00Z'
	published_at = '2026-01-01T00:00:00Z'
	assets = @(
		[ordered]@{
			name = $AssetName
			label = ''
			content_type = 'application/zip'
			state = 'uploaded'
			size = $size
			download_count = 0
			created_at = '2026-01-01T00:00:00Z'
			updated_at = '2026-01-01T00:00:00Z'
			browser_download_url = "$baseUrl/$AssetName"
			digest = "sha256:$hash"
		}
	)
}
$release | ConvertTo-Json -AsArray -Depth 5 | Set-Content (Join-Path $serveDir 'releases.json')

# モックサーバー(静的ファイル配信)を起動
$python = Get-Command python3 -ErrorAction SilentlyContinue
if (-not $python) {
	$python = Get-Command python -ErrorAction Stop
}
$serverProc = Start-Process -FilePath $python.Source `
	-ArgumentList @('-m', 'http.server', "$Port", '--bind', '127.0.0.1') `
	-WorkingDirectory $serveDir -PassThru `
	-RedirectStandardOutput (Join-Path $WorkDir 'http-server.log') `
	-RedirectStandardError (Join-Path $WorkDir 'http-server.err.log')

$proc = $null
try {
	$serverReady = $false
	for ($i = 0; $i -lt 20; $i++) {
		try {
			Invoke-WebRequest "$baseUrl/releases.json" | Out-Null
			$serverReady = $true
			break
		}
		catch {
			Start-Sleep -Milliseconds 500
		}
	}
	if (-not $serverReady) {
		throw 'モックサーバーが起動しませんでした'
	}
	Write-Host "モックサーバーを起動しました: $baseUrl (アセット: $AssetName, sha256: $hash)"

	$env:KEVI_UPDATE_API_URL = "$baseUrl/releases.json"
	Write-Host "アップデートテストを開始します: $AppExePath"
	$proc = Start-Process -FilePath $AppExePath -ArgumentList @('-c', $profileDir, '--auto-update-test') -PassThru

	$sentinel = Wait-Sentinel -Path $sentinelPath -TimeoutSec $TimeoutSec
	Write-Host "センチネルを検出しました: Stage=$($sentinel.Stage), Version=$($sentinel.Version)"

	if ($sentinel.Stage -ne 'dummy-launched') {
		throw "予期しないStageです: $($sentinel.Stage)"
	}
	if ($sentinel.Version -ne $ExpectedVersion) {
		throw "バージョンが一致しません (期待値: $ExpectedVersion, 実際: $($sentinel.Version))"
	}

	# 旧バージョンのプロセスは更新適用後に自動終了しているはず
	if (-not $proc.WaitForExit(60000)) {
		throw '旧バージョンのプロセスが終了しませんでした'
	}
	if ($proc.ExitCode -ne 0) {
		Write-Host "警告: 旧バージョンの終了コードが0ではありません: $($proc.ExitCode)"
	}

	Write-Host "アップデートテスト成功: バージョン $($sentinel.Version) への更新を確認しました"
}
catch {
	Show-AppLogs -ProfileDir $profileDir
	if ($proc -and -not $proc.HasExited) {
		$proc.Kill($true)
	}
	throw
}
finally {
	Remove-Item Env:KEVI_UPDATE_API_URL -ErrorAction SilentlyContinue
	if (-not $serverProc.HasExited) {
		$serverProc.Kill($true)
	}
}
