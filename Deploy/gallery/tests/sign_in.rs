//! Signing in and out, the session cookie and its timeouts, the sign-in delay, and known browsers.

mod support;

use std::sync::atomic::Ordering;

use axum::http::StatusCode;
use gallery::config::unix_now;
use gallery::db::Role;
use gallery::session::token_hash;
use gallery::strings as s;
use support::*;

async fn try_sign_in(app: &TestApp, login: &str, password: &str, cookie: Option<&str>) -> Answer {
    app.post_form("/login", cookie, &format!("login={login}&password={password}")).await
}

#[tokio::test]
async fn the_right_password_signs_in_with_a_host_cookie() {
    let app = TestApp::new().await;
    app.add_user("kim", "김민수", Role::Student, "bear-moon-234").await;
    let answer = try_sign_in(&app, "Kim", "bear-moon-234", None).await;
    assert_eq!(answer.status, StatusCode::SEE_OTHER);
    assert_eq!(answer.header("location"), Some("/"));
    let session = answer.all("set-cookie").into_iter().find(|c| c.starts_with("__Host-gallery_session=")).unwrap();
    for part in ["HttpOnly", "Secure", "SameSite=Lax", "Path=/"] {
        assert!(session.contains(part), "{session}");
    }
    assert!(!session.contains("Max-Age") && !session.contains("Domain"), "a browser-session cookie: {session}");
    let home = app.get(GALLERY, "/", Some(&answer.cookie("__Host-gallery_session").unwrap())).await;
    assert_eq!(home.status, StatusCode::OK);
    assert!(home.text().contains(&s::signed_in_as("김민수")));
}

#[tokio::test]
async fn a_wrong_password_is_refused_then_slowed() {
    let app = TestApp::new().await;
    app.add_user("kim", "김민수", Role::Student, "bear-moon-234").await;
    let wrong = try_sign_in(&app, "kim", "nope", None).await;
    assert_eq!(wrong.status, StatusCode::UNAUTHORIZED);
    assert!(wrong.text().contains(s::LOGIN_WRONG));
    let early = try_sign_in(&app, "kim", "bear-moon-234", None).await;
    assert_eq!(early.status, StatusCode::TOO_MANY_REQUESTS);
    assert!(early.text().contains(&s::wait_seconds(1)));
}

#[tokio::test]
async fn an_unknown_id_is_answered_without_hashing() {
    let app = TestApp::new().await;
    let before = app.state.hasher.runs.load(Ordering::Relaxed);
    let answer = try_sign_in(&app, "nobody", "x", None).await;
    assert_eq!(answer.status, StatusCode::UNAUTHORIZED);
    assert!(answer.text().contains(s::LOGIN_WRONG));
    let bad_id = try_sign_in(&app, "no%20body", "x", None).await;
    assert!(bad_id.text().contains(s::LOGIN_WRONG));
    assert_eq!(app.state.hasher.runs.load(Ordering::Relaxed), before);
}

#[tokio::test]
async fn unknown_ids_cannot_flush_a_real_accounts_delay() {
    let app = TestApp::new().await;
    let id = app.add_user("kim", "김민수", Role::Student, "bear-moon-234").await;
    try_sign_in(&app, "kim", "nope", None).await;
    let now = unix_now();
    let wait = app.state.throttle.lock().unwrap().wait(id, now);
    assert!(wait.is_some());
    let size = app.state.throttle.lock().unwrap().len();
    for n in 0..10_001 {
        let answer = try_sign_in(&app, &format!("ghost{n}"), "x", None).await;
        assert_eq!(answer.status, StatusCode::UNAUTHORIZED);
    }
    assert_eq!(app.state.throttle.lock().unwrap().wait(id, now), wait, "kim's delay is unchanged");
    assert_eq!(app.state.throttle.lock().unwrap().len(), size, "unknown IDs never enter the map");
}

#[tokio::test]
async fn a_known_browser_skips_the_delay_for_its_own_login_only() {
    let app = TestApp::new().await;
    app.add_user("kim", "김민수", Role::Student, "bear-moon-234").await;
    app.add_user("lee", "이서연", Role::Student, "fox-sun-345").await;
    let first = try_sign_in(&app, "kim", "bear-moon-234", None).await;
    let known = first.cookie("__Host-gallery_known").expect("a known-browser cookie");
    let raw = first.all("set-cookie").into_iter().find(|c| c.starts_with("__Host-gallery_known=")).unwrap();
    assert!(raw.contains("HttpOnly") && raw.contains("Secure") && raw.contains("Max-Age="), "{raw}");

    // A classmate hammers kim's ID: kim is slowed for everyone else, but not on kim's own browser.
    try_sign_in(&app, "kim", "nope", None).await;
    assert_eq!(try_sign_in(&app, "kim", "bear-moon-234", None).await.status, StatusCode::TOO_MANY_REQUESTS);
    assert_eq!(try_sign_in(&app, "kim", "bear-moon-234", Some(&known)).await.status, StatusCode::SEE_OTHER);

    // kim's browser cookie doesn't skip lee's delay.
    try_sign_in(&app, "lee", "nope", None).await;
    assert_eq!(try_sign_in(&app, "lee", "fox-sun-345", Some(&known)).await.status, StatusCode::TOO_MANY_REQUESTS);
}

#[tokio::test]
async fn a_shared_laptop_stays_known_for_both_students() {
    let app = TestApp::new().await;
    app.add_user("kim", "김민수", Role::Student, "bear-moon-234").await;
    app.add_user("lee", "이서연", Role::Student, "fox-sun-345").await;
    let known = try_sign_in(&app, "kim", "bear-moon-234", None).await.cookie("__Host-gallery_known").unwrap();
    // lee signs in on the same browser after waiting out nothing: the token is kept and gains lee.
    let second = try_sign_in(&app, "lee", "fox-sun-345", Some(&known)).await;
    assert_eq!(second.cookie("__Host-gallery_known").as_deref(), Some(known.as_str()), "the same token is kept");
    try_sign_in(&app, "kim", "nope", None).await;
    assert_eq!(try_sign_in(&app, "kim", "bear-moon-234", Some(&known)).await.status, StatusCode::SEE_OTHER);
}

#[tokio::test]
async fn sessions_time_out_idle_and_absolute() {
    let app = TestApp::new().await;
    let cookie = app.student("kim", "김민수").await;
    let token = cookie.trim_start_matches("__Host-gallery_session=");
    let hash = token_hash(token);
    let now = unix_now() as i64;
    let set = |created: i64, seen: i64| {
        let db = app.state.db.clone();
        let hash = hash.clone();
        async move {
            db.delete_session(&hash).await.unwrap();
            let user = db.user_by_login("kim").await.unwrap().unwrap();
            db.insert_session(&hash, user.id, created).await.unwrap();
            db.touch_session(&hash, seen).await.unwrap();
        }
    };
    set(now - 2 * 3600, now - 10 * 60).await;
    assert_eq!(app.get(GALLERY, "/", Some(&cookie)).await.status, StatusCode::OK, "2 h old, active 10 min ago");
    set(now - 2 * 3600, now - 3600 - 60).await;
    assert_eq!(app.get(GALLERY, "/", Some(&cookie)).await.header("location"), Some("/login"), "idle 1 h 1 min");
    set(now - 12 * 3600 - 60, now - 60).await;
    assert_eq!(app.get(GALLERY, "/", Some(&cookie)).await.header("location"), Some("/login"), "12 h 1 min old");
}

#[tokio::test]
async fn sign_out_ends_the_session() {
    let app = TestApp::new().await;
    let cookie = app.student("kim", "김민수").await;
    let out = app.post_form("/logout", Some(&cookie), "").await;
    assert_eq!(out.status, StatusCode::SEE_OTHER);
    assert!(out.all("set-cookie").iter().any(|c| c.starts_with("__Host-gallery_session=;") && c.contains("Max-Age=0")));
    assert_eq!(app.get(GALLERY, "/", Some(&cookie)).await.header("location"), Some("/login"));
    assert_eq!(app.get(GALLERY, "/logout", Some(&cookie)).await.status, StatusCode::METHOD_NOT_ALLOWED);
}

#[tokio::test]
async fn a_teacher_reset_clears_the_delay() {
    let app = TestApp::new().await;
    let teacher = app.teacher().await;
    let id = app.add_user("kim", "김민수", Role::Student, "bear-moon-234").await;
    try_sign_in(&app, "kim", "nope", None).await;
    assert!(app.state.throttle.lock().unwrap().wait(id, unix_now()).is_some());
    let reset = app.post_form(&format!("/teacher/students/{id}/reset"), Some(&teacher), "").await;
    assert_eq!(reset.status, StatusCode::OK);
    assert!(app.state.throttle.lock().unwrap().wait(id, unix_now()).is_none());
}

#[tokio::test]
async fn a_full_hashing_queue_says_try_later() {
    let app = TestApp::new().await;
    app.add_user("kim", "김민수", Role::Student, "bear-moon-234").await;
    let held = app.state.hasher.permits.clone().acquire_many_owned(2).await.unwrap();
    let answer = try_sign_in(&app, "kim", "bear-moon-234", None).await;
    assert_eq!(answer.status, StatusCode::SERVICE_UNAVAILABLE);
    assert!(answer.text().contains(s::LOGIN_BUSY));
    drop(held);
}

#[tokio::test]
async fn the_login_page_is_korean_and_signed_in_users_skip_it() {
    let app = TestApp::new().await;
    let page = app.get(GALLERY, "/login", None).await;
    assert!(page.text().contains(r#"<html lang="ko">"#) && page.text().contains(s::LOGIN_BUTTON));
    assert!(page.header("content-type").unwrap().contains("charset=utf-8"));
    let cookie = app.student("kim", "김민수").await;
    assert_eq!(app.get(GALLERY, "/login", Some(&cookie)).await.header("location"), Some("/"));
}

#[tokio::test]
async fn a_burst_of_simultaneous_attempts_gets_one_password_check() {
    use axum::body::Body;
    use axum::http::Request;
    use tower::ServiceExt;
    let app = TestApp::new().await;
    app.add_user("kim", "김민수", Role::Student, "bear-moon-234").await;
    let before = app.state.hasher.runs.load(Ordering::Relaxed);
    let mut tasks = Vec::new();
    for _ in 0..20 {
        let router = app.router.clone();
        let req = Request::builder()
            .method("POST")
            .uri("/login")
            .header("host", GALLERY)
            .header("sec-fetch-site", "same-origin")
            .header("content-type", "application/x-www-form-urlencoded")
            .body(Body::from("login=kim&password=nope"))
            .unwrap();
        tasks.push(tokio::spawn(async move { router.oneshot(req).await.unwrap().status() }));
    }
    let mut statuses = Vec::new();
    for task in tasks {
        statuses.push(task.await.unwrap());
    }
    assert_eq!(app.state.hasher.runs.load(Ordering::Relaxed) - before, 1, "{statuses:?}");
    assert_eq!(statuses.iter().filter(|s| **s == StatusCode::UNAUTHORIZED).count(), 1);
    assert_eq!(statuses.iter().filter(|s| **s == StatusCode::TOO_MANY_REQUESTS).count(), 19);
}
