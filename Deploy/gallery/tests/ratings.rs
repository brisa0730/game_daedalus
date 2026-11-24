//! Star ratings (Phase 9): the anonymous rater cookie, the three-minute gate, replacing a rating, the owner's
//! refusal, the average on cards and the play page, anonymity of the stored rows, and removal.

mod support;

use axum::http::StatusCode;
use gallery::db::Role;
use gallery::ratings::RATER_COOKIE;
use gallery::strings as s;
use support::*;

/// A game by kim, and a fresh browser that opened it.
async fn game_and_rater(app: &TestApp) -> (String, String, String) {
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "Spike Hell").await;
    let (answer, rater) = app.open_rating(&id, None).await;
    assert_eq!(answer.status, StatusCode::OK, "{}", answer.text());
    (kim, id, rater.expect("a rater cookie"))
}

#[tokio::test]
async fn the_first_open_sets_the_cookie_and_later_opens_keep_the_clock() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "Spike Hell").await;
    let (answer, rater) = app.open_rating(&id, None).await;
    let set = answer.all("set-cookie").into_iter().find(|c| c.starts_with(RATER_COOKIE)).expect("Set-Cookie");
    for part in ["HttpOnly", "Secure", "SameSite=Lax", "Path=/", "Max-Age=31536000"] {
        assert!(set.contains(part), "{set}: {part}");
    }
    let wait = answer.json()["wait"].as_u64().unwrap();
    assert!((179..=180).contains(&wait), "{wait}");
    assert!(answer.json()["mine"].is_null());

    let rater = rater.unwrap();
    let (again, none) = app.open_rating(&id, Some(&rater)).await;
    assert_eq!(again.status, StatusCode::OK);
    assert!(none.is_none(), "a browser with a cookie gets no new one");
    app.backdate_open(&id, 100).await;
    let (later, _) = app.open_rating(&id, Some(&rater)).await;
    assert!(later.json()["wait"].as_u64().unwrap() <= 80, "reopening never restarts the clock");
}

#[tokio::test]
async fn a_rating_waits_for_the_gate() {
    let app = TestApp::new().await;
    let (_kim, id, rater) = game_and_rater(&app).await;
    let early = app.rate(&id, Some(&rater), 4).await;
    assert_eq!(early.status, StatusCode::CONFLICT);
    assert!(early.json()["wait"].as_u64().unwrap() > 170);
    // The exact boundary is pinned in db.rs; here a margin, because the clock can tick between backdate and handler.
    app.backdate_open(&id, 170).await;
    assert_eq!(app.rate(&id, Some(&rater), 4).await.status, StatusCode::CONFLICT);
    app.backdate_open(&id, 180).await;
    let ok = app.rate(&id, Some(&rater), 4).await;
    assert_eq!(ok.status, StatusCode::OK, "{}", ok.text());
    assert_eq!(ok.json()["mine"], 4);
    let no_cookie = app.rate(&id, None, 4).await;
    assert_eq!(no_cookie.status, StatusCode::CONFLICT);
    assert_eq!(no_cookie.json()["wait"], 180);
}

#[tokio::test]
async fn a_new_rating_replaces_the_old_one() {
    let app = TestApp::with(|c| c.rating_gate_secs = 0).await;
    let (_kim, id, rater) = game_and_rater(&app).await;
    assert_eq!(app.rate(&id, Some(&rater), 2).await.json()["mine"], 2);
    assert_eq!(app.rate(&id, Some(&rater), 5).await.json()["mine"], 5);
    assert_eq!(app.count_rows("SELECT COUNT(*) FROM ratings").await, 1);
    assert_eq!(app.count_rows("SELECT stars FROM ratings").await, 5);
    let (reopen, _) = app.open_rating(&id, Some(&rater)).await;
    assert_eq!(reopen.json()["mine"], 5);
}

#[tokio::test]
async fn only_stars_one_to_five_are_taken() {
    let app = TestApp::with(|c| c.rating_gate_secs = 0).await;
    let (_kim, id, rater) = game_and_rater(&app).await;
    for body in ["stars=0", "stars=6", "stars=x", "", "stars=4&x"] {
        let answer = app.post_form(&format!("/play/{id}/rate"), Some(&rater), body).await;
        assert_eq!(answer.status, StatusCode::BAD_REQUEST, "{body:?}");
    }
    let long = format!("stars=4&{}", "x".repeat(100));
    assert_eq!(app.post_form(&format!("/play/{id}/rate"), Some(&rater), &long).await.status, StatusCode::PAYLOAD_TOO_LARGE);
    assert_eq!(app.count_rows("SELECT COUNT(*) FROM ratings").await, 0);
}

#[tokio::test]
async fn the_owner_cant_rate_their_own_game() {
    let app = TestApp::with(|c| c.rating_gate_secs = 0).await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "Spike Hell").await;
    let (open, rater) = app.open_rating(&id, Some(&kim)).await;
    assert_eq!(open.status, StatusCode::FORBIDDEN);
    assert!(rater.is_none());
    assert_eq!(app.rate(&id, Some(&kim), 5).await.status, StatusCode::FORBIDDEN);
    assert_eq!(app.count_rows("SELECT COUNT(*) FROM game_opens").await, 0);

    let page = app.get(GALLERY, &format!("/play/{id}"), Some(&kim)).await.text();
    assert!(page.contains(s::RATE_OWN));
    assert!(!page.contains(r#"id="rate""#));

    // The same person as a guest (no session) is just a browser.
    let (_, guest) = app.open_rating(&id, None).await;
    let guest = guest.unwrap();
    assert_eq!(app.rate(&id, Some(&guest), 5).await.status, StatusCode::OK);
    // Signed in with that browser's rater cookie, the owner is refused again.
    assert_eq!(app.rate(&id, Some(&format!("{kim}; {guest}")), 4).await.status, StatusCode::FORBIDDEN);
}

#[tokio::test]
async fn the_average_shows_from_three_ratings() {
    let app = TestApp::with(|c| c.rating_gate_secs = 0).await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "Spike Hell").await;
    let mut last = None;
    for (n, stars) in [5, 4, 4, 4].into_iter().enumerate() {
        let (_, rater) = app.open_rating(&id, None).await;
        let answer = app.rate(&id, rater.as_deref(), stars).await;
        assert_eq!(answer.status, StatusCode::OK);
        let count = n as i64 + 1;
        if count < 3 {
            assert!(answer.json()["summary"].is_null(), "{count}");
            assert_eq!(answer.json()["hint"], s::average_after(3));
            for page in [app.get(GALLERY, "/", None).await, app.get(GALLERY, "/", Some(&kim)).await] {
                assert!(!page.text().contains('★'), "no average with {count} ratings");
            }
            assert!(!app.get(GALLERY, &format!("/play/{id}"), None).await.text().contains('★'));
        }
        if count == 3 {
            // 13 / 3 = 4.33…
            assert_eq!(answer.json()["summary"], s::rating_summary(43, 3));
            assert!(answer.json()["hint"].is_null());
        }
        last = Some(answer);
    }
    // 17 / 4 = 4.25, half up.
    let summary = s::rating_summary(43, 4);
    assert_eq!(summary, "★ 4.3 (4명)");
    assert_eq!(last.unwrap().json()["summary"], summary);
    assert!(app.get(GALLERY, "/", None).await.text().contains(&summary), "모든 게임 card");
    let mine = app.get(GALLERY, "/", Some(&kim)).await.text();
    assert_eq!(mine.matches(&summary).count(), 2, "내 게임 and 모든 게임 cards");
    assert!(app.get(GALLERY, &format!("/play/{id}"), None).await.text().contains(&summary));
    assert!(app.get(GALLERY, &format!("/play/{id}"), Some(&kim)).await.text().contains(&summary), "the owner sees it");
}

#[tokio::test]
async fn ratings_store_nothing_about_who_rated() {
    let app = TestApp::with(|c| c.rating_gate_secs = 0).await;
    let (_kim, id, rater) = game_and_rater(&app).await;
    assert_eq!(app.rate(&id, Some(&rater), 3).await.status, StatusCode::OK);
    let pool = app.raw_pool().await;
    for (table, expected) in [("ratings", "game_id,rater_hash,stars,rated_at"), ("game_opens", "game_id,rater_hash,opened_at")] {
        let columns: Vec<String> = sqlx::query_scalar(&format!("SELECT name FROM pragma_table_info('{table}') ORDER BY cid"))
            .fetch_all(&pool)
            .await
            .unwrap();
        assert_eq!(columns.join(","), expected, "{table}");
    }
    let stored: Vec<u8> = sqlx::query_scalar("SELECT rater_hash FROM ratings").fetch_one(&pool).await.unwrap();
    pool.close().await;
    let token = rater.split_once('=').unwrap().1;
    assert_ne!(stored, token.as_bytes());
    assert_eq!(stored, gallery::session::token_hash(token));

    // Signing in or out never gives a browser a rater cookie (no link to the account).
    app.add_user("lee", "이서연", Role::Student, "pw-pw-234").await;
    let login = app.post_form("/login", Some(&rater), "login=lee&password=pw-pw-234").await;
    assert!(login.cookie(RATER_COOKIE).is_none());
    let session = login.cookie("__Host-gallery_session").unwrap();
    let logout = app.post_form("/logout", Some(&format!("{session}; {rater}")), "").await;
    assert!(logout.cookie(RATER_COOKIE).is_none());
}

async fn rated(app: &TestApp, id: &str) {
    let (_, rater) = app.open_rating(id, None).await;
    assert_eq!(app.rate(id, rater.as_deref(), 5).await.status, StatusCode::OK);
    app.open_rating(id, None).await;
}

async fn rows_of(app: &TestApp, id: &str) -> i64 {
    app.count_rows(&format!("SELECT (SELECT COUNT(*) FROM ratings WHERE game_id = '{id}') + (SELECT COUNT(*) FROM game_opens WHERE game_id = '{id}')"))
        .await
}

#[tokio::test]
async fn every_way_of_removing_a_game_deletes_its_ratings() {
    let app = TestApp::with(|c| c.rating_gate_secs = 0).await;
    let teacher = app.teacher().await;
    let kim = app.student("kim", "김민수").await;
    let lee_id = app.add_user("lee", "이서연", Role::Student, "pw-pw-234").await;
    let lee = app.sign_in("lee", "pw-pw-234").await;
    let by_owner = app.new_game(&kim, "One").await;
    let by_teacher = app.new_game(&kim, "Two").await;
    let by_account = app.new_game(&lee, "Three").await;
    for id in [&by_owner, &by_teacher, &by_account] {
        rated(&app, id).await;
        assert_eq!(rows_of(&app, id).await, 3);
    }

    assert_eq!(app.post_form(&format!("/games/{by_owner}/remove"), Some(&kim), "").await.status, StatusCode::SEE_OTHER);
    assert_eq!(app.post_form(&format!("/games/{by_teacher}/remove"), Some(&teacher), "").await.status, StatusCode::SEE_OTHER);
    let gone = app.post_form(&format!("/teacher/students/{lee_id}/remove"), Some(&teacher), "").await;
    assert_eq!(gone.status, StatusCode::SEE_OTHER, "{}", gone.text());
    for id in [&by_owner, &by_teacher, &by_account] {
        assert_eq!(rows_of(&app, id).await, 0, "{id}");
        let (open, _) = app.open_rating(id, None).await;
        assert_eq!(open.status, StatusCode::NOT_FOUND);
        assert_eq!(app.rate(id, None, 5).await.status, StatusCode::NOT_FOUND);
    }
}

#[tokio::test]
async fn ratings_survive_an_update() {
    let app = TestApp::with(|c| c.rating_gate_secs = 0).await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "Spike Hell").await;
    for _ in 0..3 {
        rated(&app, &id).await;
    }
    let updated = app.upload(&kim, &id, ShareZip::new("Spike Hell 2").build()).await;
    assert_eq!(updated.status, StatusCode::OK, "{}", updated.text());
    let game = app.state.db.live_game(&id).await.unwrap().unwrap();
    assert_eq!((game.version, game.ratings, game.stars_total), (2, 3, 15));
    assert!(app.get(GALLERY, "/", None).await.text().contains(&s::rating_summary(50, 3)));
}

#[tokio::test]
async fn a_returning_browser_sees_its_stars_and_the_page_writes_nothing() {
    let app = TestApp::with(|c| c.rating_gate_secs = 0).await;
    let (_kim, id, rater) = game_and_rater(&app).await;
    assert_eq!(app.rate(&id, Some(&rater), 3).await.status, StatusCode::OK);
    let before = app.count_rows("SELECT (SELECT COUNT(*) FROM ratings) * 100 + (SELECT COUNT(*) FROM game_opens)").await;

    let page = app.get(GALLERY, &format!("/play/{id}"), Some(&rater)).await;
    let html = page.text();
    assert_eq!(html.matches(r#"aria-pressed="true""#).count(), 1);
    assert!(html.contains(r#"data-stars="3" aria-label="별 3개" aria-pressed="true""#), "{html}");
    assert_eq!(html.matches(r#"class="star on""#).count(), 3);
    assert!(html.contains(r#"<span id="rate-wait" hidden>"#), "no countdown");
    assert!(html.contains(&s::average_after(3)), "rated, average not shown yet");
    assert!(page.cookie(RATER_COOKIE).is_none(), "the GET sets no cookie");

    // A fresh browser's GET records nothing either.
    let fresh = app.get(GALLERY, &format!("/play/{id}"), None).await;
    assert!(fresh.cookie(RATER_COOKIE).is_none());
    assert_eq!(app.count_rows("SELECT (SELECT COUNT(*) FROM ratings) * 100 + (SELECT COUNT(*) FROM game_opens)").await, before);
}
