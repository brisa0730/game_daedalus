---
slug: gallery-public
round: 1
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-11-03T22:16:00+09:00
blocking: 1
recommendations: 1
---

# Review round 1: gallery-public

## What I could run

I had shell access to the Mac mini repo folder (`device_bash`: `cat`, `grep`, `sed`, `git log` over `Deploy/gallery/`, `Documents/`). HEAD is `018d4f8` (v0.19), matching the plan's dependency. I did **not** run `cargo build`, `cargo test`, clippy, the look check or the live server. Everything below is checked against source text only.

## What I verified (holds)

- `app.rs`: the route table and `Access` enum are as described. `/`, `/shots/{file}`, `/play/{id}` are `Page` today; `gallery_router` sends every non-`Public` rule through `session::require_access`, so a new `Open` arm there is the only place it needs handling. `grep -n "Open, " src/app.rs` will match only route lines (the enum variant is `Open,` + newline).
- `session.rs:77-96` `require_access`: a missing session returns before any `CurrentUser` insert, so the plan's "continue without `CurrentUser` on `Open`" is a one-arm change. `login_page` already redirects a signed-in browser to `/`. `post_logout` (line 387) goes to `/login` today.
- `pages.rs` `shell`: the header renders only when `user` is `Some`; `login_form` calls `pages::page(…, None, None, …)`, so a `Nav::Login` and a signed-out header branch are needed exactly as Step 2 says.
- `gallery.rs`: `list`, `card`, `may_remove`, `play_view` all take a non-optional user today; `play_view` signs the grant from game id + time only (no user), so signing it for anyone is a no-op change. Screenshot `Cache-Control: private, …` is fine for a public image.
- `play.rs` `enter`: only frame-dest + same-site + valid grant; unchanged behaviour is correct, and the plan's Risks paragraph (curl can forge `Sec-Fetch-Dest`) is accurate.
- `guard.rs`: both layers wrap fallbacks too, so "every response they wrap" is right; `dispatch`'s unknown-host 404 is outside both.
- `teacher.rs`: `student()` (line 260) is the students-only 404 rule; `clean_label` + `config.name_max_chars` (20) is the create path's rule; `LINE_NO_NAME`, `COL_NAME`, `PASTE_HINT` exist in `strings.rs:141-153`.
- Every "session is gone" probe citation checks out: `password.rs:131-132`, `sign_in.rs:129,131,141`, `teacher.rs:98,115` all assert `location: /login` on `GET /`. I found no other test that relies on a signed-out `GET /`, `/play` or `/shots` redirecting (`games.rs`, `look.rs`, `play.rs`, `upload.rs` all pass a cookie).
- `routing.rs`: `every_signed_in_page_shows_the_name_and_sign_out` filters `!= Public`, so `Open` is covered with a cookie; `gallery_headers_are_on_every_kind_of_response` takes its 303 from `GET /` without a cookie, so it does need to move.
- `config.rs:55`: `gallery_origin` is `https://suwon.orcarin.com`, so the copy-link value the test expects is right.
- Glossary lines 20 and 25, GDD.md v0.12 and v0.16 lines, systems.md 197/200/206/208/211: all exist and say what the Notes say they say.

## Findings

### F1 — blocking — design — Step 10 (code review) runs after the public deploy
**Location:** Steps 8 → 10.
**Defect:** The plan makes the gallery reachable by anyone on the internet in Step 8.3, then runs the code-review agent in Step 10 and "fixes HIGH/MED". This change is an access-control change on an internet-facing server; the review is exactly the step most likely to find a HIGH in it (an `Open` arm that lets something through, a handler still assuming `CurrentUser`). As written, such a finding is live and public before anyone looks for it, and its fix needs a redeploy the plan never schedules, so the live binary can end up differing from the reviewed, committed code.
**Fix:** move the code review (and its fixes, plus a re-run of Step 7) to before Step 8. Step 9's walk of the criteria can stay after.

### F2 — non-blocking — accuracy — Step 8.3 "rebuild from the committed tree"
**Location:** Step 8.3; Post-Plan Cleanup item 5.
**Defect:** Nothing is committed until Post-Plan Cleanup item 5, which runs after Step 8. At 8.3 there is no committed tree with this work in it, so the instruction can't be followed literally; the real intent is "the working tree with the three routes restored". Earlier gallery plans (gallery-change-password Step 6) also deploy from the working tree, so that's the pattern; the wording just needs to match it, or the commit needs to come first.
**Fix:** say "rebuild after restoring the three routes (`git diff --stat src/app.rs` shows only this plan's change)" or move the commit ahead of the deploy.

### F3 — non-blocking — accuracy — `content.md` § Student Content is missing from the GDD notes
**Location:** Notes to GDD.
**Defect:** `Documents/GDD/content.md:31` says the class gallery is one "which only the teacher and the students can enter". After this plan that is false, and it is the line that frames the takedown path. The Notes list GDD.md, Glossary, systems.md, platforms.md and decisions.md but not content.md. Also, the Notes have two separate bullets both headed `systems.md` § Share; merging them would make the list easier to follow.
**Fix:** add content.md:31 to the Notes (the gallery is public to view; uploaders are still only the class; teacher remove is still the takedown path, now for public content).

### F4 — non-blocking — design — the rename pass doesn't cover screenshots
**Location:** Step 8.2.
**Defect:** The point of the two-deploy rollout is that no real name is ever public. Step 8.2 checks student names, the teacher's name and game titles. But each card's screenshot is now served to anyone (`/shots/{file}` becomes `Open`), and it is a frame of the student's own level, which can contain the student's name written in tiles or a sign. Nothing in the plan looks at them.
**Fix:** add "glance at each card's screenshot" to the operator's 8.2 pass (the teacher can remove such a game, as for titles).

### F5 — non-blocking — design — the signed-out header on the gallery's 404 fallback shows 로그인 to signed-in users
**Location:** Step 2, "Error pages (`not_found(None)`, `forbidden(None)`) get this header too."
**Defect:** The router fallback (`app.rs` `.fallback(|| async { pages::not_found(None) })`) and `gallery_layer`'s cross-origin refusal (`pages::forbidden(None)`) never see the session. Today they render with no header. With this plan, a signed-in student who mistypes a URL gets the signed-out header with a 로그인 link, which looks like they've been signed out. Harmless (the link bounces them to `/`), but it's a visible inconsistency that `routing.rs`'s `missing` probe (with a cookie) will show and no test asserts against.
**Fix:** either accept and say so, or have those two `None` call sites render a header with only the logo and 게임 목록 (no 로그인), so it is correct whether or not the visitor is signed in.

### R1 — recommendation — the open question on losing real names after a rename
**Location:** Notes, "Open question for the operator".
**Recommendation:** accept, as the plan proposes: the teacher keeps their own ID → student list off the server. **Reason:** a real name that is never stored on an internet-facing server can't leak through a bug or a breach, which a teacher-only column could; and it avoids a schema change and its upgrade step. The cost (slips and confirms show nicknames) is small because the login ID is printed on every slip.

## Not verified

- That `Option<Extension<CurrentUser>>` compiles as an extractor under the locked axum 0.8 version (I believe `Extension` implements `OptionalFromRequestParts` in 0.8; the compiler will settle it).
- The CDP clipboard check: reading the clipboard back in headless Chrome needs the read permission granted as well as write. Not a finding; the Author will see it when running Step 6.
- No test, build, look check or live check was run.

## Reviewer's view

I would build this plan once F1 is fixed. The access change itself is small and well-placed (one new `Access` arm in the one place every route goes through), the six probe migrations and the test changes all check out against the code, and the two-deploy rollout is the right way to keep real names off the internet. The only thing I'd stop on is the order: the code review has to come before the public deploy (F1). F2–F5 are wording and small gaps to fold in, and R1 answers the open question.
