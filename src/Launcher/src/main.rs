// KEVi ランチャー PoC Phase 3: 名前付き Win32 Event で .NET から splash 終了通知
//
// 流れ:
//   1. CreateEvent でユニーク名のイベント作成
//   2. 環境変数 KEVI_SPLASH_EVENT で .NET 側に名前を伝える
//   3. splash 表示 → .NET を別スレッドで起動
//   4. メインスレッドは MsgWaitForMultipleObjects で event とメッセージを同時待ち
//   5. .NET 側が SplashWindow.Opened などで OpenEventW + SetEvent
//   6. Rust 側がシグナル受領 → DestroyWindow → .NET 完了待ち → exit

#![windows_subsystem = "windows"]

mod extract;
mod splash;

use std::ffi::OsString;
use std::fs::OpenOptions;
use std::io::Write;
use std::mem::zeroed;
use std::ptr::{null, null_mut};
use std::thread;

use netcorehost::hostfxr::Hostfxr;
use netcorehost::nethost;
use netcorehost::pdcstring::PdCString;
use std::path::Path;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, Ordering};
use windows_sys::Win32::Foundation::{
    CloseHandle, HANDLE, WAIT_FAILED, WAIT_OBJECT_0, WAIT_TIMEOUT,
};
use windows_sys::Win32::System::Performance::{QueryPerformanceCounter, QueryPerformanceFrequency};
use windows_sys::Win32::System::Threading::CreateEventW;
use windows_sys::Win32::UI::WindowsAndMessaging::{
    DestroyWindow, DispatchMessageW, MB_ICONERROR, MB_OK, MSG, MessageBoxW,
    MsgWaitForMultipleObjects, PM_REMOVE, PeekMessageW, QS_ALLINPUT, TranslateMessage, WM_QUIT,
};

pub struct PerfClock {
    pub freq: i64,
    pub start: i64,
}

impl PerfClock {
    pub fn new() -> Self {
        unsafe {
            let mut freq: i64 = 0;
            let mut start: i64 = 0;
            QueryPerformanceFrequency(&raw mut freq);
            QueryPerformanceCounter(&raw mut start);
            Self { freq, start }
        }
    }
    pub fn elapsed_ms(&self) -> f64 {
        unsafe {
            let mut now: i64 = 0;
            QueryPerformanceCounter(&raw mut now);
            (now - self.start) as f64 * 1000.0 / self.freq as f64
        }
    }
}

pub fn log_line(msg: &str) {
    let path = std::env::temp_dir().join("kevi-launcher.log");
    if let Ok(mut f) = OpenOptions::new().create(true).append(true).open(&path) {
        let _ = writeln!(f, "{}", msg);
    }
}

fn show_error_box(message: &str) {
    let title = to_wide("KyoshinEewViewer ランチャー");
    let body = to_wide(message);
    unsafe {
        MessageBoxW(
            null_mut(),
            body.as_ptr(),
            title.as_ptr(),
            MB_OK | MB_ICONERROR,
        );
    }
}

fn to_wide(s: &str) -> Vec<u16> {
    let mut v: Vec<u16> = s.encode_utf16().collect();
    v.push(0);
    v
}

// KEVi の --no-logo (-n) 起動オプションが指定されているかを判定する。
// 値を取るオプション (-c/-s) の値や将来の別オプションを誤検出しないよう、
// CommandLineParser の bool 短縮フラグ (l=console-log, d=debug, n=no-logo) のみで
// 構成される結合フラグ (-n, -dn, -ldn 等) に限定して 'n' を探す。
fn is_splash_disabled(args: &[OsString]) -> bool {
    // KEVi の bool 短縮フラグ文字
    const BOOL_FLAGS: &[char] = &['l', 'd', 'n'];
    for a in args {
        let s = a.to_string_lossy();
        if s == "--no-logo" {
            return true;
        }
        if let Some(rest) = s.strip_prefix('-') {
            if !rest.starts_with('-')
                && !rest.is_empty()
                && rest.chars().all(|c| BOOL_FLAGS.contains(&c))
                && rest.contains('n')
            {
                return true;
            }
        }
    }
    false
}

fn resolve_dll_path() -> Option<OsString> {
    // 1. 開発用: 環境変数 KEVI_DLL
    if let Some(env) = std::env::var_os("KEVI_DLL") {
        let p = std::path::PathBuf::from(&env);
        if p.is_file() {
            return Some(env);
        }
    }
    // 2. ペイロード埋め込みあり → AppHost 互換の展開先
    if extract::has_payload() {
        match extract::extract_or_reuse() {
            Ok(dir) => {
                let candidate = dir.join("KyoshinEewViewer.Desktop.dll");
                if candidate.is_file() {
                    return Some(candidate.into_os_string());
                }
                log_line(&format!(
                    "payload extracted but dll missing: {}",
                    candidate.display()
                ));
            }
            Err(e) => {
                log_line(&format!("payload extract failed: {}", e));
            }
        }
    }
    // 4. launcher.exe と同ディレクトリ
    if let Ok(exe) = std::env::current_exe() {
        if let Some(dir) = exe.parent() {
            let candidate = dir.join("KyoshinEewViewer.Desktop.dll");
            if candidate.is_file() {
                return Some(candidate.into_os_string());
            }
        }
    }
    None
}

fn launch_dotnet(
    dll_path: OsString,
    args: Vec<OsString>,
    clock_freq: i64,
    clock_start: i64,
) {
    let clock = PerfClock {
        freq: clock_freq,
        start: clock_start,
    };
    let t_start = clock.elapsed_ms();
    log_line(&format!("T_clr_start={:.2}ms args={}", t_start, args.len()));

    let result = (|| -> Result<i32, Box<dyn std::error::Error>> {
        // self-contained アプリの場合は dll と同じディレクトリの hostfxr.dll を使う。
        // 見つからない場合のみシステムインストールの hostfxr にフォールバック。
        let hostfxr = match Path::new(&dll_path).parent().map(|d| d.join("hostfxr.dll")) {
            Some(p) if p.is_file() => Hostfxr::load_from_path(p)?,
            _ => nethost::load_hostfxr()?,
        };
        let dll_pdc = PdCString::from_os_str(&dll_path)?;
        let arg_pdcs: Vec<PdCString> = args
            .iter()
            .map(|a| PdCString::from_os_str(a))
            .collect::<Result<Vec<_>, _>>()?;
        let context = hostfxr
            .initialize_for_dotnet_command_line_with_args(&dll_pdc, arg_pdcs.iter())?;
        Ok(context.run_app().value())
    })();

    let t_done = clock.elapsed_ms();
    match result {
        Ok(code) => log_line(&format!("T_clr_done={:.2}ms exit={}", t_done, code)),
        Err(e) => {
            log_line(&format!("T_clr_done={:.2}ms ERROR: {}", t_done, e));
            show_error_box(&format!(
                ".NET ランタイムの起動に失敗しました。\n\n詳細:\n{}\n\n.NET 10 ランタイムがインストールされているか、KyoshinEewViewer.Desktop.dll が同梱されているか確認してください。",
                e
            ));
        }
    }
}

fn main() {
    let clock = PerfClock::new();
    let t0 = clock.elapsed_ms();

    // launcher へのユーザー引数 (args_os の最初は exe 自身のパスなので skip(1))
    let user_args: Vec<OsString> = std::env::args_os().skip(1).collect();
    // ユーザーが --no-logo を指定したかどうかで Rust splash の有無を決める
    let splash_disabled = is_splash_disabled(&user_args);

    // KEVi へ渡す引数: Avalonia 側 splash は常に抑制する (Rust splash が代替し、
    // KEVi の MainWindow 表示時に LauncherBridge 経由で Rust splash を閉じる)。
    // ユーザーが既に --no-logo を付けていれば追加しない。
    let mut forwarded_args = user_args.clone();
    if !splash_disabled {
        forwarded_args.push(OsString::from("--no-logo"));
    }

    // 名前付きイベント作成 (auto-reset, not signaled)
    let event_name = format!("KeviLauncherSplashClose_{}", std::process::id());
    let event_name_w = to_wide(&event_name);
    let event: HANDLE = unsafe { CreateEventW(null(), 0, 0, event_name_w.as_ptr()) };
    if event.is_null() {
        log_line("CreateEventW failed");
        return;
    }

    // 環境変数を .NET 側に渡す (子プロセスではなく同一プロセスなので Environment.GetEnvironmentVariable で取れる)
    unsafe {
        std::env::set_var("KEVI_SPLASH_EVENT", &event_name);
    }

    // splash 表示 (--no-logo 指定時はスキップ。失敗しても .NET 起動は続行する)
    let splash_handle: Option<splash::SplashHandle> = if splash_disabled {
        log_line("splash skipped (--no-logo)");
        None
    } else {
        match splash::show_splash(&clock, t0) {
            Ok(h) => {
                log_line(&format!(
                    "T0={:.2}ms T_class={:.2}ms T_decode={:.2}ms T_win={:.2}ms T_paint={:.2}ms event={}",
                    h.timings.t0,
                    h.timings.t_class,
                    h.timings.t_decode,
                    h.timings.t_win,
                    h.timings.t_paint,
                    event_name
                ));
                Some(h)
            }
            Err(e) => {
                log_line(&format!("splash error (continue without splash): {}", e));
                None
            }
        }
    };

    // .NET dll パスを以下の優先順で解決:
    //   1. 環境変数 KEVI_DLL (開発用)
    //   2. 埋め込みペイロードの展開先 (AppHost 互換)
    //   3. launcher.exe と同ディレクトリの KyoshinEewViewer.Desktop.dll
    let dll_path = resolve_dll_path();
    let dll_path = match dll_path {
        Some(p) => p,
        None => {
            log_line("KyoshinEewViewer.Desktop.dll not found");
            if let Some(h) = &splash_handle {
                unsafe { DestroyWindow(h.hwnd) };
            }
            show_error_box(
                "KyoshinEewViewer.Desktop.dll が見つかりません。\n\n本体一式と一緒に配置されているか確認してください。",
            );
            return;
        }
    };
    log_line(&format!("dll={}", dll_path.to_string_lossy()));

    // .NET を別スレッドで起動。完了 (run_app から戻り) を AtomicBool で共有し、
    // splash 終了通知が来ない早期 shutdown 等でもメインスレッドが固まらないようにする。
    let freq = clock.freq;
    let start = clock.start;
    let dll_for_thread = dll_path.clone();
    let args_for_thread = forwarded_args.clone();
    let dotnet_done = Arc::new(AtomicBool::new(false));
    let dotnet_done_thread = dotnet_done.clone();
    let dotnet_handle = thread::spawn(move || {
        launch_dotnet(dll_for_thread, args_for_thread, freq, start);
        dotnet_done_thread.store(true, Ordering::SeqCst);
    });

    // splash がある場合のみメインスレッドでメッセージループ + event 監視。
    // splash 無し (--no-logo 等) の場合は待たずに .NET の完了を待つだけ。
    if splash_handle.is_some() {
        // フェイルセーフ: 通知漏れ時でも一定時間で splash を閉じる
        const FAILSAFE_TIMEOUT_MS: f64 = 60_000.0;
        let loop_start = clock.elapsed_ms();
        unsafe {
            loop {
                // 250ms ごとに起床し、.NET 完了・タイムアウトを確認する
                let wait_result =
                    MsgWaitForMultipleObjects(1, &raw const event, 0, 250, QS_ALLINPUT);
                if wait_result == WAIT_OBJECT_0 {
                    // event signaled: .NET から splash を閉じる通知
                    let t_close = clock.elapsed_ms();
                    log_line(&format!("T_splash_close={:.2}ms (signaled)", t_close));
                    if let Some(h) = &splash_handle {
                        DestroyWindow(h.hwnd);
                    }
                    break;
                } else if wait_result == WAIT_OBJECT_0 + 1 {
                    // メッセージあり: dispatch
                    let mut msg: MSG = zeroed();
                    let mut had_quit = false;
                    while PeekMessageW(&raw mut msg, null_mut(), 0, 0, PM_REMOVE) != 0 {
                        if msg.message == WM_QUIT {
                            had_quit = true;
                            break;
                        }
                        TranslateMessage(&raw const msg);
                        DispatchMessageW(&raw const msg);
                    }
                    if had_quit {
                        break;
                    }
                } else if wait_result == WAIT_TIMEOUT {
                    // .NET が通知なしで終了した場合 (早期 shutdown 等)
                    if dotnet_done.load(Ordering::SeqCst) {
                        log_line("splash closed (dotnet thread finished without signal)");
                        if let Some(h) = &splash_handle {
                            DestroyWindow(h.hwnd);
                        }
                        break;
                    }
                    // フェイルセーフのタイムアウト
                    if clock.elapsed_ms() - loop_start > FAILSAFE_TIMEOUT_MS {
                        log_line("splash closed (failsafe timeout)");
                        if let Some(h) = &splash_handle {
                            DestroyWindow(h.hwnd);
                        }
                        break;
                    }
                } else if wait_result == WAIT_FAILED {
                    log_line("MsgWaitForMultipleObjects failed");
                    break;
                }
            }
        }
    }

    unsafe {
        CloseHandle(event);
    }

    // .NET スレッド完了待ち (KEVi の MainWindow が閉じられるまで)
    let _ = dotnet_handle.join();
    log_line("launcher exit");
}
