//! Star ratings (v0.31): anyone rates a game 1–5 stars, one rating per browser, three minutes after the browser
//! first opened the game. A browser is known only by an anonymous random cookie, stored as its SHA-256; no rating
//! row or log line names a user, a login or an address. The play page's script records the open with a POST (a
//! GET never writes), counts down, and saves stars without leaving the page, so the game keeps running.

use axum::Extension;
use axum::body::Bytes;
use axum::extract::{Path, State};
use axum::http::{HeaderMap, HeaderValue, StatusCode, header};
use axum::response::Response;
use maud::{Markup, html};
use serde_json::json;

use crate::app::AppState;
use crate::config::{Config, unix_now};
use crate::db::{Game, User};
use crate::gallery::live;
use crate::session::{self, CurrentUser};
use crate::strings as s;
use crate::upload::json_answer;

pub const RATER_COOKIE: &str = "__Host-gallery_rater";

/// The request's rater token, if it carries a well-formed one.
pub(crate) fn rater(headers: &HeaderMap) -> Option<String> {
    session::cookie(headers, RATER_COOKIE).filter(|t| session::is_token(t)).map(str::to_string)
}

/// The average in tenths of a star, rounded half up in integers: 4.25 is 43.
pub fn average_tenths(total: i64, count: i64) -> i64 {
    (total * 20 + count) / (2 * count)
}

/// Where the gold of the average stars stops, in half units of the 39 × 7 image (one CSS px each at 78 × 14):
/// five 7-unit stars 1 unit apart, a part star measured on the star and not the gap, rounded to a whole pixel so
/// the edge is never a blurred column. Integers only: 4.3 is 68 (34 units), 5.0 is 78.
pub fn gold_halves(tenths: i64) -> i64 {
    let tenths_of_unit = (tenths / 10 * 80 + tenths % 10 * 7).min(390);
    (tenths_of_unit + 2) / 5
}

/// The rows of one 7 × 7 pixel star (x, y, width): the same shape as the rating buttons' star.
const STAR_ROWS: [(i64, i64, i64); 9] =
    [(3, 0, 1), (2, 1, 3), (0, 2, 7), (1, 3, 5), (2, 4, 3), (1, 5, 2), (4, 5, 2), (1, 6, 1), (5, 6, 1)];

/// One path drawing five stars 8 units apart.
fn five_stars() -> String {
    let mut d = String::new();
    for k in 0..5 {
        for (x, y, w) in STAR_ROWS {
            d.push_str(&format!("M{} {y}h{w}v1h-{w}z", x + 8 * k));
        }
    }
    d
}

/// The average once the game has a rating (`None` before): five stars gold up to the average, then `(12명)`.
/// The number is in the hover text and the screen-reader label only. The gold copy sits in a nested svg, which
/// clips at its own width, so a page of cards needs no clip ids; sizes are attributes (the CSP has no inline style).
pub fn summary(game: &Game) -> Option<Markup> {
    (game.ratings > 0).then(|| {
        let tenths = average_tenths(game.stars_total, game.ratings);
        let label = s::rating_label(tenths, game.ratings);
        let gold = gold_halves(tenths);
        let gold = if gold % 2 == 0 { (gold / 2).to_string() } else { format!("{}.5", gold / 2) };
        let stars = five_stars();
        html! {
            span.avg-rating title=(label) {
                span.avg-stars role="img" aria-label=(label) {
                    svg viewBox="0 0 39 7" width="78" height="14" shape-rendering="crispEdges" aria-hidden="true" {
                        path.avg-off d=(stars) {}
                        svg width=(gold) height="7" {
                            path.avg-on d=(stars) {}
                        }
                    }
                }
                span.avg-count aria-hidden="true" { (s::rating_count(game.ratings)) }
            }
        }
    })
}

/// Seconds until this browser may rate: the whole gate when it hasn't opened the game yet.
pub fn wait_secs(opened_at: Option<i64>, now: i64, config: &Config) -> u64 {
    let gate = config.rating_gate_secs as i64;
    opened_at.map_or(gate, |opened| (opened + gate - now).max(0)) as u64
}

fn is_owner(game: &Game, user: Option<&User>) -> bool {
    user.is_some_and(|u| game.owner_id == Some(u.id))
}

fn failed() -> Response {
    json_answer(StatusCode::INTERNAL_SERVER_ERROR, json!({"ok": false, "message": s::RATE_FAILED}))
}

/// `POST /play/<id>/open`: records this browser's first open of the game (a later open keeps the first), giving
/// the browser its rater cookie if it has none. Answers the seconds left and the browser's own stars.
pub async fn open(
    State(state): State<AppState>,
    user: Option<Extension<CurrentUser>>,
    Path(id): Path<String>,
    headers: HeaderMap,
) -> Response {
    let user = user.map(|Extension(CurrentUser(user))| user);
    let Some(game) = live(&state, &id).await else { return json_answer(StatusCode::NOT_FOUND, json!({"ok": false})) };
    if is_owner(&game, user.as_ref()) {
        return json_answer(StatusCode::FORBIDDEN, json!({"ok": false, "message": s::RATE_OWN}));
    }
    let (token, new) = match rater(&headers) {
        Some(token) => (token, false),
        None => (session::new_token(), true),
    };
    let hash = session::token_hash(&token);
    let now = unix_now() as i64;
    let opened_at = match state.db.record_open(&game.id, &hash, now).await {
        Ok(Some(opened_at)) => opened_at,
        Ok(None) => return json_answer(StatusCode::NOT_FOUND, json!({"ok": false})),
        Err(error) => {
            tracing::warn!(game = %game.id, %error, "recording an open failed");
            return failed();
        }
    };
    let mine = match state.db.rating_state(&game.id, &hash).await {
        Ok((_, mine)) => mine,
        Err(error) => {
            tracing::warn!(game = %game.id, %error, "reading a rating failed");
            return failed();
        }
    };
    let wait = wait_secs(Some(opened_at), now, &state.config);
    let mut response = json_answer(StatusCode::OK, json!({"ok": true, "wait": wait, "mine": mine}));
    if new {
        let cookie = format!(
            "{RATER_COOKIE}={token}; HttpOnly; Secure; SameSite=Lax; Path=/; Max-Age={}",
            state.config.rater_cookie_secs
        );
        response.headers_mut().append(header::SET_COOKIE, HeaderValue::from_str(&cookie).expect("cookie is ASCII"));
    }
    response
}

/// The stars in a `stars=N` body, 1 to 5; anything else is refused.
fn parse_stars(body: &[u8]) -> Option<i64> {
    match body {
        [b's', b't', b'a', b'r', b's', b'=', n @ b'1'..=b'5'] => Some(i64::from(n - b'0')),
        _ => None,
    }
}

/// `POST /play/<id>/rate` with `stars=N`: sets this browser's stars, replacing any earlier ones, once the gate has
/// passed. The game is checked before the body is read, so a missing game is a 404 whatever the body.
pub async fn rate(
    State(state): State<AppState>,
    user: Option<Extension<CurrentUser>>,
    Path(id): Path<String>,
    headers: HeaderMap,
    body: Bytes,
) -> Response {
    let user = user.map(|Extension(CurrentUser(user))| user);
    let Some(game) = live(&state, &id).await else { return json_answer(StatusCode::NOT_FOUND, json!({"ok": false})) };
    if is_owner(&game, user.as_ref()) {
        return json_answer(StatusCode::FORBIDDEN, json!({"ok": false, "message": s::RATE_OWN}));
    }
    let Some(stars) = parse_stars(&body) else { return json_answer(StatusCode::BAD_REQUEST, json!({"ok": false})) };
    let now = unix_now() as i64;
    let config = &state.config;
    let Some(token) = rater(&headers) else {
        return json_answer(StatusCode::CONFLICT, json!({"ok": false, "wait": wait_secs(None, now, config)}));
    };
    let hash = session::token_hash(&token);
    match state.db.rate(&game.id, &hash, stars, now - config.rating_gate_secs as i64, now).await {
        Ok(true) => {}
        Ok(false) => {
            let opened_at = match state.db.rating_state(&game.id, &hash).await {
                Ok((opened, _)) => opened,
                Err(error) => {
                    tracing::warn!(game = %game.id, %error, "reading a rating failed");
                    None
                }
            };
            return json_answer(StatusCode::CONFLICT, json!({"ok": false, "wait": wait_secs(opened_at, now, config)}));
        }
        Err(error) => {
            tracing::warn!(game = %game.id, %error, "saving a rating failed");
            return failed();
        }
    }
    let Ok(Some(game)) = state.db.live_game(&game.id).await else { return failed() };
    json_answer(StatusCode::OK, json!({"ok": true, "mine": stars, "summary": summary(&game).map(Markup::into_string)}))
}

/// The play page's rating parts: the average for the title row, and the stars (or the owner's note) for the bar.
/// Reads only: the GET never records an open or sets a cookie.
pub async fn play_parts(state: &AppState, game: &Game, user: Option<&User>, headers: &HeaderMap) -> (Markup, Markup) {
    let config = &state.config;
    let summary = summary(game);
    let avg = html! { span #rating-avg.avg hidden[summary.is_none()] { @if let Some(summary) = summary { (summary) } } };
    if is_owner(game, user) {
        return (avg, html! { p.rate-own { (s::RATE_OWN) } });
    }
    let (opened_at, mine) = match rater(headers) {
        Some(token) => match state.db.rating_state(&game.id, &session::token_hash(&token)).await {
            Ok(rated) => rated,
            Err(error) => {
                tracing::warn!(game = %game.id, %error, "reading a rating failed");
                (None, None)
            }
        },
        None => (None, None),
    };
    let mine = mine.unwrap_or(0);
    let wait = wait_secs(opened_at, unix_now() as i64, config);
    let stars = html! {
        div #rate.rate role="group" aria-label=(s::RATE_PROMPT)
            data-open={ "/play/" (game.id) "/open" }
            data-rate={ "/play/" (game.id) "/rate" }
            data-wait=(wait)
            data-mine=(mine)
            data-msg-wait-min=(s::RATE_WAIT_MINUTES)
            data-msg-wait-sec=(s::RATE_WAIT_SECONDS)
            data-msg-saved=(s::RATE_SAVED)
            data-msg-failed=(s::RATE_FAILED) {
            span #rate-wait hidden[wait == 0] { (s::rate_wait(wait)) }
            span #rate-stars hidden[wait > 0] {
                span.rate-label { (s::RATE_PROMPT) }
                span #rate-buttons {
                    @for k in 1..=5_i64 {
                        button.star.on[k <= mine] type="button" data-stars=(k) aria-label=(s::star_label(k))
                            aria-pressed=(if k == mine { "true" } else { "false" }) {
                            (star_icon())
                        }
                    }
                }
            }
            span #rate-note role="status" {}
        }
    };
    (avg, stars)
}

/// A 7×7 pixel star, like the gallery's other icons.
fn star_icon() -> Markup {
    html! {
        svg viewBox="0 0 7 7" width="20" height="20" shape-rendering="crispEdges" aria-hidden="true" {
            path d="M3 0h1v1H3zM2 1h3v1H2zM0 2h7v1H0zM1 3h5v1H1zM2 4h3v1H2zM1 5h2v1H1zM4 5h2v1H4zM1 6h1v1H1zM5 6h1v1H5z"
                fill="currentColor" {}
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn the_average_rounds_half_up() {
        assert_eq!(average_tenths(17, 4), 43, "4.25 shows as 4.3");
        assert_eq!(average_tenths(13, 3), 43);
        assert_eq!(average_tenths(14, 3), 47);
        assert_eq!(average_tenths(3, 3), 10);
        assert_eq!(average_tenths(15, 3), 50);
        assert_eq!(s::rating_label(43, 12), "5점 만점에 4.3점, 12명");
        assert_eq!(s::rating_label(10, 3), "5점 만점에 1.0점, 3명");
        assert_eq!(s::rating_count(12), "(12명)");
    }

    #[test]
    fn the_gold_stops_on_a_whole_pixel_inside_the_star() {
        // 1.0: past the first star (the gap after it is empty). 4.3: two columns into the fifth star; 4.7: five.
        for (tenths, halves) in [(10, 16), (43, 68), (47, 74), (50, 78), (30, 48), (49, 77), (41, 65)] {
            assert_eq!(gold_halves(tenths), halves, "{tenths}");
        }
        assert_eq!(gold_halves(average_tenths(17, 4)), 68, "4.25 shows as 4.3");
    }

    #[test]
    fn the_average_is_stars_with_the_count() {
        let game = Game {
            id: "abc".into(),
            owner_id: None,
            owner_name: None,
            title: "t".into(),
            build_name: "b".into(),
            version: 1,
            created_at: 0,
            updated_at: 0,
            published_version: None,
            ratings: 3,
            stars_total: 13,
        };
        let html = summary(&game).unwrap().into_string();
        assert!(html.contains(r#"title="5점 만점에 4.3점, 3명""#), "{html}");
        assert!(html.contains(r#"role="img" aria-label="5점 만점에 4.3점, 3명""#), "{html}");
        assert!(html.contains(r#"<svg width="34" height="7">"#), "{html}");
        assert!(html.contains(r#"<span class="avg-count" aria-hidden="true">(3명)</span>"#), "{html}");
        assert!(html.contains(r#"class="avg-off""#) && html.contains(r#"class="avg-on""#), "both layers: {html}");
        assert_eq!(html.matches(r#" d="M"#).count(), 2, "five stars in each layer");
        assert!(!html.contains("class=\"star"), "never the buttons' class");
        // 49 / 10 = 4.9: 77 halves, the half-pixel width.
        let near_five = Game { ratings: 10, stars_total: 49, ..game.clone() };
        assert!(summary(&near_five).unwrap().into_string().contains(r#"<svg width="38.5" height="7">"#));
        assert!(summary(&Game { ratings: 0, ..game }).is_none());
    }

    #[test]
    fn the_countdown_says_minutes_then_seconds() {
        assert_eq!(s::rate_wait(180), "3분 후에 별점을 줄 수 있어요");
        assert_eq!(s::rate_wait(61), "2분 후에 별점을 줄 수 있어요");
        assert_eq!(s::rate_wait(60), "1분 후에 별점을 줄 수 있어요");
        assert_eq!(s::rate_wait(59), "59초 후에 별점을 줄 수 있어요");
        assert_eq!(s::rate_wait(1), "1초 후에 별점을 줄 수 있어요");
    }

    #[test]
    fn only_stars_one_to_five_parse() {
        for (body, stars) in [("stars=1", Some(1)), ("stars=5", Some(5)), ("stars=0", None), ("stars=6", None)] {
            assert_eq!(parse_stars(body.as_bytes()), stars, "{body}");
        }
        for body in ["", "stars=", "stars=x", "stars=4&x", "stars=44", "text=x"] {
            assert_eq!(parse_stars(body.as_bytes()), None, "{body}");
        }
    }

    #[test]
    fn the_wait_counts_from_the_first_open() {
        let config = Config::new(std::path::Path::new("/tmp"));
        assert_eq!(wait_secs(None, 1000, &config), 180);
        assert_eq!(wait_secs(Some(1000), 1100, &config), 80);
        assert_eq!(wait_secs(Some(1000), 1180, &config), 0);
        assert_eq!(wait_secs(Some(1000), 9999, &config), 0);
    }
}
