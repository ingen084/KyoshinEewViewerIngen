#!/usr/bin/env pwsh
# ダミー新バージョン(UpdateDummy)の publish 出力を、
# 本体アップデータが期待するリリースアセットzipの形にパッケージングする。
# - windows: KyoshinEewViewer.exe をzipルートに配置
# - ubuntu:  KyoshinEewViewer をzipルートに配置 (実行権限は本体アップデータが付与する)
# - macos:   build-files のテンプレートから KyoshinEewViewer.Desktop.app を組み立ててzip化
param(
	[Parameter(Mandatory = $true)][string]$PublishDir,
	[Parameter(Mandatory = $true)][ValidateSet('windows', 'ubuntu', 'macos')][string]$Platform,
	[Parameter(Mandatory = $true)][string]$OutZip,
	[string]$RepoRoot = (Join-Path $PSScriptRoot '../..'),
	[string]$BundleVersion = '99.0.0'
)

$ErrorActionPreference = 'Stop'

$PublishDir = (Resolve-Path $PublishDir).Path
$RepoRoot = (Resolve-Path $RepoRoot).Path
$outDir = Split-Path $OutZip -Parent
if ($outDir) {
	New-Item -ItemType Directory -Force $outDir | Out-Null
}
$OutZip = [System.IO.Path]::GetFullPath($OutZip)
Remove-Item $OutZip -Force -ErrorAction SilentlyContinue

$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("kevi-dummy-" + [Guid]::NewGuid())
New-Item -ItemType Directory -Force $stage | Out-Null

try {
	switch ($Platform) {
		'windows' {
			Copy-Item (Join-Path $PublishDir 'UpdateDummy.exe') (Join-Path $stage 'KyoshinEewViewer.exe')
			Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $OutZip
		}
		'ubuntu' {
			Copy-Item (Join-Path $PublishDir 'UpdateDummy') (Join-Path $stage 'KyoshinEewViewer')
			# Compress-Archive はUnixの実行権限を保持できないため zip コマンドを使う
			Push-Location $stage
			try {
				zip -j $OutZip 'KyoshinEewViewer' | Out-Null
			}
			finally {
				Pop-Location
			}
		}
		'macos' {
			$appDir = Join-Path $stage 'KyoshinEewViewer.Desktop.app'
			Copy-Item -Recurse (Join-Path $RepoRoot 'build-files/KyoshinEewViewer.Desktop.app') $appDir
			$plist = Join-Path $appDir 'Contents/Info.plist'
			(Get-Content $plist -Raw).Replace('KEVI_VERSION', $BundleVersion) | Set-Content $plist
			New-Item -ItemType Directory -Force (Join-Path $appDir 'Contents/MacOS') | Out-Null
			Copy-Item (Join-Path $PublishDir 'UpdateDummy') (Join-Path $appDir 'Contents/MacOS/KyoshinEewViewer.Desktop')
			chmod +x (Join-Path $appDir 'Contents/MacOS/KyoshinEewViewer.Desktop')
			codesign --force --deep --sign - $appDir
			# 実行権限を保持するため zip コマンドを使う
			Push-Location $stage
			try {
				zip -r $OutZip 'KyoshinEewViewer.Desktop.app' | Out-Null
			}
			finally {
				Pop-Location
			}
		}
	}
	Write-Host "ダミーアセットを作成しました: $OutZip"
}
finally {
	Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}
