// 環境変数 KEVI_PAYLOAD_DIR で指定された publish 出力を ZIP 圧縮し、
// その SHA-256 と相対パスを cargo:rustc-env で main.rs に渡す。
//
// KEVI_PAYLOAD_DIR が未指定の場合は空の ZIP を生成し、launcher は
// 外部 dll パスで動作するフォールバックモードになる。

use std::fs;
use std::io::Write;
use std::path::{Path, PathBuf};

use base64::Engine;
use sha2::Digest;
use zip::write::SimpleFileOptions;

// .NET の single-file bundle id と同じ形式: SHA-256 を Base64Url エンコードし先頭 12 文字。
const BUNDLE_ID_LENGTH: usize = 12;

fn main() {
    println!("cargo:rerun-if-env-changed=KEVI_PAYLOAD_DIR");

    // Windows ビルド時にアイコン・バージョン情報を埋め込み
    #[cfg(windows)]
    {
        let ico = std::path::Path::new("../KyoshinEewViewer.Desktop/Assets/logo.ico");
        if ico.is_file() {
            println!("cargo:rerun-if-changed={}", ico.display());
            let mut res = winresource::WindowsResource::new();
            res.set_icon(ico.to_str().unwrap());
            res.set("ProductName", "KyoshinEewViewer for ingen");
            res.set("FileDescription", "KyoshinEewViewer for ingen ランチャー");
            res.set("CompanyName", "ingen software");
            res.set("OriginalFilename", "KyoshinEewViewer.exe");
            res.set("InternalName", "KyoshinEewViewer");
            res.set("LegalCopyright", "Copyright (c) ingen software");
            if let Err(e) = res.compile() {
                println!("cargo:warning=winresource compile failed: {}", e);
            }
        }
    }

    let out_dir = PathBuf::from(std::env::var_os("OUT_DIR").expect("OUT_DIR"));
    let zip_path = out_dir.join("kevi-payload.zip");

    let payload_dir = std::env::var_os("KEVI_PAYLOAD_DIR").map(PathBuf::from);

    let bundle_id = match payload_dir {
        Some(dir) if dir.is_dir() => {
            println!("cargo:rerun-if-changed={}", dir.display());
            create_zip(&dir, &zip_path).expect("failed to create payload zip")
        }
        _ => {
            // 空の zip を作る (launcher は外部 dll で動作)。bundle id は空にする
            create_empty_zip(&zip_path).expect("failed to create empty zip");
            String::new()
        }
    };

    println!(
        "cargo:rustc-env=KEVI_PAYLOAD_ZIP={}",
        zip_path.display().to_string().replace('\\', "/")
    );
    println!("cargo:rustc-env=KEVI_PAYLOAD_HASH={}", bundle_id);
}

fn create_zip(payload_dir: &Path, zip_path: &Path) -> std::io::Result<String> {
    let file = fs::File::create(zip_path)?;
    let mut zip = zip::ZipWriter::new(file);
    // Zstd は Deflate より解凍が高速 (起動時の展開コスト削減)。圧縮率もほぼ同等。
    let options = SimpleFileOptions::default().compression_method(zip::CompressionMethod::Zstd);

    let mut hasher = sha2::Sha256::new();
    let mut entries: Vec<PathBuf> = walkdir::WalkDir::new(payload_dir)
        .into_iter()
        .filter_map(|e| e.ok())
        .filter(|e| e.file_type().is_file())
        .map(|e| e.into_path())
        .collect();
    entries.sort(); // 決定的ハッシュのため

    for path in &entries {
        let rel = path.strip_prefix(payload_dir).unwrap();
        let rel_str = rel.to_string_lossy().replace('\\', "/");
        hasher.update(rel_str.as_bytes());
        let bytes = fs::read(path)?;
        hasher.update(&bytes);
        zip.start_file(&rel_str, options)?;
        zip.write_all(&bytes)?;
    }
    zip.finish()?;

    // .NET の bundle id と同じ形式: SHA-256 を Base64Url エンコードし先頭 12 文字
    let digest = hasher.finalize();
    let b64 = base64::engine::general_purpose::URL_SAFE_NO_PAD.encode(digest);
    Ok(b64[..BUNDLE_ID_LENGTH].to_string())
}

fn create_empty_zip(zip_path: &Path) -> std::io::Result<()> {
    let file = fs::File::create(zip_path)?;
    let zip = zip::ZipWriter::new(file);
    zip.finish()?;
    Ok(())
}
