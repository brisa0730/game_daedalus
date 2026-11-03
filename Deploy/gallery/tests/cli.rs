//! The operator's teacher commands: create once, reset (which ends sessions), refuse bad input.

mod support;

use gallery::cli::{teacher_create, teacher_reset};
use gallery::db::{Db, Role};
use support::test_config;

async fn setup() -> (tempfile::TempDir, gallery::config::Config, Db) {
    let dir = tempfile::tempdir().unwrap();
    let config = test_config(dir.path());
    let db = Db::open(&config.db_path()).await.unwrap();
    (dir, config, db)
}

#[tokio::test]
async fn create_makes_one_teacher() {
    let (_dir, config, db) = setup().await;
    let password = teacher_create(&config, &db, "Teach", "선생님").await.unwrap();
    let user = db.user_by_login("teach").await.unwrap().unwrap();
    assert_eq!((user.role, user.display_name.as_str()), (Role::Teacher, "선생님"));
    let hasher = gallery::accounts::Hasher::new(&config);
    assert!(hasher.verify(user.password_hash, password).await.unwrap());
    let again = teacher_create(&config, &db, "other", "또").await;
    assert!(again.unwrap_err().contains("teacher reset"));
}

#[tokio::test]
async fn reset_changes_the_hash_and_ends_sessions() {
    let (_dir, config, db) = setup().await;
    teacher_create(&config, &db, "teach", "선생님").await.unwrap();
    let before = db.user_by_login("teach").await.unwrap().unwrap();
    db.insert_session(b"token", before.id, 1).await.unwrap();
    db.remember_browser(b"browser", before.id, 1).await.unwrap();
    teacher_reset(&config, &db, "teach").await.unwrap();
    let after = db.user_by_login("teach").await.unwrap().unwrap();
    assert_ne!(before.password_hash, after.password_hash);
    assert!(db.session(b"token").await.unwrap().is_none());
    assert!(!db.browser_known(b"browser", before.id, 0).await.unwrap());
}

#[tokio::test]
async fn bad_input_is_refused() {
    let (_dir, config, db) = setup().await;
    assert!(teacher_create(&config, &db, "t", "선생님").await.is_err(), "id too short");
    assert!(teacher_create(&config, &db, "teach", " \t").await.is_err(), "empty name");
    assert!(teacher_reset(&config, &db, "nobody").await.is_err());
    db.create_user("kim", "김", Role::Student, "h", 1).await.unwrap();
    assert!(teacher_reset(&config, &db, "kim").await.is_err(), "a student is not reset here");
    assert!(!db.teacher_exists().await.unwrap());
}
