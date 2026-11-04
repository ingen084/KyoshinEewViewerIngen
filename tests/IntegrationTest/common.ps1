# 結合テストドライバの共通関数

# プロファイルテンプレートを作業ディレクトリへ config.json としてコピーする
function Initialize-ProfileDir([string]$WorkDir, [string]$ProfileTemplate) {
	$profileDir = Join-Path $WorkDir 'profile'
	if (Test-Path $profileDir) {
		Remove-Item $profileDir -Recurse -Force
	}
	New-Item -ItemType Directory -Force $profileDir | Out-Null
	Copy-Item $ProfileTemplate (Join-Path $profileDir 'config.json')
	return (Resolve-Path $profileDir).Path
}

# 静的ファイルを配信するモックサーバーを起動し、応答できるようになるのを待ってプロセスを返す
function Start-MockServer([string]$ServeDir, [int]$Port, [string]$LogDir, [string]$ReadyPath) {
	$python = Get-Command python3 -ErrorAction SilentlyContinue
	if (-not $python) {
		$python = Get-Command python -ErrorAction Stop
	}
	$server = Start-Process -FilePath $python.Source `
		-ArgumentList @('-m', 'http.server', "$Port", '--bind', '127.0.0.1') `
		-WorkingDirectory $ServeDir -PassThru `
		-RedirectStandardOutput (Join-Path $LogDir 'http-server.log') `
		-RedirectStandardError (Join-Path $LogDir 'http-server.err.log')

	for ($i = 0; $i -lt 20; $i++) {
		try {
			Invoke-WebRequest "http://127.0.0.1:$Port/$ReadyPath" | Out-Null
			return $server
		}
		catch {
			Start-Sleep -Milliseconds 500
		}
	}
	if (-not $server.HasExited) {
		$server.Kill($true)
	}
	throw 'モックサーバーが起動しませんでした'
}

# センチネルファイルが作成されるのを待って内容を返す
function Wait-Sentinel([string]$Path, [int]$TimeoutSec) {
	$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSec)
	while ([DateTime]::UtcNow -lt $deadline) {
		if (Test-Path $Path) {
			# 書き込み途中の読み取りを避ける
			Start-Sleep -Milliseconds 200
			return Get-Content $Path -Raw | ConvertFrom-Json
		}
		Start-Sleep -Milliseconds 500
	}
	throw "センチネルファイルが時間内に作成されませんでした: $Path"
}

# アプリのログの出力先を返す
function Get-AppLogDirs([string]$ProfileDir) {
	$logDirs = @(Join-Path $ProfileDir 'Logs')
	# macOSは設定に関わらず標準のログディレクトリへ出力する。結合テストの実行中はプロファイル内の専用ディレクトリになる
	if ($IsMacOS) {
		$logDirs += Join-Path $ProfileDir 'appdata/Logs'
	}
	return $logDirs
}

# ログから指定の文字列に続く部分を探す。見つからなければ $null を返す
function Find-AppLogText([string]$ProfileDir, [string]$Prefix) {
	foreach ($logDir in Get-AppLogDirs $ProfileDir) {
		if (-not (Test-Path $logDir)) {
			continue
		}
		$hit = Get-ChildItem $logDir -File | Select-String -SimpleMatch $Prefix | Select-Object -First 1
		if ($hit) {
			return $hit.Line.Substring($hit.Line.IndexOf($Prefix) + $Prefix.Length)
		}
	}
	return $null
}

# 失敗時の調査用にアプリのログを出力する
function Show-AppLogs([string]$ProfileDir) {
	foreach ($logDir in Get-AppLogDirs $ProfileDir) {
		if (-not (Test-Path $logDir)) {
			Write-Host "ログディレクトリが存在しません: $logDir"
			continue
		}
		foreach ($file in Get-ChildItem $logDir -File) {
			Write-Host "===== $($file.FullName) ====="
			Get-Content $file.FullName -Tail 100
		}
	}
}
