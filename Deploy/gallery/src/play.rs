//! The games host (`suwon-play.orcarin.com`): trades the gallery's `enter` grant for a per-game gate cookie, then
//! serves the server's own player page and the build's four files. Nothing else from an upload is ever served.

use axum::extract::{Path, Query, State};
use axum::http::{HeaderMap, HeaderValue, StatusCode, header};
use axum::response::{Html, IntoResponse, Response};
use maud::{DOCTYPE, html};
use serde::Deserialize;

use crate::app::AppState;
use crate::config::unix_now;
use crate::db::Game;
use crate::files;
use crate::play_token::{GATE_TTL_SECS, Purpose};
use crate::session::cookie;
use crate::share_zip::build_files;
use crate::strings as s;
use crate::upload::valid_game_id;

fn gate_cookie_name(id: &str) -> String {
    format!("__Host-g_{id}")
}

async fn live(state: &AppState, id: &str) -> Option<Game> {
    if !valid_game_id(id) {
        return None;
    }
    state.db.live_game(id).await.ok().flatten()
}

/// The one-line Korean page for a missing or expired gate.
fn reopen_page() -> Response {
    let markup = html! {
        (DOCTYPE)
        html lang="ko" {
            head {
                meta charset="utf-8";
                title { (s::REOPEN_FROM_GALLERY) }
                // Shown inside the gallery's dark frame: light text, or the student sees an empty black box.
                style { "body{margin:0;padding:2em;background:#0b0f1c;color:#fff;font:18px sans-serif}" }
            }
            body { p { (s::REOPEN_FROM_GALLERY) } }
        }
    };
    (StatusCode::FORBIDDEN, Html(markup.into_string())).into_response()
}

#[derive(Deserialize)]
pub struct EnterQuery {
    grant: String,
}

/// `GET /enter/<id>?grant=…`: only as the gallery's frame (`Sec-Fetch-Dest: iframe`, same-site), so a copied link
/// opened in a tab or sent outside the class doesn't get in.
pub async fn enter(
    State(state): State<AppState>,
    Path(id): Path<String>,
    Query(query): Query<EnterQuery>,
    headers: HeaderMap,
) -> Response {
    let dest_is_frame = headers.get("sec-fetch-dest").is_some_and(|v| v == "iframe");
    let site_ok = headers.get("sec-fetch-site").is_none_or(|v| v == "same-site");
    if !dest_is_frame || !site_ok {
        return reopen_page();
    }
    let Some(game) = live(&state, &id).await else { return StatusCode::NOT_FOUND.into_response() };
    let now = unix_now();
    if !state.key.verify(Purpose::Enter, &game.id, &query.grant, now) {
        return reopen_page();
    }
    let gate = state.key.sign(Purpose::Gate, &game.id, now + GATE_TTL_SECS);
    let set_cookie = format!(
        "{}={gate}; Secure; HttpOnly; SameSite=Lax; Path=/; Max-Age={GATE_TTL_SECS}",
        gate_cookie_name(&game.id)
    );
    let mut response = (StatusCode::FOUND, [(header::LOCATION, format!("/g/{}/", game.id))]).into_response();
    response.headers_mut().insert(header::SET_COOKIE, HeaderValue::from_str(&set_cookie).expect("cookie is ASCII"));
    response
}

/// The live game, if the request carries a valid gate cookie for it.
async fn gated(state: &AppState, id: &str, headers: &HeaderMap) -> Result<Game, Response> {
    let Some(game) = live(state, id).await else { return Err(StatusCode::NOT_FOUND.into_response()) };
    let token = cookie(headers, &gate_cookie_name(&game.id)).unwrap_or("");
    if !state.key.verify(Purpose::Gate, &game.id, token, unix_now()) {
        return Err(reopen_page());
    }
    Ok(game)
}

/// `GET /g/<id>/`: the server's own player page; the zip's `index.html` is never used.
pub async fn player_page(State(state): State<AppState>, Path(id): Path<String>, headers: HeaderMap) -> Response {
    let game = match gated(&state, &id, &headers).await {
        Ok(game) => game,
        Err(response) => return response,
    };
    let [loader, data, framework, code] = build_files(&game.build_name);
    let markup = html! {
        (DOCTYPE)
        html lang="ko" {
            head {
                meta charset="utf-8";
                meta name="viewport" content="width=device-width, initial-scale=1";
                title { (game.title) }
                style { "html,body{margin:0;height:100%;background:#0b0f1c;overflow:hidden}#unity-canvas{width:100%;height:100%;display:block;outline:none}#error{position:fixed;top:1em;left:1em;color:#fff;font:16px sans-serif}" }
            }
            body {
                canvas #unity-canvas tabindex="-1"
                    data-loader={ "Build/" (loader) } data-data={ "Build/" (data) }
                    data-framework={ "Build/" (framework) } data-code={ "Build/" (code) }
                    data-error=(s::PLAYER_LOAD_FAILED) {}
                p #error hidden {}
                script src="player.js" {}
            }
        }
    };
    let mut response = Html(markup.into_string()).into_response();
    response.headers_mut().insert(header::CACHE_CONTROL, HeaderValue::from_static("private, no-cache"));
    response
}

pub async fn player_js(State(state): State<AppState>, Path(id): Path<String>, headers: HeaderMap) -> Response {
    if let Err(response) = gated(&state, &id, &headers).await {
        return response;
    }
    (
        [(header::CONTENT_TYPE, "application/javascript; charset=utf-8"), (header::CACHE_CONTROL, "private, no-cache")],
        include_str!("../static/player.js"),
    )
        .into_response()
}

/// `GET /g/<id>/Build/<file>`: only the four file names stored for the live version, with the spike's headers.
pub async fn build_file(
    State(state): State<AppState>,
    Path((id, file)): Path<(String, String)>,
    headers: HeaderMap,
) -> Response {
    let game = match gated(&state, &id, &headers).await {
        Ok(game) => game,
        Err(response) => return response,
    };
    if !build_files(&game.build_name).contains(&file) {
        return StatusCode::NOT_FOUND.into_response();
    }
    // A version folder never changes, so the version is the validator: a reload costs a 304, and a re-upload
    // (new version) changes it. Ranges are not served (`Accept-Ranges: none`), and a request for a range always gets
    // the whole file, never a 304: a browser resuming a half-cached file must not be told its fragment is complete.
    let etag = format!("\"{}-v{}\"", game.id, game.version);
    let wants_range = headers.contains_key(header::RANGE);
    if !wants_range && headers.get(header::IF_NONE_MATCH).is_some_and(|v| v.as_bytes() == etag.as_bytes()) {
        let mut response = StatusCode::NOT_MODIFIED.into_response();
        response.headers_mut().insert(header::ETAG, HeaderValue::from_str(&etag).expect("etag is ASCII"));
        response.headers_mut().insert(header::CACHE_CONTROL, HeaderValue::from_static("private, no-cache, no-transform"));
        response.headers_mut().insert(header::ACCEPT_RANGES, HeaderValue::from_static("none"));
        return response;
    }
    let path = state.config.version_dir(&game.id, game.version).join("Build").join(&file);
    let Ok(bytes) = tokio::fs::read(path).await else { return StatusCode::NOT_FOUND.into_response() };
    let mut response = bytes.into_response();
    for (name, value) in files::response_headers(&file) {
        response.headers_mut().insert(name, HeaderValue::from_static(value));
    }
    response.headers_mut().insert(header::ETAG, HeaderValue::from_str(&etag).expect("etag is ASCII"));
    response.headers_mut().insert(header::ACCEPT_RANGES, HeaderValue::from_static("none"));
    response
}
