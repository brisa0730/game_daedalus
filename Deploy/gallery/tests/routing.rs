//! Routing by host, the same-origin guard and the header layers, proven on the real router. The POST and GET checks
//! iterate `app::gallery_routes()`, the one table the router is built from.

mod support;

use axum::body::Body;
use axum::http::{Method, Request, StatusCode};
use gallery::app::{Access, gallery_routes};
use gallery::strings as s;
use support::*;

fn concrete(path: &str) -> String {
    path.replace("{file}", "aaaaaaaaaa.png").replace("{id}", "1")
}

#[tokio::test]
async fn unknown_hosts_get_an_empty_404() {
    let app = TestApp::new().await;
    for host in ["127.0.0.1:8470", "localhost", "orcarin.com", "evil.example", ""] {
        let answer = app.get(host, "/login", None).await;
        assert_eq!(answer.status, StatusCode::NOT_FOUND, "host {host}");
        assert!(answer.body.is_empty(), "host {host}");
    }
    assert_eq!(app.get("suwon.orcarin.com:443", "/login", None).await.status, StatusCode::OK);
}

#[tokio::test]
async fn every_post_route_refuses_other_origins() {
    let app = TestApp::new().await;
    let cookie = app.teacher().await;
    let bad: [&[(&str, &str)]; 6] = [
        &[("sec-fetch-site", "cross-site")],
        &[("sec-fetch-site", "same-site")],
        &[("sec-fetch-site", "none")],
        &[],
        &[("origin", "null")],
        &[("origin", "https://suwon-play.orcarin.com")],
    ];
    let posts: Vec<_> = gallery_routes().into_iter().filter(|r| r.method == Method::POST).collect();
    assert!(posts.len() >= 8, "the table lists the POST routes");
    for route in &posts {
        for headers in bad {
            let mut req = Request::builder()
                .method(Method::POST)
                .uri(concrete(route.path))
                .header("host", GALLERY)
                .header("cookie", &cookie)
                .header("content-type", "application/x-www-form-urlencoded");
            for (k, v) in headers {
                req = req.header(*k, *v);
            }
            let answer = app.send(req.body(Body::from("text=x")).unwrap()).await;
            assert_eq!(answer.status, StatusCode::FORBIDDEN, "{} with {headers:?}", route.path);
        }
    }
}

#[tokio::test]
async fn every_non_public_route_needs_a_session() {
    let app = TestApp::new().await;
    for route in gallery_routes() {
        let path = concrete(route.path);
        let answer = if route.method == Method::POST {
            app.post_form(&path, None, "text=x").await
        } else {
            app.get(GALLERY, &path, None).await
        };
        match route.access {
            Access::Public => assert!(answer.status != StatusCode::UNAUTHORIZED, "{path}"),
            Access::Api => {
                assert_eq!(answer.status, StatusCode::UNAUTHORIZED, "{path}");
                assert_eq!(answer.json()["message"], s::SESSION_ENDED);
                assert_eq!(answer.json()["login"], true);
            }
            Access::Page | Access::Teacher => {
                assert_eq!(answer.status, StatusCode::SEE_OTHER, "{path}");
                assert_eq!(answer.header("location"), Some("/login"), "{path}");
            }
        }
    }
}

#[tokio::test]
async fn every_signed_in_page_shows_the_name_and_sign_out() {
    let app = TestApp::new().await;
    let cookie = app.teacher().await;
    let game = app.new_game(&cookie, "Teacher Example").await;
    let kim = app.student("kim", "김민수").await;
    drop(kim);
    let student = app.state.db.user_by_login("kim").await.unwrap().unwrap().id;
    for route in gallery_routes().into_iter().filter(|r| r.method == Method::GET && r.access != Access::Public) {
        // Real ids, so the real pages render (not 404s): a game for /play and /games, a student for /teacher.
        let id = if route.path.starts_with("/teacher/") { student.to_string() } else { game.clone() };
        let path = route.path.replace("{file}", &format!("{game}.png")).replace("{id}", &id);
        let answer = app.get(GALLERY, &path, Some(&cookie)).await;
        assert!(answer.status.is_success(), "{path}: {}", answer.status);
        if answer.header("content-type").is_some_and(|t| t.starts_with("text/html")) {
            let html = answer.text();
            assert!(html.contains(&s::signed_in_as("선생님")), "{}", route.path);
            assert!(html.contains(s::SIGN_OUT) && html.contains(r#"action="/logout""#), "{}", route.path);
            assert!(html.contains(r#"<html lang="ko">"#), "{}", route.path);
        }
    }
}

#[tokio::test]
async fn gallery_headers_are_on_every_kind_of_response() {
    let app = TestApp::new().await;
    let cookie = app.student("kim", "김민수").await;
    let ok = app.get(GALLERY, "/", Some(&cookie)).await;
    let redirect = app.get(GALLERY, "/", None).await;
    let forbidden = app.send(
        Request::builder().method(Method::POST).uri("/logout").header("host", GALLERY).body(Body::empty()).unwrap(),
    ).await;
    let missing = app.get(GALLERY, "/nothing-here", Some(&cookie)).await;
    for (answer, status) in [(ok, 200), (redirect, 303), (forbidden, 403), (missing, 404)] {
        assert_eq!(answer.status.as_u16(), status);
        let csp = answer.all("content-security-policy").join(" | ");
        assert!(csp.contains("frame-ancestors 'self'") && csp.contains("default-src 'self'"), "{status}: {csp}");
        assert_eq!(answer.header("x-frame-options"), Some("SAMEORIGIN"));
        assert_eq!(answer.header("x-content-type-options"), Some("nosniff"));
        assert_eq!(answer.header("referrer-policy"), Some("same-origin"));
        assert_eq!(answer.header("cache-control"), Some("no-store"));
    }
    let css = app.get(GALLERY, "/static/gallery.css", None).await;
    assert_eq!(css.header("cache-control"), Some("no-cache"), "a handler's Cache-Control survives the layer");
}

#[tokio::test]
async fn the_games_host_only_serves() {
    let app = TestApp::new().await;
    for method in [Method::POST, Method::PUT, Method::DELETE] {
        let answer = app
            .send(Request::builder().method(method).uri("/g/aaaaaaaaaa/").header("host", GAMES).body(Body::empty()).unwrap())
            .await;
        assert_eq!(answer.status, StatusCode::METHOD_NOT_ALLOWED);
        assert!(answer.all("content-security-policy").iter().any(|c| c.contains("default-src 'none'")));
    }
    let missing = app.get(GAMES, "/anything", None).await;
    assert_eq!(missing.status, StatusCode::NOT_FOUND);
    assert!(missing.all("content-security-policy").iter().any(|c| c.contains("form-action 'none'")));
    assert_eq!(missing.header("referrer-policy"), Some("no-referrer"));
}

#[test]
fn no_script_listens_for_messages() {
    for script in [include_str!("../static/gallery.js"), include_str!("../static/player.js")] {
        assert!(!script.contains("addEventListener(\"message\"") && !script.contains("addEventListener('message'"));
        assert!(!script.contains("onmessage"));
    }
}
