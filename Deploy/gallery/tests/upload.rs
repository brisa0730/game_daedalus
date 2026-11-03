//! The Share zip check (one test per rejection, separate ones for symlinks, absolute paths and `..`), the zip bomb,
//! and the upload route: new games, replacing in place, the caps, removal races and the startup sweep.

mod support;

use std::io::Write;
use std::path::Path;

use axum::body::Body;
use axum::http::{Method, Request, StatusCode};
use gallery::config::Config;
use gallery::share_zip::{Limits, Reject, check_and_unpack};
use gallery::strings as s;
use gallery::upload::{sweep, valid_game_id};
use support::*;

fn check(zip: &ShareZip) -> (tempfile::TempDir, Result<gallery::share_zip::Unpacked, Reject>) {
    check_with(zip.build(), |_| {})
}

fn check_with(bytes: Vec<u8>, tweak: impl FnOnce(&mut Limits)) -> (tempfile::TempDir, Result<gallery::share_zip::Unpacked, Reject>) {
    let dir = tempfile::tempdir().unwrap();
    let zip_path = dir.path().join("upload.zip");
    std::fs::File::create(&zip_path).unwrap().write_all(&bytes).unwrap();
    let mut limits = Limits::from_config(&Config::new(dir.path()));
    tweak(&mut limits);
    let result = check_and_unpack(&zip_path, &dir.path().join("staging"), &limits);
    (dir, result)
}

fn rejected(zip: ShareZip) -> Reject {
    let (dir, result) = check(&zip);
    assert!(!dir.path().join("staging").exists(), "a rejection leaves nothing behind");
    result.expect_err("should be rejected")
}

fn files_under(root: &Path) -> Vec<String> {
    let mut out = Vec::new();
    for entry in std::fs::read_dir(root).unwrap() {
        let path = entry.unwrap().path();
        if path.is_dir() {
            for inner in files_under(&path) {
                out.push(format!("{}/{inner}", path.file_name().unwrap().to_string_lossy()));
            }
        } else {
            out.push(path.file_name().unwrap().to_string_lossy().into_owned());
        }
    }
    out.sort();
    out
}

#[test]
fn a_good_zip_unpacks_exactly_six_files() {
    let (dir, result) = check(&ShareZip::new("  Spike   Hell  "));
    let unpacked = result.unwrap();
    assert_eq!(unpacked.title, "Spike Hell");
    assert_eq!(unpacked.build_name, "game");
    let files = files_under(&dir.path().join("staging"));
    assert_eq!(
        files,
        ["Build/game.data.br", "Build/game.framework.js.br", "Build/game.loader.js", "Build/game.wasm.br", "daedalus.json", "screenshot.png"]
    );
    assert!(!files.iter().any(|f| f.contains("index.html") || f.contains("TemplateData")), "extras are ignored");
}

#[test]
fn not_a_zip() {
    let (_dir, result) = check_with(b"just text".to_vec(), |_| {});
    assert_eq!(result, Err(Reject::NotZip));
    let (_dir, result) = check_with(b"PK\x03\x04broken".to_vec(), |_| {});
    assert_eq!(result, Err(Reject::NotZip));
}

#[test]
fn a_symlink_rejects_the_zip() {
    assert_eq!(rejected(ShareZip::new("t").symlink("Build/link", "/etc/passwd")), Reject::Unsafe);
}

#[test]
fn an_absolute_path_rejects_the_zip() {
    assert_eq!(rejected(ShareZip::new("t").set("/etc/evil", b"x".to_vec())), Reject::Unsafe);
}

#[test]
fn a_parent_dir_entry_rejects_the_zip() {
    assert_eq!(rejected(ShareZip::new("t").set("Build/../../evil", b"x".to_vec())), Reject::Unsafe);
    assert_eq!(rejected(ShareZip::new("t").set("../evil", b"x".to_vec())), Reject::Unsafe);
}

#[test]
fn backslash_and_drive_names_reject_the_zip() {
    assert_eq!(rejected(ShareZip::new("t").set("Build\\evil", b"x".to_vec())), Reject::Unsafe);
    assert_eq!(rejected(ShareZip::new("t").set("C:/evil", b"x".to_vec())), Reject::Unsafe);
}

#[test]
fn a_duplicate_name_rejects_the_zip() {
    let mut zip = ShareZip::new("t").set("daedalus.jsoX", br#"{"format": 1, "title": "other"}"#.to_vec());
    zip.patches.push(("daedalus.jsoX".into(), "daedalus.json".into()));
    assert_eq!(rejected(zip), Reject::Unsafe);
}

#[test]
fn too_many_entries_reject_the_zip() {
    let mut zip = ShareZip::new("t");
    for n in 0..260 {
        zip = zip.set(&format!("TemplateData/f{n}.txt"), b"x".to_vec());
    }
    assert_eq!(rejected(zip), Reject::Unsafe);
}

#[test]
fn a_whole_unity_project() {
    let zip = ShareZip::new("t").rename_all(|n| format!("MyGame/{n}")).set("MyGame/Assets/Scenes/a.unity", b"x".to_vec());
    assert_eq!(rejected(zip), Reject::Project);
    assert_eq!(rejected(ShareZip::new("t").set("Packages/manifest.json", b"{}".to_vec())), Reject::Project);
}

#[test]
fn a_folder_of_screenshots() {
    let mut zip = ShareZip { entries: Vec::new(), symlinks: Vec::new(), patches: Vec::new() };
    zip = zip.set("shots/a.png", png(10, 10)).set("shots/b.JPG", b"x".to_vec());
    assert_eq!(rejected(zip), Reject::Screenshots);
}

#[test]
fn one_folder_too_many() {
    assert_eq!(rejected(ShareZip::new("t").rename_all(|n| format!("MyGame/{n}"))), Reject::Wrapped);
}

#[test]
fn a_wrapped_build_settings_folder_gets_the_build_settings_message() {
    let zip = ShareZip { entries: Vec::new(), symlinks: Vec::new(), patches: Vec::new() }
        .set("MyGame/index.html", b"<html>".to_vec())
        .set("MyGame/Build/MyGame.loader.js", b"x".to_vec())
        .set("MyGame/Build/MyGame.data.gz", b"x".to_vec())
        .set("MyGame/Build/MyGame.wasm.gz", b"x".to_vec())
        .set("MyGame/TemplateData/style.css", b"x".to_vec());
    assert_eq!(rejected(zip), Reject::BuildSettings);
}

#[test]
fn no_build_at_all() {
    let zip = ShareZip { entries: Vec::new(), symlinks: Vec::new(), patches: Vec::new() }
        .set("readme.txt", b"x".to_vec())
        .set("notes.md", b"x".to_vec());
    assert_eq!(rejected(zip), Reject::NotDaedalus);
}

#[test]
fn a_gzip_build_or_missing_metadata() {
    let gzip = ShareZip::new("t").without("Build/game.wasm.br").set("Build/game.wasm.gz", b"x".to_vec());
    assert_eq!(rejected(gzip), Reject::BuildSettings);
    let plain = ShareZip::new("t").set("Build/game.wasm", b"x".to_vec());
    assert_eq!(rejected(plain), Reject::BuildSettings);
    assert_eq!(rejected(ShareZip::new("t").without("daedalus.json")), Reject::BuildSettings);
}

#[test]
fn bad_or_newer_metadata() {
    assert_eq!(rejected(ShareZip::new("t").set("daedalus.json", b"{not json".to_vec())), Reject::Format);
    assert_eq!(rejected(ShareZip::new("t").set("daedalus.json", br#"{"format": 2, "title": "t"}"#.to_vec())), Reject::Format);
    let big = format!(r#"{{"format": 1, "title": "t", "pad": "{}"}}"#, "x".repeat(5000));
    assert_eq!(rejected(ShareZip::new("t").set("daedalus.json", big.into_bytes())), Reject::Format);
    let extra_keys = br#"{"format": 1, "title": "ok", "engine": "unity"}"#.to_vec();
    assert!(check(&ShareZip::new("t").set("daedalus.json", extra_keys)).1.is_ok(), "unknown keys are ignored");
}

#[test]
fn wrong_build_files() {
    assert_eq!(rejected(ShareZip::new("t").without("Build/game.data.br")), Reject::Reshare);
    assert_eq!(rejected(ShareZip::new("t").set("Build/other.data.br", b"x".to_vec())), Reject::Reshare);
    let mismatched = ShareZip::new("t").without("Build/game.wasm.br").set("Build/other.wasm.br", b"x".to_vec());
    assert_eq!(rejected(mismatched), Reject::Reshare);
    let bad_name = ShareZip::new("t").rename_all(|n| n.replace("Build/game.", "Build/ga me."));
    assert_eq!(rejected(bad_name), Reject::Reshare);
}

#[test]
fn bad_screenshots() {
    assert_eq!(rejected(ShareZip::new("t").without("screenshot.png")), Reject::Reshare);
    assert_eq!(rejected(ShareZip::new("t").set("screenshot.png", b"GIF89a not a png at all......".to_vec())), Reject::Reshare);
    assert_eq!(rejected(ShareZip::new("t").set("screenshot.png", png(1281, 720))), Reject::Reshare);
    assert_eq!(rejected(ShareZip::new("t").set("screenshot.png", png(1280, 721))), Reject::Reshare);
    let mut huge = png(100, 100);
    huge.resize(1_000_001, 0);
    assert_eq!(rejected(ShareZip::new("t").set("screenshot.png", huge)), Reject::Reshare);
}

#[test]
fn an_empty_title() {
    assert_eq!(rejected(ShareZip::new(" \u{202E}\t ")), Reject::Reshare);
}

#[test]
fn a_zip_bomb_stops_at_the_cap() {
    let bomb = ShareZip::new("t").set("Build/game.wasm.br", vec![0u8; 5_000_000]).build();
    assert!(bomb.len() < 100_000, "small compressed");
    let (dir, result) = check_with(bomb, |l| l.unpacked_bytes = 1_000_000);
    assert_eq!(result, Err(Reject::TooBig));
    assert!(!dir.path().join("staging").exists(), "no more than the cap is left on disk (nothing is)");
}

#[test]
fn every_rejection_has_a_korean_next_step() {
    for reject in [
        Reject::NotZip, Reject::Unsafe, Reject::Project, Reject::Screenshots, Reject::Wrapped,
        Reject::NotDaedalus, Reject::BuildSettings, Reject::Format, Reject::Reshare, Reject::TooBig,
    ] {
        let message = reject.message();
        assert!(message.chars().any(|c| ('\u{AC00}'..='\u{D7A3}').contains(&c)), "{message}");
    }
}

// The upload route

#[tokio::test]
async fn a_new_upload_creates_a_game() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let answer = app.upload(&kim, "new", ShareZip::new("Spike Hell").build()).await;
    assert_eq!(answer.status, StatusCode::OK);
    let play = answer.json()["play"].as_str().unwrap().to_string();
    let id = play.trim_start_matches("/play/");
    assert!(valid_game_id(id));
    let game = app.state.db.live_game(id).await.unwrap().unwrap();
    assert_eq!((game.title.as_str(), game.version, game.owner_name.as_deref()), ("Spike Hell", 1, Some("김민수")));
    assert!(app.state.config.version_dir(id, 1).join("Build/game.wasm.br").exists());
}

#[tokio::test]
async fn a_reupload_keeps_the_address_and_bumps_the_version() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "Spike Hell").await;
    let answer = app.upload(&kim, &id, ShareZip::new("Spike Hell 2").set("Build/game.wasm.br", b"new".to_vec()).build()).await;
    assert_eq!(answer.status, StatusCode::OK, "{}", answer.text());
    assert_eq!(answer.json()["play"], format!("/play/{id}"));
    let game = app.state.db.live_game(&id).await.unwrap().unwrap();
    assert_eq!((game.title.as_str(), game.version), ("Spike Hell 2", 2));
    assert!(!app.state.config.version_dir(&id, 1).exists(), "the old version is deleted");
    assert_eq!(std::fs::read(app.state.config.version_dir(&id, 2).join("Build/game.wasm.br")).unwrap(), b"new");
}

#[tokio::test]
async fn a_failed_reupload_leaves_the_old_version_playing() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "Spike Hell").await;
    let bad = app.upload(&kim, &id, ShareZip::new("t").without("daedalus.json").build()).await;
    assert_eq!(bad.status, StatusCode::UNPROCESSABLE_ENTITY);
    assert_eq!(bad.json()["message"], s::REJECT_BUILD_SETTINGS);
    let game = app.state.db.live_game(&id).await.unwrap().unwrap();
    assert_eq!((game.title.as_str(), game.version), ("Spike Hell", 1));
    assert_eq!(std::fs::read(app.state.config.version_dir(&id, 1).join("Build/game.wasm.br")).unwrap(), b"wasm");
    let gate = app.open_game(&kim, &id).await;
    let file = app.get(GAMES, &format!("/g/{id}/Build/game.wasm.br"), Some(&gate)).await;
    assert_eq!(file.body, b"wasm");
}

#[tokio::test]
async fn ids_are_never_reused() {
    let app = TestApp::with(|c| c.games_per_account = 100).await;
    let kim = app.student("kim", "김민수").await;
    let removed = app.new_game(&kim, "first").await;
    let done = app.post_form(&format!("/games/{removed}/remove"), Some(&kim), "").await;
    assert_eq!(done.status, StatusCode::SEE_OTHER);
    let mut seen = std::collections::HashSet::new();
    for n in 0..20 {
        let id = app.new_game(&kim, &format!("game {n}")).await;
        assert_ne!(id, removed);
        assert!(seen.insert(id));
    }
}

#[tokio::test]
async fn the_eleventh_game_is_refused() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    for n in 0..10 {
        app.new_game(&kim, &format!("g{n}")).await;
    }
    let answer = app.upload(&kim, "new", ShareZip::new("g10").build()).await;
    assert_eq!(answer.status, StatusCode::CONFLICT);
    assert_eq!(answer.json()["message"], s::too_many_games(10));
}

#[tokio::test]
async fn two_concurrent_new_uploads_at_nine_games() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    for n in 0..9 {
        app.new_game(&kim, &format!("g{n}")).await;
    }
    let (a, b) = tokio::join!(
        app.upload(&kim, "new", ShareZip::new("a").build()),
        app.upload(&kim, "new", ShareZip::new("b").build())
    );
    let statuses = [a.status, b.status];
    assert!(statuses.contains(&StatusCode::OK), "{statuses:?}");
    let loser = if a.status == StatusCode::OK { &b } else { &a };
    assert_eq!(loser.status, StatusCode::CONFLICT);
    // One upload at a time per student: the loser is told to wait, or, if it came after, about the cap.
    let message = loser.json()["message"].as_str().unwrap().to_string();
    assert!(message == s::UPLOAD_BUSY || message == s::too_many_games(10), "{message}");
    assert_eq!(app.state.db.live_game_count(1).await.unwrap() + app.state.db.live_game_count(2).await.unwrap(), 10);
    let folders = std::fs::read_dir(app.state.config.games_dir()).unwrap().count();
    assert_eq!(folders, 10, "the loser leaves no folder");
}

#[tokio::test]
async fn another_students_game_is_not_found() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let lee = app.student("lee", "이서연").await;
    let id = app.new_game(&kim, "mine").await;
    let answer = app.upload(&lee, &id, ShareZip::new("stolen").build()).await;
    assert_eq!(answer.status, StatusCode::NOT_FOUND);
    assert_eq!(app.upload(&lee, "zzzzzzzzzz", ShareZip::new("x").build()).await.status, StatusCode::NOT_FOUND);
    assert_eq!(app.upload(&lee, "../etc", ShareZip::new("x").build()).await.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn over_the_size_limit_by_length_or_by_stream() {
    let app = TestApp::with(|c| c.max_upload_bytes = 1000).await;
    let kim = app.student("kim", "김민수").await;
    let big = ShareZip::new("t").set("Build/game.wasm.br", (0..5000u32).map(|n| (n * 7919 % 251) as u8).collect()).build();
    assert!(big.len() > 1000);
    let by_length = app.upload(&kim, "new", big.clone()).await;
    assert_eq!(by_length.status, StatusCode::PAYLOAD_TOO_LARGE);
    assert_eq!(by_length.json()["message"], s::file_too_big(1000));
    // No Content-Length header (as with a chunked body): the handler counts the streamed bytes itself.
    let unannounced = Request::builder()
        .method(Method::POST)
        .uri("/upload?game=new")
        .header("host", GALLERY)
        .header("sec-fetch-site", "same-origin")
        .header("cookie", &kim)
        .body(Body::from(big))
        .unwrap();
    let answer = app.send(unannounced).await;
    assert_eq!(answer.status, StatusCode::PAYLOAD_TOO_LARGE);
    assert_eq!(std::fs::read_dir(app.state.config.staging_dir()).unwrap().count(), 0, "staging is cleaned");
}

#[tokio::test]
async fn removal_during_an_upload_is_refused() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "mine").await;
    let busy = app.state.lock_games(std::slice::from_ref(&id)).unwrap();
    let refused = app.post_form(&format!("/games/{id}/remove"), Some(&kim), "").await;
    assert_eq!(refused.status, StatusCode::CONFLICT);
    assert!(app.state.db.live_game(&id).await.unwrap().is_some());
    let second_upload = app.upload(&kim, &id, ShareZip::new("x").build()).await;
    assert_eq!(second_upload.status, StatusCode::CONFLICT);
    assert_eq!(second_upload.json()["message"], s::UPLOAD_BUSY);
    drop(busy);
    // A version update against a removed row changes nothing.
    app.state.db.tombstone_game(&id, 1).await.unwrap();
    assert!(!app.state.db.set_version(&id, 1, "x", "game", 1, 2).await.unwrap());
}

#[tokio::test]
async fn no_session_gets_the_json_401() {
    let app = TestApp::new().await;
    let req = Request::builder()
        .method(Method::POST)
        .uri("/upload?game=new")
        .header("host", GALLERY)
        .header("sec-fetch-site", "same-origin")
        .body(Body::from(ShareZip::new("t").build()))
        .unwrap();
    let answer = app.send(req).await;
    assert_eq!(answer.status, StatusCode::UNAUTHORIZED);
    assert_eq!(answer.json()["message"], s::SESSION_ENDED);
    assert_eq!(answer.json()["login"], true);
}

#[tokio::test]
async fn a_cross_site_upload_is_refused() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let req = Request::builder()
        .method(Method::POST)
        .uri("/upload?game=new")
        .header("host", GALLERY)
        .header("sec-fetch-site", "same-site")
        .header("cookie", &kim)
        .body(Body::from(ShareZip::new("t").build()))
        .unwrap();
    assert_eq!(app.send(req).await.status, StatusCode::FORBIDDEN);
    assert_eq!(app.state.db.live_games().await.unwrap().len(), 0);
}

#[tokio::test]
async fn the_disk_cap_is_refused() {
    let app = TestApp::with(|c| c.disk_cap_bytes = 50_000_000).await;
    let kim = app.student("kim", "김민수").await;
    let answer = app.upload(&kim, "new", ShareZip::new("t").build()).await;
    assert_eq!(answer.status, StatusCode::INSUFFICIENT_STORAGE);
    assert_eq!(answer.json()["message"], s::DISK_FULL);
}

#[tokio::test]
async fn the_upload_page_lists_only_my_games_and_carries_the_limit() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let lee = app.student("lee", "이서연").await;
    app.new_game(&kim, "Kim Game").await;
    app.new_game(&lee, "Lee Game").await;
    let html = app.get(GALLERY, "/upload", Some(&kim)).await.text();
    assert!(html.contains("Kim Game") && !html.contains("Lee Game"));
    assert!(html.contains(&format!(r#"data-max-bytes="{}""#, app.state.config.max_upload_bytes)));
    assert!(!html.contains("<script>") && !html.contains("onclick") && !html.contains("onsubmit"), "no inline script");
}

#[tokio::test]
async fn startup_sweep_removes_stale_folders() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "mine").await;
    let config = &app.state.config;
    std::fs::create_dir_all(config.version_dir(&id, 7)).unwrap();
    std::fs::create_dir_all(config.games_dir().join("orphan00000/v1")).unwrap();
    std::fs::create_dir_all(config.staging_dir().join("leftover")).unwrap();
    std::fs::write(config.staging_dir().join("x.zip"), b"x").unwrap();
    let (live, removed) = app.state.db.game_folders().await.unwrap();
    sweep(config, &live, &removed).unwrap();
    assert!(config.version_dir(&id, 1).exists());
    assert!(!config.version_dir(&id, 7).exists());
    assert!(!config.games_dir().join("orphan00000").exists());
    assert_eq!(std::fs::read_dir(config.staging_dir()).unwrap().count(), 0);
}

#[tokio::test]
async fn startup_sweep_leaves_plain_files_alone() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "mine").await;
    let config = &app.state.config;
    std::fs::write(config.games_dir().join(".gone"), b"x 1\n").unwrap();
    std::fs::write(config.games_dir().join(".DS_Store"), b"x").unwrap();
    std::fs::write(config.games_dir().join(&id).join(".DS_Store"), b"x").unwrap();
    let (live, removed) = app.state.db.game_folders().await.unwrap();
    sweep(config, &live, &removed).unwrap();
    assert!(config.version_dir(&id, 1).exists());
}

#[tokio::test]
async fn an_update_never_replaces_an_existing_version_folder() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "mine").await;
    let squatter = app.state.config.version_dir(&id, 2);
    std::fs::create_dir_all(&squatter).unwrap();
    std::fs::write(squatter.join("keep"), b"x").unwrap();
    let answer = app.upload(&kim, &id, ShareZip::new("v2").build()).await;
    assert_eq!(answer.status, StatusCode::INTERNAL_SERVER_ERROR);
    assert!(squatter.join("keep").exists(), "the existing folder is untouched");
    let game = app.state.db.live_game(&id).await.unwrap().unwrap();
    assert_eq!(game.version, 1);
    assert!(app.state.config.version_dir(&id, 1).join("Build/game.wasm.br").exists());
}

#[tokio::test]
async fn one_upload_at_a_time_per_student() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "mine").await;
    let kim_id = app.state.db.user_by_login("kim").await.unwrap().unwrap().id;
    let held = app.state.lock_games(&[gallery::upload::user_busy_key(kim_id)]).unwrap();
    for game in ["new", id.as_str()] {
        let answer = app.upload(&kim, game, ShareZip::new("x").build()).await;
        assert_eq!(answer.status, StatusCode::CONFLICT, "{game}");
        assert_eq!(answer.json()["message"], s::UPLOAD_BUSY);
    }
    drop(held);
    assert_eq!(app.upload(&kim, &id, ShareZip::new("x").build()).await.status, StatusCode::OK);
}
