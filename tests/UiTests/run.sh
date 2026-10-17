#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -P -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
repo="$(cd -P -- "$script_dir/../.." && pwd -P)"
project="$repo/tests/KyoshinEewViewer.UiTests/KyoshinEewViewer.UiTests.csproj"

if [[ "${1:-}" == --inside ]]; then
  shift
  dotnet_exe="$1"; build_dir="$2"; case_dir="$3"; filter="$4"; probe="$5"; parent_netns="$6"; packages="$7"
  ip link set lo up
  mkdir -p "$case_dir/home" "$case_dir/audit" "$case_dir/results"
  # ホストの名前解決デーモンにUnixソケット経由で委譲しない。変更は専用mount namespace内だけ。
  mount --make-rprivate /
  printf 'hosts: files\n' > "$case_dir/nsswitch.conf"
  mount --bind "$case_dir/nsswitch.conf" /etc/nsswitch.conf
  export HOME="$case_dir/home" DOTNET_CLI_HOME="$case_dir/home" KEVI_UI_TEST_HOME="$case_dir/home"
  export DOTNET_ROOT="$(dirname -- "$dotnet_exe")" NUGET_PACKAGES="$packages"
  export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true DOTNET_NOLOGO=1
  export TESTINGPLATFORM_TELEMETRY_OPTOUT=1
  export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false
  export KEVI_UI_TEST_PARENT_NETNS="$parent_netns" KEVI_UI_TEST_AUDIT_DIR="$case_dir/audit"
  export KEVI_UI_TEST_NETWORK_PROBE="$probe"
  exec "$dotnet_exe" test "$project" -c Release --no-build --no-restore --artifacts-path "$build_dir" \
    --filter "$filter" --logger 'trx;LogFileName=tests.trx' --results-directory "$case_dir/results"
fi

dotnet_cmd="${KEVI_UI_TEST_DOTNET:-dotnet}"
skip_build=false
while (($#)); do
  case "$1" in
    --dotnet) dotnet_cmd="$2"; shift 2 ;;
    --skip-build) skip_build=true; shift ;;
    *) printf '不明な引数です: %s\n' "$1" >&2; exit 2 ;;
  esac
done
dotnet_exe="$(readlink -f -- "$(command -v -- "$dotnet_cmd")")"
export DOTNET_ROOT="$(dirname -- "$dotnet_exe")"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true DOTNET_NOLOGO=1
export TESTINGPLATFORM_TELEMETRY_OPTOUT=1
if [[ "$($dotnet_exe --version)" != 10.* ]]; then
  printf '.NET 10 SDKが必要です。--dotnet で実行ファイルを指定できます。\n' >&2
  exit 2
fi
command -v ip >/dev/null
command -v python3 >/dev/null
build_dir="$repo/tmp/ui-tests/build"
output="$repo/tmp/ui-tests/$(date -u +%Y%m%dT%H%M%S)-$$"
mkdir -p "$output"

# 依存取得とビルドを終えてから、テスト対象の実行だけを通信のない名前空間へ移す。
if [[ "$skip_build" == false ]]; then
  "$dotnet_exe" build "$project" -c Release --artifacts-path "$build_dir" --nologo
fi
packages="${NUGET_PACKAGES:-$($dotnet_exe nuget locals global-packages --list | sed 's/^[^:]*: //')}"
parent_netns="$(readlink /proc/self/ns/net)"
isolator=(unshare --user --map-root-user --net --mount)
if ! "${isolator[@]}" true 2>/dev/null; then
  # GitHubホスト等で非特権user namespaceが制限される場合だけ、専用net namespaceをsudoで作る。
  isolator=(sudo -n unshare --net --mount)
  if ! "${isolator[@]}" true 2>/dev/null; then
    printf 'ネットワーク隔離を作成できません。通信可能な環境ではテストを実行しません。\n' >&2
    exit 2
  fi
fi
printf 'ネットワーク隔離: %s\n' "${isolator[*]}"

run_case() {
  local name="$1" filter="$2" probe="$3"
  "${isolator[@]}" bash "$script_dir/run.sh" --inside "$dotnet_exe" "$build_dir" "$output/$name" "$filter" "$probe" "$parent_netns" "$packages"
}

run_case positive 'FullyQualifiedName!~CaughtByAppStillFails' ''
python3 "$script_dir/verify-results.py" "$output/positive" passed

# 例外をアプリ側で捕捉しても、通信監査がそのケースを失敗させることを実プロセスで確認する。
for item in 'http:HttpRequestCaughtByAppStillFails' 'fixture:FixtureRequestCaughtByAppStillFails' 'dns:DnsRequestCaughtByAppStillFails' 'socket:SocketRequestCaughtByAppStillFails'; do
  probe="${item%%:*}"; method="${item#*:}"
  set +e
  run_case "$probe" "FullyQualifiedName~$method" "$probe"
  status=$?
  set -e
  if [[ "$status" == 0 ]]; then
    printf '通信禁止の失敗検査が成功扱いになりました: %s\n' "$probe" >&2
    exit 1
  fi
  python3 "$script_dir/verify-results.py" "$output/$probe" failed "$method"
done
printf 'UIテストと通信禁止の失敗検査が完了しました。結果: %s\n' "$output"
