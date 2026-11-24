//! Comments (v0.35): anyone comments under a game on its play page, a guest as 손님 (one browser, known by the
//! rater cookie's hash, after the rating gate); only the game's owner replies, one level deep. A writer's delete
//! only hides a comment (the teacher's 최근 댓글 still lists it as 지움 for 30 days); the teacher's delete erases it.
//! Real names show only on the teacher page. Every action is a fetch from the play page (a navigation would restart
//! the game), and the answers carry markup rendered here, so the script never builds HTML from user text.

use axum::Extension;
use axum::extract::rejection::FormRejection;
use axum::extract::{Form, Path, Query, State};
use axum::http::{HeaderMap, StatusCode};
use axum::response::Response;
use maud::{Markup, html};
use serde::Deserialize;
use serde_json::json;

use crate::app::AppState;
use crate::config::unix_now;
use crate::db::{CommentRow, Game, Refusal, ReplyRefusal, Role, User};
use crate::gallery::{days_ago, live};
use crate::ratings::{self, rater};
use crate::session::{self, CurrentUser};
use crate::strings as s;
use crate::text::{COMMENT_MAX_CHARS, CommentProblem, clean_comment};
use crate::upload::json_answer;

/// Who is looking at (or writing) comments, worked out once per request.
pub struct Viewer {
    user: Option<User>,
    /// The rater cookie's hash, if the browser has one.
    hash: Option<Vec<u8>>,
    teacher: bool,
    /// Signed in as the game's owner.
    owner: bool,
}

impl Viewer {
    pub fn new(game: &Game, user: Option<User>, headers: &HeaderMap) -> Viewer {
        let hash = rater(headers).map(|token| session::token_hash(&token));
        let teacher = user.as_ref().is_some_and(|u| u.role == Role::Teacher);
        let owner = user.as_ref().is_some_and(|u| game.owner_id == Some(u.id));
        Viewer { user, hash, teacher, owner }
    }

    fn teacher(user: &User) -> Viewer {
        Viewer { user: Some(user.clone()), hash: None, teacher: true, owner: false }
    }

    fn id(&self) -> Option<i64> {
        self.user.as_ref().map(|u| u.id)
    }

    fn hash(&self) -> Option<&[u8]> {
        self.hash.as_deref()
    }

    /// The teacher deletes anything; a writer their own (a guest from the same browser). Never the game's owner
    /// as such.
    fn may_delete(&self, row: &CommentRow) -> bool {
        self.teacher || (row.user_id.is_some() && row.user_id == self.id()) || row.by_this_browser
    }
}

/// Where the list is drawn: a game's play page, or the teacher's 최근 댓글 (game titles, real names, 지움).
#[derive(Clone, Copy, PartialEq, Eq)]
enum Place {
    Play,
    Recent,
}

fn writer_name(row: &CommentRow, place: Place) -> String {
    match (&row.nickname, place) {
        (None, _) => s::GUEST_NAME.to_string(),
        (Some(nickname), Place::Play) => nickname.clone(),
        (Some(nickname), Place::Recent) => s::teacher_view_name(row.real_name.as_deref().unwrap_or(""), nickname),
    }
}

/// One comment (with its replies on a play page). No rating, no star and no hash in any of it.
fn item(row: &CommentRow, replies: &[CommentRow], viewer: &Viewer, place: Place, open: bool, now: i64) -> Markup {
    let reply = row.parent_id.is_some();
    let may_reply = place == Place::Play && viewer.owner && !reply && open;
    html! {
        li.comment.reply[reply] data-id=(row.id) {
            p.by {
                @if place == Place::Recent {
                    a.game href={ "/play/" (row.game_id) "#comments" } { (row.game_title) }
                    @if reply { span.reply-mark { (s::REPLY) } }
                }
                span.name { (writer_name(row, place)) }
                @if row.teacher { span.pill.teacher { (s::TEACHER_BADGE) } }
                // Only the game's owner can reply, so every reply is the maker's.
                @if reply { span.pill.maker { (s::MAKER_BADGE) } }
                span.ago { (s::comment_ago(days_ago(row.created_at, now))) }
                @if place == Place::Recent && (row.hidden || row.parent_hidden) { span.gone { (s::HIDDEN_MARK) } }
            }
            p.text { (row.body) }
            @if viewer.may_delete(row) || may_reply {
                div.comment-actions {
                    @if may_reply {
                        button.comment-reply type="button" data-parent=(row.id) { (s::REPLY) }
                    }
                    @if viewer.may_delete(row) {
                        button.comment-del type="button" data-delete={ "/comments/" (row.id) "/delete" } { (s::COMMENT_DELETE) }
                    }
                }
            }
            @if place == Place::Play && !reply {
                ul.replies {
                    @for r in replies { (item(r, &[], viewer, place, open, now)) }
                }
            }
        }
    }
}

/// A page of a game's comments with their replies, and the address of the next page when there is one.
async fn page_items(state: &AppState, game: &Game, viewer: &Viewer, before: Option<i64>) -> sqlx::Result<(Markup, Option<String>)> {
    let (rows, next) = state.db.comments_page(&game.id, before, state.config.comments_page, viewer.hash()).await?;
    let ids: Vec<i64> = rows.iter().map(|r| r.id).collect();
    let replies = state.db.replies_of(&game.id, &ids, viewer.hash()).await?;
    let now = unix_now() as i64;
    let open = state.config.comments_open;
    let markup = html! {
        @for row in &rows {
            @let mine: Vec<CommentRow> = replies.iter().filter(|r| r.parent_id == Some(row.id)).cloned().collect();
            (item(row, &mine, viewer, Place::Play, open, now))
        }
    };
    Ok((markup, next.map(|n| format!("/play/{}/comments?before={n}", game.id))))
}

/// The play page's comments section, after the game's frame.
pub async fn section(state: &AppState, game: &Game, viewer: &Viewer) -> Markup {
    let (list, more) = match page_items(state, game, viewer, None).await {
        Ok(page) => page,
        Err(error) => {
            tracing::warn!(game = %game.id, %error, "reading comments failed");
            (html! {}, None)
        }
    };
    let empty = list.0.is_empty();
    let guest = viewer.user.is_none();
    // A guest's button counts down to the rating gate, on the same clock as the stars.
    let wait = match (guest, viewer.hash()) {
        (false, _) => 0,
        (true, None) => state.config.rating_gate_secs,
        (true, Some(hash)) => {
            let opened_at = state.db.rating_state(&game.id, hash).await.map(|(opened, _)| opened).unwrap_or(None);
            ratings::wait_secs(opened_at, unix_now() as i64, &state.config)
        }
    };
    let as_field = viewer.id().map_or_else(|| "guest".to_string(), |id| id.to_string());
    html! {
        section #comments.comments
            data-post={ "/play/" (game.id) "/comments" }
            data-as=(as_field)
            data-guest=(if guest { "1" } else { "0" })
            data-max=(COMMENT_MAX_CHARS)
            data-wait=(wait)
            data-msg-empty=(s::COMMENT_EMPTY)
            data-msg-wait-min=(s::COMMENT_WAIT_MINUTES)
            data-msg-wait-sec=(s::COMMENT_WAIT_SECONDS)
            data-msg-posted=(s::COMMENT_POSTED)
            data-msg-failed=(s::COMMENT_FAILED)
            data-msg-confirm=(s::COMMENT_CONFIRM)
            data-msg-gone=(s::COMMENT_GONE)
            data-label-send=(s::COMMENT_SEND)
            data-label-delete=(s::COMMENT_DELETE)
            data-label-cancel=(s::CANCEL) {
            h2 { (s::COMMENTS_HEADING) }
            @if !state.config.comments_open {
                p.comment-closed { (s::COMMENTS_CLOSED) }
            } @else if viewer.owner {
                p.comment-owner-note { (s::COMMENT_OWNER_NOTE) }
                template #reply-form {
                    div.reply-form {
                        textarea.reply-text rows="2" aria-label=(s::REPLY) {}
                        div.comment-row {
                            span.reply-count { "0/" (COMMENT_MAX_CHARS) }
                            button.reply-cancel type="button" { (s::CANCEL) }
                            button.reply-send type="button" { (s::REPLY_SEND) }
                        }
                        p.reply-msg role="status" {}
                    }
                }
            } @else {
                div #comment-box {
                    textarea #comment-text rows="3" aria-label=(s::COMMENTS_HEADING) placeholder=(s::COMMENT_PLACEHOLDER) {}
                    div.comment-row {
                        span #comment-count { "0/" (COMMENT_MAX_CHARS) }
                        button #comment-send type="button" { (s::COMMENT_SEND) }
                    }
                    p.comment-who {
                        @match &viewer.user {
                            Some(user) => (s::comment_as(&user.display_name)),
                            None => (s::COMMENT_AS_GUEST),
                        }
                    }
                    p.comment-privacy { (s::COMMENT_PRIVACY) }
                    p #comment-msg role="status" {}
                }
            }
            ul #comment-list { (list) }
            p #comments-empty hidden[!empty] { (s::COMMENTS_EMPTY) }
            @if let Some(more) = more {
                button #comments-more type="button" data-more=(more) { (s::COMMENTS_MORE) }
            }
        }
    }
}

/// 최근 댓글 items: every comment and reply on live games, real names, 지움 on hidden ones.
async fn recent_items(state: &AppState, teacher: &Viewer, before: Option<i64>) -> sqlx::Result<(Markup, Option<String>)> {
    let now = unix_now() as i64;
    let since = now - state.config.hidden_comment_keep_secs as i64;
    let (rows, next) = state.db.recent_comments(before, state.config.recent_comments_page, since).await?;
    let open = state.config.comments_open;
    let markup = html! { @for row in &rows { (item(row, &[], teacher, Place::Recent, open, now)) } };
    Ok((markup, next.map(|n| format!("/teacher/comments?before={n}"))))
}

/// The teacher page's 최근 댓글 section, at its top.
pub async fn recent_section(state: &AppState, teacher: &User) -> Markup {
    let viewer = Viewer::teacher(teacher);
    let (list, more) = match recent_items(state, &viewer, None).await {
        Ok(page) => page,
        Err(error) => {
            tracing::warn!(%error, "reading recent comments failed");
            (html! {}, None)
        }
    };
    let empty = list.0.is_empty();
    html! {
        section #recent.recent {
            h2 { (s::RECENT_COMMENTS) }
            ul #recent-comments.comment-list
                data-as=(teacher.id)
                data-msg-failed=(s::COMMENT_FAILED)
                data-msg-confirm=(s::COMMENT_CONFIRM)
                data-msg-gone=(s::COMMENT_GONE)
                data-label-delete=(s::COMMENT_DELETE)
                data-label-cancel=(s::CANCEL) {
                (list)
            }
            @if empty { p { (s::COMMENTS_EMPTY) } }
            @if let Some(more) = more {
                button #recent-more type="button" data-more=(more) { (s::COMMENTS_MORE) }
            }
        }
    }
}

fn refuse(status: StatusCode, message: &str) -> Response {
    json_answer(status, json!({"ok": false, "message": message}))
}

fn failed() -> Response {
    refuse(StatusCode::INTERNAL_SERVER_ERROR, s::COMMENT_FAILED)
}

fn not_found() -> Response {
    json_answer(StatusCode::NOT_FOUND, json!({"ok": false, "message": s::COMMENT_FAILED}))
}

/// A form the extractor refused. A body over the route's limit can never pass, so it says why.
fn rejected(rejection: FormRejection) -> Response {
    if rejection.status() == StatusCode::PAYLOAD_TOO_LARGE {
        refuse(StatusCode::PAYLOAD_TOO_LARGE, s::COMMENT_TOO_LONG)
    } else {
        refuse(rejection.status(), s::COMMENT_FAILED)
    }
}

/// The post must come from the identity the page showed: a sign-in that ended while playing never turns a
/// student's comment into 손님's, and a sign-in in another tab never turns a guest's draft into a student's.
fn check_as(as_field: &str, user: Option<&User>) -> Result<(), &'static str> {
    match (as_field, user) {
        ("guest", None) => Ok(()),
        ("guest", Some(_)) => Err(s::COMMENT_WHO_CHANGED),
        (id, Some(user)) if id == user.id.to_string() => Ok(()),
        (_, None) => Err(s::COMMENT_SIGNED_OUT),
        (_, Some(_)) => Err(s::COMMENT_WHO_CHANGED),
    }
}

#[derive(Deserialize)]
pub struct PostForm {
    #[serde(rename = "as", default)]
    as_field: String,
    #[serde(default)]
    text: String,
    parent: Option<i64>,
}

/// The new item's markup, and the seconds the writer's button waits.
async fn written(state: &AppState, viewer: &Viewer, id: i64, wait: u64) -> Response {
    match state.db.comment_by_id(id, viewer.hash()).await {
        Ok(Some(row)) => {
            let html = item(&row, &[], viewer, Place::Play, state.config.comments_open, unix_now() as i64);
            json_answer(StatusCode::OK, json!({"ok": true, "html": html.into_string(), "wait": wait}))
        }
        Ok(None) => failed(),
        Err(error) => {
            tracing::warn!(%error, "reading a new comment failed");
            failed()
        }
    }
}

/// `POST /play/<id>/comments` with `as`, `text` and, for the owner's reply, `parent`. The game is looked up before
/// the form is read, so a missing game is a 404 whatever the body. Every check that decides is inside the insert.
pub async fn post(
    State(state): State<AppState>,
    user: Option<Extension<CurrentUser>>,
    Path(id): Path<String>,
    headers: HeaderMap,
    form: Result<Form<PostForm>, FormRejection>,
) -> Response {
    let user = user.map(|Extension(CurrentUser(user))| user);
    let Some(game) = live(&state, &id).await else { return not_found() };
    let config = &state.config;
    if !config.comments_open {
        return refuse(StatusCode::FORBIDDEN, s::COMMENTS_CLOSED);
    }
    let form = match form {
        Ok(Form(form)) => form,
        Err(rejection) => return rejected(rejection),
    };
    if let Err(message) = check_as(&form.as_field, user.as_ref()) {
        return refuse(StatusCode::CONFLICT, message);
    }
    let body = match clean_comment(&form.text) {
        Ok(body) => body,
        Err(CommentProblem::Empty) => return refuse(StatusCode::BAD_REQUEST, s::COMMENT_EMPTY),
        Err(CommentProblem::TooLong) => return refuse(StatusCode::BAD_REQUEST, s::COMMENT_TOO_LONG),
    };
    let viewer = Viewer::new(&game, user, &headers);
    let now = unix_now() as i64;

    if let Some(parent) = form.parent {
        let Some(writer) = viewer.id() else { return refuse(StatusCode::FORBIDDEN, s::COMMENT_FAILED) };
        match state.db.insert_reply(&game.id, parent, writer, &body, now).await {
            Ok(Some(new)) => return written(&state, &viewer, new, 0).await,
            Ok(None) => {}
            Err(error) => {
                tracing::warn!(game = %game.id, %error, "saving a reply failed");
                return failed();
            }
        }
        return match state.db.reply_refusal(&game.id, parent, writer).await {
            Ok(Some(ReplyRefusal::GameGone)) => not_found(),
            Ok(Some(ReplyRefusal::NotOwner)) => refuse(StatusCode::FORBIDDEN, s::COMMENT_FAILED),
            Ok(Some(ReplyRefusal::ParentGone)) => refuse(StatusCode::GONE, s::COMMENT_GONE),
            Ok(Some(ReplyRefusal::NotTopLevel)) => refuse(StatusCode::BAD_REQUEST, s::COMMENT_FAILED),
            Ok(None) => refuse(StatusCode::CONFLICT, s::COMMENT_FAILED),
            Err(error) => {
                tracing::warn!(game = %game.id, %error, "reading a reply refusal failed");
                failed()
            }
        };
    }

    if viewer.owner {
        return refuse(StatusCode::FORBIDDEN, s::COMMENT_OWN);
    }
    let user_id = viewer.id();
    // A signed-in comment never stores the browser's hash.
    let hash = if user_id.is_some() { None } else { viewer.hash() };
    let gate = config.rating_gate_secs;
    if user_id.is_none() && hash.is_none() {
        return json_answer(StatusCode::CONFLICT, json!({"ok": false, "wait": gate, "message": s::comment_gate(gate)}));
    }
    let opened_by = now - gate as i64;
    let gap = config.comment_gap_secs as i64;
    let (cap, window) = (config.guest_comment_cap, config.guest_cap_window_secs as i64);
    match state.db.insert_comment(&game.id, user_id, hash, &body, now, opened_by, gap, cap, window).await {
        Ok(Some(new)) => return written(&state, &viewer, new, config.comment_gap_secs).await,
        Ok(None) => {}
        Err(error) => {
            tracing::warn!(game = %game.id, %error, "saving a comment failed");
            return failed();
        }
    }
    match state.db.comment_refusal(&game.id, user_id, hash, now, opened_by, gap, cap, window).await {
        Ok(Some(Refusal::Gate(wait))) => {
            json_answer(StatusCode::CONFLICT, json!({"ok": false, "wait": wait, "message": s::comment_gate(wait)}))
        }
        Ok(Some(Refusal::Gap(wait))) => {
            json_answer(StatusCode::TOO_MANY_REQUESTS, json!({"ok": false, "wait": wait, "message": s::comment_gap(wait)}))
        }
        Ok(Some(Refusal::Busy)) => refuse(StatusCode::TOO_MANY_REQUESTS, s::COMMENT_BUSY),
        Ok(Some(Refusal::Owner)) => refuse(StatusCode::FORBIDDEN, s::COMMENT_OWN),
        Ok(Some(Refusal::GameGone)) => not_found(),
        Ok(None) => refuse(StatusCode::CONFLICT, s::COMMENT_FAILED),
        Err(error) => {
            tracing::warn!(game = %game.id, %error, "reading a comment refusal failed");
            failed()
        }
    }
}

#[derive(Deserialize)]
pub struct More {
    before: Option<i64>,
}

/// `GET /play/<id>/comments?before=N`: the next page of a game's comments, as markup in JSON (a bare HTML fragment
/// would be a page without the signed-in header). Writes nothing.
pub async fn more(
    State(state): State<AppState>,
    user: Option<Extension<CurrentUser>>,
    Path(id): Path<String>,
    headers: HeaderMap,
    Query(query): Query<More>,
) -> Response {
    let user = user.map(|Extension(CurrentUser(user))| user);
    let Some(game) = live(&state, &id).await else { return not_found() };
    let viewer = Viewer::new(&game, user, &headers);
    match page_items(&state, &game, &viewer, query.before).await {
        Ok((html, more)) => json_answer(StatusCode::OK, json!({"ok": true, "html": html.into_string(), "more": more})),
        Err(error) => {
            tracing::warn!(game = %game.id, %error, "reading comments failed");
            failed()
        }
    }
}

/// `GET /teacher/comments?before=N`: the next page of 최근 댓글 (teacher only, route table).
pub async fn recent_more(
    State(state): State<AppState>,
    Extension(CurrentUser(user)): Extension<CurrentUser>,
    Query(query): Query<More>,
) -> Response {
    let viewer = Viewer::teacher(&user);
    match recent_items(&state, &viewer, query.before).await {
        Ok((html, more)) => json_answer(StatusCode::OK, json!({"ok": true, "html": html.into_string(), "more": more})),
        Err(error) => {
            tracing::warn!(%error, "reading recent comments failed");
            failed()
        }
    }
}

#[derive(Deserialize)]
pub struct DeleteForm {
    #[serde(rename = "as", default)]
    as_field: String,
}

/// `POST /comments/<id>/delete` with `as`. The teacher erases at once (with the replies); a writer only hides
/// their own (a guest from the same browser). Nobody else, the game's owner included.
pub async fn delete(
    State(state): State<AppState>,
    user: Option<Extension<CurrentUser>>,
    Path(id): Path<String>,
    headers: HeaderMap,
    form: Result<Form<DeleteForm>, FormRejection>,
) -> Response {
    let user = user.map(|Extension(CurrentUser(user))| user);
    let hash = rater(&headers).map(|token| session::token_hash(&token));
    let Ok(id) = id.parse::<i64>() else { return not_found() };
    let row = match state.db.comment_by_id(id, hash.as_deref()).await {
        Ok(Some(row)) => row,
        Ok(None) => return not_found(),
        Err(error) => {
            tracing::warn!(%error, "reading a comment failed");
            return failed();
        }
    };
    let form = match form {
        Ok(Form(form)) => form,
        Err(rejection) => return rejected(rejection),
    };
    if let Err(message) = check_as(&form.as_field, user.as_ref()) {
        return refuse(StatusCode::CONFLICT, message);
    }
    if let Some(teacher) = user.as_ref().filter(|u| u.role == Role::Teacher) {
        return match state.db.erase_comment(id).await {
            Ok(_) => {
                tracing::info!(comment = id, game = %row.game_id, by = %teacher.login, "comment erased");
                json_answer(StatusCode::OK, json!({"ok": true}))
            }
            Err(error) => {
                tracing::warn!(comment = id, %error, "erasing a comment failed");
                failed()
            }
        };
    }
    if row.hidden || row.parent_hidden {
        return refuse(StatusCode::GONE, s::COMMENT_GONE);
    }
    match state.db.hide_comment(id, user.as_ref().map(|u| u.id), hash.as_deref(), unix_now() as i64).await {
        Ok(true) => json_answer(StatusCode::OK, json!({"ok": true})),
        Ok(false) => refuse(StatusCode::FORBIDDEN, s::COMMENT_FAILED),
        Err(error) => {
            tracing::warn!(comment = id, %error, "hiding a comment failed");
            failed()
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn waits_say_minutes_then_seconds() {
        for (secs, button, gate) in [
            (180, "3분 후에 남길 수 있어요", "3분 후에 댓글을 남길 수 있어요"),
            (61, "2분 후에 남길 수 있어요", "2분 후에 댓글을 남길 수 있어요"),
            (60, "1분 후에 남길 수 있어요", "1분 후에 댓글을 남길 수 있어요"),
            (59, "59초 후에 남길 수 있어요", "59초 후에 댓글을 남길 수 있어요"),
            (1, "1초 후에 남길 수 있어요", "1초 후에 댓글을 남길 수 있어요"),
        ] {
            assert_eq!(s::comment_wait(secs), button);
            assert_eq!(s::comment_gate(secs), gate);
        }
        assert_eq!(s::comment_gap(12), "12초 후에 다시 남길 수 있어요.");
    }

    #[test]
    fn days_and_names() {
        assert_eq!(s::comment_ago(0), "오늘");
        assert_eq!(s::comment_ago(1), "어제");
        assert_eq!(s::comment_ago(5), "5일 전");
        assert_eq!(s::teacher_view_name("김실명", "별빛"), "김실명 (별빛)");
        assert_eq!(s::teacher_view_name("", "brisa0730"), "brisa0730");
        assert_eq!(s::comment_as("별빛"), "'별빛' 이름으로 남겨져요");
    }

    fn user(id: i64, role: Role) -> User {
        User { id, login: format!("u{id}"), display_name: "n".into(), real_name: String::new(), role, password_hash: String::new() }
    }

    #[test]
    fn the_post_must_match_the_page() {
        let kim = user(7, Role::Student);
        assert_eq!(check_as("guest", None), Ok(()));
        assert_eq!(check_as("7", Some(&kim)), Ok(()));
        assert_eq!(check_as("7", None), Err(s::COMMENT_SIGNED_OUT));
        assert_eq!(check_as("guest", Some(&kim)), Err(s::COMMENT_WHO_CHANGED));
        assert_eq!(check_as("8", Some(&kim)), Err(s::COMMENT_WHO_CHANGED));
    }
}
