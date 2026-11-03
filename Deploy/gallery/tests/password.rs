//! Changing your own password: the page, the rule, the sign-in delay, session rotation, the race with a teacher
//! reset, a full hashing queue, and no password ever echoed. Every app here starts its delay at 60 s, so neither an
//! expected wait nor an expected "no wait" depends on a retry crossing a second.

mod support;

use axum::http::StatusCode;
use gallery::db::Role;
use gallery::session::token_hash;
use gallery::strings as s;
use support::*;

const OLD: &str = "bear-moon-234";

async fn app() -> TestApp {
    TestApp::with(|c| c.throttle_first_secs = 60).await
}

async fn change(app: &TestApp, cookie: &str, current: &str, new: &str, confirm: &str) -> Answer {
    let form = format!("current={}&new={}&confirm={}", url_encode(current), url_encode(new), url_encode(confirm));
    app.post_form("/password", Some(cookie), &form).await
}

async fn try_sign_in(app: &TestApp, login: &str, password: &str, known: Option<&str>) -> Answer {
    app.post_form("/login", known, &format!("login={}&password={}", url_encode(login), url_encode(password))).await
}

async fn user_id(app: &TestApp, login: &str) -> i64 {
    app.state.db.user_by_login(login).await.unwrap().unwrap().id
}

fn hash_of(cookie: &str) -> Vec<u8> {
    token_hash(cookie.split_once('=').unwrap().1)
}

/// No typed password in the body or any header of an answer.
fn assert_no_password(answer: &Answer, typed: &[&str]) {
    let headers: Vec<String> = answer.headers.values().map(|v| String::from_utf8_lossy(v.as_bytes()).into_owned()).collect();
    for password in typed {
        assert!(!answer.text().contains(password), "a password was echoed in the page");
        assert!(!headers.iter().any(|h| h.contains(password)), "a password was echoed in a header");
    }
}

#[tokio::test]
async fn the_page_has_three_password_fields_and_is_not_stored() {
    let app = app().await;
    let kim = app.student("kim", "김민수").await;
    let page = app.get(GALLERY, "/password", Some(&kim)).await;
    assert_eq!(page.status, StatusCode::OK);
    assert_eq!(page.text().matches(r#"type="password""#).count(), 3);
    assert!(page.text().contains(r#"name="username""#) && page.text().contains(r#"value="kim""#));
    assert_eq!(page.header("cache-control"), Some("no-store"));
}

#[tokio::test]
async fn a_wrong_current_password_is_slowed_like_a_sign_in() {
    let app = app().await;
    app.add_user("kim", "김민수", Role::Student, OLD).await;
    let kim = app.sign_in("kim", OLD).await;
    let wrong = change(&app, &kim, "nope-nope", "new-pass-123", "new-pass-123").await;
    assert_eq!(wrong.status, StatusCode::UNAUTHORIZED);
    assert!(wrong.text().contains(s::PASSWORD_WRONG_CURRENT));
    assert_no_password(&wrong, &["nope-nope", "new-pass-123"]);
    let again = change(&app, &kim, OLD, "new-pass-123", "new-pass-123").await;
    assert_eq!(again.status, StatusCode::TOO_MANY_REQUESTS, "an immediate retry waits");
    assert_eq!(try_sign_in(&app, "kim", OLD, None).await.status, StatusCode::TOO_MANY_REQUESTS, "one delay per account");
    app.clear_throttle(user_id(&app, "kim").await);
    app.sign_in("kim", OLD).await; // unchanged
}

#[tokio::test]
async fn a_known_browser_is_not_slowed() {
    let app = app().await;
    app.add_user("kim", "김민수", Role::Student, OLD).await;
    let (session, known) = app.sign_in_keeping_browser("kim", OLD, None).await;
    let cookie = format!("{session}; {known}");
    for _ in 0..2 {
        let wrong = change(&app, &cookie, "nope-nope", "new-pass-123", "new-pass-123").await;
        assert_eq!(wrong.status, StatusCode::UNAUTHORIZED);
        assert_no_password(&wrong, &["nope-nope", "new-pass-123"]);
    }
}

#[tokio::test]
async fn rule_mistakes_take_no_slot_and_no_hash() {
    let app = app().await;
    app.add_user("kimminsu", "김민수", Role::Student, OLD).await;
    let kim = app.sign_in("kimminsu", OLD).await;
    let long = "a".repeat(129);
    let cases = [
        ("new-pass-123", "new-pass-124", s::PASSWORD_MISMATCH),
        ("short12", "short12", s::PASSWORD_TOO_SHORT),
        (long.as_str(), long.as_str(), s::PASSWORD_TOO_LONG),
        ("KIMMINSU", "KIMMINSU", s::PASSWORD_SAME_AS_LOGIN),
        (OLD, OLD, s::PASSWORD_SAME_AS_CURRENT),
    ];
    let runs = app.state.hasher.runs.load(std::sync::atomic::Ordering::Relaxed);
    for (new, confirm, text) in cases {
        let answer = change(&app, &kim, OLD, new, confirm).await;
        assert_eq!(answer.status, StatusCode::BAD_REQUEST, "{text}");
        assert!(answer.text().contains(text), "{text}");
        assert_no_password(&answer, &[OLD, "new-pass-123", "short12", &long, "KIMMINSU"]);
    }
    assert_eq!(app.state.hasher.runs.load(std::sync::atomic::Ordering::Relaxed), runs, "no hash for a rule mistake");
    let wrong = change(&app, &kim, "nope-nope", "new-pass-123", "new-pass-123").await;
    assert_eq!(wrong.status, StatusCode::UNAUTHORIZED, "no throttle slot was taken");
    app.clear_throttle(user_id(&app, "kimminsu").await);
    app.sign_in("kimminsu", OLD).await; // unchanged
}

#[tokio::test]
async fn a_change_rotates_this_session_and_ends_every_other() {
    let app = app().await;
    app.add_user("kim", "김민수", Role::Student, OLD).await;
    let (a_session, a_known) = app.sign_in_keeping_browser("kim", OLD, None).await;
    let (b_session, b_known) = app.sign_in_keeping_browser("kim", OLD, None).await;
    let lee = app.student("lee", "이서연").await;
    let id = user_id(&app, "kim").await;

    let answer = change(&app, &format!("{a_session}; {a_known}"), OLD, "new-pass-123", "new-pass-123").await;
    assert_eq!(answer.status, StatusCode::SEE_OTHER);
    assert_eq!(answer.header("location"), Some("/password?changed=1"));
    assert_no_password(&answer, &[OLD, "new-pass-123"]);
    let rotated = answer.cookie("__Host-gallery_session").expect("a new session cookie");
    assert_ne!(rotated, a_session);

    assert_eq!(app.get(GALLERY, "/upload", Some(&rotated)).await.status, StatusCode::OK, "this browser stays signed in");
    let done = app.get(GALLERY, "/password?changed=1", Some(&rotated)).await;
    assert!(done.text().contains(s::PASSWORD_CHANGED));
    assert_eq!(app.get(GALLERY, "/upload", Some(&a_session)).await.header("location"), Some("/login"), "a copy of the old cookie is out");
    assert_eq!(app.get(GALLERY, "/upload", Some(&b_session)).await.header("location"), Some("/login"), "the other browser is out");
    assert_eq!(app.get(GALLERY, "/upload", Some(&lee)).await.status, StatusCode::OK, "other users are untouched");

    assert!(app.state.db.browser_known(&hash_of(&a_known), id, 0).await.unwrap(), "this browser stays known");
    assert!(!app.state.db.browser_known(&hash_of(&b_known), id, 0).await.unwrap(), "the other browser is forgotten");

    assert_eq!(try_sign_in(&app, "kim", "new-pass-123", None).await.status, StatusCode::SEE_OTHER);
    assert_eq!(try_sign_in(&app, "kim", OLD, None).await.status, StatusCode::UNAUTHORIZED);
}

#[tokio::test]
async fn korean_and_symbol_passwords_work() {
    for password in ["가나다라마바사아", "a&b+c%d= e f"] {
        let app = app().await;
        app.add_user("kim", "김민수", Role::Student, OLD).await;
        let kim = app.sign_in("kim", OLD).await;
        let answer = change(&app, &kim, OLD, password, password).await;
        assert_eq!(answer.status, StatusCode::SEE_OTHER, "{password}");
        assert_no_password(&answer, &[password]);
        app.sign_in("kim", password).await;
    }
}

#[tokio::test]
async fn the_teacher_can_change_their_own_password() {
    let app = app().await;
    let teacher = app.teacher().await;
    let answer = change(&app, &teacher, "pw-pw-234", "teacher-pass-9", "teacher-pass-9").await;
    assert_eq!(answer.status, StatusCode::SEE_OTHER);
    assert_no_password(&answer, &["pw-pw-234", "teacher-pass-9"]);
    app.sign_in("teach", "teacher-pass-9").await;
}

#[tokio::test]
async fn a_teacher_reset_still_works_after_a_self_change() {
    let app = app().await;
    let teacher = app.teacher().await;
    app.add_user("kim", "김민수", Role::Student, OLD).await;
    let kim = app.sign_in("kim", OLD).await;
    assert_eq!(change(&app, &kim, OLD, "mine-mine-123", "mine-mine-123").await.status, StatusCode::SEE_OTHER);
    let id = user_id(&app, "kim").await;
    let slip = app.post_form(&format!("/teacher/students/{id}/reset"), Some(&teacher), "").await.text();
    let generated = slip_passwords(&slip).remove(0);
    app.sign_in("kim", &generated).await;
    assert_eq!(try_sign_in(&app, "kim", "mine-mine-123", None).await.status, StatusCode::UNAUTHORIZED);
}

#[tokio::test]
async fn a_post_after_a_reset_goes_to_sign_in_and_the_reset_stands() {
    // The middleware path: the reset ended the session, so the request never reaches the change (the race guard
    // itself is proven at the database layer below).
    let app = app().await;
    let teacher = app.teacher().await;
    app.add_user("kim", "김민수", Role::Student, OLD).await;
    let kim = app.sign_in("kim", OLD).await;
    let id = user_id(&app, "kim").await;
    app.post_form(&format!("/teacher/students/{id}/reset"), Some(&teacher), "").await;
    let answer = change(&app, &kim, OLD, "mine-mine-123", "mine-mine-123").await;
    assert_eq!(answer.header("location"), Some("/login"));
    assert_eq!(try_sign_in(&app, "kim", "mine-mine-123", None).await.status, StatusCode::UNAUTHORIZED);
}

#[tokio::test]
async fn a_full_hashing_queue_answers_busy_and_takes_no_slot() {
    let app = app().await;
    app.add_user("kim", "김민수", Role::Student, OLD).await;
    let kim = app.sign_in("kim", OLD).await;
    let permits = app.state.config.argon2_permits as u32;
    let held = app.state.hasher.permits.clone().acquire_many_owned(permits).await.unwrap();
    let busy = change(&app, &kim, OLD, "new-pass-123", "new-pass-123").await;
    assert_eq!(busy.status, StatusCode::SERVICE_UNAVAILABLE);
    assert!(busy.text().contains(s::LOGIN_BUSY));
    drop(held);
    let wrong = change(&app, &kim, "nope-nope", "new-pass-123", "new-pass-123").await;
    assert_eq!(wrong.status, StatusCode::UNAUTHORIZED, "the reservation was released");
}

#[tokio::test]
async fn a_known_browsers_busy_answer_keeps_someone_elses_delay() {
    let app = app().await;
    app.add_user("kim", "김민수", Role::Student, OLD).await;
    let (session, known) = app.sign_in_keeping_browser("kim", OLD, None).await;
    // Someone else's wrong guess sets the account's delay.
    assert_eq!(try_sign_in(&app, "kim", "guess-guess", None).await.status, StatusCode::UNAUTHORIZED);
    let permits = app.state.config.argon2_permits as u32;
    let held = app.state.hasher.permits.clone().acquire_many_owned(permits).await.unwrap();
    let busy = change(&app, &format!("{session}; {known}"), OLD, "new-pass-123", "new-pass-123").await;
    assert_eq!(busy.status, StatusCode::SERVICE_UNAVAILABLE);
    drop(held);
    assert_eq!(try_sign_in(&app, "kim", "guess-again", None).await.status, StatusCode::TOO_MANY_REQUESTS, "the delay stands");
}

#[tokio::test]
async fn signed_in_pages_link_to_the_password_page() {
    let app = app().await;
    let kim = app.student("kim", "김민수").await;
    assert!(app.get(GALLERY, "/", Some(&kim)).await.text().contains(r#"href="/password""#));
}

// The database layer, where the race guard and the rotation live.

#[tokio::test]
async fn a_change_racing_a_reset_loses_to_the_reset() {
    let app = app().await;
    let db = &app.state.db;
    let id = app.add_user("kim", "김민수", Role::Student, OLD).await;
    db.insert_session(b"old-session", id, 100).await.unwrap();
    db.remember_browser(b"browser-a", id, 100).await.unwrap();
    db.set_password(id, "reset-hash").await.unwrap();
    let changed = db.change_own_password(id, "mine-hash", b"old-session", b"new-session", Some(b"browser-a"), 200).await.unwrap();
    assert!(!changed);
    assert_eq!(db.user_by_id(id).await.unwrap().unwrap().password_hash, "reset-hash");
    assert!(db.session(b"new-session").await.unwrap().is_none());
    assert!(db.session(b"old-session").await.unwrap().is_none());
    assert!(!db.browser_known(b"browser-a", id, 0).await.unwrap(), "as the reset left it");
}

#[tokio::test]
async fn a_change_keeps_the_start_time_and_this_browser_only() {
    let app = app().await;
    let db = &app.state.db;
    let id = app.add_user("kim", "김민수", Role::Student, OLD).await;
    db.insert_session(b"session-1", id, 100).await.unwrap();
    db.insert_session(b"session-2", id, 150).await.unwrap();
    db.remember_browser(b"browser-a", id, 100).await.unwrap();
    db.remember_browser(b"browser-b", id, 150).await.unwrap();
    assert!(db.change_own_password(id, "new-hash", b"session-1", b"session-3", Some(b"browser-a"), 500).await.unwrap());
    let session = db.session(b"session-3").await.unwrap().expect("the new session");
    assert_eq!((session.user_id, session.created_at, session.last_seen), (id, 100, 500), "the start time is kept");
    assert!(db.session(b"session-1").await.unwrap().is_none());
    assert!(db.session(b"session-2").await.unwrap().is_none());
    assert!(db.browser_known(b"browser-a", id, 0).await.unwrap());
    assert!(!db.browser_known(b"browser-b", id, 0).await.unwrap());
}

#[tokio::test]
async fn a_change_without_a_known_browser_forgets_them_all() {
    let app = app().await;
    let db = &app.state.db;
    let id = app.add_user("kim", "김민수", Role::Student, OLD).await;
    db.insert_session(b"session-1", id, 100).await.unwrap();
    db.remember_browser(b"browser-a", id, 100).await.unwrap();
    db.remember_browser(b"browser-b", id, 100).await.unwrap();
    assert!(db.change_own_password(id, "new-hash", b"session-1", b"session-2", None, 500).await.unwrap());
    assert!(!db.browser_known(b"browser-a", id, 0).await.unwrap());
    assert!(!db.browser_known(b"browser-b", id, 0).await.unwrap());
}
