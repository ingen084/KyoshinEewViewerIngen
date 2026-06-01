// PNG をリソース埋め込みし、png crate でデコード後、UpdateLayeredWindow で表示する。
// GDI+ を排除しコードを単純化。

use std::mem::{size_of, zeroed};
use std::ptr::{null, null_mut};

use windows_sys::Win32::Foundation::{GetLastError, HINSTANCE, HWND, LPARAM, LRESULT, POINT, SIZE, WPARAM};
use windows_sys::Win32::Graphics::Gdi::{
    AC_SRC_ALPHA, AC_SRC_OVER, BITMAPINFO, BITMAPINFOHEADER, BLENDFUNCTION, CreateCompatibleDC,
    CreateDIBSection, DIB_RGB_COLORS, DeleteDC, DeleteObject, GetDC, HGDIOBJ, ReleaseDC,
    SelectObject,
};
use windows_sys::Win32::System::LibraryLoader::GetModuleHandleW;
use windows_sys::Win32::UI::WindowsAndMessaging::*;

use crate::PerfClock;

// splash.png を exe にリソース埋め込み
const SPLASH_PNG: &[u8] = include_bytes!("../../KyoshinEewViewer.Desktop/Assets/splash.png");

const WINDOW_CLASS: &[u16] = &[
    'K' as u16, 'e' as u16, 'v' as u16, 'i' as u16, 'L' as u16, 'a' as u16, 'u' as u16, 'n' as u16,
    'c' as u16, 'h' as u16, 'P' as u16, 'o' as u16, 'C' as u16, 0,
];
const WINDOW_TITLE: &[u16] = &['起' as u16, '動' as u16, '中' as u16, 0];

pub struct Timings {
    pub t0: f64,
    pub t_class: f64,
    pub t_decode: f64,
    pub t_win: f64,
    pub t_paint: f64,
}

pub struct SplashHandle {
    pub hwnd: HWND,
    pub timings: Timings,
}

unsafe impl Send for SplashHandle {}
unsafe impl Sync for SplashHandle {}

unsafe extern "system" fn wnd_proc(
    hwnd: HWND,
    msg: u32,
    wparam: WPARAM,
    lparam: LPARAM,
) -> LRESULT {
    unsafe {
        match msg {
            WM_DESTROY => {
                PostQuitMessage(0);
                0
            }
            _ => DefWindowProcW(hwnd, msg, wparam, lparam),
        }
    }
}

fn decode_png_to_bgra_premul() -> Result<(u32, u32, Vec<u8>), String> {
    let decoder = png::Decoder::new(std::io::Cursor::new(SPLASH_PNG));
    let mut reader = decoder.read_info().map_err(|e| format!("png read_info: {}", e))?;
    let info = reader.info();
    let width = info.width;
    let height = info.height;
    let color_type = info.color_type;
    let bit_depth = info.bit_depth;

    // 8bpc 想定
    if bit_depth != png::BitDepth::Eight {
        return Err(format!("unsupported bit depth: {:?}", bit_depth));
    }

    let mut src = vec![0u8; reader.output_buffer_size().unwrap_or(0)];
    let frame = reader.next_frame(&mut src).map_err(|e| format!("png next_frame: {}", e))?;
    let src_len = frame.buffer_size();
    let src = &src[..src_len];

    let mut dst = vec![0u8; (width * height * 4) as usize];

    match color_type {
        png::ColorType::Rgba => {
            // RGBA -> BGRA premultiplied
            for i in 0..(width * height) as usize {
                let r = src[i * 4];
                let g = src[i * 4 + 1];
                let b = src[i * 4 + 2];
                let a = src[i * 4 + 3];
                dst[i * 4] = ((b as u16 * a as u16 + 127) / 255) as u8;
                dst[i * 4 + 1] = ((g as u16 * a as u16 + 127) / 255) as u8;
                dst[i * 4 + 2] = ((r as u16 * a as u16 + 127) / 255) as u8;
                dst[i * 4 + 3] = a;
            }
        }
        png::ColorType::Rgb => {
            // RGB -> BGRA (α=255)
            for i in 0..(width * height) as usize {
                dst[i * 4] = src[i * 3 + 2];
                dst[i * 4 + 1] = src[i * 3 + 1];
                dst[i * 4 + 2] = src[i * 3];
                dst[i * 4 + 3] = 255;
            }
        }
        _ => {
            return Err(format!("unsupported color type: {:?}", color_type));
        }
    }

    Ok((width, height, dst))
}

pub fn show_splash(clock: &PerfClock, t0: f64) -> Result<SplashHandle, String> {
    unsafe {
        let hinstance = GetModuleHandleW(null());

        let wc = WNDCLASSEXW {
            cbSize: size_of::<WNDCLASSEXW>() as u32,
            style: 0,
            lpfnWndProc: Some(wnd_proc),
            cbClsExtra: 0,
            cbWndExtra: 0,
            hInstance: hinstance as HINSTANCE,
            hIcon: null_mut(),
            hCursor: LoadCursorW(null_mut(), IDC_ARROW),
            hbrBackground: null_mut(),
            lpszMenuName: null(),
            lpszClassName: WINDOW_CLASS.as_ptr(),
            hIconSm: null_mut(),
        };
        RegisterClassExW(&wc);
        let t_class = clock.elapsed_ms();

        // PNG デコード (BGRA premultiplied)
        let (width, height, pixels) = decode_png_to_bgra_premul()?;
        let t_decode = clock.elapsed_ms();

        // DIB 作成 (top-down)
        let bmi = BITMAPINFO {
            bmiHeader: BITMAPINFOHEADER {
                biSize: size_of::<BITMAPINFOHEADER>() as u32,
                biWidth: width as i32,
                biHeight: -(height as i32),
                biPlanes: 1,
                biBitCount: 32,
                biCompression: 0, // BI_RGB
                biSizeImage: 0,
                biXPelsPerMeter: 0,
                biYPelsPerMeter: 0,
                biClrUsed: 0,
                biClrImportant: 0,
            },
            bmiColors: [zeroed()],
        };
        let screen_dc = GetDC(null_mut());
        let mem_dc = CreateCompatibleDC(screen_dc);
        let mut dib_bits: *mut std::ffi::c_void = null_mut();
        let dib = CreateDIBSection(
            screen_dc,
            &raw const bmi,
            DIB_RGB_COLORS,
            &raw mut dib_bits,
            null_mut(),
            0,
        );
        if dib.is_null() || dib_bits.is_null() {
            return Err("CreateDIBSection failed".to_string());
        }

        std::ptr::copy_nonoverlapping(pixels.as_ptr(), dib_bits as *mut u8, pixels.len());

        let old_bitmap = SelectObject(mem_dc, dib as HGDIOBJ);

        // スクリーン中央
        let sw = GetSystemMetrics(SM_CXSCREEN);
        let sh = GetSystemMetrics(SM_CYSCREEN);
        let x = (sw - width as i32) / 2;
        let y = (sh - height as i32) / 2;

        let hwnd = CreateWindowExW(
            WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
            WINDOW_CLASS.as_ptr(),
            WINDOW_TITLE.as_ptr(),
            WS_POPUP,
            x,
            y,
            width as i32,
            height as i32,
            null_mut(),
            null_mut(),
            hinstance as HINSTANCE,
            null_mut(),
        );
        if hwnd.is_null() {
            return Err("CreateWindowExW failed".to_string());
        }
        let t_win = clock.elapsed_ms();

        let mut pt_src = POINT { x: 0, y: 0 };
        let mut pt_dst = POINT { x, y };
        let mut sz_wnd = SIZE { cx: width as i32, cy: height as i32 };
        let blend = BLENDFUNCTION {
            BlendOp: AC_SRC_OVER as u8,
            BlendFlags: 0,
            SourceConstantAlpha: 255,
            AlphaFormat: AC_SRC_ALPHA as u8,
        };
        let ok = UpdateLayeredWindow(
            hwnd,
            screen_dc,
            &raw mut pt_dst,
            &raw mut sz_wnd,
            mem_dc,
            &raw mut pt_src,
            0,
            &raw const blend,
            ULW_ALPHA,
        );
        if ok == 0 {
            return Err(format!("UpdateLayeredWindow failed: {}", GetLastError()));
        }

        ShowWindow(hwnd, SW_SHOWNOACTIVATE);
        let t_paint = clock.elapsed_ms();

        // UpdateLayeredWindow がピクセルをコピー済みなので GDI リソースは解放してよい。
        // ランチャーは .NET 本体と同一プロセスで常駐するため、明示的に回収する。
        SelectObject(mem_dc, old_bitmap);
        DeleteObject(dib as *mut std::ffi::c_void);
        DeleteDC(mem_dc);
        ReleaseDC(null_mut(), screen_dc);

        Ok(SplashHandle {
            hwnd,
            timings: Timings {
                t0,
                t_class,
                t_decode,
                t_win,
                t_paint,
            },
        })
    }
}
