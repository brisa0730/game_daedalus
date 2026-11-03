//! Uploading a Share zip: the upload page, the streamed upload, the caps, and replacing a game in place. A game's
//! address never changes; each upload unpacks into a new version folder and one database update switches to it.

use std::collections::HashSet;
use std::path::Path;

use axum::Extension;
use axum::body::Body;
use axum::extract::{Query, State};
use axum::http::{HeaderMap, StatusCode, header};
use axum::response::{IntoResponse, Json, Response};
use http_body_util::BodyExt;
use maud::html;
use rand::Rng;
use serde::Deserialize;
use tokio::io::AsyncWriteExt;

use crate::app::AppState;
use crate::config::{Config, unix_now};
use crate::db::{Game, NewGame};
use crate::pages::{self, Nav};
use crate::session::CurrentUser;
use crate::share_zip::{self, Limits};
use crate::storage;
use crate::strings as s;

/// Game ids: 10 characters from an alphabet without look-alikes; random, never a title slug, never reused.
pub const ID_ALPHABET: &[u8] = b"abcdefghjkmnpqrstuvwxyz23456789";
pub const ID_LEN: usize = 10;

pub fn valid_game_id(id: &str) -> bool {
    id.len() == ID_LEN && id.bytes().all(|b| ID_ALPHABET.contains(&b))
}

fn random_game_id() -> String {
    let mut rng = rand::rng();
    (0..ID_LEN).map(|_| char::from(ID_ALPHABET[rng.random_range(0..ID_ALPHABET.len())])).collect()
}

pub fn json_answer(status: StatusCode, body: serde_json::Value) -> Response {
    (status, Json(body)).into_response()
}

fn refuse(status: StatusCode, message: &str) -> Response {
    json_answer(status, serde_json::json!({"ok": false, "message": message}))
}

pub async fn upload_page(State(state): State<AppState>, Extension(CurrentUser(user)): Extension<CurrentUser>) -> Response {
    let games = state.db.live_games_of(user.id).await.unwrap_or_default();
    let body = html! {
        h1 { (s::UPLOAD_TITLE) }
        form #upload
            data-max-bytes=(state.config.max_upload_bytes)
            data-msg-pick-zip=(s::PICK_ZIP)
            data-msg-pick-game=(s::PICK_GAME)
            data-msg-too-big=(s::file_too_big(state.config.max_upload_bytes))
            data-msg-network=(s::UPLOAD_NETWORK_ERROR)
            data-msg-uploading=(s::UPLOADING)
            data-msg-login=(s::LOGIN_AGAIN) {
            // Numbered by the stylesheet; with no games yet there is nothing to pick, so that step is left out (the script
            // sends a new game when no choice is checked).
            ol.steps {
                li {
                    h2 { (s::STEP_MAKE) }
                    p { (s::STEP_MAKE_HOW) }
                }
                @if !games.is_empty() {
                    li {
                        h2 { (s::STEP_PICK) }
                        fieldset {
                            label.choice { input type="radio" name="target" value="new"; " " (s::NEW_GAME) }
                            label.choice { input type="radio" name="target" value="update"; " " (s::UPDATE_MY_GAME) }
                            ul.pick {
                                @for game in &games {
                                    li { label {
                                        input type="radio" name="game" value=(game.id);
                                        img src=(screenshot_url(game)) alt="" width="128" height="72" loading="lazy";
                                        " " (game.title)
                                    } }
                                }
                            }
                        }
                    }
                }
                li {
                    h2 { (s::STEP_FILE) }
                    // The file input covers the whole box, so clicking or dropping a file uses the browser's own input.
                    label.drop {
                        input #zip type="file" accept=".zip";
                        svg viewBox="0 0 10 12" width="40" height="48" shape-rendering="crispEdges" aria-hidden="true" {
                            path d="M0 0h7v3h3v9H0z" fill="#c9d0e8" {}
                            path d="M7 0h1v3H7z" fill="#8a91a3" {}
                            path d="M3 1h1v1H3zM3 3h1v1H3zM3 5h1v1H3zM2 7h3v3H2z" fill="#141a2e" {}
                        }
                        span #zip-name.drop-name { (s::CHOOSE_FILE) }
                        span.drop-hint { (s::DROP_HINT) }
                    }
                    div.send {
                        button #upload-button type="submit" { (s::UPLOAD_BUTTON) }
                        progress #progress max="100" value="0" hidden {}
                        span #progress-pct aria-hidden="true" {}
                    }
                    p #upload-message role="status" {}
                }
            }
        }
    };
    pages::page(StatusCode::OK, s::UPLOAD_TITLE, Some(&user), Some(Nav::Upload), body)
}

pub fn screenshot_url(game: &Game) -> String {
    format!("/shots/{}.png?v={}", game.id, game.version)
}

#[derive(Deserialize)]
pub struct UploadQuery {
    game: String,
}

enum Target {
    New,
    Update(Game),
}

/// `POST /upload?game=new|<id>`, body `application/zip`.
pub async fn post_upload(
    State(state): State<AppState>,
    Extension(CurrentUser(user)): Extension<CurrentUser>,
    Query(query): Query<UploadQuery>,
    headers: HeaderMap,
    body: Body,
) -> Response {
    let config = state.config.clone();
    let max = config.max_upload_bytes;
    let too_big = s::file_too_big(max);
    let length = headers.get(header::CONTENT_LENGTH).and_then(|v| v.to_str().ok()).and_then(|v| v.parse::<u64>().ok());
    if length.is_some_and(|n| n > max) {
        return refuse(StatusCode::PAYLOAD_TOO_LARGE, &too_big);
    }

    // One upload at a time per student, and the game itself held, both taken before the game is read: two updates
    // can't interleave, and removing the game or the account (which take the same keys) waits for nothing.
    let user_key = user_busy_key(user.id);
    let (target, game_id, _busy) = if query.game == "new" {
        let Some(id) = fresh_game_id(&state).await else { return refuse(StatusCode::INTERNAL_SERVER_ERROR, s::UPLOAD_FAILED) };
        let Some(busy) = state.lock_games(&[user_key, id.clone()]) else { return refuse(StatusCode::CONFLICT, s::UPLOAD_BUSY) };
        match state.db.live_game_count(user.id).await {
            Ok(n) if n < config.games_per_account => (Target::New, id, busy),
            Ok(_) => return refuse(StatusCode::CONFLICT, &s::too_many_games(config.games_per_account)),
            Err(_) => return refuse(StatusCode::INTERNAL_SERVER_ERROR, s::UPLOAD_FAILED),
        }
    } else {
        // Only the user's own live game; anything else is 404, so ids of others' games reveal nothing.
        if !valid_game_id(&query.game) {
            return refuse(StatusCode::NOT_FOUND, s::GAME_NOT_FOUND);
        }
        let Some(busy) = state.lock_games(&[user_key, query.game.clone()]) else {
            return refuse(StatusCode::CONFLICT, s::UPLOAD_BUSY);
        };
        match state.db.live_game(&query.game).await {
            Ok(Some(game)) if game.owner_id == Some(user.id) => (Target::Update(game), query.game.clone(), busy),
            Ok(_) => return refuse(StatusCode::NOT_FOUND, s::GAME_NOT_FOUND),
            Err(_) => return refuse(StatusCode::INTERNAL_SERVER_ERROR, s::UPLOAD_FAILED),
        }
    };

    if !disk_has_room(&state, config.max_unpacked_bytes).await {
        return refuse(StatusCode::INSUFFICIENT_STORAGE, s::DISK_FULL);
    }

    let staging_zip = config.staging_dir().join(format!("{}.zip", random_game_id()));
    let staging_dir = config.staging_dir().join(random_game_id());
    // Deletes the staging files however this ends, even if the client goes away mid-upload.
    let _cleanup = Cleanup(vec![staging_zip.clone(), staging_dir.clone()]);
    receive_and_place(&state, &user.id, &target, &game_id, body, &staging_zip, &staging_dir, &too_big).await
}

/// The busy-set key that allows one upload at a time per student.
pub fn user_busy_key(user_id: i64) -> String {
    format!("user:{user_id}")
}

struct Cleanup(Vec<std::path::PathBuf>);

impl Drop for Cleanup {
    fn drop(&mut self) {
        for path in &self.0 {
            let _ = if path.is_dir() { std::fs::remove_dir_all(path) } else { std::fs::remove_file(path) };
        }
    }
}

#[allow(clippy::too_many_arguments)]
async fn receive_and_place(
    state: &AppState,
    user_id: &i64,
    target: &Target,
    game_id: &str,
    body: Body,
    staging_zip: &Path,
    staging_dir: &Path,
    too_big: &str,
) -> Response {
    let config = &state.config;
    match stream_to_file(body, staging_zip, config.max_upload_bytes).await {
        Ok(true) => {}
        Ok(false) => return refuse(StatusCode::PAYLOAD_TOO_LARGE, too_big),
        Err(_) => return refuse(StatusCode::BAD_REQUEST, s::UPLOAD_NETWORK_ERROR),
    }

    let Ok(permit) = state.unpack.clone().acquire_owned().await else {
        return refuse(StatusCode::SERVICE_UNAVAILABLE, s::SERVER_BUSY);
    };
    let limits = Limits::from_config(config);
    let (zip, dir) = (staging_zip.to_path_buf(), staging_dir.to_path_buf());
    // The permit lives in the blocking task, so an abandoned request still holds its unpack slot until it's done.
    let unpacked = match tokio::task::spawn_blocking(move || {
        let _permit = permit;
        share_zip::check_and_unpack(&zip, &dir, &limits)
    })
    .await
    {
        Ok(Ok(unpacked)) => unpacked,
        Ok(Err(reject)) => {
            tracing::info!(game = game_id, reason = reject.code(), "upload rejected");
            return refuse(StatusCode::UNPROCESSABLE_ENTITY, reject.message());
        }
        Err(_) => return refuse(StatusCode::INTERNAL_SERVER_ERROR, s::UPLOAD_FAILED),
    };
    if !disk_has_room(state, unpacked.bytes).await {
        return refuse(StatusCode::INSUFFICIENT_STORAGE, s::DISK_FULL);
    }

    let now = unix_now() as i64;
    let bytes = unpacked.bytes as i64;
    match target {
        Target::New => {
            let version_dir = config.version_dir(game_id, 1);
            if place(staging_dir, &version_dir).await.is_err() {
                return refuse(StatusCode::INTERNAL_SERVER_ERROR, s::UPLOAD_FAILED);
            }
            let row = NewGame { id: game_id, owner_id: *user_id, title: &unpacked.title, build_name: &unpacked.build_name, bytes, now };
            match state.db.insert_game(&row, config.games_per_account).await {
                Ok(true) => {}
                Ok(false) => {
                    let _ = tokio::fs::remove_dir_all(config.games_dir().join(game_id)).await;
                    return refuse(StatusCode::CONFLICT, &s::too_many_games(config.games_per_account));
                }
                Err(_) => {
                    let _ = tokio::fs::remove_dir_all(config.games_dir().join(game_id)).await;
                    return refuse(StatusCode::INTERNAL_SERVER_ERROR, s::UPLOAD_FAILED);
                }
            }
            tracing::info!(game = game_id, version = 1, bytes, "upload accepted (new game)");
            publish_upload(state, game_id, None).await;
        }
        Target::Update(game) => {
            let version_dir = config.version_dir(game_id, game.version + 1);
            if place(staging_dir, &version_dir).await.is_err() {
                return refuse(StatusCode::INTERNAL_SERVER_ERROR, s::UPLOAD_FAILED);
            }
            match state.db.set_version(game_id, game.version, &unpacked.title, &unpacked.build_name, bytes, now).await {
                Ok(true) => {
                    let _ = tokio::fs::remove_dir_all(config.version_dir(game_id, game.version)).await;
                }
                Ok(false) => {
                    let _ = tokio::fs::remove_dir_all(&version_dir).await;
                    return refuse(StatusCode::CONFLICT, s::GAME_NOT_FOUND);
                }
                Err(_) => {
                    let _ = tokio::fs::remove_dir_all(&version_dir).await;
                    return refuse(StatusCode::INTERNAL_SERVER_ERROR, s::UPLOAD_FAILED);
                }
            }
            tracing::info!(game = game_id, version = game.version + 1, bytes, "upload accepted (update)");
            publish_upload(state, game_id, Some(game)).await;
        }
    }
    json_answer(StatusCode::OK, serde_json::json!({"ok": true, "play": format!("/play/{game_id}")}))
}

/// After the version switch, still inside the request and its busy locks: copies the new version to the bucket,
/// records it (only if it is still the live version), then deletes the previous upload's copy, named from the
/// pre-update row. A storage failure, or the request going away mid-publish, leaves the game unpublished: it plays
/// from the Mac until the next start's reconcile.
async fn publish_upload(state: &AppState, game_id: &str, previous: Option<&Game>) {
    let Some(bucket) = state.storage.clone() else { return };
    let deadline = state.config.publish_deadline;
    if tokio::time::timeout(deadline, publish_and_record(state, bucket.as_ref(), game_id, previous)).await.is_err() {
        tracing::warn!(game = game_id, secs = deadline.as_secs(), "publishing took too long; the game plays from the Mac");
    }
}

async fn publish_and_record(state: &AppState, bucket: &dyn storage::Storage, game_id: &str, previous: Option<&Game>) {
    let game = match state.db.live_game(game_id).await {
        Ok(Some(game)) => game,
        Ok(None) => {
            tracing::warn!(game = game_id, "the game vanished under its busy lock; not published");
            return;
        }
        Err(error) => {
            tracing::warn!(game = game_id, %error, "reading the game to publish failed");
            return;
        }
    };
    if let Err(error) = storage::publish(bucket, &state.config, &game).await {
        tracing::warn!(game = game_id, %error, "publishing to the bucket failed; the game plays from the Mac");
        return;
    }
    match state.db.set_published(game_id, game.version).await {
        Ok(true) => {}
        Ok(false) => {
            tracing::warn!(game = game_id, "the game changed under its busy lock; publish not recorded");
            return;
        }
        Err(error) => {
            tracing::warn!(game = game_id, %error, "recording the publish failed");
            return;
        }
    }
    if let Some(previous) = previous
        && let Err(error) = bucket.delete_prefix(&storage::upload_prefix(previous)).await
    {
        tracing::warn!(game = game_id, %error, "deleting the previous upload from the bucket failed");
    }
}

/// Streams the body into a new file, counting bytes. `Ok(false)`: past `max` (a chunked body may not say its size).
async fn stream_to_file(body: Body, path: &Path, max: u64) -> std::io::Result<bool> {
    let mut file = tokio::fs::OpenOptions::new().write(true).create_new(true).open(path).await?;
    let mut body = body;
    let mut total = 0u64;
    while let Some(frame) = body.frame().await {
        let frame = frame.map_err(std::io::Error::other)?;
        if let Ok(data) = frame.into_data() {
            total += data.len() as u64;
            if total > max {
                return Ok(false);
            }
            file.write_all(&data).await?;
        }
    }
    file.flush().await?;
    Ok(true)
}

/// Moves the unpacked files into their version folder, which must not exist yet (a live folder is never replaced).
async fn place(staging_dir: &Path, version_dir: &Path) -> std::io::Result<()> {
    if tokio::fs::try_exists(version_dir).await? {
        return Err(std::io::Error::new(std::io::ErrorKind::AlreadyExists, "version folder exists"));
    }
    if let Some(parent) = version_dir.parent() {
        tokio::fs::create_dir_all(parent).await?;
    }
    tokio::fs::rename(staging_dir, version_dir).await
}

async fn disk_has_room(state: &AppState, extra: u64) -> bool {
    match state.db.live_bytes().await {
        Ok(live) => (live.max(0) as u64).saturating_add(extra) <= state.config.disk_cap_bytes,
        Err(_) => false,
    }
}

async fn fresh_game_id(state: &AppState) -> Option<String> {
    for _ in 0..20 {
        let id = random_game_id();
        if !state.db.game_id_used(&id).await.ok()? {
            return Some(id);
        }
    }
    None
}

/// At startup: deletes every leftover in `staging/` and every game folder that isn't a live game's current version.
/// Plain files among the game folders (a backup's `.gone`, a Finder `.DS_Store`) are left alone.
pub fn sweep(config: &Config, live: &[(String, i64)], removed: &[String]) -> std::io::Result<()> {
    let staging = config.staging_dir();
    if staging.exists() {
        for entry in std::fs::read_dir(&staging)? {
            let path = entry?.path();
            if path.is_dir() {
                std::fs::remove_dir_all(&path)?;
            } else {
                std::fs::remove_file(&path)?;
            }
        }
    }
    let keep: HashSet<(String, String)> = live.iter().map(|(id, v)| (id.clone(), format!("v{v}"))).collect();
    let removed: HashSet<&String> = removed.iter().collect();
    let games = config.games_dir();
    if !games.exists() {
        return Ok(());
    }
    for game in std::fs::read_dir(&games)? {
        let game = game?.path();
        if !game.is_dir() {
            continue;
        }
        let Some(id) = game.file_name().and_then(|n| n.to_str()).map(str::to_string) else { continue };
        if removed.contains(&id) || !live.iter().any(|(live_id, _)| *live_id == id) {
            std::fs::remove_dir_all(&game)?;
            continue;
        }
        for version in std::fs::read_dir(&game)? {
            let version = version?.path();
            if !version.is_dir() {
                continue;
            }
            let name = version.file_name().and_then(|n| n.to_str()).unwrap_or_default().to_string();
            if !keep.contains(&(id.clone(), name)) {
                std::fs::remove_dir_all(&version)?;
            }
        }
    }
    Ok(())
}
