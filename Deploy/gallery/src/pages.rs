//! The gallery's page shell and small shared pieces. Every page goes through maud, which escapes all text.

use axum::http::{StatusCode, header};
use axum::response::{Html, IntoResponse, Response};
use maud::{DOCTYPE, Markup, html};

use crate::db::{Role, User};
use crate::strings as s;

/// A full gallery page: Korean, UTF-8, the stylesheet and script, and (when signed in) the header with the
/// signed-in name, the navigation and the Sign out button.
pub fn shell(title: &str, user: Option<&User>, body: Markup) -> Markup {
    html! {
        (DOCTYPE)
        html lang="ko" {
            head {
                meta charset="utf-8";
                meta name="viewport" content="width=device-width, initial-scale=1";
                title { (title) " · " (s::SITE_NAME) }
                link rel="stylesheet" href="/static/gallery.css";
                script src="/static/gallery.js" defer {}
            }
            body {
                @if let Some(user) = user {
                    header.top {
                        a.brand href="/" { (s::SITE_NAME) }
                        nav {
                            a href="/" { (s::NAV_GAMES) }
                            a href="/upload" { (s::NAV_UPLOAD) }
                            @if user.role == Role::Teacher {
                                a href="/teacher" { (s::NAV_TEACHER) }
                            }
                        }
                        div.who {
                            span.signed-in { (s::signed_in_as(&user.display_name)) }
                            form method="post" action="/logout" {
                                button type="submit" { (s::SIGN_OUT) }
                            }
                        }
                    }
                    p.shared-note { (s::SHARED_LAPTOP_NOTE) }
                }
                main { (body) }
            }
        }
    }
}

pub fn page(status: StatusCode, title: &str, user: Option<&User>, body: Markup) -> Response {
    (status, Html(shell(title, user, body).into_string())).into_response()
}

/// A one-line page (403, 404 and the like).
pub fn message(status: StatusCode, user: Option<&User>, text: &str) -> Response {
    page(status, text, user, html! { p.message { (text) } })
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

pub fn static_file(content_type: &'static str, body: &'static str) -> Response {
    ([(header::CONTENT_TYPE, content_type), (header::CACHE_CONTROL, "no-cache")], body).into_response()
}

#[cfg(test)]
mod tests {
    use super::*;

    fn user(name: &str) -> User {
        User { id: 1, login: "kim".into(), display_name: name.into(), role: Role::Student, password_hash: String::new() }
    }

    #[test]
    fn the_shell_is_korean_utf8_and_escapes_names() {
        let html = shell("t", Some(&user("<script>alert(1)</script>")), html! {}).into_string();
        assert!(html.contains(r#"<html lang="ko">"#));
        assert!(html.contains(r#"<meta charset="utf-8">"#));
        assert!(html.contains("&lt;script&gt;alert(1)&lt;/script&gt;"));
        assert!(!html.contains("<script>alert(1)"));
        assert!(html.contains(s::SIGN_OUT));
    }
}
