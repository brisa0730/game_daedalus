//! The game list (with "My games" on top), screenshots, the play view and removing a game.

use axum::Extension;
use axum::extract::{Path, State};
use axum::http::{HeaderValue, StatusCode, header};
use axum::response::{IntoResponse, Response};
use maud::{Markup, html};

use crate::app::AppState;
use crate::config::unix_now;
use crate::db::{Game, Role, User};
use crate::pages::{self, Nav};
use crate::play_token::{ENTER_TTL_SECS, Purpose};
use crate::session::CurrentUser;
use crate::strings as s;
use crate::upload::{screenshot_url, valid_game_id};

/// A game counts as new for its first week; an update doesn't make it new again.
const NEW_FOR_SECS: i64 = 7 * 24 * 60 * 60;
/// Korea's offset from UTC, so "today" and "yesterday" follow the class's calendar.
const KST_SECS: i64 = 9 * 60 * 60;

/// New for its first week after the first upload.
fn is_new(created_at: i64, now: i64) -> bool {
    now - created_at < NEW_FOR_SECS
}

/// Whole Korean calendar days from `then` to `now` (never negative).
fn days_ago(then: i64, now: i64) -> i64 {
    ((now + KST_SECS).div_euclid(86_400) - (then + KST_SECS).div_euclid(86_400)).max(0)
}

/// A game card. In "My games" (`mine`) it shows when the game was last uploaded instead of the owner's name.
fn card(game: &Game, user: &User, mine: bool, now: i64) -> Markup {
    let can_remove = may_remove(game, user);
    html! {
        li.card {
            a href={ "/play/" (game.id) } {
                span.thumb {
                    img src=(screenshot_url(game)) alt="" width="320" height="180" loading="lazy";
                    @if is_new(game.created_at, now) { span.badge { (s::NEW_BADGE) } }
                }
                span.title { (game.title) }
                @if !mine { span.owner { (game.owner_name.as_deref().unwrap_or("")) } }
            }
            @if mine || can_remove {
                div.card-foot {
                    @if mine { span.when { (s::uploaded_ago(days_ago(game.updated_at, now))) } }
                    @if can_remove { a.remove href={ "/games/" (game.id) "/remove" } { (s::REMOVE) } }
                }
            }
        }
    }
}

pub async fn list(State(state): State<AppState>, Extension(CurrentUser(user)): Extension<CurrentUser>) -> Response {
    let (Ok(mine), Ok(all)) = (state.db.live_games_of(user.id).await, state.db.live_games().await) else {
        return pages::message(StatusCode::INTERNAL_SERVER_ERROR, Some(&user), s::SERVER_BUSY);
    };
    let now = unix_now() as i64;
    let at_limit = mine.len() as i64 >= state.config.games_per_account;
    let body = html! {
        section.mine {
            h2 { (s::MY_GAMES) }
            ul.cards {
                @for game in &mine { (card(game, &user, true, now)) }
                li.add-slot {
                    @if at_limit {
                        p.limit { (s::too_many_games(state.config.games_per_account)) }
                    } @else {
                        a.add-card href="/upload" {
                            svg viewBox="0 0 7 7" width="28" height="28" shape-rendering="crispEdges" aria-hidden="true" {
                                rect x="3" width="1" height="7" fill="currentColor" {}
                                rect y="3" width="7" height="1" fill="currentColor" {}
                            }
                            @if mine.is_empty() { span.empty { (s::MY_GAMES_EMPTY) } }
                            span { (s::UPLOAD_FIRST) }
                        }
                    }
                }
            }
        }
        section.all {
            div.section-head {
                h2 { (s::ALL_GAMES) }
                @if !all.is_empty() { span.count { (s::game_count(all.len())) } }
            }
            @if all.is_empty() {
                p { (s::NO_GAMES) }
            } @else {
                ul.cards { @for game in &all { (card(game, &user, false, now)) } }
            }
        }
    };
    pages::page(StatusCode::OK, s::NAV_GAMES, Some(&user), Some(Nav::Games), body)
}

/// `GET /shots/<id>.png?v=<version>`: the live game's screenshot, always as `image/png` (never a type from the
/// upload), sandboxed and not sniffed. The current version is always served; `?v=` only makes the address change
/// with each upload, so the long browser cache never shows an old picture.
pub async fn screenshot(State(state): State<AppState>, Path(file): Path<String>) -> Response {
    let Some(id) = file.strip_suffix(".png").filter(|id| valid_game_id(id)) else {
        return StatusCode::NOT_FOUND.into_response();
    };
    let Ok(Some(game)) = state.db.live_game(id).await else { return StatusCode::NOT_FOUND.into_response() };
    let path = state.config.version_dir(&game.id, game.version).join("screenshot.png");
    let Ok(bytes) = tokio::fs::read(path).await else { return StatusCode::NOT_FOUND.into_response() };
    let mut response = bytes.into_response();
    let headers = response.headers_mut();
    headers.insert(header::CONTENT_TYPE, HeaderValue::from_static("image/png"));
    headers.insert(header::X_CONTENT_TYPE_OPTIONS, HeaderValue::from_static("nosniff"));
    headers.insert(header::CONTENT_SECURITY_POLICY, HeaderValue::from_static("sandbox"));
    // The address carries `?v=<version>` and a version's files never change, so the browser may keep it.
    headers.insert(header::CACHE_CONTROL, HeaderValue::from_static("private, max-age=31536000, immutable"));
    response
}

/// The play view: the game full-page in the games host's sandboxed frame, entered with a fresh 60 s grant.
pub async fn play_view(
    State(state): State<AppState>,
    Extension(CurrentUser(user)): Extension<CurrentUser>,
    Path(id): Path<String>,
) -> Response {
    let Some(game) = live(&state, &id).await else { return pages::not_found(Some(&user)) };
    let grant = state.key.sign(Purpose::Enter, &game.id, unix_now() + ENTER_TTL_SECS);
    let src = format!("{}/enter/{}?grant={}", state.config.games_origin, game.id, grant);
    let body = html! {
        div.theater {
            div.playbar {
                a.back href="/" { (s::BACK_TO_GAMES) }
                div.play-title {
                    h1 { (game.title) }
                    span.owner { (s::made_by(game.owner_name.as_deref().unwrap_or(""))) }
                }
                span.note { (s::NEEDS_KEYBOARD) }
                button #fullscreen type="button" {
                    svg viewBox="0 0 7 7" width="16" height="16" shape-rendering="crispEdges" aria-hidden="true" {
                        path d="M0 0h3v1H1v2H0zM4 0h3v3H6V1H4zM0 4h1v2h2v1H0zM6 4h1v3H4V6h2z" fill="currentColor" {}
                    }
                    (s::FULLSCREEN)
                }
            }
            // The box keeps the game at 16:9 on the page, and is what goes full screen (letterboxed).
            div #frame.frame {
                iframe #game src=(src) sandbox="allow-scripts allow-same-origin allow-pointer-lock"
                    allow="fullscreen; gamepad; autoplay" title=(game.title) {}
            }
        }
    };
    pages::page(StatusCode::OK, &game.title, Some(&user), None, body)
}

async fn live(state: &AppState, id: &str) -> Option<Game> {
    if !valid_game_id(id) {
        return None;
    }
    state.db.live_game(id).await.ok().flatten()
}

fn may_remove(game: &Game, user: &User) -> bool {
    game.owner_id == Some(user.id) || user.role == Role::Teacher
}

pub async fn remove_confirm(
    State(state): State<AppState>,
    Extension(CurrentUser(user)): Extension<CurrentUser>,
    Path(id): Path<String>,
) -> Response {
    let Some(game) = live(&state, &id).await.filter(|g| may_remove(g, &user)) else {
        return pages::not_found(Some(&user));
    };
    let body = html! {
        p.message { (s::remove_game_confirm(&game.title)) }
        form method="post" action={ "/games/" (game.id) "/remove" } {
            button.danger type="submit" { (s::REMOVE_BUTTON) }
            " " a href="/" { (s::CANCEL) }
        }
    };
    pages::page(StatusCode::OK, s::REMOVE, Some(&user), None, body)
}

pub async fn remove(
    State(state): State<AppState>,
    Extension(CurrentUser(user)): Extension<CurrentUser>,
    Path(id): Path<String>,
) -> Response {
    let Some(game) = live(&state, &id).await.filter(|g| may_remove(g, &user)) else {
        return pages::not_found(Some(&user));
    };
    let Some(_busy) = state.lock_games(std::slice::from_ref(&game.id)) else {
        return pages::message(StatusCode::CONFLICT, Some(&user), s::UPLOAD_BUSY);
    };
    if remove_game(&state, &game.id).await.is_err() {
        return pages::message(StatusCode::INTERNAL_SERVER_ERROR, Some(&user), s::SERVER_BUSY);
    }
    tracing::info!(game = %game.id, by = %user.login, "game removed");
    pages::see_other("/")
}

/// Tombstones the game (its id stays taken) and deletes its files. The caller holds the game's busy lock.
pub async fn remove_game(state: &AppState, id: &str) -> Result<(), sqlx::Error> {
    state.db.tombstone_game(id, unix_now() as i64).await?;
    let _ = tokio::fs::remove_dir_all(state.config.games_dir().join(id)).await;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn days_follow_the_korean_calendar() {
        // 2026-11-03 00:30 KST is 2026-11-02 15:30 UTC.
        let just_after_midnight = 1_793_633_400;
        assert_eq!(days_ago(just_after_midnight - 60 * 60, just_after_midnight), 1, "23:30 the evening before");
        assert_eq!(days_ago(just_after_midnight, just_after_midnight + 23 * 60 * 60), 0);
        assert_eq!(days_ago(just_after_midnight - 2 * 86_400, just_after_midnight), 2);
        assert_eq!(days_ago(just_after_midnight + 10, just_after_midnight), 0, "a clock step back is today");
        assert_eq!(s::uploaded_ago(0), "오늘 올림");
        assert_eq!(s::uploaded_ago(1), "어제 올림");
        assert_eq!(s::uploaded_ago(30), "30일 전에 올림");
    }

    #[test]
    fn a_game_is_new_for_exactly_a_week() {
        let now = 1_793_633_400;
        assert!(is_new(now, now));
        assert!(is_new(now - NEW_FOR_SECS + 1, now));
        assert!(!is_new(now - NEW_FOR_SECS, now));
    }
}
