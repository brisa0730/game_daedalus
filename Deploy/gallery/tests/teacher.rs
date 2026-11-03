//! The teacher page: students refused, preview errors, all-or-nothing creation with slips, resets and removals.

mod support;

use axum::http::{Method, StatusCode};
use gallery::app::{Access, gallery_routes};
use gallery::strings as s;
use support::*;

fn form_text(text: &str) -> String {
    format!("text={}", url_encode(text))
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
    let text = "ok1, 하나, 일\nbad id, 둘, 이\nnoname\nnonick, 다섯\nok1, 셋, 삼\ntaken, 넷, 사";
    let answer = app.post_form("/teacher/students/preview", Some(&teacher), &form_text(text)).await;
    assert_eq!(answer.status, StatusCode::UNPROCESSABLE_ENTITY);
    let html = answer.text();
    for status in [s::LINE_OK, s::LINE_BAD_ID, s::LINE_NO_NAME, s::LINE_NO_NICKNAME, s::LINE_DUPLICATE, s::LINE_TAKEN] {
        assert!(html.contains(status), "{status}");
    }
    assert!(!html.contains(r#"action="/teacher/students/create""#), "no Create button while any line is wrong");
}

#[tokio::test]
async fn create_refuses_any_error_and_creates_nothing() {
    let app = TestApp::new().await;
    let teacher = app.teacher().await;
    let answer = app.post_form("/teacher/students/create", Some(&teacher), &form_text("ok1, 하나, 일\nbad id, 둘, 이")).await;
    assert_eq!(answer.status, StatusCode::UNPROCESSABLE_ENTITY);
    assert!(answer.text().contains(s::FIX_ERRORS));
    assert!(app.state.db.user_by_login("ok1").await.unwrap().is_none());
}

#[tokio::test]
async fn create_makes_accounts_whose_slips_sign_in() {
    let app = TestApp::new().await;
    let teacher = app.teacher().await;
    let text = form_text("kim, 김민수, 별빛\nlee\t이서연\t구름");
    let preview = app.post_form("/teacher/students/preview", Some(&teacher), &text).await;
    assert_eq!(preview.status, StatusCode::OK);
    assert!(preview.text().contains(r#"action="/teacher/students/create""#));
    let slips = app.post_form("/teacher/students/create", Some(&teacher), &text).await;
    assert_eq!(slips.status, StatusCode::OK);
    assert_eq!(slips.header("cache-control"), Some("no-store"));
    let html = slips.text();
    assert!(html.contains("https://suwon.orcarin.com") && html.contains("김민수") && html.contains("이서연"));
    assert!(html.contains("별빛") && html.contains("구름"), "each slip names the nickname beside the real name");
    let kim_row = app.state.db.user_by_login("kim").await.unwrap().unwrap();
    assert_eq!((kim_row.real_name.as_str(), kim_row.display_name.as_str()), ("김민수", "별빛"));
    let passwords = slip_passwords(&html);
    assert_eq!(passwords.len(), 2);
    let kim = app.sign_in("kim", &passwords[0]).await;
    let home = app.get(GALLERY, "/", Some(&kim)).await;
    assert!(home.text().contains(&s::signed_in_as("별빛")), "the header shows the nickname");
    app.sign_in("lee", &passwords[1]).await;
}

#[tokio::test]
async fn the_add_one_form_previews_one_line() {
    let app = TestApp::new().await;
    let teacher = app.teacher().await;
    let answer = app.post_form("/teacher/students/preview", Some(&teacher), "id=park&name=%EB%B0%95&nickname=%EB%B3%84").await;
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
    assert_eq!(app.get(GALLERY, "/upload", Some(&kim)).await.header("location"), Some("/login"));
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
    app.state.db.set_names(id, "김민수", "별빛").await.unwrap();
    let confirm = app.get(GALLERY, &format!("/teacher/students/{id}/remove"), Some(&teacher)).await;
    assert!(confirm.text().contains(&s::remove_account_confirm("김민수", "별빛", 1)));
    let done = app.post_form(&format!("/teacher/students/{id}/remove"), Some(&teacher), "").await;
    assert_eq!(done.status, StatusCode::SEE_OTHER);
    assert!(app.state.db.user_by_id(id).await.unwrap().is_none());
    assert_eq!(app.get(GALLERY, "/upload", Some(&kim)).await.header("location"), Some("/login"));
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

#[tokio::test]
async fn real_names_never_leave_the_teacher_pages() {
    let app = TestApp::new().await;
    let teacher = app.teacher().await;
    let created = app.post_form("/teacher/students/create", Some(&teacher), &form_text("kim, 김실명, 별빛")).await;
    let password = slip_passwords(&created.text()).remove(0);
    let kim = app.sign_in("kim", &password).await;
    let game = app.new_game(&kim, "Spike Hell").await;
    let id = app.state.db.user_by_login("kim").await.unwrap().unwrap().id;
    let lee = app.student("lee", "이도윤").await;

    assert!(created.text().contains("김실명"), "the slip");
    assert!(app.get(GALLERY, "/teacher", Some(&teacher)).await.text().contains("김실명"));
    assert!(app.get(GALLERY, &format!("/teacher/students/{id}/remove"), Some(&teacher)).await.text().contains("김실명"));

    let play = format!("/play/{game}");
    let remove_game = format!("/games/{game}/remove");
    for (path, cookie) in [
        ("/", None),
        ("/", Some(&lee)),
        ("/", Some(&kim)),
        (play.as_str(), None),
        (play.as_str(), Some(&lee)),
        (play.as_str(), Some(&kim)),
        ("/upload", Some(&kim)),
        ("/password", Some(&kim)),
        (remove_game.as_str(), Some(&kim)),
        ("/nothing-here", None),
    ] {
        let answer = app.get(GALLERY, path, cookie.map(String::as_str)).await;
        assert!(answer.status.is_success() || answer.status == StatusCode::NOT_FOUND, "{path}: {}", answer.status);
        let html = answer.text();
        assert!(!html.contains("김실명"), "{path} shows the real name");
        if path == "/" || path.starts_with("/play/") {
            assert!(html.contains("별빛"), "{path} shows the nickname");
        }
    }
}

#[tokio::test]
async fn rename_changes_the_nickname_everywhere() {
    let app = TestApp::new().await;
    let teacher = app.teacher().await;
    let kim = app.student("kim", "김민수").await;
    let game = app.new_game(&kim, "Spike Hell").await;
    let id = app.state.db.user_by_login("kim").await.unwrap().unwrap().id;
    let rename = |body: &'static str| {
        let path = format!("/teacher/students/{id}/rename");
        let teacher = teacher.clone();
        let app = &app;
        async move { app.post_form(&path, Some(&teacher), body).await }
    };

    let done = rename("real_name=%20%EA%B9%80%20%20%EC%8B%A4%EB%AA%85%20&nickname=%20%EB%B3%84%20%20%EB%B9%9B%20").await;
    assert_eq!(done.status, StatusCode::SEE_OTHER);
    assert_eq!(done.header("location"), Some("/teacher"));
    let row = app.state.db.user_by_id(id).await.unwrap().unwrap();
    assert_eq!((row.real_name.as_str(), row.display_name.as_str()), ("김 실명", "별 빛"), "both names are cleaned");

    let list = app.get(GALLERY, "/", None).await.text();
    let play = app.get(GALLERY, &format!("/play/{game}"), None).await.text();
    assert!(list.contains("별 빛") && !list.contains("김민수") && !list.contains("김 실명"));
    assert!(play.contains(r#"<span class="owner">별 빛</span>"#) && !play.contains("김민수"));
    assert!(app.get(GALLERY, "/teacher", Some(&teacher)).await.text().contains("김 실명"));

    for (body, message) in [
        ("real_name=&nickname=%EB%B3%84", s::LINE_NO_NAME),
        ("real_name=%EA%B9%80&nickname=%20%20", s::LINE_NO_NICKNAME),
    ] {
        let refused = rename(body).await;
        assert_eq!(refused.status, StatusCode::BAD_REQUEST, "{body}");
        assert!(refused.text().contains(message), "{body}");
    }
    let row = app.state.db.user_by_id(id).await.unwrap().unwrap();
    assert_eq!((row.real_name.as_str(), row.display_name.as_str()), ("김 실명", "별 빛"), "a refusal changes nothing");

    let teacher_id = app.state.db.user_by_login("teach").await.unwrap().unwrap().id;
    for target in [teacher_id.to_string(), "999".to_string(), "x".to_string()] {
        let answer = app.post_form(&format!("/teacher/students/{target}/rename"), Some(&teacher), "real_name=a&nickname=b").await;
        assert_eq!(answer.status, StatusCode::NOT_FOUND, "{target}");
    }
    assert_eq!(app.state.db.user_by_id(teacher_id).await.unwrap().unwrap().display_name, "선생님");

    rename("real_name=%3Cb%3Er%3C%2Fb%3E&nickname=%3Cb%3En%3C%2Fb%3E").await;
    let teacher_page = app.get(GALLERY, "/teacher", Some(&teacher)).await.text();
    let list = app.get(GALLERY, "/", None).await.text();
    for html in [&teacher_page, &list] {
        assert!(!html.contains("<b>"), "names are escaped");
    }
    assert!(list.contains("&lt;b&gt;n&lt;/b&gt;"));
}

trait GameDirs {
    fn dir_has_game(&self, id: &str) -> bool;
}

impl GameDirs for TestApp {
    fn dir_has_game(&self, id: &str) -> bool {
        self.state.config.games_dir().join(id).exists()
    }
}
