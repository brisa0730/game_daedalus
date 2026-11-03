//! `gallery backup --data <dir> --to <dir>`: a dated copy of the database (the newest 7 kept) and a mirror of the
//! games' version folders. A folder that left the source (a replaced version, a removed game) stays in the mirror
//! for 7 days, so every kept database copy can be restored with its files.
//!
//! Restore: `launchctl bootout gui/$(id -u)/com.orcapafea.daedalus.gallery`; in the data folder delete
//! `gallery.db-wal` and `gallery.db-shm` (a live log replayed onto the restored file would corrupt or mix it); copy
//! the chosen `<to>/db/gallery-YYYY-MM-DD.db` to `gallery.db` and `<to>/games/` back to `games/` (its `.gone`
//! file is harmless there); bootstrap the job. Copies are dated in UTC: the 03:30 Korean-time run carries the
//! previous day's date.
//!
//! It protects against database corruption and mistakes within 7 days, not against losing the disk: the backup
//! sits on the same disk.

use std::collections::BTreeMap;
use std::fs;
use std::io;
use std::path::{Path, PathBuf};

use crate::db::Db;

pub const KEEP_COPIES: usize = 7;
pub const KEEP_GONE_SECS: u64 = 7 * 24 * 3600;
const GONE_FILE: &str = ".gone";

#[derive(Debug)]
pub struct Report {
    pub db_copy: PathBuf,
    pub copied: usize,
    pub pruned: usize,
}

pub async fn run(data: &Path, to: &Path, now: u64) -> Result<Report, String> {
    let db_dir = to.join("db");
    fs::create_dir_all(&db_dir).map_err(|e| format!("create {}: {e}", db_dir.display()))?;
    for entry in fs::read_dir(&db_dir).map_err(|e| e.to_string())? {
        let path = entry.map_err(|e| e.to_string())?.path();
        if path.extension().is_some_and(|e| e == "tmp") {
            fs::remove_file(&path).map_err(|e| format!("remove {}: {e}", path.display()))?;
        }
    }
    let db = Db::open_existing(&data.join("gallery.db")).await.map_err(|e| format!("open database: {e}"))?;
    let temp = db_dir.join(format!("gallery-{now}-{}.tmp", std::process::id()));
    let vacuum = db.vacuum_into(&temp).await;
    db.close().await;
    vacuum.map_err(|e| format!("VACUUM INTO: {e}"))?;
    let db_copy = db_dir.join(format!("gallery-{}.db", utc_date(now)));
    fs::rename(&temp, &db_copy).map_err(|e| format!("rename backup: {e}"))?;
    prune_copies(&db_dir).map_err(|e| format!("prune copies: {e}"))?;
    let (copied, pruned) =
        mirror_games(&data.join("games"), &to.join("games"), now).map_err(|e| format!("mirror games: {e}"))?;
    Ok(Report { db_copy, copied, pruned })
}

fn prune_copies(db_dir: &Path) -> io::Result<()> {
    let mut copies: Vec<PathBuf> = fs::read_dir(db_dir)?
        .filter_map(|e| e.ok().map(|e| e.path()))
        .filter(|p| p.file_name().and_then(|n| n.to_str()).is_some_and(|n| n.starts_with("gallery-") && n.ends_with(".db")))
        .collect();
    copies.sort();
    while copies.len() > KEEP_COPIES {
        fs::remove_file(copies.remove(0))?;
    }
    Ok(())
}

/// `<id>/v<n>` relative paths of every version folder under `games`.
fn version_folders(games: &Path) -> io::Result<Vec<String>> {
    let mut out = Vec::new();
    if !games.exists() {
        return Ok(out);
    }
    for game in fs::read_dir(games)? {
        let game = game?;
        if !game.file_type()?.is_dir() {
            continue;
        }
        for version in fs::read_dir(game.path())? {
            let version = version?;
            if version.file_type()?.is_dir() {
                out.push(format!("{}/{}", game.file_name().to_string_lossy(), version.file_name().to_string_lossy()));
            }
        }
    }
    out.sort();
    Ok(out)
}

/// Copies new version folders into the mirror; folders gone from the source are kept 7 days, then pruned.
/// Returns (copied, pruned).
fn mirror_games(source: &Path, mirror: &Path, now: u64) -> io::Result<(usize, usize)> {
    fs::create_dir_all(mirror)?;
    let in_source = version_folders(source)?;
    let mut copied = 0;
    for rel in &in_source {
        let target = mirror.join(rel);
        if !target.exists() {
            let temp = mirror.join(format!("{rel}.partial"));
            let _ = fs::remove_dir_all(&temp);
            copy_dir(&source.join(rel), &temp)?;
            fs::rename(&temp, &target)?;
            copied += 1;
        }
    }
    let mut gone = read_gone(mirror);
    gone.retain(|rel, _| !in_source.contains(rel));
    let mut pruned = 0;
    for rel in version_folders(mirror)? {
        if in_source.contains(&rel) {
            continue;
        }
        let since = *gone.entry(rel.clone()).or_insert(now);
        if now.saturating_sub(since) >= KEEP_GONE_SECS {
            fs::remove_dir_all(mirror.join(&rel))?;
            gone.remove(&rel);
            pruned += 1;
            if let Some(parent) = mirror.join(&rel).parent() {
                let _ = fs::remove_dir(parent); // only if now empty
            }
        }
    }
    write_gone(mirror, &gone)?;
    Ok((copied, pruned))
}

fn read_gone(mirror: &Path) -> BTreeMap<String, u64> {
    fs::read_to_string(mirror.join(GONE_FILE))
        .unwrap_or_default()
        .lines()
        .filter_map(|l| l.rsplit_once(' '))
        .filter_map(|(rel, t)| t.parse().ok().map(|t| (rel.to_string(), t)))
        .collect()
}

fn write_gone(mirror: &Path, gone: &BTreeMap<String, u64>) -> io::Result<()> {
    let text: String = gone.iter().map(|(rel, t)| format!("{rel} {t}\n")).collect();
    fs::write(mirror.join(GONE_FILE), text)
}

fn copy_dir(from: &Path, to: &Path) -> io::Result<()> {
    fs::create_dir_all(to)?;
    for entry in fs::read_dir(from)? {
        let entry = entry?;
        let target = to.join(entry.file_name());
        if entry.file_type()?.is_dir() {
            copy_dir(&entry.path(), &target)?;
        } else {
            fs::copy(entry.path(), target)?;
        }
    }
    Ok(())
}

/// `YYYY-MM-DD` in UTC for Unix seconds.
pub fn utc_date(unix: u64) -> String {
    let days = (unix / 86_400) as i64;
    // Howard Hinnant's days-from-civil, inverted.
    let z = days + 719_468;
    let era = z.div_euclid(146_097);
    let doe = z.rem_euclid(146_097);
    let yoe = (doe - doe / 1460 + doe / 36_524 - doe / 146_096) / 365;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
    let mp = (5 * doy + 2) / 153;
    let day = doy - (153 * mp + 2) / 5 + 1;
    let month = if mp < 10 { mp + 3 } else { mp - 9 };
    let year = yoe + era * 400 + i64::from(month <= 2);
    format!("{year:04}-{month:02}-{day:02}")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn dates() {
        assert_eq!(utc_date(0), "1970-01-01");
        assert_eq!(utc_date(951_782_400), "2000-02-29");
        assert_eq!(utc_date(1_793_664_000), "2026-11-03");
    }
}
