//! Keeps games from acting through the gallery: only the gallery's own pages may change anything, no other site
//! may frame a gallery page, and every page carries its security headers. Both hosts get their headers from the
//! layers here, so no route can be added without them.

use axum::extract::{Request, State};
use axum::http::{HeaderMap, HeaderValue, Method, StatusCode, header};
use axum::middleware::Next;
use axum::response::{IntoResponse, Response};

use crate::pages;

/// True only for a request the gallery's own page sent. Fails closed: no header, `null` or another origin
/// (a game on the sibling games host is *same-site*, so `SameSite` cookies don't stop it) is refused.
pub fn same_origin(headers: &HeaderMap, own_origin: &str) -> bool {
    if let Some(site) = headers.get("sec-fetch-site") {
        return site == "same-origin";
    }
    headers.get("origin").is_some_and(|origin| origin == own_origin)
}

pub const GALLERY_CSP: &str = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; \
     frame-src https://suwon-play.orcarin.com; frame-ancestors 'self'; form-action 'self'; base-uri 'none'; \
     object-src 'none'";

/// The games host's policy (Step 15): strict, relaxed only for what Unity's loader is seen to need.
pub const PLAY_CSP: &str = "default-src 'none'; script-src 'self' blob: 'wasm-unsafe-eval'; worker-src 'self' blob:; \
     connect-src 'self' blob: data:; img-src 'self' blob: data:; style-src 'self' 'unsafe-inline'; \
     media-src 'self' blob: data:; form-action 'none'; base-uri 'none'; object-src 'none'; \
     frame-ancestors https://suwon.orcarin.com";

/// On every games-host HTML page, so a page opened outside the gallery's frame is sandboxed exactly like the frame
/// (no top navigation, no popups, no forms); `allow-same-origin` keeps the game's own saves working.
pub const PLAY_SANDBOX_CSP: &str = "sandbox allow-scripts allow-same-origin allow-pointer-lock";

/// The gallery is public (v0.20) but asks search engines not to list it, on both hosts. A header, not `robots.txt`:
/// a crawler kept out by `robots.txt` never sees the noindex.
const ROBOTS_TAG: header::HeaderName = header::HeaderName::from_static("x-robots-tag");
const NOINDEX: &str = "noindex, nofollow";

/// The gallery host's layer: refuses any state change from anywhere but the gallery's own pages (fails closed),
/// then adds the gallery's headers. A handler's own CSP is kept (both apply) and its `Cache-Control` is left alone.
pub async fn gallery_layer(State(origin): State<String>, request: Request, next: Next) -> Response {
    let method = request.method().clone();
    let mut response = if method == Method::GET || method == Method::HEAD || same_origin(request.headers(), &origin) {
        next.run(request).await
    } else {
        tracing::info!(path = %request.uri().path(), "refused a state change from another origin");
        pages::forbidden(None)
    };
    let headers = response.headers_mut();
    headers.append(header::CONTENT_SECURITY_POLICY, HeaderValue::from_static(GALLERY_CSP));
    headers.insert(header::X_FRAME_OPTIONS, HeaderValue::from_static("SAMEORIGIN"));
    headers.insert(header::X_CONTENT_TYPE_OPTIONS, HeaderValue::from_static("nosniff"));
    headers.insert(header::REFERRER_POLICY, HeaderValue::from_static("same-origin"));
    headers.insert(ROBOTS_TAG, HeaderValue::from_static(NOINDEX));
    if !headers.contains_key(header::CACHE_CONTROL) {
        headers.insert(header::CACHE_CONTROL, HeaderValue::from_static("no-store"));
    }
    response
}

/// The games host's layer: it only ever serves (anything but GET/HEAD is 405), and every response carries the
/// play CSP; HTML also carries the sandbox.
pub async fn games_layer(request: Request, next: Next) -> Response {
    let method = request.method().clone();
    let mut response = if method == Method::GET || method == Method::HEAD {
        next.run(request).await
    } else {
        StatusCode::METHOD_NOT_ALLOWED.into_response()
    };
    let is_html = response
        .headers()
        .get(header::CONTENT_TYPE)
        .and_then(|v| v.to_str().ok())
        .is_some_and(|v| v.starts_with("text/html"));
    let headers = response.headers_mut();
    headers.append(header::CONTENT_SECURITY_POLICY, HeaderValue::from_static(PLAY_CSP));
    if is_html {
        headers.append(header::CONTENT_SECURITY_POLICY, HeaderValue::from_static(PLAY_SANDBOX_CSP));
    }
    headers.insert(header::X_CONTENT_TYPE_OPTIONS, HeaderValue::from_static("nosniff"));
    headers.insert(header::REFERRER_POLICY, HeaderValue::from_static("no-referrer"));
    headers.insert(ROBOTS_TAG, HeaderValue::from_static(NOINDEX));
    response
}

#[cfg(test)]
mod tests {
    use super::*;

    const GALLERY: &str = "https://suwon.orcarin.com";

    fn headers(pairs: &[(&'static str, &'static str)]) -> HeaderMap {
        let mut map = HeaderMap::new();
        for (name, value) in pairs {
            map.insert(*name, value.parse().unwrap());
        }
        map
    }

    #[test]
    fn the_gallerys_own_requests_pass() {
        assert!(same_origin(&headers(&[("sec-fetch-site", "same-origin")]), GALLERY));
        assert!(same_origin(&headers(&[("origin", GALLERY)]), GALLERY));
    }

    #[test]
    fn everything_else_is_refused() {
        assert!(!same_origin(&headers(&[]), GALLERY));
        assert!(!same_origin(&headers(&[("origin", "null")]), GALLERY));
        assert!(!same_origin(&headers(&[("origin", "https://suwon-play.orcarin.com")]), GALLERY));
        assert!(!same_origin(&headers(&[("sec-fetch-site", "same-site")]), GALLERY));
        assert!(!same_origin(&headers(&[("sec-fetch-site", "cross-site")]), GALLERY));
        assert!(!same_origin(&headers(&[("sec-fetch-site", "none")]), GALLERY));
        // Sec-Fetch-Site wins over a matching Origin.
        assert!(!same_origin(&headers(&[("sec-fetch-site", "same-site"), ("origin", GALLERY)]), GALLERY));
    }
}
