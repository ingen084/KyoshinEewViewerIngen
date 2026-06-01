// AppHost SingleFile 互換の payload 展開ロジック。
//
// 展開先:
//   $DOTNET_BUNDLE_EXTRACT_BASE_DIR/<exe-name>/<bundle-id>/
//   未設定なら Windows では %TEMP%\.net\
//
// 2 フェーズ展開:
//   1) <exe-name>/<pid hex>/ に展開
//   2) 全部完了したら atomic rename で final へ
//   3) リネーム失敗時 (別プロセスが既に commit 済み) は working を削除して終了

use std::fs;
use std::io::{Cursor, Read};
use std::path::{Path, PathBuf};
use std::time::Duration;

// build.rs が埋め込んだ ZIP と bundle id (.NET 互換の Base64Url 12 文字)。
// payload なしでビルドした場合は bundle id が空文字になる。
pub const PAYLOAD_ZIP: &[u8] = include_bytes!(env!("KEVI_PAYLOAD_ZIP"));
pub const PAYLOAD_HASH: &str = env!("KEVI_PAYLOAD_HASH");

pub fn has_payload() -> bool {
    !PAYLOAD_HASH.is_empty() && !PAYLOAD_ZIP.is_empty()
}

pub fn extract_or_reuse() -> std::io::Result<PathBuf> {
    let exe_name = std::env::current_exe()
        .ok()
        .and_then(|p| p.file_stem().map(|s| s.to_os_string()))
        .unwrap_or_else(|| "KyoshinEewViewer".into());

    let base = std::env::var_os("DOTNET_BUNDLE_EXTRACT_BASE_DIR")
        .map(PathBuf::from)
        .unwrap_or_else(default_extract_base);

    let app_dir = base.join(&exe_name);
    let final_dir = app_dir.join(PAYLOAD_HASH);

    // final_dir は working_dir からの atomic rename でのみ作られるため、
    // 存在していれば展開は完了している。通常起動 (最頻ケース) を高速化するため、
    // 全ファイルの照合はせず主要 dll の存在だけを O(1) で確認する。
    if final_dir.join("KyoshinEewViewer.Desktop.dll").is_file() {
        return Ok(final_dir);
    }
    // ディレクトリはあるが主要 dll が無い = 不完全/破損 → 作り直す
    if final_dir.exists() {
        let _ = fs::remove_dir_all(&final_dir);
    }

    // working_dir = app_dir/<pid hex>
    let pid_hex = format!("{:x}", std::process::id());
    let working_dir = app_dir.join(pid_hex);
    if working_dir.exists() {
        let _ = fs::remove_dir_all(&working_dir);
    }
    fs::create_dir_all(&working_dir)?;

    extract_zip_to(&working_dir)?;

    // atomic rename with retry
    match rename_with_retries(&working_dir, &final_dir) {
        Ok(()) => Ok(final_dir),
        Err(e) => {
            // 別プロセスが先に commit していれば final が存在
            if final_dir.is_dir() {
                let _ = fs::remove_dir_all(&working_dir);
                Ok(final_dir)
            } else {
                Err(e)
            }
        }
    }
}

#[cfg(windows)]
fn default_extract_base() -> PathBuf {
    std::env::temp_dir().join(".net")
}

#[cfg(not(windows))]
fn default_extract_base() -> PathBuf {
    std::env::temp_dir().join(".net")
}

// ZIP (Zstd 圧縮) を複数スレッドで並列展開する。
// PAYLOAD_ZIP は 'static なので各スレッドが独自に ZipArchive を開き、
// AtomicUsize でエントリ番号を取り合う work-stealing で負荷を均一化する。
// 解凍 (CPU) とファイル書き込み (I/O) の双方が並列化される。
fn extract_zip_to(dir: &Path) -> std::io::Result<()> {
    use std::sync::atomic::{AtomicUsize, Ordering};

    let len = {
        let cursor = Cursor::new(PAYLOAD_ZIP);
        zip::ZipArchive::new(cursor)
            .map_err(|e| std::io::Error::new(std::io::ErrorKind::Other, format!("zip: {}", e)))?
            .len()
    };
    if len == 0 {
        return Ok(());
    }

    let n_threads = std::thread::available_parallelism()
        .map(|n| n.get())
        .unwrap_or(4)
        .min(8)
        .min(len);

    let next = AtomicUsize::new(0);
    std::thread::scope(|s| -> std::io::Result<()> {
        let mut handles = Vec::with_capacity(n_threads);
        for _ in 0..n_threads {
            let next = &next;
            handles.push(s.spawn(move || -> std::io::Result<()> {
                let cursor = Cursor::new(PAYLOAD_ZIP);
                let mut archive = zip::ZipArchive::new(cursor).map_err(|e| {
                    std::io::Error::new(std::io::ErrorKind::Other, format!("zip: {}", e))
                })?;
                loop {
                    let i = next.fetch_add(1, Ordering::Relaxed);
                    if i >= len {
                        break;
                    }
                    extract_entry(&mut archive, i, dir)?;
                }
                Ok(())
            }));
        }
        for h in handles {
            h.join().map_err(|_| {
                std::io::Error::new(std::io::ErrorKind::Other, "展開スレッドが異常終了しました")
            })??;
        }
        Ok(())
    })
}

fn extract_entry(
    archive: &mut zip::ZipArchive<Cursor<&'static [u8]>>,
    i: usize,
    dir: &Path,
) -> std::io::Result<()> {
    let mut file = archive
        .by_index(i)
        .map_err(|e| std::io::Error::new(std::io::ErrorKind::Other, format!("zip entry: {}", e)))?;
    let rel = file
        .enclosed_name()
        .ok_or_else(|| std::io::Error::new(std::io::ErrorKind::Other, "unsafe path"))?;
    let out_path = dir.join(rel);
    if file.is_dir() {
        fs::create_dir_all(&out_path)?;
        return Ok(());
    }
    if let Some(parent) = out_path.parent() {
        fs::create_dir_all(parent)?;
    }
    let mut buf = Vec::with_capacity(file.size() as usize);
    file.read_to_end(&mut buf)?;
    fs::write(&out_path, buf)?;
    Ok(())
}

fn rename_with_retries(src: &Path, dst: &Path) -> std::io::Result<()> {
    let mut last_err = None;
    for attempt in 0..10 {
        match fs::rename(src, dst) {
            Ok(()) => return Ok(()),
            Err(e) => {
                if dst.is_dir() {
                    // 別プロセスが先に commit
                    return Err(std::io::Error::new(
                        std::io::ErrorKind::AlreadyExists,
                        "destination exists",
                    ));
                }
                last_err = Some(e);
                std::thread::sleep(Duration::from_millis(100 * (attempt + 1)));
            }
        }
    }
    Err(last_err.unwrap_or_else(|| std::io::Error::new(std::io::ErrorKind::Other, "rename failed")))
}
