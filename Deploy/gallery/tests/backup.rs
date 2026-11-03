//! The nightly backup: a valid database copy, same-day reruns, a leftover temp file, 7-day retention of folders
//! that left the source, and keeping the newest 7 copies.

mod support;

use gallery::backup::{KEEP_GONE_SECS, run, utc_date};
use gallery::db::Db;
use support::*;

const DAY: u64 = 86_400;
const T0: u64 = 1_793_664_000; // 2026-11-03 00:00 UTC

#[tokio::test]
async fn a_backup_is_a_valid_copy_and_reruns_the_same_day() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "Spike Hell").await;
    let to = tempfile::tempdir().unwrap();
    let first = run(&app.state.config.data, to.path(), T0 + 3600).await.unwrap();
    assert!(first.db_copy.ends_with("db/gallery-2026-11-03.db"));
    let again = run(&app.state.config.data, to.path(), T0 + 7200).await.unwrap();
    assert_eq!(again.db_copy, first.db_copy, "a second run the same day replaces the copy");
    let copy = Db::open(&first.db_copy).await.unwrap();
    assert!(copy.user_by_login("kim").await.unwrap().is_some());
    assert_eq!(copy.live_game(&id).await.unwrap().unwrap().title, "Spike Hell");
    assert!(to.path().join(format!("games/{id}/v1/Build/game.wasm.br")).exists());
}

#[tokio::test]
async fn a_leftover_temp_file_does_not_stop_the_backup() {
    let app = TestApp::new().await;
    let to = tempfile::tempdir().unwrap();
    std::fs::create_dir_all(to.path().join("db")).unwrap();
    std::fs::write(to.path().join("db/gallery-123-456.tmp"), b"half a database").unwrap();
    run(&app.state.config.data, to.path(), T0).await.unwrap();
    assert!(!to.path().join("db/gallery-123-456.tmp").exists());
}

#[tokio::test]
async fn a_removed_games_files_stay_seven_days() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "Spike Hell").await;
    let to = tempfile::tempdir().unwrap();
    run(&app.state.config.data, to.path(), T0).await.unwrap();
    app.post_form(&format!("/games/{id}/remove"), Some(&kim), "").await;
    let mirrored = to.path().join(format!("games/{id}/v1"));
    run(&app.state.config.data, to.path(), T0 + DAY).await.unwrap();
    assert!(mirrored.exists(), "kept the day after");
    run(&app.state.config.data, to.path(), T0 + DAY + KEEP_GONE_SECS - 1).await.unwrap();
    assert!(mirrored.exists(), "kept until 7 days have passed");
    let report = run(&app.state.config.data, to.path(), T0 + DAY + KEEP_GONE_SECS).await.unwrap();
    assert!(!mirrored.exists(), "pruned after 7 days");
    assert_eq!(report.pruned, 1);
}

#[tokio::test]
async fn a_replaced_version_stays_seven_days_too() {
    let app = TestApp::new().await;
    let kim = app.student("kim", "김민수").await;
    let id = app.new_game(&kim, "v1").await;
    let to = tempfile::tempdir().unwrap();
    run(&app.state.config.data, to.path(), T0).await.unwrap();
    app.upload(&kim, &id, ShareZip::new("v2").build()).await;
    run(&app.state.config.data, to.path(), T0 + DAY).await.unwrap();
    assert!(to.path().join(format!("games/{id}/v1")).exists() && to.path().join(format!("games/{id}/v2")).exists());
}

#[tokio::test]
async fn the_newest_seven_copies_are_kept() {
    let app = TestApp::new().await;
    let to = tempfile::tempdir().unwrap();
    for day in 0..8 {
        run(&app.state.config.data, to.path(), T0 + day * DAY).await.unwrap();
    }
    let mut copies: Vec<String> = std::fs::read_dir(to.path().join("db"))
        .unwrap()
        .map(|e| e.unwrap().file_name().to_string_lossy().into_owned())
        .collect();
    copies.sort();
    assert_eq!(copies.len(), 7);
    assert_eq!(copies[0], format!("gallery-{}.db", utc_date(T0 + DAY)), "the oldest went");
}

#[tokio::test]
async fn a_wrong_data_folder_fails_instead_of_backing_up_nothing() {
    let empty = tempfile::tempdir().unwrap();
    let to = tempfile::tempdir().unwrap();
    assert!(run(empty.path(), to.path(), T0).await.is_err());
    assert!(!empty.path().join("gallery.db").exists(), "no database is created");
}
