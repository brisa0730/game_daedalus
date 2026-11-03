//! The SQLite database: users, sessions, known browsers and games. Every SQL statement lives in this file.

use std::path::Path;
use std::time::Duration;

use sqlx::sqlite::{SqliteConnectOptions, SqliteJournalMode, SqlitePool, SqlitePoolOptions};
use sqlx::{Row, sqlite::SqliteRow};

pub type Result<T> = std::result::Result<T, sqlx::Error>;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Role {
    Teacher,
    Student,
}

impl Role {
    fn as_str(self) -> &'static str {
        match self {
            Role::Teacher => "teacher",
            Role::Student => "student",
        }
    }
}

#[derive(Clone, Debug)]
pub struct User {
    pub id: i64,
    pub login: String,
    pub display_name: String,
    pub role: Role,
    pub password_hash: String,
}

impl User {
    fn from_row(row: &SqliteRow) -> User {
        let role: String = row.get("role");
        User {
            id: row.get("id"),
            login: row.get("login"),
            display_name: row.get("display_name"),
            role: if role == "teacher" { Role::Teacher } else { Role::Student },
            password_hash: row.get("password_hash"),
        }
    }
}

#[derive(Clone, Debug)]
pub struct Session {
    pub user_id: i64,
    pub created_at: i64,
    pub last_seen: i64,
}

#[derive(Clone, Debug)]
pub struct Game {
    pub id: String,
    pub owner_id: Option<i64>,
    /// The owner's display name, as the teacher typed it; `None` once the owner's account is gone.
    pub owner_name: Option<String>,
    pub title: String,
    pub build_name: String,
    pub version: i64,
    /// Unix seconds: first upload (an update leaves it) and latest upload.
    pub created_at: i64,
    pub updated_at: i64,
}

impl Game {
    fn from_row(row: &SqliteRow) -> Game {
        Game {
            id: row.get("id"),
            owner_id: row.get("owner_id"),
            owner_name: row.get("owner_name"),
            title: row.get("title"),
            build_name: row.get("build_name"),
            version: row.get("version"),
            created_at: row.get("created_at"),
            updated_at: row.get("updated_at"),
        }
    }
}

/// A student row on the teacher page.
#[derive(Clone, Debug)]
pub struct StudentRow {
    pub id: i64,
    pub login: String,
    pub display_name: String,
    pub live_games: i64,
}

/// A new game's row, inserted only after its files are in place.
pub struct NewGame<'a> {
    pub id: &'a str,
    pub owner_id: i64,
    pub title: &'a str,
    pub build_name: &'a str,
    pub bytes: i64,
    pub now: i64,
}

const GAME_COLUMNS: &str =
    "g.id, g.owner_id, u.display_name AS owner_name, g.title, g.build_name, g.version, g.created_at, g.updated_at FROM games g LEFT JOIN users u ON u.id = g.owner_id";

#[derive(Clone)]
pub struct Db {
    pool: SqlitePool,
}

impl Db {
    pub async fn open(path: &Path) -> Result<Db> {
        Self::open_with(path, true).await
    }

    /// Opens a database that must already exist (the backup: a mistyped folder must fail, not back up nothing).
    pub async fn open_existing(path: &Path) -> Result<Db> {
        Self::open_with(path, false).await
    }

    async fn open_with(path: &Path, create: bool) -> Result<Db> {
        let options = SqliteConnectOptions::new()
            .filename(path)
            .create_if_missing(create)
            .foreign_keys(true)
            .journal_mode(SqliteJournalMode::Wal)
            .busy_timeout(Duration::from_secs(5));
        let pool = SqlitePoolOptions::new().max_connections(8).connect_with(options).await?;
        sqlx::migrate!("./migrations").run(&pool).await?;
        Ok(Db { pool })
    }

    pub async fn close(&self) {
        self.pool.close().await;
    }

    // Users

    pub async fn user_by_login(&self, login: &str) -> Result<Option<User>> {
        let row = sqlx::query("SELECT * FROM users WHERE login = ?").bind(login).fetch_optional(&self.pool).await?;
        Ok(row.as_ref().map(User::from_row))
    }

    pub async fn user_by_id(&self, id: i64) -> Result<Option<User>> {
        let row = sqlx::query("SELECT * FROM users WHERE id = ?").bind(id).fetch_optional(&self.pool).await?;
        Ok(row.as_ref().map(User::from_row))
    }

    pub async fn teacher_exists(&self) -> Result<bool> {
        let row = sqlx::query("SELECT EXISTS(SELECT 1 FROM users WHERE role = 'teacher')").fetch_one(&self.pool).await?;
        Ok(row.get::<i64, _>(0) != 0)
    }

    pub async fn create_user(&self, login: &str, name: &str, role: Role, hash: &str, now: i64) -> Result<i64> {
        let row = sqlx::query(
            "INSERT INTO users (login, display_name, role, password_hash, created_at) VALUES (?, ?, ?, ?, ?) RETURNING id",
        )
        .bind(login)
        .bind(name)
        .bind(role.as_str())
        .bind(hash)
        .bind(now)
        .fetch_one(&self.pool)
        .await?;
        Ok(row.get(0))
    }

    /// Creates every student in one transaction: all or none.
    pub async fn create_students(&self, students: &[(String, String, String)], now: i64) -> Result<()> {
        let mut tx = self.pool.begin().await?;
        for (login, name, hash) in students {
            sqlx::query(
                "INSERT INTO users (login, display_name, role, password_hash, created_at) VALUES (?, ?, 'student', ?, ?)",
            )
            .bind(login)
            .bind(name)
            .bind(hash)
            .bind(now)
            .execute(&mut *tx)
            .await?;
        }
        tx.commit().await
    }

    /// New password; every session and known browser of the user is forgotten.
    pub async fn set_password(&self, user_id: i64, hash: &str) -> Result<()> {
        let mut tx = self.pool.begin().await?;
        sqlx::query("UPDATE users SET password_hash = ? WHERE id = ?").bind(hash).bind(user_id).execute(&mut *tx).await?;
        sqlx::query("DELETE FROM sessions WHERE user_id = ?").bind(user_id).execute(&mut *tx).await?;
        sqlx::query("DELETE FROM known_browsers WHERE user_id = ?").bind(user_id).execute(&mut *tx).await?;
        tx.commit().await
    }

    /// A user's own password change, unlike [`Db::set_password`] (the teacher's reset, which forgets every session):
    /// only while `old_session` still exists (a reset that ended it meanwhile wins: `Ok(false)`, nothing changed), the
    /// session moves to `new_session` keeping its start time, every other session of the user ends, and every known
    /// browser of the user but `keep_browser` is forgotten (all of them when there is none).
    pub async fn change_own_password(
        &self,
        user_id: i64,
        hash: &str,
        old_session: &[u8],
        new_session: &[u8],
        keep_browser: Option<&[u8]>,
        now: i64,
    ) -> Result<bool> {
        let mut tx = self.pool.begin().await?;
        let changed = sqlx::query(
            "UPDATE users SET password_hash = ? WHERE id = ? \
             AND EXISTS (SELECT 1 FROM sessions WHERE token_hash = ? AND user_id = ?)",
        )
        .bind(hash)
        .bind(user_id)
        .bind(old_session)
        .bind(user_id)
        .execute(&mut *tx)
        .await?;
        if changed.rows_affected() == 0 {
            tx.rollback().await?;
            return Ok(false);
        }
        sqlx::query(
            "INSERT INTO sessions (token_hash, user_id, created_at, last_seen) \
             SELECT ?, user_id, created_at, ? FROM sessions WHERE token_hash = ?",
        )
        .bind(new_session)
        .bind(now)
        .bind(old_session)
        .execute(&mut *tx)
        .await?;
        sqlx::query("DELETE FROM sessions WHERE user_id = ? AND token_hash != ?")
            .bind(user_id)
            .bind(new_session)
            .execute(&mut *tx)
            .await?;
        // Two statements: `token_hash != NULL` would match nothing.
        match keep_browser {
            Some(keep) => {
                sqlx::query("DELETE FROM known_browsers WHERE user_id = ? AND token_hash != ?")
                    .bind(user_id)
                    .bind(keep)
                    .execute(&mut *tx)
                    .await?
            }
            None => sqlx::query("DELETE FROM known_browsers WHERE user_id = ?").bind(user_id).execute(&mut *tx).await?,
        };
        tx.commit().await?;
        Ok(true)
    }

    /// Deletes the user (sessions and known browsers cascade; games keep their rows with no owner).
    pub async fn delete_user(&self, user_id: i64) -> Result<()> {
        sqlx::query("DELETE FROM users WHERE id = ?").bind(user_id).execute(&self.pool).await?;
        Ok(())
    }

    pub async fn students(&self) -> Result<Vec<StudentRow>> {
        let rows = sqlx::query(
            "SELECT u.id, u.login, u.display_name, \
             (SELECT COUNT(*) FROM games g WHERE g.owner_id = u.id AND g.removed_at IS NULL) AS live_games \
             FROM users u WHERE u.role = 'student' ORDER BY u.login",
        )
        .fetch_all(&self.pool)
        .await?;
        Ok(rows
            .iter()
            .map(|r| StudentRow {
                id: r.get("id"),
                login: r.get("login"),
                display_name: r.get("display_name"),
                live_games: r.get("live_games"),
            })
            .collect())
    }

    pub async fn existing_logins(&self, logins: &[String]) -> Result<Vec<String>> {
        let mut taken = Vec::new();
        for login in logins {
            if self.user_by_login(login).await?.is_some() {
                taken.push(login.clone());
            }
        }
        Ok(taken)
    }

    // Sessions

    pub async fn insert_session(&self, token_hash: &[u8], user_id: i64, now: i64) -> Result<()> {
        sqlx::query("INSERT INTO sessions (token_hash, user_id, created_at, last_seen) VALUES (?, ?, ?, ?)")
            .bind(token_hash)
            .bind(user_id)
            .bind(now)
            .bind(now)
            .execute(&self.pool)
            .await?;
        Ok(())
    }

    pub async fn session(&self, token_hash: &[u8]) -> Result<Option<Session>> {
        let row = sqlx::query("SELECT user_id, created_at, last_seen FROM sessions WHERE token_hash = ?")
            .bind(token_hash)
            .fetch_optional(&self.pool)
            .await?;
        Ok(row.map(|r| Session { user_id: r.get(0), created_at: r.get(1), last_seen: r.get(2) }))
    }

    pub async fn touch_session(&self, token_hash: &[u8], now: i64) -> Result<()> {
        sqlx::query("UPDATE sessions SET last_seen = ? WHERE token_hash = ?")
            .bind(now)
            .bind(token_hash)
            .execute(&self.pool)
            .await?;
        Ok(())
    }

    pub async fn delete_session(&self, token_hash: &[u8]) -> Result<()> {
        sqlx::query("DELETE FROM sessions WHERE token_hash = ?").bind(token_hash).execute(&self.pool).await?;
        Ok(())
    }

    /// Deletes sessions idle since `idle_before` or created before `created_before`.
    pub async fn prune_sessions(&self, idle_before: i64, created_before: i64) -> Result<()> {
        sqlx::query("DELETE FROM sessions WHERE last_seen < ? OR created_at < ?")
            .bind(idle_before)
            .bind(created_before)
            .execute(&self.pool)
            .await?;
        Ok(())
    }

    // Known browsers

    pub async fn remember_browser(&self, token_hash: &[u8], user_id: i64, now: i64) -> Result<()> {
        sqlx::query("INSERT OR IGNORE INTO known_browsers (token_hash, user_id, created_at) VALUES (?, ?, ?)")
            .bind(token_hash)
            .bind(user_id)
            .bind(now)
            .execute(&self.pool)
            .await?;
        Ok(())
    }

    pub async fn browser_known(&self, token_hash: &[u8], user_id: i64, created_after: i64) -> Result<bool> {
        let row = sqlx::query(
            "SELECT EXISTS(SELECT 1 FROM known_browsers WHERE token_hash = ? AND user_id = ? AND created_at > ?)",
        )
        .bind(token_hash)
        .bind(user_id)
        .bind(created_after)
        .fetch_one(&self.pool)
        .await?;
        Ok(row.get::<i64, _>(0) != 0)
    }

    // Games

    /// Inserts a new game only while its owner has fewer than `max_live` live games, in one statement, so two
    /// concurrent uploads can't both pass. `false` means the cap was reached.
    pub async fn insert_game(&self, game: &NewGame<'_>, max_live: i64) -> Result<bool> {
        let done = sqlx::query(
            "INSERT INTO games (id, owner_id, title, build_name, version, bytes, created_at, updated_at) \
             SELECT ?, ?, ?, ?, 1, ?, ?, ? \
             WHERE (SELECT COUNT(*) FROM games WHERE owner_id = ? AND removed_at IS NULL) < ?",
        )
        .bind(game.id)
        .bind(game.owner_id)
        .bind(game.title)
        .bind(game.build_name)
        .bind(game.bytes)
        .bind(game.now)
        .bind(game.now)
        .bind(game.owner_id)
        .bind(max_live)
        .execute(&self.pool)
        .await?;
        Ok(done.rows_affected() == 1)
    }

    /// Whether any row, removed ones included, has this id.
    pub async fn game_id_used(&self, id: &str) -> Result<bool> {
        let row = sqlx::query("SELECT EXISTS(SELECT 1 FROM games WHERE id = ?)").bind(id).fetch_one(&self.pool).await?;
        Ok(row.get::<i64, _>(0) != 0)
    }

    pub async fn live_game(&self, id: &str) -> Result<Option<Game>> {
        let row = sqlx::query(&format!("SELECT {GAME_COLUMNS} WHERE g.id = ? AND g.removed_at IS NULL"))
            .bind(id)
            .fetch_optional(&self.pool)
            .await?;
        Ok(row.as_ref().map(Game::from_row))
    }

    /// Every live game, newest update first.
    pub async fn live_games(&self) -> Result<Vec<Game>> {
        let rows = sqlx::query(&format!("SELECT {GAME_COLUMNS} WHERE g.removed_at IS NULL ORDER BY g.updated_at DESC, g.id"))
            .fetch_all(&self.pool)
            .await?;
        Ok(rows.iter().map(Game::from_row).collect())
    }

    pub async fn live_games_of(&self, owner_id: i64) -> Result<Vec<Game>> {
        let rows = sqlx::query(&format!(
            "SELECT {GAME_COLUMNS} WHERE g.owner_id = ? AND g.removed_at IS NULL ORDER BY g.updated_at DESC, g.id"
        ))
        .bind(owner_id)
        .fetch_all(&self.pool)
        .await?;
        Ok(rows.iter().map(Game::from_row).collect())
    }

    pub async fn live_game_count(&self, owner_id: i64) -> Result<i64> {
        let row = sqlx::query("SELECT COUNT(*) FROM games WHERE owner_id = ? AND removed_at IS NULL")
            .bind(owner_id)
            .fetch_one(&self.pool)
            .await?;
        Ok(row.get(0))
    }

    pub async fn live_bytes(&self) -> Result<i64> {
        let row = sqlx::query("SELECT COALESCE(SUM(bytes), 0) FROM games WHERE removed_at IS NULL").fetch_one(&self.pool).await?;
        Ok(row.get(0))
    }

    /// Points a live game at its new version, only if it is still live and still at `old_version`. `false` means
    /// it was removed or replaced meanwhile.
    #[allow(clippy::too_many_arguments)]
    pub async fn set_version(
        &self,
        id: &str,
        old_version: i64,
        title: &str,
        build_name: &str,
        bytes: i64,
        now: i64,
    ) -> Result<bool> {
        let done = sqlx::query(
            "UPDATE games SET version = version + 1, title = ?, build_name = ?, bytes = ?, updated_at = ? \
             WHERE id = ? AND removed_at IS NULL AND version = ?",
        )
        .bind(title)
        .bind(build_name)
        .bind(bytes)
        .bind(now)
        .bind(id)
        .bind(old_version)
        .execute(&self.pool)
        .await?;
        Ok(done.rows_affected() == 1)
    }

    pub async fn tombstone_game(&self, id: &str, now: i64) -> Result<()> {
        sqlx::query("UPDATE games SET removed_at = ? WHERE id = ? AND removed_at IS NULL")
            .bind(now)
            .bind(id)
            .execute(&self.pool)
            .await?;
        Ok(())
    }

    /// `(id, version)` of every live game, and the ids of removed ones: the startup sweep keeps exactly the live
    /// versions' folders.
    pub async fn game_folders(&self) -> Result<(Vec<(String, i64)>, Vec<String>)> {
        let rows = sqlx::query("SELECT id, version, removed_at FROM games").fetch_all(&self.pool).await?;
        let mut live = Vec::new();
        let mut removed = Vec::new();
        for r in rows {
            let removed_at: Option<i64> = r.get(2);
            if removed_at.is_some() {
                removed.push(r.get(0));
            } else {
                live.push((r.get(0), r.get(1)));
            }
        }
        Ok((live, removed))
    }

    /// `VACUUM INTO` a fresh file (the target must not exist).
    pub async fn vacuum_into(&self, target: &Path) -> Result<()> {
        sqlx::query("VACUUM INTO ?").bind(target.to_string_lossy().into_owned()).execute(&self.pool).await?;
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    async fn temp_db() -> (tempfile::TempDir, Db) {
        let dir = tempfile::tempdir().unwrap();
        let db = Db::open(&dir.path().join("gallery.db")).await.unwrap();
        (dir, db)
    }

    #[tokio::test]
    async fn deleting_a_user_cascades_sessions_and_orphans_games() {
        let (_dir, db) = temp_db().await;
        let id = db.create_user("kim", "김민수", Role::Student, "h", 1).await.unwrap();
        db.insert_session(b"t", id, 1).await.unwrap();
        db.remember_browser(b"b", id, 1).await.unwrap();
        let game = NewGame { id: "abc", owner_id: id, title: "T", build_name: "b", bytes: 1, now: 1 };
        assert!(db.insert_game(&game, 10).await.unwrap());
        db.delete_user(id).await.unwrap();
        assert!(db.session(b"t").await.unwrap().is_none());
        assert!(!db.browser_known(b"b", id, 0).await.unwrap());
        let g = db.live_game("abc").await.unwrap().unwrap();
        assert_eq!(g.owner_id, None);
        assert_eq!(g.owner_name, None);
    }

    #[tokio::test]
    async fn a_removed_games_id_cant_be_inserted_again() {
        let (_dir, db) = temp_db().await;
        let id = db.create_user("kim", "김", Role::Student, "h", 1).await.unwrap();
        let game = NewGame { id: "abc", owner_id: id, title: "T", build_name: "b", bytes: 1, now: 1 };
        assert!(db.insert_game(&game, 10).await.unwrap());
        db.tombstone_game("abc", 2).await.unwrap();
        assert!(db.live_game("abc").await.unwrap().is_none());
        assert!(db.game_id_used("abc").await.unwrap());
        assert!(db.insert_game(&game, 10).await.is_err());
    }

    #[tokio::test]
    async fn logins_are_unique() {
        let (_dir, db) = temp_db().await;
        db.create_user("kim", "김", Role::Student, "h", 1).await.unwrap();
        assert!(db.create_user("kim", "다른 김", Role::Student, "h", 1).await.is_err());
    }

    #[tokio::test]
    async fn the_game_cap_is_checked_in_the_insert() {
        let (_dir, db) = temp_db().await;
        let id = db.create_user("kim", "김", Role::Student, "h", 1).await.unwrap();
        for n in 0..2 {
            let gid = format!("g{n}");
            let game = NewGame { id: &gid, owner_id: id, title: "T", build_name: "b", bytes: 1, now: 1 };
            assert!(db.insert_game(&game, 2).await.unwrap());
        }
        let game = NewGame { id: "g9", owner_id: id, title: "T", build_name: "b", bytes: 1, now: 1 };
        assert!(!db.insert_game(&game, 2).await.unwrap());
        assert!(!db.game_id_used("g9").await.unwrap());
    }

    #[tokio::test]
    async fn a_version_update_only_hits_the_expected_live_version() {
        let (_dir, db) = temp_db().await;
        let id = db.create_user("kim", "김", Role::Student, "h", 1).await.unwrap();
        let game = NewGame { id: "abc", owner_id: id, title: "T", build_name: "b", bytes: 1, now: 1 };
        db.insert_game(&game, 10).await.unwrap();
        assert!(!db.set_version("abc", 7, "T2", "b", 1, 2).await.unwrap());
        assert!(db.set_version("abc", 1, "T2", "b", 1, 2).await.unwrap());
        assert_eq!(db.live_game("abc").await.unwrap().unwrap().version, 2);
        db.tombstone_game("abc", 3).await.unwrap();
        assert!(!db.set_version("abc", 2, "T3", "b", 1, 4).await.unwrap());
    }
}
