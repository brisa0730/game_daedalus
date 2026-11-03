//! The gallery's page shell and small shared pieces. Every page goes through maud, which escapes all text.

use axum::http::{StatusCode, header};
use axum::response::{Html, IntoResponse, Response};
use maud::{DOCTYPE, Markup, html};

use crate::db::{Role, User};
use crate::strings as s;

/// Which navigation item a page belongs to, so the header can mark it.
#[derive(Clone, Copy, PartialEq, Eq)]
pub enum Nav {
    Games,
    Upload,
    Teacher,
    Login,
}

/// The served path of the heading font (Galmuri11 Bold, v2.40.4, SIL OFL 1.1; `static/fonts/OFL.txt`). The
/// release is in the name, so the file can be cached for good; a new font is a new path. `gallery.css` names it.
pub const FONT_PATH: &str = "/static/fonts/Galmuri11-Bold-v2.40.4.woff2";

/// The logo: the player block (white, one eye, a yellow band) and the site name.
fn logo() -> Markup {
    html! {
        svg.logo-mark viewBox="0 0 8 12" width="20" height="30" shape-rendering="crispEdges" aria-hidden="true" {
            rect width="8" height="12" fill="#eef1fa" {}
            rect x="5" y="2" width="1" height="1" fill="#141a2e" {}
            rect y="5" width="8" height="1" fill="#ffd23f" {}
        }
        span.logo-en { (s::LOGO_EN) }
        span.logo-ko { (s::LOGO_KO) }
    }
}

/// A strip of grass-and-dirt tiles.
pub fn ground() -> Markup {
    html! { div.ground aria-hidden="true" {} }
}

/// A full gallery page: Korean, UTF-8, the stylesheet and script, and the header with the logo and the navigation
/// (`nav` marks the current item): signed in, the name and the Sign out button; signed out (anyone may browse and
/// play), only the game list and Sign in.
pub fn shell(title: &str, user: Option<&User>, nav: Option<Nav>, body: Markup) -> Markup {
    let current = |item: Nav| (nav == Some(item)).then_some("page");
    html! {
        (DOCTYPE)
        html lang="ko" {
            head {
                meta charset="utf-8";
                meta name="viewport" content="width=device-width, initial-scale=1";
                meta name="color-scheme" content="dark";
                title { (title) " · " (s::SITE_NAME) }
                link rel="stylesheet" href="/static/gallery.css";
                script src="/static/gallery.js" defer {}
            }
            body {
                header.top {
                    div.top-in {
                        a.brand href="/" { (logo()) }
                        nav {
                            a href="/" aria-current=[current(Nav::Games)] { (s::NAV_GAMES) }
                            @if let Some(user) = user {
                                a href="/upload" aria-current=[current(Nav::Upload)] { (s::NAV_UPLOAD) }
                                @if user.role == Role::Teacher {
                                    a href="/teacher" aria-current=[current(Nav::Teacher)] { (s::NAV_TEACHER) }
                                }
                            }
                        }
                        div.who {
                            @if let Some(user) = user {
                                span.signed-in { (s::signed_in_as(&user.display_name)) }
                                a href="/password" { (s::NAV_PASSWORD) }
                                form method="post" action="/logout" {
                                    button type="submit" { (s::SIGN_OUT) }
                                }
                            } @else {
                                a.sign-in href="/login" aria-current=[current(Nav::Login)] { (s::NAV_LOGIN) }
                            }
                        }
                    }
                    (ground())
                }
                @if user.is_some() { p.shared-note { (s::SHARED_LAPTOP_NOTE) } }
                main { (body) }
            }
        }
    }
}

pub fn page(status: StatusCode, title: &str, user: Option<&User>, nav: Option<Nav>, body: Markup) -> Response {
    (status, Html(shell(title, user, nav, body).into_string())).into_response()
}

/// A one-line page (403, 404 and the like).
pub fn message(status: StatusCode, user: Option<&User>, text: &str) -> Response {
    page(status, text, user, None, html! { p.message { (text) } })
}

pub fn not_found(user: Option<&User>) -> Response {
    message(StatusCode::NOT_FOUND, user, s::NOT_FOUND)
}

pub fn forbidden(user: Option<&User>) -> Response {
    message(StatusCode::FORBIDDEN, user, s::FORBIDDEN)
}

pub fn see_other(location: &str) -> Response {
    (StatusCode::SEE_OTHER, [(header::LOCATION, location.to_string())]).into_response()
}

pub async fn gallery_css() -> Response {
    static_file("text/css; charset=utf-8", include_str!("../static/gallery.css"))
}

pub async fn gallery_js() -> Response {
    static_file("application/javascript; charset=utf-8", include_str!("../static/gallery.js"))
}

/// The heading font: bytes, cached for good (the release is in its path).
pub async fn font() -> Response {
    let bytes: &'static [u8] = include_bytes!("../static/fonts/Galmuri11-Bold-v2.40.4.woff2");
    ([(header::CONTENT_TYPE, "font/woff2"), (header::CACHE_CONTROL, "public, max-age=31536000, immutable")], bytes)
        .into_response()
}

pub fn static_file(content_type: &'static str, body: &'static str) -> Response {
    ([(header::CONTENT_TYPE, content_type), (header::CACHE_CONTROL, "no-cache")], body).into_response()
}

#[cfg(test)]
mod tests {
    use super::*;

    fn user(name: &str) -> User {
        User {
            id: 1,
            login: "kim".into(),
            display_name: name.into(),
            real_name: String::new(),
            role: Role::Student,
            password_hash: String::new(),
        }
    }

    #[test]
    fn the_shell_is_korean_utf8_and_escapes_names() {
        let html = shell("t", Some(&user("<script>alert(1)</script>")), Some(Nav::Games), html! {}).into_string();
        assert!(html.contains(r#"<html lang="ko">"#));
        assert!(html.contains(r#"<meta charset="utf-8">"#));
        assert!(html.contains("&lt;script&gt;alert(1)&lt;/script&gt;"));
        assert!(!html.contains("<script>alert(1)"));
        assert!(html.contains(s::SIGN_OUT));
        assert!(html.contains(r#"<a href="/" aria-current="page">"#) && !html.contains(r#"<a href="/upload" aria-current"#));
    }

    #[test]
    fn the_font_is_real_and_the_stylesheet_names_it() {
        let bytes = include_bytes!("../static/fonts/Galmuri11-Bold-v2.40.4.woff2");
        assert_eq!(&bytes[..4], b"wOF2", "a clone without Git LFS would embed a pointer file here");
        assert!(FONT_PATH.ends_with("Galmuri11-Bold-v2.40.4.woff2"), "FONT_PATH and include_bytes! name one file");
        let relative = FONT_PATH.strip_prefix("/static/").unwrap();
        assert!(include_str!("../static/gallery.css").contains(&format!("url(\"{relative}\")")));
    }
}
