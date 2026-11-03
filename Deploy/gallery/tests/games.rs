//! The game list (both wires: the title from the zip, the name from the account), screenshots, removal, and the
//! play view page.

mod support;

use axum::http::StatusCode;
use gallery::config::unix_now;
use gallery::play_token::Purpose;
use gallery::strings as s;
use support::*;

#[tokio::test]
async fn cards_show_the_zip_title_and_the_account_name() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let lee = app.student("lee", "이서연").await;
    app.new_game(&kim, "Spike Hell").await;
    app.new_game(&lee, "Floor Is Lava").await;
    let html = app.get(GALLERY, "/", Some(&kim)).await.text();
    assert!(html.contains("Spike Hell") && html.contains("김민수"));
    assert!(html.contains("Floor Is Lava") && html.contains("이서연"));
    let mine = &html[html.find(s::MY_GAMES).unwrap()..html.find(s::ALL_GAMES).unwrap()];
    assert!(mine.contains("Spike Hell") && !mine.contains("Floor Is Lava"), "My games shows only mine");
    let all = &html[html.find(s::ALL_GAMES).unwrap()..];
    assert!(all.contains(r#"<span class="owner">김민수</span>"#), "the card itself carries the account name");
    assert!(all.contains(r#"<span class="owner">이서연</span>"#));
    assert!(html.contains(r#"loading="lazy""#) && html.contains(r#"width="320""#) && html.contains(r#"height="180""#));
}

#[tokio::test]
async fn hostile_titles_and_names_are_escaped() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "<img src=x onerror=alert(1)>").await;
    app.new_game(&kim, "<script>alert(2)</script>").await;
    let html = app.get(GALLERY, "/", Some(&kim)).await.text();
    assert!(!html.contains("<script>alert(2)") && html.contains("&lt;script&gt;alert(2)&lt;/script&gt;"));
    assert!(!html.contains("<img src=x") && html.contains("&lt;img src=x onerror=alert(1)&gt;"));
}

#[tokio::test]
async fn screenshots_are_served_as_sandboxed_png() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "Spike Hell").await;
    let shot = app.get(GALLERY, &format!("/shots/{id}.png?v=1"), Some(&kim)).await;
    assert_eq!(shot.status, StatusCode::OK);
    assert_eq!(shot.header("content-type"), Some("image/png"));
    assert_eq!(shot.header("x-content-type-options"), Some("nosniff"));
    assert_eq!(shot.header("cache-control"), Some("private, max-age=31536000, immutable"));
    assert!(shot.all("content-security-policy").iter().any(|c| c == "sandbox"));
    assert_eq!(&shot.body[..8], b"\x89PNG\r\n\x1a\n");
    for bad in ["/shots/zzzzzzzzzz.png", "/shots/..%2f..%2fetc.png", &format!("/shots/{id}.jpg"), &format!("/shots/{id}")] {
        assert_eq!(app.get(GALLERY, bad, Some(&kim)).await.status, StatusCode::NOT_FOUND, "{bad}");
    }
}

#[tokio::test]
async fn a_removed_game_is_gone_everywhere() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "Spike Hell").await;
    let gate = app.open_game(&kim, &id).await;
    let confirm = app.get(GALLERY, &format!("/games/{id}/remove"), Some(&kim)).await;
    assert!(confirm.text().contains(&s::remove_game_confirm("Spike Hell")));
    assert_eq!(app.post_form(&format!("/games/{id}/remove"), Some(&kim), "").await.status, StatusCode::SEE_OTHER);
    assert!(!app.get(GALLERY, "/", Some(&kim)).await.text().contains("Spike Hell"));
    assert_eq!(app.get(GALLERY, &format!("/shots/{id}.png"), Some(&kim)).await.status, StatusCode::NOT_FOUND);
    assert_eq!(app.get(GALLERY, &format!("/play/{id}"), Some(&kim)).await.status, StatusCode::NOT_FOUND);
    assert_eq!(app.get(GAMES, &format!("/g/{id}/"), Some(&gate)).await.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn only_the_owner_or_the_teacher_removes_a_game() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let lee = app.student("lee", "이서연").await;
    let teacher = app.teacher().await;
    let id = app.new_game(&kim, "Spike Hell").await;
    assert_eq!(app.post_form(&format!("/games/{id}/remove"), Some(&lee), "").await.status, StatusCode::NOT_FOUND);
    assert_eq!(app.get(GALLERY, &format!("/games/{id}/remove"), Some(&lee)).await.status, StatusCode::NOT_FOUND);
    assert!(app.state.db.live_game(&id).await.unwrap().is_some());
    assert_eq!(app.post_form(&format!("/games/{id}/remove"), Some(&teacher), "").await.status, StatusCode::SEE_OTHER);
    assert!(app.state.db.live_game(&id).await.unwrap().is_none());
}

#[tokio::test]
async fn the_play_view_frames_the_game_with_a_fresh_grant() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "Spike Hell").await;
    let html = app.get(GALLERY, &format!("/play/{id}"), Some(&kim)).await.text();
    assert!(html.contains(r#"sandbox="allow-scripts allow-same-origin allow-pointer-lock""#));
    assert!(html.contains(r#"allow="fullscreen; gamepad; autoplay""#));
    assert!(html.contains(s::BACK_TO_GAMES) && html.contains(s::FULLSCREEN));
    let src = frame_src(&html);
    let prefix = format!("https://suwon-play.orcarin.com/enter/{id}?grant=");
    assert!(src.starts_with(&prefix), "{src}");
    let grant = &src[prefix.len()..];
    assert!(app.state.key.verify(Purpose::Enter, &id, grant, unix_now()));
    assert!(!app.state.key.verify(Purpose::Enter, "aaaaaaaaaa", grant, unix_now()));
    assert_eq!(app.get(GALLERY, "/play/zzzzzzzzzz", Some(&kim)).await.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn anyone_browses_and_plays_without_signing_in() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let lee = app.student("lee", "이서연").await;
    let id = app.new_game(&kim, "Spike Hell").await;
    app.new_game(&lee, "Floor Is Lava").await;

    let list = app.get(GALLERY, "/", None).await;
    assert_eq!(list.status, StatusCode::OK);
    let html = list.text();
    for text in ["Spike Hell", "김민수", "Floor Is Lava", "이서연", s::ALL_GAMES, r#"href="/login""#] {
        assert!(html.contains(text), "{text}");
    }
    for text in [s::MY_GAMES, r#"href="/upload""#, "/remove", s::SIGN_OUT, s::SHARED_LAPTOP_NOTE] {
        assert!(!html.contains(text), "{text}");
    }

    let shot = app.get(GALLERY, &format!("/shots/{id}.png?v=1"), None).await;
    assert_eq!(shot.status, StatusCode::OK);
    assert_eq!(shot.header("content-type"), Some("image/png"));

    let play = app.get(GALLERY, &format!("/play/{id}"), None).await;
    assert_eq!(play.status, StatusCode::OK);
    let play_html = play.text();
    assert!(play_html.contains(r#"href="/login""#) && play_html.contains(r#"id="fullscreen""#));
    let src = frame_src(&play_html);
    let grant = src.split_once("?grant=").unwrap().1.to_string();
    assert!(app.state.key.verify(Purpose::Enter, &id, &grant, unix_now()));
    let entered = app.enter(&format!("/enter/{id}?grant={grant}"), "iframe", Some("same-site")).await;
    assert_eq!(entered.status, StatusCode::FOUND, "a signed-out visitor's grant opens the game in the frame");
    assert!(entered.cookie(&format!("__Host-g_{id}")).is_some());
    let alone = app.enter(&format!("/enter/{id}?grant={grant}"), "document", Some("same-site")).await;
    assert_eq!(alone.status, StatusCode::FORBIDDEN, "still only inside the gallery's frame");

    for path in ["/upload", "/password", &format!("/games/{id}/remove"), "/teacher"] {
        let answer = app.get(GALLERY, path, None).await;
        assert_eq!(answer.status, StatusCode::SEE_OTHER, "{path}");
        assert_eq!(answer.header("location"), Some("/login"), "{path}");
    }
    let upload = app.post_form("/upload", None, "").await;
    assert_eq!(upload.status, StatusCode::UNAUTHORIZED);
}
