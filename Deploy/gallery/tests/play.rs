//! The games host: frame-only entry, the grant → gate cookie → clean address flow, the server's player page, the
//! four build files with the spike's headers, and the play CSP with its sandbox.

mod support;

use axum::http::StatusCode;
use gallery::config::unix_now;
use gallery::play_token::{ENTER_TTL_SECS, Purpose};
use gallery::strings as s;
use support::*;

async fn app_with_game() -> (TestApp, String, String) {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "Spike Hell").await;
    (app, kim, id)
}

fn grant(app: &TestApp, id: &str) -> String {
    app.state.key.sign(Purpose::Enter, id, unix_now() + ENTER_TTL_SECS)
}

#[tokio::test]
async fn a_good_grant_sets_the_gate_and_goes_to_the_clean_address() {
    let (app, _kim, id) = app_with_game().await;
    let answer = app.enter(&format!("/enter/{id}?grant={}", grant(&app, &id)), "iframe", Some("same-site")).await;
    assert_eq!(answer.status, StatusCode::FOUND);
    assert_eq!(answer.header("location"), Some(format!("/g/{id}/").as_str()));
    let raw = answer.all("set-cookie").into_iter().find(|c| c.starts_with(&format!("__Host-g_{id}="))).unwrap();
    for part in ["Secure", "HttpOnly", "SameSite=Lax", "Path=/", "Max-Age=7200"] {
        assert!(raw.contains(part), "{raw}");
    }
    assert!(!raw.contains("Domain"));
}

#[tokio::test]
async fn entry_works_only_as_a_frame() {
    let (app, _kim, id) = app_with_game().await;
    let path = format!("/enter/{id}?grant={}", grant(&app, &id));
    for (dest, site) in [("document", Some("same-site")), ("iframe", Some("cross-site")), ("iframe", Some("none"))] {
        let answer = app.enter(&path, dest, site).await;
        assert_eq!(answer.status, StatusCode::FORBIDDEN, "{dest} {site:?}");
        assert!(answer.cookie(&format!("__Host-g_{id}")).is_none());
    }
    let no_dest = app.get(GAMES, &path, None).await;
    assert_eq!(no_dest.status, StatusCode::FORBIDDEN);
    assert!(no_dest.text().contains(s::REOPEN_FROM_GALLERY));
    assert!(no_dest.text().contains("color:#fff"), "readable inside the gallery's black frame");
}

#[tokio::test]
async fn bad_grants_are_refused() {
    let (app, _kim, id) = app_with_game().await;
    let expired = app.state.key.sign(Purpose::Enter, &id, unix_now() - 1);
    let other = app.state.key.sign(Purpose::Enter, "aaaaaaaaaa", unix_now() + 60);
    let gate_kind = app.state.key.sign(Purpose::Gate, &id, unix_now() + 60);
    for g in [expired, other, gate_kind, "123.abc".to_string(), String::new()] {
        let answer = app.enter(&format!("/enter/{id}?grant={g}"), "iframe", Some("same-site")).await;
        assert_eq!(answer.status, StatusCode::FORBIDDEN, "{g}");
    }
}

#[tokio::test]
async fn a_gate_opens_only_its_own_game() {
    let (app, kim, a) = app_with_game().await;
    let b = app.new_game(&kim, "Other").await;
    let gate_a = app.open_game(&kim, &a).await;
    assert_eq!(app.get(GAMES, &format!("/g/{a}/"), Some(&gate_a)).await.status, StatusCode::OK);
    let refused = app.get(GAMES, &format!("/g/{b}/"), Some(&gate_a)).await;
    assert_eq!(refused.status, StatusCode::FORBIDDEN);
    assert!(refused.text().contains(s::REOPEN_FROM_GALLERY));
    assert_eq!(app.get(GAMES, &format!("/g/{a}/"), None).await.status, StatusCode::FORBIDDEN);
}

#[tokio::test]
async fn a_restart_ends_old_gates_and_reopening_works() {
    let (app, kim, id) = app_with_game().await;
    let gate = app.open_game(&kim, &id).await;
    let restarted = app.restart().await;
    assert_eq!(restarted.get(GAMES, &format!("/g/{id}/"), Some(&gate)).await.status, StatusCode::FORBIDDEN);
    let kim_again = restarted.sign_in("kim", "pw-pw-234").await;
    let fresh = restarted.open_game(&kim_again, &id).await;
    assert_eq!(restarted.get(GAMES, &format!("/g/{id}/"), Some(&fresh)).await.status, StatusCode::OK);
}

#[tokio::test]
async fn the_player_page_names_exactly_the_stored_files() {
    let (app, kim, id) = app_with_game().await;
    let gate = app.open_game(&kim, &id).await;
    let page = app.get(GAMES, &format!("/g/{id}/"), Some(&gate)).await;
    let html = page.text();
    for attr in [
        r#"data-loader="Build/game.loader.js""#,
        r#"data-data="Build/game.data.br""#,
        r#"data-framework="Build/game.framework.js.br""#,
        r#"data-code="Build/game.wasm.br""#,
        r#"<script src="player.js">"#,
        r#"<html lang="ko">"#,
    ] {
        assert!(html.contains(attr), "{attr}");
    }
    assert!(!html.contains("student page"), "the zip's index.html is never used");
    assert!(html.contains("background:#0b0f1c"), "the frame's background matches the gallery's theater while loading");
}

#[tokio::test]
async fn build_files_keep_the_spike_headers() {
    let (app, kim, id) = app_with_game().await;
    let gate = app.open_game(&kim, &id).await;
    let wasm = app.get(GAMES, &format!("/g/{id}/Build/game.wasm.br"), Some(&gate)).await;
    assert_eq!(wasm.status, StatusCode::OK);
    assert_eq!(wasm.all("content-encoding"), ["br"], "encoded exactly once");
    assert_eq!(wasm.header("content-type"), Some("application/wasm"));
    assert_eq!(wasm.header("vary"), Some("Accept-Encoding"));
    assert_eq!(wasm.header("cache-control"), Some("private, no-cache, no-transform"));
    let loader = app.get(GAMES, &format!("/g/{id}/Build/game.loader.js"), Some(&gate)).await;
    assert_eq!(loader.header("content-type"), Some("application/javascript"));
    assert!(loader.header("content-encoding").is_none());
}

#[tokio::test]
async fn nothing_else_from_the_zip_is_served() {
    let (app, kim, id) = app_with_game().await;
    let gate = app.open_game(&kim, &id).await;
    for path in [
        "index.html",
        "TemplateData/style.css",
        "Build/other.wasm.br",
        "Build/..%2f..%2fdaedalus.json",
        "%2e%2e/%2e%2e/gallery.db",
        "daedalus.json",
        "screenshot.png",
    ] {
        let answer = app.get(GAMES, &format!("/g/{id}/{path}"), Some(&gate)).await;
        assert_eq!(answer.status, StatusCode::NOT_FOUND, "{path}");
    }
}

#[tokio::test]
async fn the_play_csp_and_sandbox_are_on_every_response() {
    let (app, kim, id) = app_with_game().await;
    let gate = app.open_game(&kim, &id).await;
    let page = app.get(GAMES, &format!("/g/{id}/"), Some(&gate)).await;
    let script = app.get(GAMES, &format!("/g/{id}/player.js"), Some(&gate)).await;
    let file = app.get(GAMES, &format!("/g/{id}/Build/game.data.br"), Some(&gate)).await;
    let forbidden = app.get(GAMES, &format!("/g/{id}/"), None).await;
    let missing = app.get(GAMES, "/g/zzzzzzzzzz/", None).await;
    for (name, answer) in [("page", &page), ("script", &script), ("file", &file), ("403", &forbidden), ("404", &missing)] {
        let csp = answer.all("content-security-policy").join(" | ");
        assert!(csp.contains("default-src 'none'") && csp.contains("frame-ancestors https://suwon.orcarin.com"), "{name}: {csp}");
        assert!(csp.contains("form-action 'none'"), "{name}");
        assert_eq!(answer.header("x-content-type-options"), Some("nosniff"), "{name}");
    }
    for (name, answer) in [("page", &page), ("403", &forbidden)] {
        let csp = answer.all("content-security-policy");
        assert!(csp.iter().any(|c| c == "sandbox allow-scripts allow-same-origin allow-pointer-lock"), "{name}: {csp:?}");
    }
    assert!(script.text().contains("createUnityInstance"));
}

#[tokio::test]
async fn build_files_answer_304_until_a_reupload() {
    let (app, kim, id) = app_with_game().await;
    let gate = app.open_game(&kim, &id).await;
    let path = format!("/g/{id}/Build/game.wasm.br");
    let first = app.get(GAMES, &path, Some(&gate)).await;
    let etag = first.header("etag").expect("an ETag").to_string();
    assert_eq!(etag, format!("\"{id}-v1\""));
    let revalidate = |tag: String| {
        let req = axum::http::Request::builder()
            .uri(path.clone())
            .header("host", GAMES)
            .header("cookie", gate.clone())
            .header("if-none-match", tag)
            .body(axum::body::Body::empty())
            .unwrap();
        app.send(req)
    };
    assert_eq!(first.header("accept-ranges"), Some("none"));
    let again = revalidate(etag.clone()).await;
    assert_eq!(again.status, StatusCode::NOT_MODIFIED);
    assert!(again.body.is_empty());
    // A range request (a browser resuming a half-cached file) always gets the whole file, never a 304.
    let resume = axum::http::Request::builder()
        .uri(path.clone())
        .header("host", GAMES)
        .header("cookie", gate.clone())
        .header("if-none-match", etag.clone())
        .header("range", "bytes=2-")
        .body(axum::body::Body::empty())
        .unwrap();
    let resumed = app.send(resume).await;
    assert_eq!(resumed.status, StatusCode::OK);
    assert_eq!(resumed.body, first.body, "the whole file");
    app.upload(&kim, &id, ShareZip::new("v2").build()).await;
    let after = revalidate(etag).await;
    assert_eq!(after.status, StatusCode::OK, "a re-upload changes the validator");
    assert_eq!(after.header("etag"), Some(format!("\"{id}-v2\"").as_str()));
}
