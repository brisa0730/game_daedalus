//! The Naver bucket copy (v0.23), with an in-memory bucket: publishing on upload, the player page's addresses and
//! CSP, the fallback, removal, and the reconcile at start.

mod support;

use std::sync::atomic::Ordering;
use std::time::Duration;

use axum::http::StatusCode;
use gallery::guard::PLAY_CSP;
use gallery::storage;
use support::*;

async fn bucket_app() -> (TestApp, String) {
    let app = TestApp::with_bucket().await;
    let kim = app.student("kim", "김민수").await;
    (app, kim)
}

fn origin() -> String {
    test_storage_config().public_origin()
}

async fn game(app: &TestApp, id: &str) -> gallery::db::Game {
    app.state.db.live_game(id).await.unwrap().expect("a live game")
}

async fn player(app: &TestApp, cookie: &str, id: &str, query: &str) -> Answer {
    let gate = app.open_game(cookie, id).await;
    app.get(GAMES, &format!("/g/{id}/{query}"), Some(&gate)).await
}

/// Waits (briefly) for a background bucket delete.
async fn until_gone(app: &TestApp, prefix: &str) {
    for _ in 0..200 {
        if app.bucket().keys(prefix).is_empty() {
            return;
        }
        tokio::time::sleep(Duration::from_millis(5)).await;
    }
    panic!("{prefix} still in the bucket: {:?}", app.bucket().keys(prefix));
}

#[tokio::test]
async fn an_upload_publishes_the_four_files_with_their_headers() {
    let (app, kim) = bucket_app().await;
    let id = app.new_game(&kim, "Spike Hell").await;
    let g = game(&app, &id).await;
    assert_eq!(g.published_version, Some(1));
    let prefix = storage::upload_prefix(&g);
    assert_eq!(prefix, format!("games/{id}/v1-{}/", g.updated_at));
    let keys = app.bucket().keys(&format!("games/{id}/"));
    assert_eq!(keys.len(), 4, "{keys:?}");
    let (body, h) = app.bucket().object(&format!("{prefix}Build/game.wasm.br")).unwrap();
    assert_eq!(body, b"wasm");
    assert_eq!((h.content_type, h.content_encoding), ("application/wasm", Some("br")));
    let (_, h) = app.bucket().object(&format!("{prefix}Build/game.loader.js")).unwrap();
    assert_eq!((h.content_type, h.content_encoding), ("application/javascript", None));
    let (_, h) = app.bucket().object(&format!("{prefix}Build/game.data.br")).unwrap();
    assert_eq!((h.content_type, h.content_encoding), ("application/octet-stream", Some("br")));
}

#[tokio::test]
async fn a_published_game_loads_from_the_bucket_with_a_fallback() {
    let (app, kim) = bucket_app().await;
    let id = app.new_game(&kim, "Spike Hell").await;
    let g = game(&app, &id).await;
    let page = player(&app, &kim, &id, "").await;
    let html = page.text();
    let base = format!("{}/{}Build/", origin(), storage::upload_prefix(&g));
    for (attr, file) in [("loader", "game.loader.js"), ("data", "game.data.br"), ("framework", "game.framework.js.br"), ("code", "game.wasm.br")] {
        assert!(html.contains(&format!(r#"data-{attr}="{base}{file}""#)), "{attr}: {html}");
    }
    assert!(html.contains(r#"data-fallback="1""#));
    let csp = page.all("content-security-policy").into_iter().find(|c| c.contains("default-src 'none'")).unwrap();
    assert_eq!(csp, gallery::guard::play_csp(Some(&origin())), "the page and the CSP name the same origin");
    assert!(csp.contains(&format!("'wasm-unsafe-eval' {};", origin())) && csp.contains(&format!("data: {};", origin())));

    let local = player(&app, &kim, &id, "?local=1").await.text();
    assert!(local.contains(r#"data-loader="Build/game.loader.js""#), "{local}");
    assert!(!local.contains("data-fallback"));
}

#[tokio::test]
async fn without_storage_a_published_row_still_plays_from_the_mac() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "Spike Hell").await;
    assert!(app.state.db.set_published(&id, 1).await.unwrap(), "a row marked published, as after storage was on");
    let page = player(&app, &kim, &id, "").await;
    assert!(page.text().contains(r#"data-loader="Build/game.loader.js""#));
    assert!(!page.text().contains("data-fallback"));
    assert!(page.all("content-security-policy").contains(&PLAY_CSP.to_string()), "today's CSP, unchanged");
}

#[tokio::test]
async fn an_update_publishes_the_new_upload_then_deletes_the_old_one() {
    let (app, kim) = bucket_app().await;
    let id = app.new_game(&kim, "Spike Hell").await;
    let old = storage::upload_prefix(&game(&app, &id).await);
    let answer = app.upload(&kim, &id, ShareZip::new("Spike Hell 2").build()).await;
    assert_eq!(answer.status, StatusCode::OK, "{}", answer.text());
    let g = game(&app, &id).await;
    assert_eq!((g.version, g.published_version), (2, Some(2)));
    assert!(app.bucket().keys(&old).is_empty(), "the old upload is gone");
    assert_eq!(app.bucket().keys(&storage::upload_prefix(&g)).len(), 4);
}

#[tokio::test]
async fn a_publish_finishing_late_records_nothing() {
    let (app, kim) = bucket_app().await;
    let id = app.new_game(&kim, "Spike Hell").await;
    app.upload(&kim, &id, ShareZip::new("Spike Hell 2").build()).await;
    assert!(!app.state.db.set_published(&id, 1).await.unwrap(), "a newer version is live");
    assert_eq!(game(&app, &id).await.published_version, Some(2));
    app.state.db.tombstone_game(&id, 1).await.unwrap();
    assert!(!app.state.db.set_published(&id, 2).await.unwrap(), "a removed game");
}

#[tokio::test]
async fn a_failed_publish_still_answers_ok_and_plays_from_the_mac() {
    let (app, kim) = bucket_app().await;
    app.bucket().fail_puts.store(true, Ordering::SeqCst);
    let id = app.new_game(&kim, "Spike Hell").await;
    assert_eq!(game(&app, &id).await.published_version, None);
    let html = player(&app, &kim, &id, "").await.text();
    assert!(html.contains(r#"data-loader="Build/game.loader.js""#) && !html.contains("data-fallback"));
}

#[tokio::test]
async fn a_request_dropped_mid_publish_leaves_the_game_unpublished() {
    let (app, kim) = bucket_app().await;
    app.bucket().hang_puts.store(true, Ordering::SeqCst);
    let dropped = tokio::time::timeout(Duration::from_millis(300), app.upload(&kim, "new", ShareZip::new("Spike Hell").build())).await;
    assert!(dropped.is_err(), "the publish never finished");
    let games = app.state.db.live_games().await.unwrap();
    assert_eq!(games.len(), 1, "the game itself was saved before the publish");
    assert_eq!(games[0].published_version, None);
    app.bucket().hang_puts.store(false, Ordering::SeqCst);
    let html = player(&app, &kim, &games[0].id, "").await.text();
    assert!(html.contains(r#"data-loader="Build/game.loader.js""#));
}

#[tokio::test]
async fn a_slow_bucket_still_answers_the_upload_in_time() {
    let app = TestApp::with_bucket_and(|c| c.publish_deadline = Duration::from_millis(200)).await;
    let kim = app.student("kim", "김민수").await;
    app.bucket().hang_puts.store(true, Ordering::SeqCst);
    let answer = tokio::time::timeout(Duration::from_secs(5), app.upload(&kim, "new", ShareZip::new("Spike Hell").build()))
        .await
        .expect("the upload answers by the publish deadline");
    assert_eq!(answer.status, StatusCode::OK, "{}", answer.text());
    let games = app.state.db.live_games().await.unwrap();
    assert_eq!(games[0].published_version, None, "unpublished: it plays from the Mac");
    app.bucket().hang_puts.store(false, Ordering::SeqCst);
}

#[tokio::test]
async fn clearing_a_publish_needs_the_same_version() {
    let (app, kim) = bucket_app().await;
    let id = app.new_game(&kim, "Spike Hell").await;
    assert_eq!(game(&app, &id).await.published_version, Some(1));
    app.state.db.clear_published(&id, 2).await.unwrap();
    assert_eq!(game(&app, &id).await.published_version, Some(1), "another version's clear leaves it alone");
    app.state.db.clear_published(&id, 1).await.unwrap();
    assert_eq!(game(&app, &id).await.published_version, None);
}

#[tokio::test]
async fn removing_a_game_deletes_its_copy_without_holding_the_lock() {
    let (app, kim) = bucket_app().await;
    let id = app.new_game(&kim, "Spike Hell").await;
    app.bucket().hold_deletes.store(true, Ordering::SeqCst);
    let done = tokio::time::timeout(Duration::from_secs(2), app.post_form(&format!("/games/{id}/remove"), Some(&kim), "")).await;
    assert_eq!(done.expect("removal doesn't wait for the bucket").status, StatusCode::SEE_OTHER);
    assert!(app.state.lock_games(std::slice::from_ref(&id)).is_some(), "the busy lock is already free");
    assert_eq!(app.bucket().keys(&format!("games/{id}/")).len(), 4, "the delete is still waiting");
    app.bucket().hold_deletes.store(false, Ordering::SeqCst);
    until_gone(&app, &format!("games/{id}/")).await;
}

#[tokio::test]
async fn removing_a_student_deletes_every_copy() {
    let (app, kim) = bucket_app().await;
    let teacher = app.teacher().await;
    let a = app.new_game(&kim, "One").await;
    let b = app.new_game(&kim, "Two").await;
    let user = app.state.db.user_by_login("kim").await.unwrap().unwrap().id;
    let done = app.post_form(&format!("/teacher/students/{user}/remove"), Some(&teacher), "").await;
    assert_eq!(done.status, StatusCode::SEE_OTHER);
    until_gone(&app, &format!("games/{a}/")).await;
    until_gone(&app, &format!("games/{b}/")).await;
}

#[tokio::test]
async fn reconcile_publishes_what_never_was() {
    let (app, kim) = bucket_app().await;
    app.bucket().fail_puts.store(true, Ordering::SeqCst);
    let id = app.new_game(&kim, "Spike Hell").await;
    app.bucket().fail_puts.store(false, Ordering::SeqCst);
    let done = storage::reconcile(&app.state).await;
    assert_eq!((done.repaired, done.failed), (1, 0));
    let g = game(&app, &id).await;
    assert_eq!(g.published_version, Some(1));
    assert_eq!(app.bucket().keys(&storage::upload_prefix(&g)).len(), 4);
}

#[tokio::test]
async fn reconcile_repairs_a_copy_the_column_only_claims() {
    let (app, kim) = bucket_app().await;
    let id = app.new_game(&kim, "Spike Hell").await;
    let g = game(&app, &id).await;
    app.bucket().remove(&format!("{}Build/game.wasm.br", storage::upload_prefix(&g)));
    let done = storage::reconcile(&app.state).await;
    assert_eq!(done.repaired, 1);
    assert_eq!(app.bucket().keys(&storage::upload_prefix(&g)).len(), 4);
    assert_eq!(game(&app, &id).await.published_version, Some(1));
    assert_eq!(storage::reconcile(&app.state).await.published, 1, "complete the second time");
}

#[tokio::test]
async fn reconcile_deletes_old_uploads_and_unknown_games() {
    let (app, kim) = bucket_app().await;
    let id = app.new_game(&kim, "Spike Hell").await;
    app.bucket().insert(&format!("games/{id}/v0-1/Build/game.wasm.br"));
    app.bucket().insert("games/zzzzzzzzzz/v1-1/Build/game.wasm.br");
    let removed = app.new_game(&kim, "Gone").await;
    app.state.db.tombstone_game(&removed, 1).await.unwrap();
    let done = storage::reconcile(&app.state).await;
    assert_eq!((done.published, done.deleted), (1, 3), "{done:?}");
    assert!(app.bucket().keys("games/zzzzzzzzzz/").is_empty());
    assert!(app.bucket().keys(&format!("games/{removed}/")).is_empty());
    assert_eq!(app.bucket().keys(&format!("games/{id}/")).len(), 4);
}

#[tokio::test]
async fn reconcile_skips_a_busy_game() {
    let (app, kim) = bucket_app().await;
    let id = app.new_game(&kim, "Spike Hell").await;
    let _busy = app.state.lock_games(std::slice::from_ref(&id)).unwrap();
    assert_eq!(storage::reconcile(&app.state).await.skipped, 1);
}

#[tokio::test]
async fn an_upload_between_the_listing_and_its_turn_keeps_its_copy() {
    let (app, kim) = bucket_app().await;
    let id = app.new_game(&kim, "Spike Hell").await;
    app.bucket().pause_listing.store(true, Ordering::SeqCst);
    let state = app.state.clone();
    let run = tokio::spawn(async move { storage::reconcile(&state).await });
    app.bucket().listed.notified().await;
    // The listing is taken (it names v1); now the student updates the game.
    let answer = app.upload(&kim, &id, ShareZip::new("Spike Hell 2").build()).await;
    assert_eq!(answer.status, StatusCode::OK);
    app.bucket().resume.notify_one();
    let done = run.await.unwrap();
    assert_eq!(done.failed, 0, "{done:?}");
    let g = game(&app, &id).await;
    assert_eq!((g.version, g.published_version), (2, Some(2)));
    assert_eq!(app.bucket().keys(&format!("games/{id}/")), app.bucket().keys(&storage::upload_prefix(&g)));
    assert_eq!(app.bucket().keys(&storage::upload_prefix(&g)).len(), 4);
}

#[test]
fn the_player_falls_back_on_every_kind_of_load_failure() {
    let script = include_str!("../static/player.js");
    for needle in [
        "showBanner: banner",
        r#"if (type === "error") { fail(); }"#,
        r#"location.replace(location.pathname + "?local=1")"#,
        "if (running) return;",
        "script.onerror = fail;",
        ".then(started).catch(fail)",
    ] {
        assert!(script.contains(needle), "{needle}");
    }
}
