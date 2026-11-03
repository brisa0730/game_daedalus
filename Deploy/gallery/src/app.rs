//! The server's shared state, the one table of gallery routes, and routing by `Host` between the gallery and the
//! games host.

use std::collections::HashSet;
use std::sync::{Arc, Mutex};

use axum::Router;
use axum::extract::{Request, State};
use axum::http::{Method, StatusCode, header};
use axum::middleware::{from_fn, from_fn_with_state};
use axum::response::{IntoResponse, Response};
use axum::routing::{MethodRouter, get, post};
use tokio::sync::Semaphore;
use tower::ServiceExt;

use crate::accounts::Hasher;
use crate::config::{Config, GALLERY_HOST, GAMES_HOST};
use crate::db::Db;
use crate::play_token::Key;
use crate::session::Throttle;
use crate::{gallery, guard, pages, play, session, teacher, upload};

#[derive(Clone)]
pub struct AppState {
    pub config: Arc<Config>,
    pub db: Db,
    /// Per process: a restart ends every open game's file access, and reopening it from the gallery fixes that.
    pub key: Arc<Key>,
    pub hasher: Arc<Hasher>,
    pub throttle: Arc<Mutex<Throttle>>,
    pub unpack: Arc<Semaphore>,
    /// Games being uploaded to or removed right now.
    pub busy: Arc<Mutex<HashSet<String>>>,
}

impl AppState {
    pub fn new(config: Config, db: Db) -> AppState {
        AppState {
            hasher: Arc::new(Hasher::new(&config)),
            unpack: Arc::new(Semaphore::new(config.unpack_permits)),
            config: Arc::new(config),
            db,
            key: Arc::new(Key::generate()),
            throttle: Arc::new(Mutex::new(Throttle::default())),
            busy: Arc::new(Mutex::new(HashSet::new())),
        }
    }

    /// Marks these games busy, or `None` (nothing marked) if any already is. Unmarked when the guard drops.
    pub fn lock_games(&self, ids: &[String]) -> Option<BusyGuard> {
        let mut busy = self.busy.lock().expect("busy lock");
        if ids.iter().any(|id| busy.contains(id)) {
            return None;
        }
        busy.extend(ids.iter().cloned());
        Some(BusyGuard { busy: self.busy.clone(), ids: ids.to_vec() })
    }
}

pub struct BusyGuard {
    busy: Arc<Mutex<HashSet<String>>>,
    ids: Vec<String>,
}

impl Drop for BusyGuard {
    fn drop(&mut self) {
        let mut busy = self.busy.lock().expect("busy lock");
        for id in &self.ids {
            busy.remove(id);
        }
    }
}

/// Who may use a gallery route.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Access {
    Public,
    /// Signed in; without a session the browser is sent to `/login`.
    Page,
    /// Signed in; without a session the answer is a 401 in the upload API's JSON shape.
    Api,
    /// The teacher only.
    Teacher,
}

pub struct RouteSpec {
    pub method: Method,
    pub path: &'static str,
    pub access: Access,
    pub handler: MethodRouter<AppState>,
}

fn route(method: Method, path: &'static str, access: Access, handler: MethodRouter<AppState>) -> RouteSpec {
    RouteSpec { method, path, access, handler }
}

/// Every gallery route, in one table. `router()` is built from it and the route tests iterate it, so a route can't
/// exist without its access rule (axum's `Router` can't list its routes).
pub fn gallery_routes() -> Vec<RouteSpec> {
    use Access::*;
    vec![
        route(Method::GET, "/static/gallery.css", Public, get(pages::gallery_css)),
        route(Method::GET, "/static/gallery.js", Public, get(pages::gallery_js)),
        route(Method::GET, pages::FONT_PATH, Public, get(pages::font)),
        route(Method::GET, "/login", Public, get(session::login_page)),
        route(Method::POST, "/login", Public, post(session::post_login)),
        route(Method::POST, "/logout", Page, post(session::post_logout)),
        route(Method::GET, "/password", Page, get(session::password_page)),
        route(Method::POST, "/password", Page, post(session::post_password)),
        route(Method::GET, "/", Page, get(gallery::list)),
        route(Method::GET, "/shots/{file}", Page, get(gallery::screenshot)),
        route(Method::GET, "/play/{id}", Page, get(gallery::play_view)),
        route(Method::GET, "/games/{id}/remove", Page, get(gallery::remove_confirm)),
        route(Method::POST, "/games/{id}/remove", Page, post(gallery::remove)),
        route(Method::GET, "/upload", Page, get(upload::upload_page)),
        route(Method::POST, "/upload", Api, post(upload::post_upload)),
        route(Method::GET, "/teacher", Teacher, get(teacher::page)),
        route(Method::POST, "/teacher/students/preview", Teacher, post(teacher::preview)),
        route(Method::POST, "/teacher/students/create", Teacher, post(teacher::create)),
        route(Method::POST, "/teacher/students/{id}/reset", Teacher, post(teacher::reset)),
        route(Method::GET, "/teacher/students/{id}/remove", Teacher, get(teacher::remove_confirm)),
        route(Method::POST, "/teacher/students/{id}/remove", Teacher, post(teacher::remove)),
    ]
}

fn gallery_router(state: &AppState) -> Router {
    let mut router = Router::new();
    for spec in gallery_routes() {
        let handler = match spec.access {
            Access::Public => spec.handler,
            access => spec.handler.route_layer(from_fn_with_state((state.clone(), access), session::require_access)),
        };
        router = router.route(spec.path, handler);
    }
    router
        .fallback(|| async { pages::not_found(None) })
        .with_state(state.clone())
        .layer(from_fn_with_state(state.config.gallery_origin.clone(), guard::gallery_layer))
}

fn games_router(state: &AppState) -> Router {
    Router::new()
        .route("/enter/{id}", get(play::enter))
        .route("/g/{id}/", get(play::player_page))
        .route("/g/{id}/player.js", get(play::player_js))
        .route("/g/{id}/Build/{file}", get(play::build_file))
        .fallback(|| async { StatusCode::NOT_FOUND })
        .with_state(state.clone())
        .layer(from_fn(guard::games_layer))
}

#[derive(Clone)]
struct Hosts {
    gallery: Router,
    games: Router,
}

/// The whole server: the gallery on `suwon.orcarin.com`, the games host on `suwon-play.orcarin.com`, and an empty
/// 404 for any other `Host` (the tunnel keeps the public `Host`, so a local name never arrives through it).
pub fn router(state: AppState) -> Router {
    let hosts = Hosts { gallery: gallery_router(&state), games: games_router(&state) };
    Router::new().fallback(dispatch).with_state(hosts)
}

async fn dispatch(State(hosts): State<Hosts>, request: Request) -> Response {
    let host = request.headers().get(header::HOST).and_then(|h| h.to_str().ok()).unwrap_or("");
    let name = host.rsplit_once(':').map_or(host, |(name, _)| name);
    let target = match name {
        GALLERY_HOST => hosts.gallery,
        GAMES_HOST => hosts.games,
        _ => return StatusCode::NOT_FOUND.into_response(),
    };
    match target.oneshot(request).await {
        Ok(response) => response,
        Err(never) => match never {},
    }
}
