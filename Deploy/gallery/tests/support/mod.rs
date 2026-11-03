//! A test app on a temporary data folder, request helpers, and a synthetic Share zip builder. No binary fixture is
//! committed: every zip is built here, in memory.
#![allow(dead_code)]

use std::collections::BTreeMap;
use std::io::{Cursor, Write};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Mutex};

use axum::Router;
use axum::body::Body;
use axum::http::{HeaderMap, Method, Request, StatusCode};
use http_body_util::BodyExt;
use tower::ServiceExt;
use zip::write::SimpleFileOptions;
use zip::{CompressionMethod, ZipWriter};

use gallery::app::{AppState, router};
use gallery::config::{Config, GALLERY_HOST, GAMES_HOST, unix_now};
use gallery::db::{Db, Role};
use gallery::storage::{BoxFuture, ObjectHeaders, Storage, StorageConfig, StorageError};

pub const GALLERY: &str = GALLERY_HOST;
pub const GAMES: &str = GAMES_HOST;
pub const ORIGIN: &str = "https://suwon.orcarin.com";

pub struct TestApp {
    pub dir: tempfile::TempDir,
    pub state: AppState,
    pub router: Router,
    /// The in-memory bucket, for apps made `with_bucket`.
    pub bucket: Option<Arc<FakeBucket>>,
}

/// The settings a bucket test runs with (never contacted: the bucket is `FakeBucket`).
pub fn test_storage_config() -> StorageConfig {
    StorageConfig {
        endpoint: "https://kr.object.ncloudstorage.com".into(),
        region: "kr-standard".into(),
        bucket: "test-bucket".into(),
        access_key: "access".into(),
        secret_key: "secret".into(),
    }
}

/// An in-memory bucket with switches for the failure orders the storage tests need.
#[derive(Default)]
pub struct FakeBucket {
    pub objects: Mutex<BTreeMap<String, (Vec<u8>, ObjectHeaders)>>,
    /// Every put fails.
    pub fail_puts: AtomicBool,
    /// Every put never finishes (a request dropped mid-publish).
    pub hang_puts: AtomicBool,
    /// Deletes wait while this is set.
    pub hold_deletes: AtomicBool,
    /// The next listing of `games/` signals `listed` and waits for `resume`.
    pub pause_listing: AtomicBool,
    pub listed: tokio::sync::Notify,
    pub resume: tokio::sync::Notify,
}

impl FakeBucket {
    pub fn keys(&self, prefix: &str) -> Vec<String> {
        self.objects.lock().unwrap().keys().filter(|k| k.starts_with(prefix)).cloned().collect()
    }

    pub fn object(&self, key: &str) -> Option<(Vec<u8>, ObjectHeaders)> {
        self.objects.lock().unwrap().get(key).cloned()
    }

    pub fn insert(&self, key: &str) {
        let headers = ObjectHeaders { content_type: "application/octet-stream", content_encoding: None };
        self.objects.lock().unwrap().insert(key.to_string(), (b"x".to_vec(), headers));
    }

    pub fn remove(&self, prefix: &str) {
        self.objects.lock().unwrap().retain(|k, _| !k.starts_with(prefix));
    }
}

impl Storage for FakeBucket {
    fn put<'a>(&'a self, key: &'a str, body: Vec<u8>, headers: ObjectHeaders) -> BoxFuture<'a, Result<(), StorageError>> {
        Box::pin(async move {
            if self.hang_puts.load(Ordering::SeqCst) {
                std::future::pending::<()>().await;
            }
            if self.fail_puts.load(Ordering::SeqCst) {
                return Err(StorageError("put failed (fake)".into()));
            }
            self.objects.lock().unwrap().insert(key.to_string(), (body, headers));
            Ok(())
        })
    }

    fn list_prefix<'a>(&'a self, prefix: &'a str) -> BoxFuture<'a, Result<Vec<String>, StorageError>> {
        Box::pin(async move {
            if prefix == "games/" && self.pause_listing.swap(false, Ordering::SeqCst) {
                let keys = self.keys(prefix);
                self.listed.notify_one();
                self.resume.notified().await;
                return Ok(keys);
            }
            Ok(self.keys(prefix))
        })
    }

    fn delete_prefix<'a>(&'a self, prefix: &'a str) -> BoxFuture<'a, Result<usize, StorageError>> {
        Box::pin(async move {
            while self.hold_deletes.load(Ordering::SeqCst) {
                tokio::time::sleep(std::time::Duration::from_millis(5)).await;
            }
            let n = self.keys(prefix).len();
            self.remove(prefix);
            Ok(n)
        })
    }
}

pub struct Answer {
    pub status: StatusCode,
    pub headers: HeaderMap,
    pub body: Vec<u8>,
}

impl Answer {
    pub fn text(&self) -> String {
        String::from_utf8_lossy(&self.body).into_owned()
    }

    pub fn header(&self, name: &str) -> Option<&str> {
        self.headers.get(name).and_then(|v| v.to_str().ok())
    }

    pub fn all(&self, name: &str) -> Vec<String> {
        self.headers.get_all(name).iter().filter_map(|v| v.to_str().ok().map(String::from)).collect()
    }

    pub fn json(&self) -> serde_json::Value {
        serde_json::from_slice(&self.body).expect("a JSON body")
    }

    /// `name=value` of a Set-Cookie with this name.
    pub fn cookie(&self, name: &str) -> Option<String> {
        self.all("set-cookie").into_iter().find(|c| c.starts_with(&format!("{name}="))).map(|c| {
            c.split(';').next().unwrap_or_default().to_string()
        })
    }
}

/// Percent-encodes a form value (everything but unreserved ASCII), so Korean, spaces and `& + % =` survive.
pub fn url_encode(text: &str) -> String {
    text.bytes()
        .map(|b| match b {
            b'a'..=b'z' | b'A'..=b'Z' | b'0'..=b'9' | b'-' | b'_' | b'.' => (b as char).to_string(),
            _ => format!("%{b:02X}"),
        })
        .collect()
}

/// The passwords printed on a slips page, in order.
pub fn slip_passwords(html: &str) -> Vec<String> {
    html.split(r#"<dd class="big">"#).skip(1).map(|p| p[..p.find('<').unwrap()].to_string()).collect::<Vec<_>>()
        .chunks(2).map(|pair| pair[1].clone()).collect()
}

pub fn test_config(dir: &std::path::Path) -> Config {
    let mut config = Config::new(dir);
    config.argon2.memory_kib = 64;
    config.argon2.iterations = 1;
    config.argon2_wait = std::time::Duration::from_millis(200);
    config
}

impl TestApp {
    pub async fn new() -> TestApp {
        Self::with(|_| {}).await
    }

    pub async fn with(tweak: impl FnOnce(&mut Config)) -> TestApp {
        let dir = tempfile::tempdir().unwrap();
        let mut config = test_config(dir.path());
        tweak(&mut config);
        for sub in ["games", "staging"] {
            std::fs::create_dir_all(dir.path().join(sub)).unwrap();
        }
        let db = Db::open(&config.db_path()).await.unwrap();
        let state = AppState::new(config, db);
        let router = router(state.clone());
        TestApp { dir, state, router, bucket: None }
    }

    /// An app whose games are published to an in-memory bucket.
    pub async fn with_bucket() -> TestApp {
        TestApp::with_bucket_and(|_| {}).await
    }

    pub async fn with_bucket_and(tweak: impl FnOnce(&mut Config)) -> TestApp {
        let dir = tempfile::tempdir().unwrap();
        let mut config = test_config(dir.path());
        config.storage = Some(test_storage_config());
        tweak(&mut config);
        for sub in ["games", "staging"] {
            std::fs::create_dir_all(dir.path().join(sub)).unwrap();
        }
        let db = Db::open(&config.db_path()).await.unwrap();
        let bucket = Arc::new(FakeBucket::default());
        let state = AppState::with_storage(config, db, Some(bucket.clone() as Arc<dyn Storage>));
        let router = router(state.clone());
        TestApp { dir, state, router, bucket: Some(bucket) }
    }

    pub fn bucket(&self) -> &FakeBucket {
        self.bucket.as_deref().expect("an app made with_bucket")
    }

    /// A fresh app state over the same data folder: what a restart looks like.
    pub async fn restart(&self) -> TestApp {
        let config = (*self.state.config).clone();
        let db = Db::open(&config.db_path()).await.unwrap();
        let state = AppState::with_storage(config, db, self.state.storage.clone());
        let router = router(state.clone());
        TestApp { dir: tempfile::tempdir().unwrap(), state, router, bucket: self.bucket.clone() }
    }

    pub async fn send(&self, request: Request<Body>) -> Answer {
        let response = self.router.clone().oneshot(request).await.unwrap();
        let status = response.status();
        let headers = response.headers().clone();
        let body = response.into_body().collect().await.unwrap().to_bytes().to_vec();
        Answer { status, headers, body }
    }

    pub async fn get(&self, host: &str, path: &str, cookie: Option<&str>) -> Answer {
        let mut req = Request::builder().method(Method::GET).uri(path).header("host", host);
        if let Some(c) = cookie {
            req = req.header("cookie", c);
        }
        self.send(req.body(Body::empty()).unwrap()).await
    }

    /// A same-origin form POST on the gallery, as the gallery's own page sends it.
    pub async fn post_form(&self, path: &str, cookie: Option<&str>, form: &str) -> Answer {
        let mut req = Request::builder()
            .method(Method::POST)
            .uri(path)
            .header("host", GALLERY)
            .header("sec-fetch-site", "same-origin")
            .header("content-type", "application/x-www-form-urlencoded");
        if let Some(c) = cookie {
            req = req.header("cookie", c);
        }
        self.send(req.body(Body::from(form.to_string())).unwrap()).await
    }

    pub async fn upload(&self, cookie: &str, game: &str, zip: Vec<u8>) -> Answer {
        let req = Request::builder()
            .method(Method::POST)
            .uri(format!("/upload?game={game}"))
            .header("host", GALLERY)
            .header("sec-fetch-site", "same-origin")
            .header("content-type", "application/zip")
            .header("content-length", zip.len())
            .header("cookie", cookie)
            .body(Body::from(zip))
            .unwrap();
        self.send(req).await
    }

    pub async fn add_user(&self, login: &str, name: &str, role: Role, password: &str) -> i64 {
        let hash = self.state.hasher.hash(password.to_string()).await.unwrap();
        self.state.db.create_user(login, name, role, &hash, unix_now() as i64).await.unwrap()
    }

    /// Signs in and returns the `Cookie` header value for the session.
    pub async fn sign_in(&self, login: &str, password: &str) -> String {
        self.sign_in_keeping_browser(login, password, None).await.0
    }

    /// Signs in from a browser (its known cookie, if any, is sent) and returns that browser's session and known
    /// cookies as `name=value`.
    pub async fn sign_in_keeping_browser(&self, login: &str, password: &str, known: Option<&str>) -> (String, String) {
        let form = format!("login={}&password={}", url_encode(login), url_encode(password));
        let answer = self.post_form("/login", known, &form).await;
        assert_eq!(answer.status, StatusCode::SEE_OTHER, "sign-in failed: {}", answer.text());
        let session = answer.cookie("__Host-gallery_session").expect("a session cookie");
        let known = answer.cookie("__Host-gallery_known").expect("a known-browser cookie");
        (session, known)
    }

    /// Forgets one account's sign-in delay, so a test can sign in right after a deliberate failure.
    pub fn clear_throttle(&self, user_id: i64) {
        self.state.throttle.lock().unwrap().clear(user_id);
    }

    pub async fn student(&self, login: &str, name: &str) -> String {
        self.add_user(login, name, Role::Student, "pw-pw-234").await;
        self.sign_in(login, "pw-pw-234").await
    }

    pub async fn teacher(&self) -> String {
        self.add_user("teach", "선생님", Role::Teacher, "pw-pw-234").await;
        self.sign_in("teach", "pw-pw-234").await
    }

    /// Uploads a new game and returns its id.
    pub async fn new_game(&self, cookie: &str, title: &str) -> String {
        let answer = self.upload(cookie, "new", ShareZip::new(title).build()).await;
        assert_eq!(answer.status, StatusCode::OK, "{}", answer.text());
        answer.json()["play"].as_str().unwrap().trim_start_matches("/play/").to_string()
    }

    /// Opens a game through the play view and the grant, returning the gate `Cookie` value.
    pub async fn open_game(&self, cookie: &str, id: &str) -> String {
        let view = self.get(GALLERY, &format!("/play/{id}"), Some(cookie)).await;
        assert_eq!(view.status, StatusCode::OK);
        let src = frame_src(&view.text());
        let path = src.trim_start_matches("https://suwon-play.orcarin.com");
        let enter = self.enter(path, "iframe", Some("same-site")).await;
        assert_eq!(enter.status, StatusCode::FOUND, "{}", enter.text());
        enter.cookie(&format!("__Host-g_{id}")).expect("a gate cookie")
    }

    pub async fn enter(&self, path: &str, dest: &str, site: Option<&str>) -> Answer {
        let mut req = Request::builder().uri(path).header("host", GAMES).header("sec-fetch-dest", dest);
        if let Some(site) = site {
            req = req.header("sec-fetch-site", site);
        }
        self.send(req.body(Body::empty()).unwrap()).await
    }
}

/// The iframe's `src` on a play view page (maud escapes `&` as `&amp;`).
pub fn frame_src(html: &str) -> String {
    let start = html.find(r#"<iframe id="game" src=""#).expect("an iframe") + r#"<iframe id="game" src=""#.len();
    let end = html[start..].find('"').unwrap();
    html[start..start + end].replace("&amp;", "&")
}

/// A minimal PNG: the signature and an IHDR chunk with the given size.
pub fn png(width: u32, height: u32) -> Vec<u8> {
    let mut bytes = b"\x89PNG\r\n\x1a\n".to_vec();
    bytes.extend_from_slice(&13u32.to_be_bytes());
    bytes.extend_from_slice(b"IHDR");
    bytes.extend_from_slice(&width.to_be_bytes());
    bytes.extend_from_slice(&height.to_be_bytes());
    bytes.extend_from_slice(&[8, 6, 0, 0, 0, 0, 0, 0, 0]);
    bytes
}

/// A Share zip in the contract's shape, with tiny fake build files and some harmless extras, plus mutators for every
/// bad case.
pub struct ShareZip {
    pub entries: Vec<(String, Vec<u8>)>,
    pub symlinks: Vec<(String, String)>,
    /// Renames applied to the finished bytes (same length), to make names the writer refuses (duplicates).
    pub patches: Vec<(String, String)>,
}

impl ShareZip {
    pub fn new(title: &str) -> ShareZip {
        let name = "game";
        ShareZip {
            entries: vec![
                ("daedalus.json".into(), format!(r#"{{"format": 1, "title": {}}}"#, serde_json::to_string(title).unwrap()).into_bytes()),
                ("screenshot.png".into(), png(1280, 720)),
                (format!("Build/{name}.loader.js"), b"// loader".to_vec()),
                (format!("Build/{name}.data.br"), b"data".to_vec()),
                (format!("Build/{name}.framework.js.br"), b"framework".to_vec()),
                (format!("Build/{name}.wasm.br"), b"wasm".to_vec()),
                ("index.html".into(), b"<html>student page</html>".to_vec()),
                ("TemplateData/style.css".into(), b"body{}".to_vec()),
            ],
            symlinks: Vec::new(),
            patches: Vec::new(),
        }
    }

    pub fn set(mut self, name: &str, bytes: Vec<u8>) -> Self {
        self.entries.retain(|(n, _)| n != name);
        self.entries.push((name.into(), bytes));
        self
    }

    pub fn without(mut self, name: &str) -> Self {
        self.entries.retain(|(n, _)| n != name);
        self
    }

    pub fn rename_all(mut self, f: impl Fn(&str) -> String) -> Self {
        self.entries = self.entries.into_iter().map(|(n, b)| (f(&n), b)).collect();
        self
    }

    pub fn symlink(mut self, name: &str, target: &str) -> Self {
        self.symlinks.push((name.into(), target.into()));
        self
    }

    pub fn build(&self) -> Vec<u8> {
        let mut writer = ZipWriter::new(Cursor::new(Vec::new()));
        let options = SimpleFileOptions::default().compression_method(CompressionMethod::Deflated);
        for (name, bytes) in &self.entries {
            writer.start_file(name, options).unwrap();
            writer.write_all(bytes).unwrap();
        }
        for (name, target) in &self.symlinks {
            writer.add_symlink(name, target, options).unwrap();
        }
        let mut bytes = writer.finish().unwrap().into_inner();
        for (from, to) in &self.patches {
            assert_eq!(from.len(), to.len());
            replace_all(&mut bytes, from.as_bytes(), to.as_bytes());
        }
        bytes
    }
}

fn replace_all(haystack: &mut [u8], from: &[u8], to: &[u8]) {
    let mut i = 0;
    while i + from.len() <= haystack.len() {
        if &haystack[i..i + from.len()] == from {
            haystack[i..i + from.len()].copy_from_slice(to);
            i += from.len();
        } else {
            i += 1;
        }
    }
}
