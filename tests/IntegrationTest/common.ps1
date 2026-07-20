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

# 失敗時の調査用にアプリのログを出力する
function Show-AppLogs([string]$ProfileDir) {
	$logDir = Join-Path $ProfileDir 'Logs'
	if (-not (Test-Path $logDir)) {
		Write-Host "ログディレクトリが存在しません: $logDir"
		return
	}
	foreach ($file in Get-ChildItem $logDir -File) {
		Write-Host "===== $($file.FullName) ====="
		Get-Content $file.FullName -Tail 100
	}
}
