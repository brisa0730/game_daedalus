//! The teacher page: the students list (real name and public nickname, both editable), adding students one at
//! a time or by pasting a list (with a preview), the printable slips, password resets and removing an account. Every route here is teacher-only (route table).

use std::collections::HashSet;

use axum::extract::{Form, Path, State};
use axum::http::{HeaderValue, StatusCode, header};
use axum::response::Response;
use axum::Extension;
use maud::{Markup, html};
use serde::Deserialize;

use crate::accounts::{HashError, generate_password};
use crate::app::AppState;
use crate::config::{GALLERY_HOST, unix_now};
use crate::db::{Role, User};
use crate::gallery::remove_game;
use crate::pages::{self, Nav};
use crate::session::CurrentUser;
use crate::strings as s;
use crate::text::{clean_label, normalize_login};
use crate::upload::user_busy_key;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum LineStatus {
    Ok,
    BadId,
    NoName,
    NoNickname,
    Duplicate,
    Taken,
}

impl LineStatus {
    fn text(self) -> &'static str {
        match self {
            LineStatus::Ok => s::LINE_OK,
            LineStatus::BadId => s::LINE_BAD_ID,
            LineStatus::NoName => s::LINE_NO_NAME,
            LineStatus::NoNickname => s::LINE_NO_NICKNAME,
            LineStatus::Duplicate => s::LINE_DUPLICATE,
            LineStatus::Taken => s::LINE_TAKEN,
        }
    }
}

#[derive(Debug)]
pub struct Line {
    pub raw_id: String,
    pub raw_name: String,
    pub raw_nickname: String,
    pub login: Option<String>,
    pub name: Option<String>,
    pub nickname: Option<String>,
    pub status: LineStatus,
}

/// Splits at the first comma or tab: the part before it, and the (trimmed) rest.
fn split_field(text: &str) -> (&str, &str) {
    match text.find([',', '\t']) {
        Some(at) => (text[..at].trim(), text[at + 1..].trim()),
        None => (text.trim(), ""),
    }
}

/// One student per line, `ID, real name, nickname` (split at the first two commas or tabs, so a nickname may contain
/// commas and a real name may not). Both names are required: a missing nickname never falls back to the real name,
/// which would publish it. Blank lines are skipped.
pub fn parse_lines(text: &str, name_max: usize) -> Vec<Line> {
    let mut seen = HashSet::new();
    text.lines()
        .filter(|l| !l.trim().is_empty())
        .map(|l| {
            let (raw_id, rest) = split_field(l);
            let (raw_name, raw_nickname) = split_field(rest);
            let login = normalize_login(raw_id);
            let name = clean_label(raw_name, name_max);
            let nickname = clean_label(raw_nickname, name_max);
            let status = match (&login, &name, &nickname) {
                (None, _, _) => LineStatus::BadId,
                (_, None, _) => LineStatus::NoName,
                (_, _, None) => LineStatus::NoNickname,
                (Some(id), _, _) if !seen.insert(id.clone()) => LineStatus::Duplicate,
                _ => LineStatus::Ok,
            };
            let (raw_id, raw_name, raw_nickname) = (raw_id.into(), raw_name.into(), raw_nickname.into());
            Line { raw_id, raw_name, raw_nickname, login, name, nickname, status }
        })
        .collect()
}

async fn check_lines(state: &AppState, text: &str) -> Vec<Line> {
    let mut lines = parse_lines(text, state.config.name_max_chars);
    let logins: Vec<String> = lines.iter().filter(|l| l.status == LineStatus::Ok).filter_map(|l| l.login.clone()).collect();
    let taken: HashSet<String> = state.db.existing_logins(&logins).await.unwrap_or(logins).into_iter().collect();
    for line in &mut lines {
        if line.status == LineStatus::Ok && line.login.as_ref().is_some_and(|l| taken.contains(l)) {
            line.status = LineStatus::Taken;
        }
    }
    lines
}

pub async fn page(State(state): State<AppState>, Extension(CurrentUser(user)): Extension<CurrentUser>) -> Response {
    let students = state.db.students().await.unwrap_or_default();
    let body = html! {
        h1 { (s::TEACHER_TITLE) }
        section {
            h2 { (s::STUDENTS) }
            @if students.is_empty() {
                p { (s::NO_STUDENTS) }
            } @else {
                p.note { (s::NAMES_HINT) }
                table.students {
                    thead {
                        tr { th { (s::COL_ID) } th { (s::COL_NAME) " · " (s::COL_NICKNAME) } th { (s::COL_GAMES) } th {} th {} }
                    }
                    tbody {
                        @for st in &students {
                            tr {
                                td { (st.login) }
                                td {
                                    form.rename method="post" action={ "/teacher/students/" (st.id) "/rename" } {
                                        input name="real_name" value=(st.real_name) placeholder=(s::COL_NAME) aria-label=(s::COL_NAME)
                                            autocomplete="off" required;
                                        input name="nickname" value=(st.display_name) placeholder=(s::COL_NICKNAME)
                                            aria-label=(s::COL_NICKNAME) autocomplete="off" required;
                                        button type="submit" { (s::RENAME) }
                                    }
                                }
                                td { (st.live_games) }
                                td { form method="post" action={ "/teacher/students/" (st.id) "/reset" } {
                                    button type="submit" { (s::RESET_PASSWORD) }
                                } }
                                td { a href={ "/teacher/students/" (st.id) "/remove" } { (s::REMOVE_ACCOUNT) } }
                            }
                        }
                    }
                }
            }
        }
        section {
            h2 { (s::ADD_ONE) }
            form.add method="post" action="/teacher/students/preview" {
                label { (s::COL_ID) input name="id" autocapitalize="none"; }
                // Autofill off: a remembered real name must never be offered in a nickname box.
                label { (s::COL_NAME) input name="name" autocomplete="off"; }
                label { (s::COL_NICKNAME) input name="nickname" autocomplete="off"; }
                button type="submit" { (s::PREVIEW) }
            }
        }
        section {
            h2 { (s::PASTE_MANY) }
            form method="post" action="/teacher/students/preview" {
                label { (s::PASTE_HINT) textarea name="text" rows="10" {} }
                button type="submit" { (s::PREVIEW) }
            }
        }
    };
    pages::page(StatusCode::OK, s::TEACHER_TITLE, Some(&user), Some(Nav::Teacher), body)
}

#[derive(Deserialize)]
pub struct StudentsForm {
    #[serde(default)]
    text: String,
    #[serde(default)]
    id: String,
    #[serde(default)]
    name: String,
    #[serde(default)]
    nickname: String,
}

impl StudentsForm {
    /// The add-one form's three fields become one line.
    fn text(&self) -> String {
        let fields = [self.id.trim(), self.name.trim(), self.nickname.trim()];
        if self.text.trim().is_empty() && fields.iter().any(|f| !f.is_empty()) {
            fields.join(", ")
        } else {
            self.text.clone()
        }
    }
}

fn preview_page(user: &User, text: &str, lines: &[Line], note: Option<&str>) -> Response {
    let all_ok = !lines.is_empty() && lines.iter().all(|l| l.status == LineStatus::Ok);
    let body = html! {
        h1 { (s::PREVIEW) }
        @if let Some(note) = note { p.error role="alert" { (note) } }
        @if lines.is_empty() {
            p { (s::NOTHING_TO_ADD) }
        } @else {
            table.preview {
                thead { tr { th { (s::COL_ID) } th { (s::COL_NAME) } th { (s::COL_NICKNAME) } th { (s::COL_STATUS) } } }
                tbody {
                    @for line in lines {
                        tr class=(if line.status == LineStatus::Ok { "ok" } else { "bad" }) {
                            td { (line.raw_id) } td { (line.raw_name) } td { (line.raw_nickname) } td { (line.status.text()) }
                        }
                    }
                }
            }
        }
        @if all_ok {
            form method="post" action="/teacher/students/create" {
                input type="hidden" name="text" value=(text);
                button type="submit" { (s::CREATE) }
            }
        } @else if !lines.is_empty() {
            p { (s::FIX_ERRORS) }
        }
        form method="post" action="/teacher/students/preview" {
            textarea name="text" rows="10" { (text) }
            button type="submit" { (s::PREVIEW) }
        }
        p { a href="/teacher" { (s::BACK_TO_TEACHER) } }
    };
    let status = if all_ok || lines.is_empty() { StatusCode::OK } else { StatusCode::UNPROCESSABLE_ENTITY };
    pages::page(status, s::PREVIEW, Some(user), Some(Nav::Teacher), body)
}

pub async fn preview(
    State(state): State<AppState>,
    Extension(CurrentUser(user)): Extension<CurrentUser>,
    Form(form): Form<StudentsForm>,
) -> Response {
    let text = form.text();
    let lines = check_lines(&state, &text).await;
    preview_page(&user, &text, &lines, None)
}

/// Creates every student in one go (all or none), then shows their slips once.
pub async fn create(
    State(state): State<AppState>,
    Extension(CurrentUser(user)): Extension<CurrentUser>,
    Form(form): Form<StudentsForm>,
) -> Response {
    let text = form.text();
    let lines = check_lines(&state, &text).await;
    if lines.is_empty() || lines.iter().any(|l| l.status != LineStatus::Ok) {
        return preview_page(&user, &text, &lines, Some(s::FIX_ERRORS));
    }
    let mut rows = Vec::new();
    let mut slips = Vec::new();
    for line in &lines {
        let (Some(login), Some(name), Some(nickname)) =
            (line.login.clone(), line.name.clone(), line.nickname.clone())
        else {
            continue;
        };
        let password = generate_password();
        let hash = match state.hasher.hash(password.clone()).await {
            Ok(hash) => hash,
            Err(HashError::Busy | HashError::Internal) => {
                return pages::message(StatusCode::SERVICE_UNAVAILABLE, Some(&user), s::SERVER_BUSY);
            }
        };
        rows.push((login.clone(), name.clone(), nickname.clone(), hash));
        slips.push(Slip { login, real_name: name, nickname, password });
    }
    if state.db.create_students(&rows, unix_now() as i64).await.is_err() {
        // Someone took an ID meanwhile: nothing was created.
        let lines = check_lines(&state, &text).await;
        return preview_page(&user, &text, &lines, Some(s::FIX_ERRORS));
    }
    tracing::info!(count = slips.len(), by = %user.login, "students created");
    slips_page(&user, s::SLIPS_TITLE, &slips)
}

/// One printed account slip: the real name (so it reaches the right student) with the nickname beside it.
struct Slip {
    login: String,
    real_name: String,
    nickname: String,
    password: String,
}

fn slip(slip: &Slip) -> Markup {
    html! {
        div.slip {
            p.slip-name { (slip.real_name) " " span.slip-nickname { "(" (slip.nickname) ")" } }
            dl {
                dt { (s::SLIP_ADDRESS) } dd { "https://" (GALLERY_HOST) }
                dt { (s::SLIP_ID) } dd.big { (slip.login) }
                dt { (s::SLIP_PASSWORD) } dd.big { (slip.password) }
            }
        }
    }
}

fn slips_page(user: &User, title: &str, slips: &[Slip]) -> Response {
    let body = html! {
        h1 { (title) }
        p.note.no-print { (s::SLIPS_NOTE) }
        p.no-print { button #print type="button" { (s::PRINT) } " " a href="/teacher" { (s::BACK_TO_TEACHER) } }
        div.slips { @for one in slips { (slip(one)) } }
    };
    let mut response = pages::page(StatusCode::OK, title, Some(user), Some(Nav::Teacher), body);
    response.headers_mut().insert(header::CACHE_CONTROL, HeaderValue::from_static("no-store"));
    response
}

async fn student(state: &AppState, id: &str) -> Option<User> {
    let id: i64 = id.parse().ok()?;
    state.db.user_by_id(id).await.ok().flatten().filter(|u| u.role == Role::Student)
}

pub async fn reset(
    State(state): State<AppState>,
    Extension(CurrentUser(user)): Extension<CurrentUser>,
    Path(id): Path<String>,
) -> Response {
    let Some(target) = student(&state, &id).await else { return pages::not_found(Some(&user)) };
    let password = generate_password();
    let Ok(hash) = state.hasher.hash(password.clone()).await else {
        return pages::message(StatusCode::SERVICE_UNAVAILABLE, Some(&user), s::SERVER_BUSY);
    };
    if state.db.set_password(target.id, &hash).await.is_err() {
        return pages::message(StatusCode::INTERNAL_SERVER_ERROR, Some(&user), s::SERVER_BUSY);
    }
    state.throttle.lock().expect("throttle lock").clear(target.id);
    tracing::info!(student = %target.login, by = %user.login, "password reset");
    let one = Slip { login: target.login, real_name: target.real_name, nickname: target.display_name, password };
    slips_page(&user, s::NEW_PASSWORD_TITLE, &[one])
}

#[derive(Deserialize)]
pub struct RenameForm {
    #[serde(default)]
    real_name: String,
    #[serde(default)]
    nickname: String,
}

/// `POST /teacher/students/<id>/rename`: sets both names. Both are required; an empty one changes nothing.
pub async fn rename(
    State(state): State<AppState>,
    Extension(CurrentUser(user)): Extension<CurrentUser>,
    Path(id): Path<String>,
    Form(form): Form<RenameForm>,
) -> Response {
    let Some(target) = student(&state, &id).await else { return pages::not_found(Some(&user)) };
    let max = state.config.name_max_chars;
    let Some(real_name) = clean_label(&form.real_name, max) else {
        return pages::message(StatusCode::BAD_REQUEST, Some(&user), s::LINE_NO_NAME);
    };
    let Some(nickname) = clean_label(&form.nickname, max) else {
        return pages::message(StatusCode::BAD_REQUEST, Some(&user), s::LINE_NO_NICKNAME);
    };
    if state.db.set_names(target.id, &real_name, &nickname).await.is_err() {
        return pages::message(StatusCode::INTERNAL_SERVER_ERROR, Some(&user), s::SERVER_BUSY);
    }
    tracing::info!(student = %target.login, by = %user.login, "student renamed");
    pages::see_other("/teacher")
}

pub async fn remove_confirm(
    State(state): State<AppState>,
    Extension(CurrentUser(user)): Extension<CurrentUser>,
    Path(id): Path<String>,
) -> Response {
    let Some(target) = student(&state, &id).await else { return pages::not_found(Some(&user)) };
    let games = state.db.live_game_count(target.id).await.unwrap_or(0);
    let body = html! {
        p.message { (s::remove_account_confirm(&target.real_name, &target.display_name, games)) }
        form method="post" action={ "/teacher/students/" (target.id) "/remove" } {
            button.danger type="submit" { (s::REMOVE_BUTTON) }
            " " a href="/teacher" { (s::CANCEL) }
        }
    };
    pages::page(StatusCode::OK, s::REMOVE_ACCOUNT, Some(&user), Some(Nav::Teacher), body)
}

/// Removes a student and all their games. Waits for nothing: if any of their games is uploading, it refuses.
pub async fn remove(
    State(state): State<AppState>,
    Extension(CurrentUser(user)): Extension<CurrentUser>,
    Path(id): Path<String>,
) -> Response {
    let Some(target) = student(&state, &id).await else { return pages::not_found(Some(&user)) };
    let Ok(games) = state.db.live_games_of(target.id).await else {
        return pages::message(StatusCode::INTERNAL_SERVER_ERROR, Some(&user), s::SERVER_BUSY);
    };
    let ids: Vec<String> = games.iter().map(|g| g.id.clone()).collect();
    // Their games and their upload slot: no upload can add a game while the account goes.
    let mut keys = ids.clone();
    keys.push(user_busy_key(target.id));
    let Some(_busy) = state.lock_games(&keys) else {
        return pages::message(StatusCode::CONFLICT, Some(&user), s::UPLOAD_BUSY);
    };
    for id in &ids {
        if remove_game(&state, id).await.is_err() {
            return pages::message(StatusCode::INTERNAL_SERVER_ERROR, Some(&user), s::SERVER_BUSY);
        }
    }
    if state.db.delete_user(target.id).await.is_err() {
        return pages::message(StatusCode::INTERNAL_SERVER_ERROR, Some(&user), s::SERVER_BUSY);
    }
    state.throttle.lock().expect("throttle lock").clear(target.id);
    tracing::info!(student = %target.login, games = ids.len(), by = %user.login, "account removed");
    pages::see_other("/teacher")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn pasted_lines_are_checked() {
        let text = "kim, 김민수, 별빛\n\nlee\t이서연\t구름\nbad id, 박, 박\nkim, 다른 김, 김\npark,\nchoi, 최민준\n";
        let lines = parse_lines(text, 20);
        let statuses: Vec<LineStatus> = lines.iter().map(|l| l.status).collect();
        use LineStatus::*;
        assert_eq!(statuses, [Ok, Ok, BadId, Duplicate, NoName, NoNickname]);
        assert_eq!((lines[0].name.as_deref(), lines[0].nickname.as_deref()), (Some("김민수"), Some("별빛")));
        assert_eq!(lines[1].login.as_deref(), Some("lee"));
        assert_eq!(lines[1].nickname.as_deref(), Some("구름"));
    }

    #[test]
    fn a_nickname_may_contain_commas() {
        let lines = parse_lines("kim, 김민수, 별, 빛", 20);
        assert_eq!((lines[0].name.as_deref(), lines[0].nickname.as_deref()), (Some("김민수"), Some("별, 빛")));
    }
}
