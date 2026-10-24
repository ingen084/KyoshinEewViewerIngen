param(
	[string]$Distribution = 'Ubuntu-22.04',
	[string]$DotnetPath = 'dotnet',
	[switch]$SkipBuild,
	# 一部のテストだけを実行する場合の dotnet test のフィルタ
	[string]$Filter
)
$ErrorActionPreference = 'Stop'
$scriptPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot 'run.sh')).Path
$linuxPath = & wsl.exe -d $Distribution --exec wslpath -a $scriptPath
if ($LASTEXITCODE -ne 0) { throw 'WSLのパスを取得できませんでした。' }
$testArguments = @('-d', $Distribution, '--exec', 'bash', $linuxPath.Trim(), '--dotnet', $DotnetPath)
if ($SkipBuild) { $testArguments += '--skip-build' }
if ($Filter) { $testArguments += @('--filter', $Filter) }
& wsl.exe @testArguments
if ($LASTEXITCODE -ne 0) { throw "UIテストが失敗しました (終了コード $LASTEXITCODE)。" }
