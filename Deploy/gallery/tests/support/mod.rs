//! A test app on a temporary data folder, request helpers, and a synthetic Share zip builder. No binary fixture is
//! committed: every zip is built here, in memory.
#![allow(dead_code)]

use std::io::{Cursor, Write};

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

pub const GALLERY: &str = GALLERY_HOST;
pub const GAMES: &str = GAMES_HOST;
pub const ORIGIN: &str = "https://suwon.orcarin.com";

pub struct TestApp {
    pub dir: tempfile::TempDir,
    pub state: AppState,
    pub router: Router,
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
        TestApp { dir, state, router }
    }

    /// A fresh app state over the same data folder: what a restart looks like.
    pub async fn restart(&self) -> TestApp {
        let config = (*self.state.config).clone();
        let db = Db::open(&config.db_path()).await.unwrap();
        let state = AppState::new(config, db);
        let router = router(state.clone());
        TestApp { dir: tempfile::tempdir().unwrap(), state, router }
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
        let answer = self.post_form("/login", None, &format!("login={login}&password={password}")).await;
        assert_eq!(answer.status, StatusCode::SEE_OTHER, "sign-in failed: {}", answer.text());
        answer.cookie("__Host-gallery_session").expect("a session cookie")
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
