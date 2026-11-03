//! The teacher page: students refused, preview errors, all-or-nothing creation with slips, resets and removals.

mod support;

use axum::http::{Method, StatusCode};
use gallery::app::{Access, gallery_routes};
use gallery::strings as s;
use support::*;

fn form_text(text: &str) -> String {
    format!("text={}", url_encode(text))
}

fn url_encode(text: &str) -> String {
    text.bytes()
        .map(|b| match b {
            b'a'..=b'z' | b'A'..=b'Z' | b'0'..=b'9' | b'-' | b'_' | b'.' => (b as char).to_string(),
            _ => format!("%{b:02X}"),
        })
        .collect()
}

/// The passwords printed on a slips page, in order.
fn slip_passwords(html: &str) -> Vec<String> {
    html.split(r#"<dd class="big">"#).skip(1).map(|p| p[..p.find('<').unwrap()].to_string()).collect::<Vec<_>>()
        .chunks(2).map(|pair| pair[1].clone()).collect()
}

#[tokio::test]
async fn students_are_refused_on_every_teacher_route() {
    let app = TestApp::new().await;
    let student = app.student("kim", "김민수").await;
    for route in gallery_routes().into_iter().filter(|r| r.access == Access::Teacher) {
        let path = route.path.replace("{id}", "1");
        let answer = if route.method == Method::POST {
            app.post_form(&path, Some(&student), "text=x").await
        } else {
            app.get(GALLERY, &path, Some(&student)).await
        };
        assert_eq!(answer.status, StatusCode::FORBIDDEN, "{path}");
    }
}

#[tokio::test]
async fn the_preview_marks_every_error() {
    let app = TestApp::new().await;
    let teacher = app.teacher().await;
    app.student("taken", "이미있음").await;
    let text = "ok1, 하나\nbad id, 둘\nnoname\nok1, 셋\ntaken, 넷";
    let answer = app.post_form("/teacher/students/preview", Some(&teacher), &form_text(text)).await;
    assert_eq!(answer.status, StatusCode::UNPROCESSABLE_ENTITY);
    let html = answer.text();
    for status in [s::LINE_OK, s::LINE_BAD_ID, s::LINE_NO_NAME, s::LINE_DUPLICATE, s::LINE_TAKEN] {
        assert!(html.contains(status), "{status}");
    }
    assert!(!html.contains(r#"action="/teacher/students/create""#), "no Create button while any line is wrong");
}

#[tokio::test]
async fn create_refuses_any_error_and_creates_nothing() {
    let app = TestApp::new().await;
    let teacher = app.teacher().await;
    let answer = app.post_form("/teacher/students/create", Some(&teacher), &form_text("ok1, 하나\nbad id, 둘")).await;
    assert_eq!(answer.status, StatusCode::UNPROCESSABLE_ENTITY);
    assert!(answer.text().contains(s::FIX_ERRORS));
    assert!(app.state.db.user_by_login("ok1").await.unwrap().is_none());
}

#[tokio::test]
async fn create_makes_accounts_whose_slips_sign_in() {
    let app = TestApp::new().await;
    let teacher = app.teacher().await;
    let preview = app.post_form("/teacher/students/preview", Some(&teacher), &form_text("kim, 김민수\nlee\t이서연")).await;
    assert_eq!(preview.status, StatusCode::OK);
    assert!(preview.text().contains(r#"action="/teacher/students/create""#));
    let slips = app.post_form("/teacher/students/create", Some(&teacher), &form_text("kim, 김민수\nlee\t이서연")).await;
    assert_eq!(slips.status, StatusCode::OK);
    assert_eq!(slips.header("cache-control"), Some("no-store"));
    let html = slips.text();
    assert!(html.contains("https://suwon.orcarin.com") && html.contains("김민수") && html.contains("이서연"));
    let passwords = slip_passwords(&html);
    assert_eq!(passwords.len(), 2);
    let kim = app.sign_in("kim", &passwords[0]).await;
    let home = app.get(GALLERY, "/", Some(&kim)).await;
    assert!(home.text().contains(&s::signed_in_as("김민수")));
    app.sign_in("lee", &passwords[1]).await;
}

#[tokio::test]
async fn the_add_one_form_previews_one_line() {
    let app = TestApp::new().await;
    let teacher = app.teacher().await;
    let answer = app.post_form("/teacher/students/preview", Some(&teacher), "id=park&name=%EB%B0%95").await;
    assert_eq!(answer.status, StatusCode::OK);
    assert!(answer.text().contains("park") && answer.text().contains(s::LINE_OK));
}

#[tokio::test]
async fn reset_ends_old_sessions_and_the_new_password_works() {
    let app = TestApp::new().await;
    let teacher = app.teacher().await;
    app.add_user("kim", "김민수", gallery::db::Role::Student, "pw-pw-234").await;
    let first = app.post_form("/login", None, "login=kim&password=pw-pw-234").await;
    let kim = first.cookie("__Host-gallery_session").unwrap();
    let known = first.cookie("__Host-gallery_known").unwrap();
    let known_hash = gallery::session::token_hash(known.trim_start_matches("__Host-gallery_known="));
    let id = app.state.db.user_by_login("kim").await.unwrap().unwrap().id;
    assert!(app.state.db.browser_known(&known_hash, id, 0).await.unwrap());
    let page = app.post_form(&format!("/teacher/students/{id}/reset"), Some(&teacher), "").await;
    assert!(!app.state.db.browser_known(&known_hash, id, 0).await.unwrap(), "known browsers are forgotten");
    assert_eq!(page.status, StatusCode::OK);
    assert_eq!(page.header("cache-control"), Some("no-store"));
    assert_eq!(app.get(GALLERY, "/", Some(&kim)).await.header("location"), Some("/login"));
    let password = slip_passwords(&page.text()).remove(0);
    app.sign_in("kim", &password).await;
}

#[tokio::test]
async fn removing_an_account_removes_its_games_and_keeps_their_ids_taken() {
    let app = TestApp::new().await;
    let teacher = app.teacher().await;
    let kim = app.student("kim", "김민수").await;
    let game = app.new_game(&kim, "Spike Hell").await;
    let id = app.state.db.user_by_login("kim").await.unwrap().unwrap().id;
    let confirm = app.get(GALLERY, &format!("/teacher/students/{id}/remove"), Some(&teacher)).await;
    assert!(confirm.text().contains(&s::remove_account_confirm("김민수", 1)));
    let done = app.post_form(&format!("/teacher/students/{id}/remove"), Some(&teacher), "").await;
    assert_eq!(done.status, StatusCode::SEE_OTHER);
    assert!(app.state.db.user_by_id(id).await.unwrap().is_none());
    assert_eq!(app.get(GALLERY, "/", Some(&kim)).await.header("location"), Some("/login"));
    assert!(app.state.db.live_game(&game).await.unwrap().is_none());
    assert!(app.state.db.game_id_used(&game).await.unwrap());
    assert!(!app.dir_has_game(&game));
}

#[tokio::test]
async fn removing_an_account_waits_for_nothing_while_its_game_uploads() {
    let app = TestApp::new().await;
    let teacher = app.teacher().await;
    let kim = app.student("kim", "김민수").await;
    let game = app.new_game(&kim, "Spike Hell").await;
    let id = app.state.db.user_by_login("kim").await.unwrap().unwrap().id;
    let busy = app.state.lock_games(std::slice::from_ref(&game)).unwrap();
    let refused = app.post_form(&format!("/teacher/students/{id}/remove"), Some(&teacher), "").await;
    assert_eq!(refused.status, StatusCode::CONFLICT);
    assert!(app.state.db.user_by_id(id).await.unwrap().is_some());
    drop(busy);
}

#[tokio::test]
async fn hostile_names_are_escaped_on_the_teacher_page() {
    let app = TestApp::new().await;
    let teacher = app.teacher().await;
    app.student("kim", "<b>x</b>").await;
    let html = app.get(GALLERY, "/teacher", Some(&teacher)).await.text();
    assert!(html.contains("&lt;b&gt;x&lt;/b&gt;") && !html.contains("<b>x</b>"));
}

trait GameDirs {
    fn dir_has_game(&self, id: &str) -> bool;
}

impl GameDirs for TestApp {
    fn dir_has_game(&self, id: &str) -> bool {
        self.state.config.games_dir().join(id).exists()
    }
}
