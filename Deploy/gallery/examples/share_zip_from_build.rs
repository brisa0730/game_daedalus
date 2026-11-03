//! Dev only: writes a Share zip (platforms.md § Share zip) from a Unity WebGL Brotli build folder, for checking the
//! gallery with a real build before the editor's Share my game exists.
//!
//! `cargo run --example share_zip_from_build -- <build dir> <out.zip> <title> <screenshot.png>`

use std::fs;
use std::io::Write;
use std::path::Path;

use zip::write::SimpleFileOptions;
use zip::{CompressionMethod, ZipWriter};

fn main() -> Result<(), String> {
    let args: Vec<String> = std::env::args().skip(1).collect();
    let [build, out, title, screenshot] = args.as_slice() else {
        return Err("usage: share_zip_from_build <build dir> <out.zip> <title> <screenshot.png>".into());
    };
    let build_dir = Path::new(build).join("Build");
    let loader = fs::read_dir(&build_dir)
        .map_err(|e| format!("{}: {e}", build_dir.display()))?
        .filter_map(|e| e.ok())
        .map(|e| e.file_name().to_string_lossy().into_owned())
        .find(|n| n.ends_with(".loader.js"))
        .ok_or("no *.loader.js in Build/")?;
    let name = loader.trim_end_matches(".loader.js");
    let mut zip = ZipWriter::new(fs::File::create(out).map_err(|e| format!("{out}: {e}"))?);
    // The .br files are already compressed: store them; deflate the rest.
    let stored = SimpleFileOptions::default().compression_method(CompressionMethod::Stored);
    let deflated = SimpleFileOptions::default().compression_method(CompressionMethod::Deflated);
    let meta = serde_json::json!({"format": 1, "title": title}).to_string();
    let entries: Vec<(String, Vec<u8>, SimpleFileOptions)> = vec![
        ("daedalus.json".into(), meta.into_bytes(), deflated),
        ("screenshot.png".into(), fs::read(screenshot).map_err(|e| format!("{screenshot}: {e}"))?, stored),
        (format!("Build/{name}.loader.js"), read(&build_dir, &format!("{name}.loader.js"))?, deflated),
        (format!("Build/{name}.data.br"), read(&build_dir, &format!("{name}.data.br"))?, stored),
        (format!("Build/{name}.framework.js.br"), read(&build_dir, &format!("{name}.framework.js.br"))?, stored),
        (format!("Build/{name}.wasm.br"), read(&build_dir, &format!("{name}.wasm.br"))?, stored),
    ];
    for (path, bytes, options) in entries {
        zip.start_file(&path, options).map_err(|e| e.to_string())?;
        zip.write_all(&bytes).map_err(|e| e.to_string())?;
    }
    zip.finish().map_err(|e| e.to_string())?;
    println!("wrote {out} (build name {name})");
    Ok(())
}

fn read(dir: &Path, file: &str) -> Result<Vec<u8>, String> {
    fs::read(dir.join(file)).map_err(|e| format!("{file}: {e}"))
}
