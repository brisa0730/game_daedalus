//! Signing in and out, the session cookie, the sign-in delay, known browsers, and the layer that keeps every
//! non-public gallery route behind a session.

use std::collections::HashMap;

use axum::extract::{Form, Request, State};
use axum::http::{HeaderMap, HeaderValue, StatusCode, header};
use axum::middleware::Next;
use axum::response::{IntoResponse, Response};
use maud::html;
use serde::Deserialize;
use sha2::{Digest, Sha256};

use crate::accounts::HashError;
use crate::app::{Access, AppState};
use crate::config::{Config, unix_now};
use crate::db::{Role, User};
use crate::pages;
use crate::strings as s;
use crate::upload::json_answer;

pub const SESSION_COOKIE: &str = "__Host-gallery_session";
pub const KNOWN_COOKIE: &str = "__Host-gallery_known";

/// The signed-in user of a request, put there by [`require_access`].
#[derive(Clone)]
pub struct CurrentUser(pub User);

/// The SHA-256 of the request's session token, for sign-out.
#[derive(Clone)]
pub struct SessionHash(pub Vec<u8>);

pub fn cookie<'a>(headers: &'a HeaderMap, name: &str) -> Option<&'a str> {
    headers
        .get_all(header::COOKIE)
        .iter()
        .filter_map(|v| v.to_str().ok())
        .flat_map(|v| v.split(';'))
        .filter_map(|pair| pair.trim().split_once('='))
        .find(|(k, _)| *k == name)
        .map(|(_, v)| v)
}

pub fn new_token() -> String {
    rand::random::<[u8; 32]>().iter().map(|b| format!("{b:02x}")).collect()
}

pub fn token_hash(token: &str) -> Vec<u8> {
    Sha256::digest(token.as_bytes()).to_vec()
}

fn is_token(value: &str) -> bool {
    value.len() == 64 && value.bytes().all(|b| b.is_ascii_hexdigit())
}

/// The user behind a request's session cookie, if the session exists and is neither idle nor too old.
pub async fn current_user(state: &AppState, headers: &HeaderMap) -> Option<(User, Vec<u8>)> {
    let token = cookie(headers, SESSION_COOKIE).filter(|t| is_token(t))?;
    let hash = token_hash(token);
    let session = state.db.session(&hash).await.ok()??;
    let now = unix_now() as i64;
    let config = &state.config;
    if now - session.last_seen > config.session_idle_secs as i64 || now - session.created_at > config.session_max_secs as i64 {
        let _ = state.db.delete_session(&hash).await;
        return None;
    }
    if now - session.last_seen > config.session_touch_secs as i64 {
        let _ = state.db.touch_session(&hash, now).await;
    }
    let user = state.db.user_by_id(session.user_id).await.ok()??;
    Some((user, hash))
}

/// Every route that isn't public passes through here (wired from the route table in `app.rs`). A missing session
/// sends a page to `/login`; the upload API answers 401 in its JSON shape; a student on a teacher route gets 403.
pub async fn require_access(
    State((state, access)): State<(AppState, Access)>,
    mut request: Request,
    next: Next,
) -> Response {
    let Some((user, hash)) = current_user(&state, request.headers()).await else {
        return match access {
            Access::Api => {
                json_answer(StatusCode::UNAUTHORIZED, serde_json::json!({"ok": false, "message": s::SESSION_ENDED, "login": true}))
            }
            _ => pages::see_other("/login"),
        };
    };
    if access == Access::Teacher && user.role != Role::Teacher {
        return pages::forbidden(Some(&user));
    }
    request.extensions_mut().insert(CurrentUser(user));
    request.extensions_mut().insert(SessionHash(hash));
    next.run(request).await
}

/// The sign-in delay, per existing account: after a failure the next try waits 1 s, doubling to the cap.
/// Unknown IDs never get an entry (they are never hashed, so there is nothing to slow down), so nobody can flood it.
/// An attempt reserves its slot before the password is checked, so simultaneous attempts can't all get through.
#[derive(Default)]
pub struct Throttle {
    entries: HashMap<i64, Entry>,
}

struct Entry {
    failures: u32,
    next_allowed: u64,
    touched: u64,
}

impl Throttle {
    /// Seconds still to wait before this account may try again, if any.
    pub fn wait(&self, user_id: i64, now: u64) -> Option<u64> {
        let entry = self.entries.get(&user_id)?;
        (now < entry.next_allowed).then(|| entry.next_allowed - now)
    }

    /// Claims this account's next attempt, as if it will fail: `Err(seconds)` if it must wait. Checked and claimed
    /// under one lock, so a burst of simultaneous attempts gets one password check, not one each. Success clears it.
    pub fn reserve(&mut self, user_id: i64, now: u64, config: &Config) -> Result<(), u64> {
        if let Some(wait) = self.wait(user_id, now) {
            return Err(wait);
        }
        self.entries.retain(|_, e| now.saturating_sub(e.touched) < config.throttle_forget_secs);
        let entry = self.entries.entry(user_id).or_insert(Entry { failures: 0, next_allowed: 0, touched: now });
        entry.next_allowed = now + delay_after(entry.failures + 1, config);
        entry.touched = now;
        Ok(())
    }

    pub fn fail(&mut self, user_id: i64, now: u64, config: &Config) {
        self.entries.retain(|_, e| now.saturating_sub(e.touched) < config.throttle_forget_secs);
        let entry = self.entries.entry(user_id).or_insert(Entry { failures: 0, next_allowed: 0, touched: now });
        entry.failures += 1;
        entry.next_allowed = now + delay_after(entry.failures, config);
        entry.touched = now;
    }

    pub fn clear(&mut self, user_id: i64) {
        self.entries.remove(&user_id);
    }

    /// Undoes a reservation whose password was never checked (the hashing queue was full).
    pub fn clear_reservation(&mut self, user_id: i64) {
        match self.entries.get_mut(&user_id) {
            Some(entry) if entry.failures == 0 => {
                self.entries.remove(&user_id);
            }
            Some(entry) => entry.next_allowed = entry.touched,
            None => {}
        }
    }

    pub fn len(&self) -> usize {
        self.entries.len()
    }

    pub fn is_empty(&self) -> bool {
        self.entries.is_empty()
    }
}

/// 1, 2, 4, 8, 16, 30, 30… seconds after the 1st, 2nd, 3rd… failure.
pub fn delay_after(failures: u32, config: &Config) -> u64 {
    let doubled = config.throttle_first_secs.saturating_mul(1u64 << failures.saturating_sub(1).min(32));
    doubled.min(config.throttle_cap_secs)
}

pub async fn login_page(State(state): State<AppState>, headers: HeaderMap) -> Response {
    if current_user(&state, &headers).await.is_some() {
        return pages::see_other("/");
    }
    login_form(StatusCode::OK, "", None)
}

fn login_form(status: StatusCode, login: &str, error: Option<&str>) -> Response {
    let body = html! {
        h1 { (s::LOGIN_TITLE) }
        form.login method="post" action="/login" {
            @if let Some(error) = error { p.error role="alert" { (error) } }
            label { (s::LOGIN_ID) input name="login" value=(login) autocomplete="username" autocapitalize="none" required; }
            label { (s::LOGIN_PASSWORD) input name="password" type="password" autocomplete="current-password" required; }
            button type="submit" { (s::LOGIN_BUTTON) }
        }
    };
    pages::page(status, s::LOGIN_TITLE, None, body)
}

#[derive(Deserialize)]
pub struct LoginForm {
    login: String,
    password: String,
}

pub async fn post_login(State(state): State<AppState>, headers: HeaderMap, Form(form): Form<LoginForm>) -> Response {
    let typed = form.login.trim().to_string();
    let wrong = || login_form(StatusCode::UNAUTHORIZED, &typed, Some(s::LOGIN_WRONG));
    let Some(login) = crate::text::normalize_login(&form.login) else { return wrong() };
    // Unknown IDs are answered at once, never hashed (IDs are on slips; hashing them would let random IDs fill
    // the hashing slots and lock the whole class out).
    let Ok(Some(user)) = state.db.user_by_login(&login).await else {
        tracing::info!(%login, "sign-in failed: unknown id");
        return wrong();
    };
    let now = unix_now();
    let known_token = cookie(&headers, KNOWN_COOKIE).filter(|t| is_token(t)).map(str::to_string);
    let known = match &known_token {
        Some(token) => {
            let since = now as i64 - state.config.known_browser_secs as i64;
            state.db.browser_known(&token_hash(token), user.id, since).await.unwrap_or(false)
        }
        None => false,
    };
    if !known && let Err(wait) = state.throttle.lock().expect("throttle lock").reserve(user.id, now, &state.config) {
        return login_form(StatusCode::TOO_MANY_REQUESTS, &typed, Some(&s::wait_seconds(wait)));
    }
    match state.hasher.verify(user.password_hash.clone(), form.password).await {
        Ok(true) => {}
        Ok(false) => {
            state.throttle.lock().expect("throttle lock").fail(user.id, now, &state.config);
            tracing::info!(%login, "sign-in failed: wrong password");
            return wrong();
        }
        // Nothing was checked: the reservation is released so the student can simply try again.
        Err(HashError::Busy) => {
            state.throttle.lock().expect("throttle lock").clear_reservation(user.id);
            return login_form(StatusCode::SERVICE_UNAVAILABLE, &typed, Some(s::LOGIN_BUSY));
        }
        Err(HashError::Internal) => return login_form(StatusCode::INTERNAL_SERVER_ERROR, &typed, Some(s::LOGIN_BUSY)),
    }
    state.throttle.lock().expect("throttle lock").clear(user.id);
    let session = new_token();
    let known_token = known_token.unwrap_or_else(new_token);
    if state.db.insert_session(&token_hash(&session), user.id, now as i64).await.is_err()
        || state.db.remember_browser(&token_hash(&known_token), user.id, now as i64).await.is_err()
    {
        return login_form(StatusCode::INTERNAL_SERVER_ERROR, &typed, Some(s::LOGIN_BUSY));
    }
    tracing::info!(%login, "signed in");
    let mut response = pages::see_other("/");
    let headers = response.headers_mut();
    let session_cookie = format!("{SESSION_COOKIE}={session}; HttpOnly; Secure; SameSite=Lax; Path=/");
    let known_cookie =
        format!("{KNOWN_COOKIE}={known_token}; HttpOnly; Secure; SameSite=Lax; Path=/; Max-Age={}", state.config.known_browser_secs);
    headers.append(header::SET_COOKIE, HeaderValue::from_str(&session_cookie).expect("cookie is ASCII"));
    headers.append(header::SET_COOKIE, HeaderValue::from_str(&known_cookie).expect("cookie is ASCII"));
    response
}

pub async fn post_logout(
    State(state): State<AppState>,
    axum::Extension(SessionHash(hash)): axum::Extension<SessionHash>,
) -> Response {
    let _ = state.db.delete_session(&hash).await;
    let mut response = pages::see_other("/login");
    response.headers_mut().append(
        header::SET_COOKIE,
        HeaderValue::from_static("__Host-gallery_session=; HttpOnly; Secure; SameSite=Lax; Path=/; Max-Age=0"),
    );
    response.into_response()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn the_delay_doubles_and_caps() {
        let config = Config::new(std::path::Path::new("/x"));
        let delays: Vec<u64> = (1..=8).map(|n| delay_after(n, &config)).collect();
        assert_eq!(delays, [1, 2, 4, 8, 16, 30, 30, 30]);
        assert_eq!(delay_after(200, &config), 30);
    }

    #[test]
    fn the_throttle_waits_then_clears() {
        let config = Config::new(std::path::Path::new("/x"));
        let mut t = Throttle::default();
        assert_eq!(t.wait(1, 100), None);
        t.fail(1, 100, &config);
        assert_eq!(t.wait(1, 100), Some(1));
        assert_eq!(t.wait(1, 101), None);
        t.fail(1, 101, &config);
        assert_eq!(t.wait(1, 101), Some(2));
        assert_eq!(t.wait(2, 101), None);
        t.clear(1);
        assert_eq!(t.wait(1, 101), None);
    }

    #[test]
    fn a_reservation_holds_off_simultaneous_attempts() {
        let config = Config::new(std::path::Path::new("/x"));
        let mut t = Throttle::default();
        assert_eq!(t.reserve(1, 100, &config), Ok(()));
        assert_eq!(t.reserve(1, 100, &config), Err(1));
        t.clear(1);
        assert_eq!(t.reserve(1, 100, &config), Ok(()));
        t.clear_reservation(1);
        assert_eq!(t.reserve(1, 100, &config), Ok(()));
    }

    #[test]
    fn old_entries_are_forgotten() {
        let config = Config::new(std::path::Path::new("/x"));
        let mut t = Throttle::default();
        t.fail(1, 100, &config);
        t.fail(2, 100 + config.throttle_forget_secs, &config);
        assert_eq!(t.len(), 1);
    }

    #[test]
    fn cookies_are_parsed() {
        let mut h = HeaderMap::new();
        h.insert(header::COOKIE, "a=1; __Host-gallery_session=abc; b=2".parse().unwrap());
        assert_eq!(cookie(&h, SESSION_COOKIE), Some("abc"));
        assert_eq!(cookie(&h, "c"), None);
    }
}
