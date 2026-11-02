//! Keeps games from acting through the gallery: only the gallery's own pages may change anything, and no other
//! site may frame a gallery page.

use axum::http::HeaderMap;

/// True only for a request the gallery's own page sent. Fails closed: no header, `null` or another origin
/// (a game on the sibling games host is *same-site*, so `SameSite` cookies don't stop it) is refused.
pub fn same_origin(headers: &HeaderMap, own_origin: &str) -> bool {
    if let Some(site) = headers.get("sec-fetch-site") {
        return site == "same-origin";
    }
    headers.get("origin").is_some_and(|origin| origin == own_origin)
}

/// Sent on every gallery response.
pub const FRAMING_HEADERS: [(&str, &str); 2] = [
    ("content-security-policy", "frame-ancestors 'self'"),
    ("x-frame-options", "SAMEORIGIN"),
];

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
