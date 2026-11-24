//! The look check: every gallery page, rendered from real handlers with fixture data, saved as HTML into the
//! repo's `TestResults/gallery-look/` (with the stylesheet, script, font and screenshots beside it) for the
//! headless-Chrome screenshots, plus the rules a screenshot can't prove: no inline styles (the gallery's CSP is
//! `style-src 'self'`), the ids the script needs, and which cards get the NEW badge and the upload time. The play
//! pages cover every rating state: counting down, ready, rated (one rating and three), and the owner's own.

mod support;

use std::path::{Path, PathBuf};

use axum::http::StatusCode;
use gallery::config::unix_now;
use gallery::pages::FONT_PATH;
use gallery::strings as s;
use support::*;

const DAY: i64 = 86_400;

fn out_dir() -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR")).join("../../TestResults/gallery-look")
}

/// Rewrites a page to load its files from the snapshot folder and never reach the live games host.
fn local(html: &str) -> String {
    let html = html.replace(r#"href="/static/"#, r#"href="static/"#).replace(r#"src="/static/"#, r#"src="static/"#);
    let html = html.replace(r#"src="/shots/"#, r#"src="shots/"#);
    match html.find(r#"src="https://suwon-play.orcarin.com/"#) {
        Some(at) => {
            let end = at + 5 + html[at + 5..].find('"').unwrap() + 1;
            format!("{}src=\"about:blank\"{}", &html[..at], &html[end..])
        }
        None => html,
    }
}

fn no_inline_style(name: &str, html: &str) {
    assert!(!html.contains(" style="), "{name}: a style attribute (the CSP blocks it)");
    assert!(!html.contains("<style"), "{name}: a style element (the CSP blocks it)");
}

/// Sets a game's first and latest upload times directly in the test's database (the `Db` pool is private).
async fn backdate(app: &TestApp, id: &str, created_days: i64, updated_days: i64) {
    let url = format!("sqlite://{}", app.state.config.db_path().display());
    let pool = sqlx::SqlitePool::connect(&url).await.unwrap();
    let now = unix_now() as i64;
    sqlx::query("UPDATE games SET created_at = ?, updated_at = ? WHERE id = ?")
        .bind(now - created_days * DAY)
        .bind(now - updated_days * DAY)
        .bind(id)
        .execute(&pool)
        .await
        .unwrap();
    pool.close().await;
}

#[tokio::test]
async fn every_page_renders_without_inline_styles_and_keeps_its_wires() {
    let app = TestApp::with(|c| c.games_per_account = 3).await;
    let teacher = app.teacher().await;
    let kim = app.student("kim", "김하늘").await;
    let lee = app.student("lee", "이도윤").await;
    let park = app.student("park", "박서연").await;
    let fresh = app.student("choi", "최민준").await;

    let old = app.new_game(&kim, "절대 못 깨는 길").await;
    let clouds = app.new_game(&kim, "구름 위 달리기").await;
    let traps = app.new_game(&lee, "함정 천국").await;
    let spikes = app.new_game(&park, "가시 조심!!").await;
    // kim updated `clouds` yesterday: its cards say 업데이트, the others 올림.
    let updated = app.upload(&kim, &clouds, ShareZip::new("구름 위 달리기").build()).await;
    assert_eq!(updated.status, StatusCode::OK, "{}", updated.text());
    backdate(&app, &old, 20, 10).await;
    backdate(&app, &clouds, 9, 1).await;
    backdate(&app, &spikes, 2, 2).await;
    // lee's game is brand new: today, and NEW.

    // Ratings: `old` has three, `traps` one (each average shows). Each browser opens, waits out the gate
    // (backdated) and rates; `ready` opened `traps` and may rate but hasn't.
    let (_, ready) = app.open_rating(&traps, None).await;
    let ready = ready.unwrap();
    let mut raters = Vec::new();
    for (game, stars) in [(&old, 5), (&old, 4), (&old, 4), (&traps, 3)] {
        let (_, rater) = app.open_rating(game, None).await;
        app.backdate_open(game, 200).await;
        let rater = rater.unwrap();
        assert_eq!(app.rate(game, Some(&rater), stars).await.status, StatusCode::OK);
        raters.push(rater);
    }
    let (rated_old, rated_traps) = (&raters[0], &raters[3]);

    let mut pages: Vec<(&str, String)> = Vec::new();
    let mut keep = |name: &'static str, answer: Answer| {
        let html = answer.text();
        no_inline_style(name, &html);
        pages.push((name, html));
    };

    keep("login", app.get(GALLERY, "/login", None).await);
    keep("login-wrong", app.post_form("/login", None, "login=kim&password=nope-nope-1").await);
    keep("list-empty", app.get(GALLERY, "/", Some(&fresh)).await);
    keep("list", app.get(GALLERY, "/", Some(&lee)).await);
    keep("list-mine", app.get(GALLERY, "/", Some(&kim)).await);
    keep("list-teacher", app.get(GALLERY, "/", Some(&teacher)).await);
    keep("play", app.get(GALLERY, &format!("/play/{traps}"), Some(&lee)).await);
    keep("list-public", app.get(GALLERY, "/", None).await);
    keep("play-public", app.get(GALLERY, &format!("/play/{traps}"), None).await);
    keep("not-found-public", app.get(GALLERY, "/nothing-here", None).await);
    keep("play-rate-wait", app.get(GALLERY, &format!("/play/{old}"), Some(&lee)).await);
    keep("play-rate-ready", app.get(GALLERY, &format!("/play/{traps}"), Some(&ready)).await);
    keep("play-rated-one", app.get(GALLERY, &format!("/play/{traps}"), Some(rated_traps)).await);
    keep("play-rated-avg", app.get(GALLERY, &format!("/play/{old}"), Some(rated_old)).await);
    keep("play-own", app.get(GALLERY, &format!("/play/{old}"), Some(&kim)).await);
    keep("upload", app.get(GALLERY, "/upload", Some(&kim)).await);
    keep("upload-first", app.get(GALLERY, "/upload", Some(&fresh)).await);
    keep("password", app.get(GALLERY, "/password", Some(&kim)).await);
    keep("remove-game", app.get(GALLERY, &format!("/games/{old}/remove"), Some(&kim)).await);
    keep("not-found", app.get(GALLERY, "/nothing-here", Some(&kim)).await);
    keep("teacher", app.get(GALLERY, "/teacher", Some(&teacher)).await);
    let bad = format!("text={}", url_encode("ok-one, 정하은, 하늘빛\nBAD ID, 오시우, 시우\nkim, 김하늘, 하늘\nno-nick, 한예린"));
    keep("preview", app.post_form("/teacher/students/preview", Some(&teacher), &bad).await);
    let good = format!("text={}", url_encode("jung, 정하은, 하늘빛\noh-siu, 오시우, 번개\nhan, 한예린, 별사탕"));
    keep("slips", app.post_form("/teacher/students/create", Some(&teacher), &good).await);
    let kim_id = app.state.db.students().await.unwrap().into_iter().find(|st| st.login == "kim").unwrap().id;
    keep("remove-account", app.get(GALLERY, &format!("/teacher/students/{kim_id}/remove"), Some(&teacher)).await);

    let page = |name: &str| pages.iter().find(|(n, _)| *n == name).map(|(_, h)| h.as_str()).unwrap();

    // The script's wires: every id and data attribute gallery.js reads is still rendered.
    let owner_pages = ["play", "play-own"];
    let rating_pages = ["play-public", "play-rate-wait", "play-rate-ready", "play-rated-one", "play-rated-avg"];
    for play in owner_pages.iter().chain(&rating_pages) {
        for id in ["game", "frame", "fullscreen", "rating-avg"] {
            assert!(page(play).contains(&format!(r#"id="{id}""#)), "{play}: #{id}");
        }
    }
    for play in rating_pages {
        let html = page(play);
        for id in ["rate", "rate-wait", "rate-stars", "rate-buttons", "rate-note"] {
            assert!(html.contains(&format!(r#"id="{id}""#)), "{play}: #{id}");
        }
        for data in ["open", "rate", "wait", "mine", "msg-wait-min", "msg-wait-sec", "msg-saved", "msg-failed"] {
            assert!(html.contains(&format!("data-{data}=")), "{play}: data-{data}");
        }
        assert_eq!(html.matches(r#"class="star"#).count(), 5, "{play}: five stars");
        assert!(!html.contains(s::RATE_OWN), "{play}");
    }
    for play in owner_pages {
        assert!(page(play).contains(s::RATE_OWN) && !page(play).contains(r#"id="rate""#), "{play}");
    }
    // The states: counting down (a fresh browser), ready, rated (one rating already shows), and three.
    assert!(page("play-rate-wait").contains(&s::rate_wait(180)));
    assert!(page("play-rate-ready").contains(r#"<span id="rate-wait" hidden>"#));
    let one = s::rating_summary(30, 1);
    assert!(page("play-rated-one").contains(&one));
    assert!(page("list-public").contains(&one), "a single rating shows on the card");
    let old_average = s::rating_summary(43, 3);
    for play in ["play-rated-avg", "play-own"] {
        assert!(page(play).contains(&old_average), "{play}");
    }
    assert!(page("list-public").contains(&old_average));
    // The teacher's list edits both names per student, under the hint that the nickname is public.
    assert!(page("teacher").contains(s::NAMES_HINT));
    // Each student row and the add-one form: the nickname box is always named `nickname`, never `name` (which the
    // add-one form uses for the real name), with autofill off, so a remembered real name is never offered there.
    let teacher_html = page("teacher");
    let rows = teacher_html.matches(r#"/rename""#).count();
    assert_eq!(rows, 4, "one rename form per student");
    assert_eq!(teacher_html.matches(r#"name="real_name""#).count(), rows);
    assert_eq!(teacher_html.matches(r#"name="nickname""#).count(), rows + 1);
    assert_eq!(teacher_html.matches(r#"autocomplete="off""#).count(), 2 * rows + 2);
    for upload in ["upload", "upload-first"] {
        let html = page(upload);
        for id in ["upload", "zip", "zip-name", "progress", "progress-pct", "upload-message", "upload-button"] {
            assert!(html.contains(&format!(r#"id="{id}""#)), "{upload}: #{id}");
        }
        for data in ["max-bytes", "msg-pick-zip", "msg-pick-game", "msg-too-big", "msg-network", "msg-uploading", "msg-login"] {
            assert!(html.contains(&format!("data-{data}=")), "{upload}: data-{data}");
        }
    }
    assert!(page("slips").contains(r#"id="print""#));
    assert!(page("play").contains(r#"src="https://suwon-play.orcarin.com/enter/"#), "the real frame source");

    // The badge: games first uploaded within a week (lee's today, park's 2 days ago); not kim's (20 and 9 days ago,
    // though one was updated yesterday).
    let all = &page("list-mine")[page("list-mine").find(s::ALL_GAMES).unwrap()..];
    assert_eq!(all.matches(r#"<span class="badge">"#).count(), 2);
    for (title, new) in [("함정 천국", true), ("가시 조심!!", true), ("절대 못 깨는 길", false), ("구름 위 달리기", false)] {
        let card = &all[..all.find(title).unwrap()];
        let card = &card[card.rfind(r#"<li class="card">"#).unwrap()..];
        assert_eq!(card.contains("badge"), new, "{title}");
    }

    // The upload time on every card and the play page (업데이트 once updated); at the limit the add card gives way
    // to the limit line.
    let kims = page("list-mine");
    let mine = &kims[kims.find(s::MY_GAMES).unwrap()..kims.find(s::ALL_GAMES).unwrap()];
    assert!(mine.contains(&s::uploaded_ago(10, false)) && mine.contains(&s::uploaded_ago(1, true)));
    for when in [s::uploaded_ago(10, false), s::uploaded_ago(1, true), s::uploaded_ago(0, false), s::uploaded_ago(2, false)] {
        assert!(all.contains(&when), "모든 게임: {when}");
    }
    assert_eq!(all.matches(r#"<span class="when">"#).count(), 4, "every card");
    assert!(page("play-own").contains(&s::uploaded_ago(10, false)));
    assert!(page("play-public").contains(&s::uploaded_ago(0, false)));
    assert!(mine.contains(r#"class="add-card""#), "kim has 2 of 3 games");
    let lees = page("list");
    let lee_mine = &lees[lees.find(s::MY_GAMES).unwrap()..lees.find(s::ALL_GAMES).unwrap()];
    assert!(lee_mine.contains(&s::uploaded_ago(0, false)));
    app.new_game(&kim, "세 번째 게임").await;
    let full = app.get(GALLERY, "/", Some(&kim)).await.text();
    let full_mine = &full[full.find(s::MY_GAMES).unwrap()..full.find(s::ALL_GAMES).unwrap()];
    assert!(full_mine.contains(&s::too_many_games(3)) && !full_mine.contains(r#"class="add-card""#));
    no_inline_style("list-full", &full);
    pages.push(("list-full", full));

    // The snapshot folder for the screenshots: pages, the stylesheet, script and font, and each game's picture
    // (a real screenshot from earlier test runs when one is there; the fixture zips carry a header-only PNG).
    let out = out_dir();
    let _ = std::fs::remove_dir_all(&out);
    std::fs::create_dir_all(out.join("static/fonts")).unwrap();
    std::fs::create_dir_all(out.join("shots")).unwrap();
    std::fs::write(out.join("static/gallery.css"), include_str!("../static/gallery.css")).unwrap();
    std::fs::write(out.join("static/gallery.js"), include_str!("../static/gallery.js")).unwrap();
    let font = app.get(GALLERY, FONT_PATH, None).await;
    assert_eq!(font.status, StatusCode::OK);
    std::fs::write(out.join(FONT_PATH.trim_start_matches('/')), &font.body).unwrap();
    let results = Path::new(env!("CARGO_MANIFEST_DIR")).join("../../TestResults");
    let pictures: Vec<PathBuf> = ["6000.6.9f1-share-check-screenshot.png", "6000.6.9f1-traps-counter.png", "6000.6.9f1-traps-win.png"]
        .iter()
        .map(|f| results.join(f))
        .filter(|p| p.exists())
        .collect();
    for (i, id) in [&old, &clouds, &traps, &spikes].into_iter().enumerate() {
        let target = out.join(format!("shots/{id}.png"));
        match pictures.get(i % pictures.len().max(1)) {
            Some(picture) => std::fs::copy(picture, target).map(|_| ()).unwrap(),
            None => std::fs::write(target, app.get(GALLERY, &format!("/shots/{id}.png"), Some(&kim)).await.body).unwrap(),
        }
    }
    for (name, html) in &pages {
        std::fs::write(out.join(format!("{name}.html")), local(html)).unwrap();
    }
}
