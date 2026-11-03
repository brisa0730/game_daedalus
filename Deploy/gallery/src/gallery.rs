//! The game list (with "My games" on top), screenshots, the play view and removing a game.

use axum::Extension;
use axum::extract::{Path, State};
use axum::http::{HeaderValue, StatusCode, header};
use axum::response::{IntoResponse, Response};
use maud::{Markup, html};

use crate::app::AppState;
use crate::config::unix_now;
use crate::db::{Game, Role, User};
use crate::pages;
use crate::play_token::{ENTER_TTL_SECS, Purpose};
use crate::session::CurrentUser;
use crate::strings as s;
use crate::upload::{screenshot_url, valid_game_id};

fn card(game: &Game, user: &User) -> Markup {
    let can_remove = game.owner_id == Some(user.id) || user.role == Role::Teacher;
    html! {
        li.card {
            a href={ "/play/" (game.id) } {
                img src=(screenshot_url(game)) alt="" width="320" height="180" loading="lazy";
                span.title { (game.title) }
                span.owner { (game.owner_name.as_deref().unwrap_or("")) }
            }
            @if can_remove {
                a.remove href={ "/games/" (game.id) "/remove" } { (s::REMOVE) }
            }
        }
    }
}

pub async fn list(State(state): State<AppState>, Extension(CurrentUser(user)): Extension<CurrentUser>) -> Response {
    let (Ok(mine), Ok(all)) = (state.db.live_games_of(user.id).await, state.db.live_games().await) else {
        return pages::message(StatusCode::INTERNAL_SERVER_ERROR, Some(&user), s::SERVER_BUSY);
    };
    let body = html! {
        section.mine {
            h2 { (s::MY_GAMES) }
            @if mine.is_empty() {
                p { (s::MY_GAMES_EMPTY) " " a href="/upload" { (s::UPLOAD_FIRST) } }
            } @else {
                ul.cards { @for game in &mine { (card(game, &user)) } }
            }
        }
        section.all {
            h2 { (s::ALL_GAMES) }
            @if all.is_empty() {
                p { (s::NO_GAMES) }
            } @else {
                ul.cards { @for game in &all { (card(game, &user)) } }
            }
        }
    };
    pages::page(StatusCode::OK, s::NAV_GAMES, Some(&user), body)
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
        div.playbar {
            a href="/" { (s::BACK_TO_GAMES) }
            span.title { (game.title) }
            span.owner { (game.owner_name.as_deref().unwrap_or("")) }
            button #fullscreen type="button" { (s::FULLSCREEN) }
            span.note { (s::NEEDS_KEYBOARD) }
        }
        iframe #game src=(src) sandbox="allow-scripts allow-same-origin allow-pointer-lock"
            allow="fullscreen; gamepad; autoplay" title=(game.title) {}
    };
    pages::page(StatusCode::OK, &game.title, Some(&user), body)
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
    pages::page(StatusCode::OK, s::REMOVE, Some(&user), body)
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
