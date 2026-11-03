//! Dev only: runs the gallery's own upload check on a Share zip, as an upload would, and prints one JSON line. Used by
//! `Tools/share_check.py` to judge the zip the editor's Share my game made.
//!
//! `cargo run --example check_share_zip -- <zip> [--expect-title-file <utf8 file>]`
//!
//! With `--expect-title-file`, the raw title the editor wrote into `daedalus.json` must equal `clean_label` of the file's
//! text: the editor's port of the title cleaning must match the gallery's exactly, not just be repaired by it.

use std::fs;
use std::path::Path;
use std::process::ExitCode;

use gallery::config::Config;
use gallery::share_zip::{self, Limits};
use gallery::text::clean_label;
use serde_json::{Value, json};

fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().skip(1).collect();
    let (zip, expect) = match args.as_slice() {
        [zip] => (zip, None),
        [zip, flag, file] if flag == "--expect-title-file" => (zip, Some(file)),
        _ => {
            eprintln!("usage: check_share_zip <zip> [--expect-title-file <utf8 file>]");
            return ExitCode::FAILURE;
        }
    };

    let temp = tempfile::tempdir().expect("temp dir");
    let config = Config::new(temp.path());
    let zip_bytes = match fs::metadata(zip) {
        Ok(meta) => meta.len(),
        Err(error) => return fail(json!({"ok": false, "reject": "missing", "message": error.to_string()})),
    };
    // The upload's own cap (upload.rs), on the zip's bytes.
    if zip_bytes > config.max_upload_bytes {
        let message = share_zip::Reject::TooBig.message();
        return fail(json!({"ok": false, "reject": "too-big", "message": message, "zip_bytes": zip_bytes}));
    }

    let staging = temp.path().join("staging");
    let unpacked = match share_zip::check_and_unpack(Path::new(zip), &staging, &Limits::from_config(&config)) {
        Ok(unpacked) => unpacked,
        Err(reject) => return fail(json!({"ok": false, "reject": reject.code(), "message": reject.message()})),
    };

    if let Some(file) = expect {
        let raw_input = fs::read_to_string(file).expect("title file is UTF-8");
        let expected = clean_label(&raw_input, config.title_max_chars);
        let meta: Value = serde_json::from_slice(&fs::read(staging.join("daedalus.json")).expect("staged daedalus.json"))
            .expect("checked JSON");
        let written = meta["title"].as_str().map(str::to_owned);
        if written != expected || written.as_deref() != Some(unpacked.title.as_str()) {
            return fail(json!({
                "ok": false,
                "reject": "title-mismatch",
                "editor": written.as_deref().map(code_points),
                "gallery_clean_label": expected.as_deref().map(code_points),
                "gallery_card": code_points(&unpacked.title),
            }));
        }
    }

    println!(
        "{}",
        json!({"ok": true, "title": unpacked.title, "build_name": unpacked.build_name, "bytes": unpacked.bytes, "zip_bytes": zip_bytes})
    );
    ExitCode::SUCCESS
}

fn fail(report: Value) -> ExitCode {
    println!("{report}");
    ExitCode::FAILURE
}

/// The text with its code points, so a mismatch shows invisible characters.
fn code_points(text: &str) -> String {
    let points: Vec<String> = text.chars().map(|c| format!("U+{:04X}", c as u32)).collect();
    format!("{text} [{}]", points.join(" "))
}
