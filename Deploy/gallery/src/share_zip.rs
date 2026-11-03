//! The Share zip: the one file Daedalus → Share my game makes and the gallery accepts (contract: platforms.md
//! § Share zip). This checks an uploaded zip and unpacks exactly its six files, or says in one Korean sentence what
//! the student should do instead. Pure of HTTP; run it off the async threads.
//!
//! At the zip's root: `daedalus.json` (`{"format": 1, "title": "…"}`), `screenshot.png`, and
//! `Build/<name>.loader.js`, `Build/<name>.data.br`, `Build/<name>.framework.js.br`, `Build/<name>.wasm.br`.
//! Anything else is ignored, never unpacked. Any unsafe entry name rejects the whole zip.

use std::collections::HashSet;
use std::fs::{self, File, OpenOptions};
use std::io::{self, Read, Write};
use std::path::{Path, PathBuf};

use zip::CompressionMethod;
use zip::ZipArchive;

use crate::config::Config;
use crate::strings as s;
use crate::text::clean_label;

pub const FORMAT: i64 = 1;
const PNG_SIGNATURE: &[u8] = b"\x89PNG\r\n\x1a\n";

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Reject {
    NotZip,
    Unsafe,
    Project,
    Screenshots,
    Wrapped,
    NotDaedalus,
    BuildSettings,
    Format,
    Reshare,
    TooBig,
}

impl Reject {
    pub fn message(self) -> &'static str {
        match self {
            Reject::NotZip => s::REJECT_NOT_ZIP,
            Reject::Unsafe => s::REJECT_UNSAFE,
            Reject::Project => s::REJECT_PROJECT,
            Reject::Screenshots => s::REJECT_SCREENSHOTS,
            Reject::Wrapped => s::REJECT_WRAPPED,
            Reject::NotDaedalus => s::REJECT_NOT_DAEDALUS,
            Reject::BuildSettings => s::REJECT_BUILD_SETTINGS,
            Reject::Format => s::REJECT_FORMAT,
            Reject::Reshare => s::REJECT_RESHARE,
            Reject::TooBig => s::REJECT_TOO_BIG,
        }
    }

    /// For the log.
    pub fn code(self) -> &'static str {
        match self {
            Reject::NotZip => "not-zip",
            Reject::Unsafe => "unsafe",
            Reject::Project => "project",
            Reject::Screenshots => "screenshots",
            Reject::Wrapped => "wrapped",
            Reject::NotDaedalus => "not-daedalus",
            Reject::BuildSettings => "build-settings",
            Reject::Format => "format",
            Reject::Reshare => "reshare",
            Reject::TooBig => "too-big",
        }
    }
}

#[derive(Clone, Debug)]
pub struct Limits {
    pub entries: usize,
    pub unpacked_bytes: u64,
    pub screenshot_bytes: u64,
    pub screenshot_width: u32,
    pub screenshot_height: u32,
    pub metadata_bytes: u64,
    pub title_chars: usize,
}

impl Limits {
    pub fn from_config(config: &Config) -> Limits {
        Limits {
            entries: config.max_zip_entries,
            unpacked_bytes: config.max_unpacked_bytes,
            screenshot_bytes: config.max_screenshot_bytes,
            screenshot_width: config.max_screenshot_width,
            screenshot_height: config.max_screenshot_height,
            metadata_bytes: config.max_metadata_bytes,
            title_chars: config.title_max_chars,
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Unpacked {
    pub title: String,
    pub build_name: String,
    /// Bytes actually written.
    pub bytes: u64,
}

/// The four build files' names for `<name>`, in the order loader, data, framework, code.
pub fn build_files(name: &str) -> [String; 4] {
    [format!("{name}.loader.js"), format!("{name}.data.br"), format!("{name}.framework.js.br"), format!("{name}.wasm.br")]
}

struct EntryInfo {
    index: usize,
    name: String,
    size: u64,
}

/// Checks the zip at `zip_path` and unpacks its six files into `staging` (which must not exist yet). On any
/// rejection nothing is left in `staging`.
pub fn check_and_unpack(zip_path: &Path, staging: &Path, limits: &Limits) -> Result<Unpacked, Reject> {
    let result = check_and_unpack_inner(zip_path, staging, limits);
    if result.is_err() {
        let _ = fs::remove_dir_all(staging);
    }
    result
}

fn check_and_unpack_inner(zip_path: &Path, staging: &Path, limits: &Limits) -> Result<Unpacked, Reject> {
    // 1. A zip at all.
    let mut file = File::open(zip_path).map_err(|_| Reject::NotZip)?;
    let mut magic = [0u8; 2];
    if file.read_exact(&mut magic).is_err() || &magic != b"PK" {
        return Err(Reject::NotZip);
    }
    let mut archive = ZipArchive::new(File::open(zip_path).map_err(|_| Reject::NotZip)?).map_err(|_| Reject::NotZip)?;

    // 2. Entry count, duplicates, unsafe names, links, compression.
    if archive.len() > limits.entries || archive.has_duplicate_names() {
        return Err(Reject::Unsafe);
    }
    let mut files = Vec::new();
    for index in 0..archive.len() {
        let entry = archive.by_index_raw(index).map_err(|_| Reject::Unsafe)?;
        let name = entry.name().map_err(|_| Reject::Unsafe)?.into_owned();
        let is_link = entry.is_symlink() || entry.unix_mode().is_some_and(|m| m & 0o170000 == 0o120000);
        if unsafe_name(&name) || entry.enclosed_name().is_none() || is_link {
            return Err(Reject::Unsafe);
        }
        if !matches!(entry.compression(), CompressionMethod::Stored | CompressionMethod::Deflated) {
            return Err(Reject::Unsafe);
        }
        if !entry.is_dir() {
            files.push(EntryInfo { index, name, size: entry.size() });
        }
    }
    let names: HashSet<&str> = files.iter().map(|f| f.name.as_str()).collect();

    // 3. A whole Unity project.
    let is_project = files.iter().any(|f| {
        let parts: Vec<&str> = f.name.split('/').collect();
        let dirs = &parts[..parts.len().saturating_sub(1)];
        dirs.iter().any(|d| *d == "Assets" || *d == "ProjectSettings") || f.name.ends_with("Packages/manifest.json")
    });
    if is_project {
        return Err(Reject::Project);
    }

    // 4. Only pictures.
    let is_image = |n: &str| {
        let lower = n.to_ascii_lowercase();
        [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"].iter().any(|e| lower.ends_with(e))
    };
    if !files.is_empty() && files.iter().all(|f| is_image(&f.name)) {
        return Err(Reject::Screenshots);
    }

    // 5. One folder too many: a single top-level folder holding `daedalus.json`.
    let wrapper = single_top_folder(&files);
    if !names.contains("daedalus.json")
        && let Some(folder) = &wrapper
        && names.contains(format!("{folder}/daedalus.json").as_str())
    {
        return Err(Reject::Wrapped);
    }

    // 6. No build at all (at the root, or inside a single wrapper folder).
    let build_prefix = if files.iter().any(|f| f.name.starts_with("Build/")) {
        "Build/".to_string()
    } else if let Some(folder) = wrapper.as_ref().filter(|w| files.iter().any(|f| f.name.starts_with(&format!("{w}/Build/")))) {
        format!("{folder}/Build/")
    } else {
        return Err(Reject::NotDaedalus);
    };

    // 7. A Build Settings build: gzip or uncompressed files, or no Daedalus metadata.
    let build_settings = files.iter().filter(|f| f.name.starts_with(&build_prefix)).any(|f| {
        f.name.ends_with(".gz") || f.name.ends_with(".wasm") || f.name.ends_with(".data") || f.name.ends_with(".framework.js")
    });
    if build_settings || !names.contains("daedalus.json") {
        return Err(Reject::BuildSettings);
    }

    // 8. The metadata.
    let meta = find(&files, "daedalus.json").ok_or(Reject::BuildSettings)?;
    if meta.size > limits.metadata_bytes {
        return Err(Reject::Format);
    }
    let meta_bytes = read_small(&mut archive, meta.index, limits.metadata_bytes).ok_or(Reject::Format)?;
    let json: serde_json::Value = serde_json::from_slice(&meta_bytes).map_err(|_| Reject::Format)?;
    if json.get("format").and_then(|f| f.as_i64()) != Some(FORMAT) {
        return Err(Reject::Format);
    }
    let raw_title = json.get("title").and_then(|t| t.as_str()).ok_or(Reject::Reshare)?;

    // 9. Exactly one of each build file, one shared name.
    let build_name = build_name(&files).ok_or(Reject::Reshare)?;

    // 10. The screenshot.
    let shot = find(&files, "screenshot.png").ok_or(Reject::Reshare)?;
    if shot.size > limits.screenshot_bytes {
        return Err(Reject::Reshare);
    }
    let shot_bytes = read_small(&mut archive, shot.index, limits.screenshot_bytes).ok_or(Reject::Reshare)?;
    let (width, height) = png_size(&shot_bytes).ok_or(Reject::Reshare)?;
    if width == 0 || height == 0 || width > limits.screenshot_width || height > limits.screenshot_height {
        return Err(Reject::Reshare);
    }

    // 11. The title.
    let title = clean_label(raw_title, limits.title_chars).ok_or(Reject::Reshare)?;

    // Unpack only the six files, at paths built from fixed names and the checked build name.
    fs::create_dir_all(staging.parent().unwrap_or(Path::new("."))).map_err(|_| Reject::Reshare)?;
    fs::create_dir(staging).map_err(|_| Reject::Reshare)?;
    fs::create_dir(staging.join("Build")).map_err(|_| Reject::Reshare)?;
    let mut written = 0u64;
    write_new(&staging.join("daedalus.json"), &meta_bytes)?;
    write_new(&staging.join("screenshot.png"), &shot_bytes)?;
    written += (meta_bytes.len() + shot_bytes.len()) as u64;
    for file_name in build_files(&build_name) {
        let entry = find(&files, &format!("Build/{file_name}")).ok_or(Reject::Reshare)?;
        let target = staging.join("Build").join(&file_name);
        written += copy_limited(&mut archive, entry.index, &target, limits.unpacked_bytes.saturating_sub(written))?;
    }
    Ok(Unpacked { title, build_name, bytes: written })
}

fn unsafe_name(name: &str) -> bool {
    name.is_empty()
        || name.starts_with('/')
        || name.contains('\\')
        || name.contains(':')
        || name.contains('\0')
        || name.split('/').any(|part| part == "..")
}

fn find<'a>(files: &'a [EntryInfo], name: &str) -> Option<&'a EntryInfo> {
    files.iter().find(|f| f.name == name)
}

fn single_top_folder(files: &[EntryInfo]) -> Option<String> {
    let mut tops = files.iter().map(|f| f.name.split_once('/').map(|(top, _)| top));
    let first = tops.next()??;
    tops.all(|t| t == Some(first)).then(|| first.to_string())
}

fn valid_build_name(name: &str) -> bool {
    (1..=64).contains(&name.len()) && name.bytes().all(|b| b.is_ascii_alphanumeric() || b == b'_' || b == b'-')
}

/// The one `<name>` shared by exactly one of each of the four build files at `Build/`.
fn build_name(files: &[EntryInfo]) -> Option<String> {
    let in_build: Vec<&str> = files.iter().filter_map(|f| f.name.strip_prefix("Build/")).filter(|n| !n.contains('/')).collect();
    let suffixes = [".loader.js", ".data.br", ".framework.js.br", ".wasm.br"];
    let mut name: Option<&str> = None;
    for suffix in suffixes {
        let matches: Vec<&str> = in_build.iter().filter_map(|n| n.strip_suffix(suffix)).collect();
        if matches.len() != 1 || !valid_build_name(matches[0]) {
            return None;
        }
        match name {
            None => name = Some(matches[0]),
            Some(n) if n == matches[0] => {}
            Some(_) => return None,
        }
    }
    name.map(str::to_string)
}

fn png_size(bytes: &[u8]) -> Option<(u32, u32)> {
    if bytes.len() < 24 || &bytes[..8] != PNG_SIGNATURE || &bytes[12..16] != b"IHDR" {
        return None;
    }
    let width = u32::from_be_bytes(bytes[16..20].try_into().ok()?);
    let height = u32::from_be_bytes(bytes[20..24].try_into().ok()?);
    Some((width, height))
}

/// An entry's bytes, or `None` if it is larger than `max` once decompressed.
fn read_small(archive: &mut ZipArchive<File>, index: usize, max: u64) -> Option<Vec<u8>> {
    let entry = archive.by_index(index).ok()?;
    let mut bytes = Vec::new();
    entry.take(max + 1).read_to_end(&mut bytes).ok()?;
    (bytes.len() as u64 <= max).then_some(bytes)
}

fn write_new(path: &Path, bytes: &[u8]) -> Result<(), Reject> {
    let mut file = OpenOptions::new().write(true).create_new(true).open(path).map_err(|_| Reject::Reshare)?;
    file.write_all(bytes).map_err(|_| Reject::Reshare)
}

/// Decompresses one entry into a new file, counting the bytes actually written; more than `budget` stops it.
/// Header sizes are never trusted.
fn copy_limited(archive: &mut ZipArchive<File>, index: usize, target: &PathBuf, budget: u64) -> Result<u64, Reject> {
    let entry = archive.by_index(index).map_err(|_| Reject::Reshare)?;
    let mut out = OpenOptions::new().write(true).create_new(true).open(target).map_err(|_| Reject::Reshare)?;
    let mut limited = entry.take(budget + 1);
    let copied = io::copy(&mut limited, &mut out).map_err(|_| Reject::Reshare)?;
    if copied > budget {
        return Err(Reject::TooBig);
    }
    Ok(copied)
}
