//! Every setting and limit the gallery has, in one place, and the one clock read.

use std::net::{Ipv4Addr, SocketAddr, SocketAddrV4};
use std::path::{Path, PathBuf};
use std::time::{Duration, SystemTime, UNIX_EPOCH};

use crate::storage::StorageConfig;

/// The only address the server ever listens on: loopback, behind the `suwon` tunnel (Deploy/cloudflared/suwon.yml).
pub const BIND: SocketAddr = SocketAddr::V4(SocketAddrV4::new(Ipv4Addr::LOCALHOST, 8470));

pub const GALLERY_HOST: &str = "suwon.orcarin.com";
pub const GAMES_HOST: &str = "suwon-play.orcarin.com";

#[derive(Clone, Debug)]
pub struct Argon2Params {
    pub memory_kib: u32,
    pub iterations: u32,
}

#[derive(Clone, Debug)]
pub struct Config {
    pub data: PathBuf,
    pub gallery_origin: String,
    pub games_origin: String,
    pub argon2: Argon2Params,
    /// How long a sign-in waits for a free hashing slot before it is told to try again.
    pub argon2_wait: Duration,
    pub argon2_permits: usize,
    pub session_idle_secs: u64,
    pub session_max_secs: u64,
    /// Refresh a session's `last_seen` at most this often.
    pub session_touch_secs: u64,
    pub known_browser_secs: u64,
    pub throttle_first_secs: u64,
    pub throttle_cap_secs: u64,
    /// A sign-in delay nobody touched for this long is dropped.
    pub throttle_forget_secs: u64,
    pub title_max_chars: usize,
    pub name_max_chars: usize,
    pub max_upload_bytes: u64,
    pub max_unpacked_bytes: u64,
    pub max_zip_entries: usize,
    pub max_screenshot_bytes: u64,
    pub max_screenshot_width: u32,
    pub max_screenshot_height: u32,
    pub max_metadata_bytes: u64,
    pub games_per_account: i64,
    pub disk_cap_bytes: u64,
    pub unpack_permits: usize,
    /// The most an upload waits for its bucket copy before answering: Cloudflare drops a request at about 100 s,
    /// and the zip has already crossed the tunnel. Past it the game plays from the Mac until the next start.
    pub publish_deadline: Duration,
    /// The Naver bucket for build files (`<data>/storage.json`, read by `main`); `None` serves every game from the Mac.
    pub storage: Option<StorageConfig>,
}

impl Config {
    pub fn new(data: &Path) -> Self {
        Config {
            data: data.to_path_buf(),
            gallery_origin: format!("https://{GALLERY_HOST}"),
            games_origin: format!("https://{GAMES_HOST}"),
            // Measured on the Mac Mini in a release build (phase 5 log): 64 MiB × 3 passes ≈ 66 ms per hash
            // (19 MiB × 2 was 13 ms, under the 50–100 ms target). Two at once peak near 128 MiB.
            argon2: Argon2Params { memory_kib: 64 * 1024, iterations: 3 },
            argon2_wait: Duration::from_secs(3),
            argon2_permits: 2,
            session_idle_secs: 3600,
            session_max_secs: 12 * 3600,
            session_touch_secs: 300,
            known_browser_secs: 365 * 24 * 3600,
            throttle_first_secs: 1,
            throttle_cap_secs: 30,
            throttle_forget_secs: 3600,
            title_max_chars: 40,
            name_max_chars: 20,
            max_upload_bytes: 50_000_000,
            max_unpacked_bytes: 120_000_000,
            max_zip_entries: 256,
            max_screenshot_bytes: 1_000_000,
            max_screenshot_width: 1280,
            max_screenshot_height: 720,
            max_metadata_bytes: 4096,
            games_per_account: 10,
            disk_cap_bytes: 20_000_000_000,
            unpack_permits: 3,
            publish_deadline: Duration::from_secs(45),
            storage: None,
        }
    }

    pub fn db_path(&self) -> PathBuf {
        self.data.join("gallery.db")
    }

    pub fn games_dir(&self) -> PathBuf {
        self.data.join("games")
    }

    pub fn staging_dir(&self) -> PathBuf {
        self.data.join("staging")
    }

    /// `games/<id>/v<version>`
    pub fn version_dir(&self, game: &str, version: i64) -> PathBuf {
        self.games_dir().join(game).join(format!("v{version}"))
    }
}

/// Unix seconds. The only clock read in the server; everything that needs time takes `now` from here.
pub fn unix_now() -> u64 {
    SystemTime::now().duration_since(UNIX_EPOCH).expect("the clock is after 1970").as_secs()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn binds_to_loopback_only() {
        assert!(BIND.ip().is_loopback());
        assert_eq!(BIND.port(), 8470);
    }
}
