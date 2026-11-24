//! Comments (Phase 10): who a comment shows as, the guest gate, the gap and the guest cap, the identity check,
//! text rules, the owner's replies, hiding and erasing, 최근 댓글, paging, removal and updates, privacy, and closing.

mod support;

use axum::http::StatusCode;
use gallery::config::{Config, unix_now};
use gallery::strings as s;
use support::*;

/// kim owns "Spike Hell"; lee (이서연) is another student.
struct Setup {
    app: TestApp,
    kim: String,
    lee: String,
    kim_id: String,
    lee_id: String,
    game: String,
}

async fn setup(tweak: impl FnOnce(&mut Config)) -> Setup {
    let app = TestApp::with(tweak).await;
    let kim = app.student("kim", "김민수").await;
    let lee = app.student("lee", "이서연").await;
    let game = app.new_game(&kim, "Spike Hell").await;
    let kim_id = app.user_id("kim").await.to_string();
    let lee_id = app.user_id("lee").await.to_string();
    Setup { app, kim, lee, kim_id, lee_id, game }
}

fn open_all(c: &mut Config) {
    c.comment_gap_secs = 0;
    c.rating_gate_secs = 0;
}

fn message(answer: &Answer) -> String {
    answer.json()["message"].as_str().unwrap_or_default().to_string()
}

#[tokio::test]
async fn a_comment_shows_the_nickname_or_guest_newest_first() {
    let t = setup(|c| c.comment_gap_secs = 0).await;
    let app = &t.app;
    let by_lee = app.comment(&t.game, Some(&t.lee), &t.lee_id, "<b>x</b>").await;
    assert_eq!(by_lee.status, StatusCode::OK, "{}", by_lee.text());
    let html = by_lee.json()["html"].as_str().unwrap().to_string();
    assert!(html.contains("이서연") && html.contains("&lt;b&gt;x&lt;/b&gt;") && !html.contains("<b>x</b>"), "{html}");
    let lee_comment = comment_id(&by_lee);
    let guest = app.guest(&t.game).await;
    let by_guest = app.comment(&t.game, Some(&guest), "guest", "손님 글").await;
    assert_eq!(by_guest.status, StatusCode::OK, "{}", by_guest.text());
    assert!(by_guest.json()["html"].as_str().unwrap().contains(s::GUEST_NAME));
    let first = app.reply(&t.game, Some(&t.kim), &t.kim_id, lee_comment, "고마워").await;
    let second = app.reply(&t.game, Some(&t.kim), &t.kim_id, lee_comment, "또 고마워").await;
    assert_eq!((first.status, second.status), (StatusCode::OK, StatusCode::OK));

    let page = app.get(GALLERY, &format!("/play/{}", t.game), None).await.text();
    assert!(!page.contains("<b>x</b>"));
    let at = |needle: &str| page.find(needle).unwrap_or_else(|| panic!("{needle} on the page"));
    assert!(at("손님 글") < at("&lt;b&gt;x"), "newest first");
    assert!(at("&lt;b&gt;x") < at("고마워") && at("고마워") < at("또 고마워"), "replies oldest first under their comment");
}

#[tokio::test]
async fn a_guest_waits_for_the_gate_but_a_student_does_not() {
    let t = setup(|c| c.comment_gap_secs = 0).await;
    let app = &t.app;
    let no_cookie = app.comment(&t.game, None, "guest", "hi").await;
    assert_eq!(no_cookie.status, StatusCode::CONFLICT);
    assert_eq!(no_cookie.json()["wait"], 180);
    assert_eq!(message(&no_cookie), s::comment_gate(180));
    let (_, rater) = app.open_rating(&t.game, None).await;
    let rater = rater.unwrap();
    let fresh = app.comment(&t.game, Some(&rater), "guest", "hi").await;
    assert_eq!(fresh.status, StatusCode::CONFLICT);
    assert!(fresh.json()["wait"].as_u64().unwrap() > 170);
    app.backdate_open(&t.game, 170).await;
    assert_eq!(app.comment(&t.game, Some(&rater), "guest", "hi").await.status, StatusCode::CONFLICT);
    app.backdate_open(&t.game, 180).await;
    assert_eq!(app.comment(&t.game, Some(&rater), "guest", "hi").await.status, StatusCode::OK);
    assert_eq!(app.comment(&t.game, Some(&t.lee), &t.lee_id, "hi").await.status, StatusCode::OK, "no open needed");

    let before = app.count_rows("SELECT COUNT(*) FROM comments").await;
    let page = app.get(GALLERY, &format!("/play/{}", t.game), None).await;
    assert!(page.cookie(gallery::ratings::RATER_COOKIE).is_none());
    assert_eq!(app.count_rows("SELECT COUNT(*) FROM comments").await, before, "the GET writes nothing");
    assert!(page.text().contains(r#"data-wait="180""#), "a fresh guest's button counts down");
}

#[tokio::test]
async fn every_writer_waits_between_comments_but_replies_do_not() {
    let t = setup(|c| c.rating_gate_secs = 0).await;
    let app = &t.app;
    let first = app.comment(&t.game, Some(&t.lee), &t.lee_id, "one").await;
    assert_eq!(first.status, StatusCode::OK);
    assert_eq!(first.json()["wait"], 30);
    let again = app.comment(&t.game, Some(&t.lee), &t.lee_id, "two").await;
    assert_eq!(again.status, StatusCode::TOO_MANY_REQUESTS);
    let wait = again.json()["wait"].as_u64().unwrap();
    assert!((1..=30).contains(&wait), "{wait}");
    assert_eq!(message(&again), s::comment_gap(wait));
    let parent = comment_id(&first);
    for text in ["a", "b"] {
        let reply = app.reply(&t.game, Some(&t.kim), &t.kim_id, parent, text).await;
        assert_eq!(reply.status, StatusCode::OK, "{}", reply.text());
        assert_eq!(reply.json()["wait"], 0);
    }

    let t = setup(open_all).await;
    assert_eq!(t.app.comment(&t.game, Some(&t.lee), &t.lee_id, "one").await.status, StatusCode::OK);
    assert_eq!(t.app.comment(&t.game, Some(&t.lee), &t.lee_id, "two").await.status, StatusCode::OK, "no gap");
}

#[tokio::test]
async fn the_guest_cap_counts_the_whole_gallery_and_never_students() {
    let t = setup(|c| {
        open_all(c);
        c.guest_comment_cap = 2;
    })
    .await;
    let app = &t.app;
    for n in 0..3 {
        let (_, rater) = app.open_rating(&t.game, None).await;
        let answer = app.comment(&t.game, rater.as_deref(), "guest", "hi").await;
        if n < 2 {
            assert_eq!(answer.status, StatusCode::OK, "{}", answer.text());
        } else {
            assert_eq!(answer.status, StatusCode::TOO_MANY_REQUESTS);
            assert_eq!(message(&answer), s::COMMENT_BUSY);
            assert!(answer.json()["wait"].is_null());
        }
    }
    assert_eq!(app.comment(&t.game, Some(&t.lee), &t.lee_id, "hi").await.status, StatusCode::OK);
}

#[tokio::test]
async fn a_post_must_match_the_identity_the_page_showed() {
    let t = setup(open_all).await;
    let app = &t.app;
    for (cookie, as_, expected) in [
        (None, t.lee_id.as_str(), s::COMMENT_SIGNED_OUT),
        (Some(t.lee.as_str()), "guest", s::COMMENT_WHO_CHANGED),
        (Some(t.lee.as_str()), t.kim_id.as_str(), s::COMMENT_WHO_CHANGED),
    ] {
        let answer = app.comment(&t.game, cookie, as_, "hi").await;
        assert_eq!(answer.status, StatusCode::CONFLICT, "{as_}");
        assert_eq!(message(&answer), expected, "{as_}");
    }
    assert_eq!(app.count_rows("SELECT COUNT(*) FROM comments").await, 0);
}

async fn lee_says(t: &Setup, text: &str) -> Answer {
    t.app.comment(&t.game, Some(&t.lee), &t.lee_id, text).await
}

#[tokio::test]
async fn text_is_refused_never_cut() {
    let t = setup(open_all).await;
    let app = &t.app;
    for blank in [" \n\t ", "\u{200B}\u{3164} \n\u{FEFF}"] {
        let answer = lee_says(&t, blank).await;
        assert_eq!((answer.status, message(&answer)), (StatusCode::BAD_REQUEST, s::COMMENT_EMPTY.to_string()));
    }
    let long = lee_says(&t, &"가".repeat(301)).await;
    assert_eq!((long.status, message(&long)), (StatusCode::BAD_REQUEST, s::COMMENT_TOO_LONG.to_string()));
    assert_eq!(lee_says(&t, &"가".repeat(300)).await.status, StatusCode::OK);
    assert_eq!(lee_says(&t, &"😀".repeat(300)).await.status, StatusCode::OK, "300 four-byte characters fit the 4 KB limit");
    let before = app.count_rows("SELECT COUNT(*) FROM comments").await;
    let huge = app.post_form(&format!("/play/{}/comments", t.game), Some(&t.lee), &format!("as={}&text={}", t.lee_id, "a".repeat(5000))).await;
    assert_eq!((huge.status, message(&huge)), (StatusCode::PAYLOAD_TOO_LARGE, s::COMMENT_TOO_LONG.to_string()));
    assert_eq!(app.count_rows("SELECT COUNT(*) FROM comments").await, before);
}

#[tokio::test]
async fn only_the_owner_replies_and_the_owner_deletes_nobody_else() {
    let t = setup(open_all).await;
    let app = &t.app;
    let own = app.comment(&t.game, Some(&t.kim), &t.kim_id, "my game").await;
    assert_eq!((own.status, message(&own)), (StatusCode::FORBIDDEN, s::COMMENT_OWN.to_string()));
    let parent = comment_id(&app.comment(&t.game, Some(&t.lee), &t.lee_id, "hi").await);
    let reply = app.reply(&t.game, Some(&t.kim), &t.kim_id, parent, "thanks").await;
    assert!(reply.json()["html"].as_str().unwrap().contains(s::MAKER_BADGE));
    let reply_id = comment_id(&reply);
    assert_eq!(app.reply(&t.game, Some(&t.kim), &t.kim_id, reply_id, "x").await.status, StatusCode::BAD_REQUEST, "a reply to a reply");
    assert_eq!(app.reply(&t.game, Some(&t.lee), &t.lee_id, parent, "x").await.status, StatusCode::FORBIDDEN, "not the owner");
    let guest = app.guest(&t.game).await;
    assert_eq!(app.reply(&t.game, Some(&guest), "guest", parent, "x").await.status, StatusCode::FORBIDDEN, "a guest");
    assert_eq!(app.delete_comment(parent, Some(&t.kim), &t.kim_id).await.status, StatusCode::FORBIDDEN);
    assert_eq!(app.count_rows(&format!("SELECT COUNT(*) FROM comments WHERE id = {parent} AND hidden_at IS NULL")).await, 1);
    assert_eq!(app.delete_comment(parent, Some(&t.lee), &t.lee_id).await.status, StatusCode::OK);
    let gone = app.reply(&t.game, Some(&t.kim), &t.kim_id, parent, "x").await;
    assert_eq!((gone.status, message(&gone)), (StatusCode::GONE, s::COMMENT_GONE.to_string()));
    let page = app.get(GALLERY, &format!("/play/{}", t.game), Some(&t.kim)).await.text();
    assert!(page.contains(s::COMMENT_OWNER_NOTE) && page.contains(r#"id="reply-form""#));
    assert!(!page.contains(r#"id="comment-text""#), "the owner has no box");
}

#[tokio::test]
async fn a_writer_hides_and_the_teacher_erases() {
    let t = setup(open_all).await;
    let app = &t.app;
    let teacher = app.teacher().await;
    let teacher_id = app.user_id("teach").await.to_string();
    let by_teacher = app.comment(&t.game, Some(&teacher), &teacher_id, "선생님 글").await;
    assert!(by_teacher.json()["html"].as_str().unwrap().contains(s::TEACHER_BADGE), "the teacher's badge");
    let hidden = comment_id(&app.comment(&t.game, Some(&t.lee), &t.lee_id, "숨길 글").await);
    let under = comment_id(&app.reply(&t.game, Some(&t.kim), &t.kim_id, hidden, "그 밑 답글").await);
    assert_eq!(app.delete_comment(hidden, Some(&t.lee), &t.lee_id).await.status, StatusCode::OK);
    assert_eq!(app.count_rows(&format!("SELECT COUNT(*) FROM comments WHERE id = {hidden} AND hidden_at IS NOT NULL")).await, 1);
    for cookie in [None, Some(teacher.as_str()), Some(t.kim.as_str())] {
        let page = app.get(GALLERY, &format!("/play/{}", t.game), cookie).await.text();
        assert!(!page.contains("숨길 글") && !page.contains("그 밑 답글"), "{cookie:?}");
    }
    let gone = app.delete_comment(hidden, Some(&t.lee), &t.lee_id).await;
    assert_eq!((gone.status, message(&gone)), (StatusCode::GONE, s::COMMENT_GONE.to_string()));
    assert_eq!(app.delete_comment(under, Some(&t.kim), &t.kim_id).await.status, StatusCode::GONE, "under a hidden comment");

    let guest = app.guest(&t.game).await;
    let other = app.guest(&t.game).await;
    let by_guest = comment_id(&app.comment(&t.game, Some(&guest), "guest", "손님 글").await);
    assert_eq!(app.delete_comment(by_guest, Some(&other), "guest").await.status, StatusCode::FORBIDDEN);
    assert_eq!(app.delete_comment(by_guest, Some(&guest), "guest").await.status, StatusCode::OK);

    let erased = comment_id(&app.comment(&t.game, Some(&t.lee), &t.lee_id, "지울 글").await);
    app.reply(&t.game, Some(&t.kim), &t.kim_id, erased, "답").await;
    assert_eq!(app.delete_comment(erased, Some(&teacher), &teacher_id).await.status, StatusCode::OK);
    assert_eq!(app.count_rows(&format!("SELECT COUNT(*) FROM comments WHERE id = {erased} OR parent_id = {erased}")).await, 0);
    assert_eq!(app.delete_comment(erased, Some(&teacher), &teacher_id).await.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn the_teacher_sees_recent_comments_with_real_names() {
    let t = setup(|c| {
        open_all(c);
        c.recent_comments_page = 2;
    })
    .await;
    let app = &t.app;
    let teacher = app.teacher().await;
    app.state.db.set_names(app.user_id("lee").await, "이실명", "이서연").await.unwrap();
    let old = comment_id(&app.comment(&t.game, Some(&t.lee), &t.lee_id, "오래 전에 지운 글").await);
    app.reply(&t.game, Some(&t.kim), &t.kim_id, old, "오래된 답글").await;
    let first = comment_id(&app.comment(&t.game, Some(&t.lee), &t.lee_id, "첫 글").await);
    let guest = app.guest(&t.game).await;
    app.comment(&t.game, Some(&guest), "guest", "손님 글").await;
    app.reply(&t.game, Some(&t.kim), &t.kim_id, first, "만든이 답글").await;
    assert_eq!(app.delete_comment(first, Some(&t.lee), &t.lee_id).await.status, StatusCode::OK);
    // `old` was hidden more than 30 days ago.
    let pool = app.raw_pool().await;
    sqlx::query("UPDATE comments SET hidden_at = ? WHERE id = ?")
        .bind(unix_now() as i64 - 31 * 24 * 3600)
        .bind(old)
        .execute(&pool)
        .await
        .unwrap();
    pool.close().await;

    let page = app.get(GALLERY, "/teacher", Some(&teacher)).await.text();
    assert!(page.contains(r#"id="recent-comments""#));
    assert!(page.contains("만든이 답글") && page.contains("손님 글"), "the newest two, a reply included");
    assert!(page.find("만든이 답글").unwrap() < page.find("손님 글").unwrap(), "newest first");
    assert!(page.contains(s::GUEST_NAME));
    assert!(page.contains(&format!(r#"<span class="gone">{}</span>"#, s::HIDDEN_MARK)), "the reply under a hidden comment");
    let next = attr(&page, "data-more").expect("a more button");
    let more = app.get(GALLERY, &next, Some(&teacher)).await;
    assert_eq!(more.status, StatusCode::OK);
    let html = more.json()["html"].as_str().unwrap().to_string();
    assert!(html.contains("첫 글") && html.contains("이실명 (이서연)"), "{html}");
    assert!(more.json()["more"].is_null());
    assert!(!html.contains("오래 전에 지운 글") && !html.contains("오래된 답글"), "hidden too long ago");
    assert_eq!(app.get(GALLERY, &next, Some(&t.lee)).await.status, StatusCode::FORBIDDEN, "teacher only");
}

#[tokio::test]
async fn twenty_comments_per_page_then_more() {
    let t = setup(open_all).await;
    let app = &t.app;
    for n in 0..25 {
        assert_eq!(app.comment(&t.game, Some(&t.lee), &t.lee_id, &format!("글{n}")).await.status, StatusCode::OK);
    }
    let page = app.get(GALLERY, &format!("/play/{}", t.game), None).await.text();
    assert_eq!(page.matches(r#"<li class="comment" data-id="#).count(), 20);
    let next = attr(&page, "data-more").expect("a more button");
    let more = app.get(GALLERY, &next, None).await;
    assert_eq!(more.json()["html"].as_str().unwrap().matches("data-id=").count(), 5);
    assert!(more.json()["more"].is_null());
    assert!(more.header("content-type").unwrap().starts_with("application/json"));
}

async fn comments_of(app: &TestApp, game: &str) -> i64 {
    app.count_rows(&format!("SELECT COUNT(*) FROM comments WHERE game_id = '{game}'")).await
}

#[tokio::test]
async fn every_way_of_removing_a_game_or_account_deletes_its_comments() {
    let t = setup(open_all).await;
    let app = &t.app;
    let teacher = app.teacher().await;
    let by_teacher = app.new_game(&t.kim, "Two").await;
    let lees = app.new_game(&t.lee, "Three").await;
    for game in [&t.game, &by_teacher, &lees] {
        let guest = app.guest(game).await;
        assert_eq!(app.comment(game, Some(&guest), "guest", "hi").await.status, StatusCode::OK);
    }
    // lee's comment on kim's game, and kim's reply under it, go with lee's account.
    let on_kims = comment_id(&app.comment(&by_teacher, Some(&t.lee), &t.lee_id, "lee was here").await);
    app.reply(&by_teacher, Some(&t.kim), &t.kim_id, on_kims, "re").await;
    assert_eq!(comments_of(app, &by_teacher).await, 3);

    let lee_id = app.user_id("lee").await;
    assert_eq!(app.post_form(&format!("/teacher/students/{lee_id}/remove"), Some(&teacher), "").await.status, StatusCode::SEE_OTHER);
    assert_eq!(comments_of(app, &lees).await, 0, "account removal");
    assert_eq!(comments_of(app, &by_teacher).await, 1, "lee's comment and the reply under it went");
    assert_eq!(app.post_form(&format!("/games/{}/remove", t.game), Some(&t.kim), "").await.status, StatusCode::SEE_OTHER);
    assert_eq!(comments_of(app, &t.game).await, 0, "owner removal");
    assert_eq!(app.post_form(&format!("/games/{by_teacher}/remove"), Some(&teacher), "").await.status, StatusCode::SEE_OTHER);
    assert_eq!(comments_of(app, &by_teacher).await, 0, "teacher removal");
    assert_eq!(app.comment(&t.game, None, "guest", "hi").await.status, StatusCode::NOT_FOUND);
}

#[tokio::test]
async fn comments_survive_an_update() {
    let t = setup(open_all).await;
    let app = &t.app;
    app.comment(&t.game, Some(&t.lee), &t.lee_id, "남아 있어요").await;
    let updated = app.upload(&t.kim, &t.game, ShareZip::new("Spike Hell 2").build()).await;
    assert_eq!(updated.status, StatusCode::OK, "{}", updated.text());
    assert!(app.get(GALLERY, &format!("/play/{}", t.game), None).await.text().contains("남아 있어요"));
}

#[tokio::test]
async fn comments_keep_no_link_between_accounts_and_browsers() {
    let t = setup(open_all).await;
    let app = &t.app;
    let pool = app.raw_pool().await;
    let columns: Vec<String> = sqlx::query_scalar("SELECT name FROM pragma_table_info('comments') ORDER BY cid")
        .fetch_all(&pool)
        .await
        .unwrap();
    pool.close().await;
    assert_eq!(columns.join(","), "id,game_id,parent_id,user_id,guest_hash,body,created_at,hidden_at");

    // lee's browser has a rater cookie; signed in, the comment stores none of it.
    let (_, rater) = app.open_rating(&t.game, Some(&t.lee)).await;
    let rater = rater.unwrap();
    let both = format!("{}; {rater}", t.lee);
    assert_eq!(app.comment(&t.game, Some(&both), &t.lee_id, "signed in").await.status, StatusCode::OK);
    assert_eq!(app.count_rows("SELECT COUNT(*) FROM comments WHERE guest_hash IS NOT NULL").await, 0);

    // A guest who rated 5 and then commented: no star in the item, and the hash nowhere.
    let guest = app.guest(&t.game).await;
    assert_eq!(app.rate(&t.game, Some(&guest), 5).await.status, StatusCode::OK);
    let item = app.comment(&t.game, Some(&guest), "guest", "guest").await;
    let html = item.json()["html"].as_str().unwrap().to_string();
    assert!(!html.contains("avg-stars") && !html.contains("5점 만점에"), "{html}");
    let token = guest.split_once('=').unwrap().1;
    let hex: String = gallery::session::token_hash(token).iter().map(|b| format!("{b:02x}")).collect();
    let page = app.get(GALLERY, &format!("/play/{}", t.game), Some(&guest)).await.text();
    let more = app.get(GALLERY, &format!("/play/{}/comments", t.game), Some(&guest)).await.text();
    for text in [&html, &page, &more] {
        assert!(!text.contains(&hex) && !text.contains(token));
    }
    assert!(page.contains("comment-del"), "the guest can delete its own comment");
}

#[tokio::test]
async fn closed_comments_stay_readable_and_deletable() {
    let t = setup(|c| {
        open_all(c);
        c.comments_open = false;
    })
    .await;
    let app = &t.app;
    let lee_id: i64 = t.lee_id.parse().unwrap();
    let id = app.state.db.insert_comment(&t.game, Some(lee_id), None, "그 전에 쓴 글", 1, 1, 0, 20, 600).await.unwrap().unwrap();
    let answer = app.comment(&t.game, Some(&t.lee), &t.lee_id, "hi").await;
    assert_eq!((answer.status, message(&answer)), (StatusCode::FORBIDDEN, s::COMMENTS_CLOSED.to_string()));
    for cookie in [Some(t.lee.as_str()), Some(t.kim.as_str())] {
        let page = app.get(GALLERY, &format!("/play/{}", t.game), cookie).await.text();
        assert!(page.contains("그 전에 쓴 글") && page.contains(s::COMMENTS_CLOSED));
        assert!(!page.contains(r#"id="comment-box""#) && !page.contains("comment-reply"));
    }
    assert_eq!(app.delete_comment(id, Some(&t.lee), &t.lee_id).await.status, StatusCode::OK);
}
