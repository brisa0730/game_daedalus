//! The Naver Object Storage copy of every game's build files (Documents/Plans/archive/gallery-naver-storage.md).
//! Cloudflare sends Korean visitors through LA; the bucket is in Korea. The Mac keeps the original of every game:
//! the player loads from the bucket only once a publish has finished, falls back to the Mac's copy on any load
//! error, and `reconcile` fixes the bucket at every start.

use std::collections::{BTreeMap, BTreeSet};
use std::future::Future;
use std::os::unix::fs::PermissionsExt;
use std::path::Path;
use std::pin::Pin;
use std::time::Duration;

use hmac::{Hmac, Mac};
use serde::Deserialize;
use sha2::{Digest, Sha256};

use crate::app::AppState;
use crate::config::Config;
use crate::db::Game;
use crate::files::headers_for;
use crate::share_zip::build_files;

pub type BoxFuture<'a, T> = Pin<Box<dyn Future<Output = T> + Send + 'a>>;

/// Bucket keys are unique per upload, so browsers may keep them for a year.
pub const IMMUTABLE: &str = "public, max-age=31536000, immutable";
const TIMEOUT: Duration = Duration::from_secs(30);

/// A storage failure, worded for the operator's log. Never carries a header, a key or the settings.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct StorageError(pub String);

impl std::fmt::Display for StorageError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(&self.0)
    }
}

/// What an object is stored with, besides `Cache-Control: IMMUTABLE` and a public-read ACL.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct ObjectHeaders {
    pub content_type: &'static str,
    pub content_encoding: Option<&'static str>,
}

/// The bucket, as the server uses it. `NaverStorage` in production; the tests use an in-memory one.
pub trait Storage: Send + Sync {
    fn put<'a>(&'a self, key: &'a str, body: Vec<u8>, headers: ObjectHeaders) -> BoxFuture<'a, Result<(), StorageError>>;
    fn list_prefix<'a>(&'a self, prefix: &'a str) -> BoxFuture<'a, Result<Vec<String>, StorageError>>;
    /// Deletes every key under `prefix`; the number deleted.
    fn delete_prefix<'a>(&'a self, prefix: &'a str) -> BoxFuture<'a, Result<usize, StorageError>>;
}

/// `<data>/storage.json`, written by the operator (mode 0600). The secret never reaches a log.
#[derive(Clone, PartialEq, Eq, Deserialize)]
pub struct StorageConfig {
    pub endpoint: String,
    pub region: String,
    pub bucket: String,
    pub access_key: String,
    pub secret_key: String,
}

impl std::fmt::Debug for StorageConfig {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("StorageConfig").field("endpoint", &self.endpoint).field("bucket", &self.bucket).finish_non_exhaustive()
    }
}

impl StorageConfig {
    /// `Ok(None)` without the file (the gallery serves from the Mac, as before v0.23); `Err` names what is wrong,
    /// never a value.
    pub fn load(data: &Path) -> Result<Option<StorageConfig>, String> {
        let path = data.join("storage.json");
        let meta = match std::fs::metadata(&path) {
            Ok(meta) => meta,
            Err(e) if e.kind() == std::io::ErrorKind::NotFound => return Ok(None),
            Err(e) => return Err(format!("storage.json can't be read ({})", e.kind())),
        };
        if meta.permissions().mode() & 0o077 != 0 {
            return Err("storage.json is readable by other users; chmod 600 it".into());
        }
        let text = std::fs::read_to_string(&path).map_err(|e| format!("storage.json can't be read ({})", e.kind()))?;
        let config: StorageConfig = serde_json::from_str(&text)
            .map_err(|e| {
                // serde's missing-field message names only the field; other messages may quote a value.
                let detail = e.to_string();
                match detail.strip_prefix("missing field ") {
                    Some(rest) => format!("storage.json: missing field {}", rest.split(' ').next().unwrap_or("")),
                    None => format!("storage.json isn't valid ({:?} error at line {})", e.classify(), e.line()),
                }
            })?;
        config.check()?;
        Ok(Some(config))
    }

    fn check(&self) -> Result<(), String> {
        for (name, value) in [("region", &self.region), ("access_key", &self.access_key), ("secret_key", &self.secret_key)] {
            if value.trim().is_empty() || value.chars().any(char::is_whitespace) {
                return Err(format!("storage.json: {name} is empty or has spaces"));
            }
        }
        if endpoint_host(&self.endpoint).is_none() {
            return Err("storage.json: endpoint must be https://<host>".into());
        }
        // Virtual-host addresses: a dot or an uppercase letter breaks the certificate or the DNS name.
        let b = self.bucket.as_bytes();
        let ok = (3..=63).contains(&b.len())
            && b.iter().all(|c| c.is_ascii_lowercase() || c.is_ascii_digit() || *c == b'-')
            && b[0] != b'-'
            && b[b.len() - 1] != b'-';
        if !ok {
            return Err("storage.json: bucket must be 3-63 of a-z 0-9 - (no dots)".into());
        }
        Ok(())
    }

    /// `<bucket>.<endpoint host>`: the virtual-host name the server talks to and players download from.
    pub fn host(&self) -> String {
        format!("{}.{}", self.bucket, endpoint_host(&self.endpoint).unwrap_or_default())
    }

    /// The one source for the player page's file addresses and the play CSP.
    pub fn public_origin(&self) -> String {
        format!("https://{}", self.host())
    }
}

/// The endpoint's host, if it is only `[a-z0-9.-]`: it goes into every games-host CSP header, where a stray
/// character would break the header (and every game with it).
fn endpoint_host(endpoint: &str) -> Option<&str> {
    let host = endpoint.strip_prefix("https://")?.trim_end_matches('/');
    let ok = !host.is_empty() && host.bytes().all(|c| c.is_ascii_lowercase() || c.is_ascii_digit() || c == b'.' || c == b'-');
    ok.then_some(host)
}

/// `games/<id>/v<version>-<updated_at>/`: unique per upload (a restored backup can repeat a version number, and the
/// keys are `immutable`); everything of that upload is under it.
pub fn upload_prefix(game: &Game) -> String {
    format!("games/{}/v{}-{}/", game.id, game.version, game.updated_at)
}

pub fn game_prefix(id: &str) -> String {
    format!("games/{id}/")
}

/// The key of one of the four build files.
pub fn build_key(game: &Game, file: &str) -> String {
    format!("{}Build/{file}", upload_prefix(game))
}

/// The four build files' public addresses, in `build_files` order.
pub fn build_urls(config: &StorageConfig, game: &Game) -> [String; 4] {
    let origin = config.public_origin();
    build_files(&game.build_name).map(|file| format!("{origin}/{}", build_key(game, &file)))
}

/// Copies the game's current version from the Mac to the bucket. The caller records it with `set_published`.
pub async fn publish(storage: &dyn Storage, config: &Config, game: &Game) -> Result<(), StorageError> {
    let dir = config.version_dir(&game.id, game.version).join("Build");
    for file in build_files(&game.build_name) {
        let body = tokio::fs::read(dir.join(&file))
            .await
            .map_err(|e| StorageError(format!("read {file}: {}", e.kind())))?;
        let h = headers_for(&file);
        let headers = ObjectHeaders { content_type: h.content_type, content_encoding: h.content_encoding };
        storage.put(&build_key(game, &file), body, headers).await?;
    }
    Ok(())
}

/// What one reconcile did, for its log line.
#[derive(Debug, Default, Clone, PartialEq, Eq)]
pub struct Reconciled {
    /// Live games whose bucket copy was complete.
    pub published: usize,
    /// Live games published or re-published now.
    pub repaired: usize,
    /// Upload prefixes (old versions, removed or unknown games) deleted.
    pub deleted: usize,
    /// Games busy with an upload or a removal: fixed at the next start.
    pub skipped: usize,
    pub failed: usize,
}

/// At start, with the server already taking uploads: lists the bucket first, then takes each game's busy lock,
/// re-reads its row and decides from that row only. Moves the existing games, repairs after a restored backup or a
/// changed bucket, and retries removals whose delete failed. Never touches local files.
pub async fn reconcile(state: &AppState) -> Reconciled {
    let mut done = Reconciled::default();
    let Some(storage) = state.storage.clone() else { return done };
    let listed = match storage.list_prefix("games/").await {
        Ok(keys) => keys,
        Err(error) => {
            tracing::warn!(%error, "reconcile: listing the bucket failed");
            done.failed += 1;
            return done;
        }
    };
    let mut ids: BTreeSet<String> = listed.iter().filter_map(|k| k.split('/').nth(1)).map(String::from).collect();
    match state.db.live_games().await {
        Ok(games) => ids.extend(games.into_iter().map(|g| g.id)),
        Err(error) => {
            tracing::warn!(%error, "reconcile: reading the games failed");
            done.failed += 1;
            return done;
        }
    }
    for id in ids {
        let Some(_busy) = state.lock_games(std::slice::from_ref(&id)) else {
            done.skipped += 1;
            continue;
        };
        if let Err(error) = reconcile_game(state, storage.as_ref(), &id, &mut done).await {
            tracing::warn!(game = %id, %error, "reconcile failed for a game");
            done.failed += 1;
        }
    }
    tracing::info!(
        published = done.published,
        repaired = done.repaired,
        deleted = done.deleted,
        skipped = done.skipped,
        failed = done.failed,
        "reconcile done"
    );
    done
}

/// One game, under its busy lock: its bucket copy is listed again here, so an upload that landed after the first
/// listing is seen as it is now.
async fn reconcile_game(state: &AppState, storage: &dyn Storage, id: &str, done: &mut Reconciled) -> Result<(), StorageError> {
    let game = state.db.live_game(id).await.map_err(|e| StorageError(format!("read game: {e}")))?;
    let keys = storage.list_prefix(&game_prefix(id)).await?;
    let mut uploads: BTreeMap<String, BTreeSet<String>> = BTreeMap::new();
    for key in &keys {
        if let Some((upload, file)) = key.strip_prefix(&game_prefix(id)).and_then(|rest| rest.split_once('/')) {
            uploads.entry(format!("{}{upload}/", game_prefix(id))).or_default().insert(file.to_string());
        }
    }
    let current = game.as_ref().map(upload_prefix);
    if let Some(game) = &game {
        let have = uploads.get(&upload_prefix(game));
        let complete = build_files(&game.build_name).iter().all(|f| have.is_some_and(|h| h.contains(&format!("Build/{f}"))));
        let db_err = |e: sqlx::Error| StorageError(format!("database: {e}"));
        if complete {
            state.db.set_published(id, game.version).await.map_err(db_err)?;
            done.published += 1;
        } else {
            state.db.clear_published(id, game.version).await.map_err(db_err)?;
            publish(storage, &state.config, game).await?;
            state.db.set_published(id, game.version).await.map_err(db_err)?;
            done.repaired += 1;
        }
    }
    for prefix in uploads.keys().filter(|p| current.as_ref() != Some(*p)) {
        storage.delete_prefix(prefix).await?;
        done.deleted += 1;
    }
    Ok(())
}

// Naver Object Storage over its S3 API, signed with SigV4 exactly as Deploy/naver/bucket.py signs.

type HmacSha256 = Hmac<Sha256>;

pub struct NaverStorage {
    config: StorageConfig,
    client: reqwest::Client,
}

/// A request ready to send: its URL and every header to set.
#[derive(Debug, PartialEq, Eq)]
pub struct SignedRequest {
    pub url: String,
    pub headers: Vec<(String, String)>,
}

impl NaverStorage {
    pub fn new(config: StorageConfig) -> NaverStorage {
        let client = reqwest::Client::builder().timeout(TIMEOUT).build().expect("an HTTP client");
        NaverStorage { config, client }
    }

    /// Builds and signs a request at `amz_date` (`YYYYMMDDTHHMMSSZ`). `query` is already canonical (sorted, encoded).
    pub fn signed(&self, method: &str, key: &str, query: &str, extra: &[(&str, &str)], body: &[u8], amz_date: &str) -> SignedRequest {
        let payload = hex(&Sha256::digest(body));
        let host = self.config.host();
        let mut headers: Vec<(String, String)> = extra.iter().map(|(k, v)| (k.to_ascii_lowercase(), v.trim().to_string())).collect();
        headers.push(("host".into(), host.clone()));
        headers.push(("x-amz-date".into(), amz_date.into()));
        headers.push(("x-amz-content-sha256".into(), payload.clone()));
        headers.sort();
        let path = format!("/{}", encode(key, true));
        let signature = sign(&self.config.secret_key, &self.config.region, method, &path, query, &headers, &payload, amz_date);
        let signed: Vec<&str> = headers.iter().map(|(k, _)| k.as_str()).collect();
        let day = &amz_date[..8];
        let authorization = format!(
            "AWS4-HMAC-SHA256 Credential={}/{day}/{}/s3/aws4_request, SignedHeaders={}, Signature={signature}",
            self.config.access_key,
            self.config.region,
            signed.join(";")
        );
        headers.retain(|(k, _)| k != "host");
        headers.push(("authorization".into(), authorization));
        let url = format!("https://{host}{path}{}", if query.is_empty() { String::new() } else { format!("?{query}") });
        SignedRequest { url, headers }
    }

    async fn send(&self, method: reqwest::Method, key: &str, query: &str, extra: &[(&str, &str)], body: Vec<u8>) -> Result<(u16, Vec<u8>), StorageError> {
        let request = self.signed(method.as_str(), key, query, extra, &body, &amz_now());
        let mut builder = self.client.request(method, &request.url);
        for (k, v) in &request.headers {
            builder = builder.header(k, v);
        }
        // reqwest's own error text can name the URL, never a header; keep only its kind.
        let response = builder.body(body).send().await.map_err(|e| StorageError(request_failure(&e)))?;
        let status = response.status().as_u16();
        let bytes = response.bytes().await.map_err(|e| StorageError(request_failure(&e)))?;
        Ok((status, bytes.to_vec()))
    }

    /// The probe for `gallery storage check`: put, a public GET, delete. Returns the step that failed and its status.
    pub async fn check(&self) -> Result<(), String> {
        let key = format!("probe/check-{}.txt", crate::config::unix_now());
        let headers = ObjectHeaders { content_type: "text/plain", content_encoding: None };
        self.put(&key, b"daedalus storage check".to_vec(), headers).await.map_err(|e| format!("put: {e}"))?;
        let url = format!("{}/{key}", self.config.public_origin());
        let status = self.client.get(&url).send().await.map(|r| r.status().as_u16()).map_err(|e| format!("public get: {}", request_failure(&e)))?;
        let deleted = self.delete_prefix(&key).await.map_err(|e| format!("delete: {e}"))?;
        if status != 200 {
            return Err(format!("public get: HTTP {status}"));
        }
        if deleted != 1 {
            return Err(format!("delete: {deleted} objects"));
        }
        Ok(())
    }
}

fn request_failure(e: &reqwest::Error) -> String {
    if e.is_timeout() {
        "timed out".into()
    } else if e.is_connect() {
        "couldn't connect".into()
    } else {
        "request failed".into()
    }
}

impl Storage for NaverStorage {
    fn put<'a>(&'a self, key: &'a str, body: Vec<u8>, headers: ObjectHeaders) -> BoxFuture<'a, Result<(), StorageError>> {
        Box::pin(async move {
            let mut extra = vec![("content-type", headers.content_type), ("cache-control", IMMUTABLE), ("x-amz-acl", "public-read")];
            if let Some(encoding) = headers.content_encoding {
                extra.push(("content-encoding", encoding));
            }
            match self.send(reqwest::Method::PUT, key, "", &extra, body).await? {
                (200, _) => Ok(()),
                (status, _) => Err(StorageError(format!("put {key}: HTTP {status}"))),
            }
        })
    }

    fn list_prefix<'a>(&'a self, prefix: &'a str) -> BoxFuture<'a, Result<Vec<String>, StorageError>> {
        Box::pin(async move {
            let mut keys = Vec::new();
            let mut token: Option<String> = None;
            loop {
                let mut params = vec![("list-type".to_string(), "2".to_string()), ("prefix".to_string(), encode(prefix, false))];
                if let Some(t) = &token {
                    params.push(("continuation-token".into(), encode(t, false)));
                }
                params.sort();
                let query = params.iter().map(|(k, v)| format!("{k}={v}")).collect::<Vec<_>>().join("&");
                let (status, body) = self.send(reqwest::Method::GET, "", &query, &[], Vec::new()).await?;
                if status != 200 {
                    return Err(StorageError(format!("list {prefix}: HTTP {status}")));
                }
                let text = String::from_utf8_lossy(&body);
                keys.extend(elements(&text, "Key"));
                let next = elements(&text, "NextContinuationToken").into_iter().next();
                if next.is_some() && next == token {
                    return Err(StorageError(format!("list {prefix}: the same continuation token came back")));
                }
                token = next;
                if token.is_none() {
                    return Ok(keys);
                }
            }
        })
    }

    fn delete_prefix<'a>(&'a self, prefix: &'a str) -> BoxFuture<'a, Result<usize, StorageError>> {
        Box::pin(async move {
            let keys = self.list_prefix(prefix).await?;
            for key in &keys {
                let (status, _) = self.send(reqwest::Method::DELETE, key, "", &[], Vec::new()).await?;
                if status != 204 && status != 200 {
                    return Err(StorageError(format!("delete {key}: HTTP {status}")));
                }
            }
            Ok(keys.len())
        })
    }
}

/// The text of every `<name>…</name>` element (ListObjectsV2's keys and token; no XML crate for two tags). Only
/// `&amp;` is unescaped: our keys are `[a-z0-9/._-]` and tokens are base64, so no other entity can appear.
fn elements(xml: &str, name: &str) -> Vec<String> {
    let (open, close) = (format!("<{name}>"), format!("</{name}>"));
    xml.split(&open)
        .skip(1)
        .filter_map(|part| part.split_once(&close).map(|(v, _)| v.replace("&amp;", "&")))
        .collect()
}

/// SigV4's URI encoding: unreserved characters kept, `/` kept in paths.
fn encode(text: &str, keep_slash: bool) -> String {
    text.bytes()
        .map(|b| match b {
            b'A'..=b'Z' | b'a'..=b'z' | b'0'..=b'9' | b'-' | b'_' | b'.' | b'~' => (b as char).to_string(),
            b'/' if keep_slash => "/".into(),
            _ => format!("%{b:02X}"),
        })
        .collect()
}

fn hex(bytes: &[u8]) -> String {
    bytes.iter().map(|b| format!("{b:02x}")).collect()
}

fn hmac(key: &[u8], data: &str) -> Vec<u8> {
    let mut mac = HmacSha256::new_from_slice(key).expect("HMAC takes any key");
    mac.update(data.as_bytes());
    mac.finalize().into_bytes().to_vec()
}

/// The SigV4 signature (S3, `headers` lowercase and sorted, `host` among them).
#[allow(clippy::too_many_arguments)]
pub fn sign(secret: &str, region: &str, method: &str, path: &str, query: &str, headers: &[(String, String)], payload: &str, amz_date: &str) -> String {
    let canonical_headers: String = headers.iter().map(|(k, v)| format!("{k}:{v}\n")).collect();
    let signed: Vec<&str> = headers.iter().map(|(k, _)| k.as_str()).collect();
    let canonical = [method, path, query, &canonical_headers, &signed.join(";"), payload].join("\n");
    let day = &amz_date[..8];
    let scope = format!("{day}/{region}/s3/aws4_request");
    let to_sign = ["AWS4-HMAC-SHA256", amz_date, &scope, &hex(&Sha256::digest(canonical.as_bytes()))].join("\n");
    let mut key = format!("AWS4{secret}").into_bytes();
    for part in [day, region, "s3", "aws4_request"] {
        key = hmac(&key, part);
    }
    hex(&hmac(&key, &to_sign))
}

fn amz_now() -> String {
    // Civil date from Unix seconds (Howard Hinnant's algorithm); the server reads no other clock.
    let secs = crate::config::unix_now() as i64;
    let (days, rem) = (secs.div_euclid(86_400), secs.rem_euclid(86_400));
    let z = days + 719_468;
    let era = z.div_euclid(146_097);
    let doe = z - era * 146_097;
    let yoe = (doe - doe / 1_460 + doe / 36_524 - doe / 146_096) / 365;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
    let mp = (5 * doy + 2) / 153;
    let day = doy - (153 * mp + 2) / 5 + 1;
    let month = if mp < 10 { mp + 3 } else { mp - 9 };
    let year = yoe + era * 400 + i64::from(month <= 2);
    format!("{year:04}{month:02}{day:02}T{:02}{:02}{:02}Z", rem / 3600, rem % 3600 / 60, rem % 60)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn config() -> StorageConfig {
        StorageConfig {
            endpoint: "https://kr.object.ncloudstorage.com".into(),
            region: "kr-standard".into(),
            bucket: "daedalus".into(),
            access_key: "AKIDEXAMPLE".into(),
            secret_key: "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY".into(),
        }
    }

    #[test]
    fn signs_the_aws_documented_example() {
        // AWS SigV4 docs, "Example: GET Object": GET /test.txt from examplebucket with Range: bytes=0-9.
        let empty = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        let headers = vec![
            ("host".to_string(), "examplebucket.s3.amazonaws.com".to_string()),
            ("range".into(), "bytes=0-9".into()),
            ("x-amz-content-sha256".into(), empty.into()),
            ("x-amz-date".into(), "20130524T000000Z".into()),
        ];
        let signature = sign("wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY", "us-east-1", "GET", "/test.txt", "", &headers, empty, "20130524T000000Z");
        assert_eq!(signature, "f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41");
    }

    #[test]
    fn a_put_carries_every_header_the_player_needs() {
        let storage = NaverStorage::new(config());
        let extra = [("content-type", "application/wasm"), ("cache-control", IMMUTABLE), ("x-amz-acl", "public-read"), ("content-encoding", "br")];
        let request = storage.signed("PUT", "games/abc/v1-5/Build/game.wasm.br", "", &extra, b"wasm", "20261104T000000Z");
        assert_eq!(request.url, "https://daedalus.kr.object.ncloudstorage.com/games/abc/v1-5/Build/game.wasm.br");
        let get = |name: &str| request.headers.iter().find(|(k, _)| k == name).map(|(_, v)| v.as_str());
        assert_eq!(get("content-type"), Some("application/wasm"));
        assert_eq!(get("content-encoding"), Some("br"));
        assert_eq!(get("cache-control"), Some(IMMUTABLE));
        assert_eq!(get("x-amz-acl"), Some("public-read"));
        assert_eq!(get("x-amz-date"), Some("20261104T000000Z"));
        assert_eq!(get("x-amz-content-sha256"), Some(hex(&Sha256::digest(b"wasm")).as_str()));
        let auth = get("authorization").unwrap();
        assert!(auth.starts_with("AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/20261104/kr-standard/s3/aws4_request, "));
        assert!(auth.contains(
            "SignedHeaders=cache-control;content-encoding;content-type;host;x-amz-acl;x-amz-content-sha256;x-amz-date, Signature="
        ));
        assert!(!request.headers.iter().any(|(_, v)| v.contains("wJalr")), "the secret is never sent");
    }

    #[test]
    fn the_origin_is_the_virtual_host() {
        assert_eq!(config().public_origin(), "https://daedalus.kr.object.ncloudstorage.com");
        assert!(!format!("{:?}", config()).contains("EXAMPLEKEY"), "Debug hides the secret");
    }

    #[test]
    fn bad_settings_are_named_without_values() {
        let mut c = config();
        c.bucket = "my.bucket".into();
        assert!(c.check().unwrap_err().contains("no dots"));
        c.bucket = "Daedalus".into();
        assert!(c.check().is_err());
        let mut c = config();
        c.endpoint = "http://kr.object.ncloudstorage.com".into();
        assert!(c.check().is_err());
        for host in ["kr.object.ncloud;storage.com", "kr.object ncloudstorage.com", "kr.objéct.ncloudstorage.com"] {
            c.endpoint = format!("https://{host}");
            assert!(c.check().is_err(), "{host} would go into the CSP header");
        }
        let mut c = config();
        c.secret_key = String::new();
        assert!(c.check().unwrap_err().contains("secret_key"));
    }

    #[test]
    fn the_file_must_be_private() {
        let dir = tempfile::tempdir().unwrap();
        assert_eq!(StorageConfig::load(dir.path()), Ok(None));
        let path = dir.path().join("storage.json");
        std::fs::write(&path, r#"{"endpoint":"https://kr.object.ncloudstorage.com","region":"kr-standard","bucket":"daedalus","access_key":"a","secret_key":"s3cr3t"}"#).unwrap();
        std::fs::set_permissions(&path, std::fs::Permissions::from_mode(0o644)).unwrap();
        assert!(StorageConfig::load(dir.path()).unwrap_err().contains("chmod 600"));
        std::fs::set_permissions(&path, std::fs::Permissions::from_mode(0o600)).unwrap();
        assert_eq!(StorageConfig::load(dir.path()).unwrap().unwrap().bucket, "daedalus");
        std::fs::write(&path, r#"{"endpoint":"https://kr.object.ncloudstorage.com","bucket":"daedalus","secret_key":"s3cr3t"}"#).unwrap();
        let error = StorageConfig::load(dir.path()).unwrap_err();
        assert!(!error.contains("s3cr3t"), "{error}");
        assert!(error.contains("missing field `region`"), "the missing field is named: {error}");
    }

    #[test]
    fn the_clock_formats_as_sigv4_wants() {
        let now = amz_now();
        assert_eq!(now.len(), 16);
        assert!(now.starts_with("20") && now.ends_with('Z') && now.as_bytes()[8] == b'T');
    }

    #[test]
    fn list_answers_are_read() {
        let xml = "<ListBucketResult><Contents><Key>games/a/v1-2/Build/x.js</Key></Contents><Contents><Key>games/b/v3-4/Build/y.br</Key></Contents><NextContinuationToken>t&amp;1</NextContinuationToken></ListBucketResult>";
        assert_eq!(elements(xml, "Key"), ["games/a/v1-2/Build/x.js", "games/b/v3-4/Build/y.br"]);
        assert_eq!(elements(xml, "NextContinuationToken"), ["t&1"]);
        assert_eq!(encode("games/a b", false), "games%2Fa%20b");
    }
}
