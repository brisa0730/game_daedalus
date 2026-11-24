//! The SQLite database: users, sessions, known browsers, games, ratings and comments. Every SQL statement lives in this file.

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
    /// The public nickname: on cards, the play view and the header.
    pub display_name: String,
    /// The name the teacher knows the student by; only the teacher's pages show it (empty for the teacher).
    pub real_name: String,
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
            real_name: row.get("real_name"),
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
    /// The owner's nickname (never the real name); `None` once the owner's account is gone.
    pub owner_name: Option<String>,
    pub title: String,
    pub build_name: String,
    pub version: i64,
    /// Unix seconds: first upload (an update leaves it) and latest upload.
    pub created_at: i64,
    pub updated_at: i64,
    /// The version whose build files are on the Naver bucket (storage.rs); `None` until a publish finishes.
    pub published_version: Option<i64>,
    /// How many ratings the game has, and their stars added up (ratings.rs turns them into the average).
    pub ratings: i64,
    pub stars_total: i64,
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
            published_version: row.get("published_version"),
            ratings: row.get("ratings"),
            stars_total: row.get("stars_total"),
        }
    }
}

/// A student row on the teacher page.
#[derive(Clone, Debug)]
pub struct StudentRow {
    pub id: i64,
    pub login: String,
    pub display_name: String,
    pub real_name: String,
    pub live_games: i64,
}

/// A comment or reply as the pages show it (v0.35). The writer's names are joined in when it is read, so a renamed
/// nickname shows on old comments too. The guest hash is never read into it: `by_this_browser` is computed in SQL.
#[derive(Clone, Debug)]
pub struct CommentRow {
    pub id: i64,
    pub game_id: String,
    pub parent_id: Option<i64>,
    /// `None` for a guest (손님).
    pub user_id: Option<i64>,
    pub body: String,
    pub created_at: i64,
    /// Its writer deleted it (it is only hidden), and whether its parent was.
    pub hidden: bool,
    pub parent_hidden: bool,
    pub nickname: Option<String>,
    pub real_name: Option<String>,
    /// The writer is the teacher.
    pub teacher: bool,
    /// A guest comment written from the viewing browser (the viewer's rater hash matches).
    pub by_this_browser: bool,
    pub game_title: String,
}

impl CommentRow {
    fn from_row(row: &SqliteRow) -> CommentRow {
        CommentRow {
            id: row.get("id"),
            game_id: row.get("game_id"),
            parent_id: row.get("parent_id"),
            user_id: row.get("user_id"),
            body: row.get("body"),
            created_at: row.get("created_at"),
            hidden: row.get("hidden"),
            parent_hidden: row.get("parent_hidden"),
            nickname: row.get("nickname"),
            real_name: row.get("real_name"),
            teacher: row.get("teacher"),
            by_this_browser: row.get("by_this_browser"),
            game_title: row.get("game_title"),
        }
    }
}

/// Why `insert_comment` wrote nothing, read after the fact (the insert itself checks everything).
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Refusal {
    GameGone,
    Owner,
    /// A guest before the gate: seconds left.
    Gate(u64),
    /// The writer's last top-level comment was too recent: seconds left.
    Gap(u64),
    /// The gallery-wide guest cap.
    Busy,
}

/// Why `insert_reply` wrote nothing.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ReplyRefusal {
    GameGone,
    NotOwner,
    /// No such comment on this game, or it was deleted.
    ParentGone,
    /// The parent is itself a reply.
    NotTopLevel,
}

/// The columns of a `CommentRow`; `?1` is the viewer's rater hash (or NULL).
const COMMENT_COLUMNS: &str = "c.id, c.game_id, c.parent_id, c.user_id, c.body, c.created_at, \
     c.hidden_at IS NOT NULL AS hidden, p.hidden_at IS NOT NULL AS parent_hidden, \
     u.display_name AS nickname, u.real_name AS real_name, COALESCE(u.role = 'teacher', 0) AS teacher, \
     COALESCE(c.guest_hash IS NOT NULL AND c.guest_hash = ?1, 0) AS by_this_browser, g.title AS game_title \
     FROM comments c LEFT JOIN comments p ON p.id = c.parent_id LEFT JOIN users u ON u.id = c.user_id \
     JOIN games g ON g.id = c.game_id";

/// Rows read with one more than the page, and the cursor for the next page when there were more.
fn paged(mut rows: Vec<CommentRow>, limit: i64) -> (Vec<CommentRow>, Option<i64>) {
    let more = rows.len() as i64 > limit;
    rows.truncate(limit.max(0) as usize);
    let next = if more { rows.last().map(|r| r.id) } else { None };
    (rows, next)
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

const GAME_COLUMNS: &str = "g.id, g.owner_id, u.display_name AS owner_name, g.title, g.build_name, g.version, \
     g.created_at, g.updated_at, g.published_version, \
     (SELECT COUNT(*) FROM ratings r WHERE r.game_id = g.id) AS ratings, \
     (SELECT COALESCE(SUM(r.stars), 0) FROM ratings r WHERE r.game_id = g.id) AS stars_total \
     FROM games g LEFT JOIN users u ON u.id = g.owner_id";

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

    /// Creates every student in one transaction: all or none. Each is `(login, real name, nickname, hash)`.
    pub async fn create_students(&self, students: &[(String, String, String, String)], now: i64) -> Result<()> {
        let mut tx = self.pool.begin().await?;
        for (login, real_name, nickname, hash) in students {
            sqlx::query(
                "INSERT INTO users (login, real_name, display_name, role, password_hash, created_at) \
                 VALUES (?, ?, ?, 'student', ?, ?)",
            )
            .bind(login)
            .bind(real_name)
            .bind(nickname)
            .bind(hash)
            .bind(now)
            .execute(&mut *tx)
            .await?;
        }
        tx.commit().await
    }

    pub async fn set_names(&self, user_id: i64, real_name: &str, nickname: &str) -> Result<()> {
        sqlx::query("UPDATE users SET real_name = ?, display_name = ? WHERE id = ?")
            .bind(real_name)
            .bind(nickname)
            .bind(user_id)
            .execute(&self.pool)
            .await?;
        Ok(())
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
            "SELECT u.id, u.login, u.display_name, u.real_name, \
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
                real_name: r.get("real_name"),
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

    /// Marks `version` as published, only while it is still the game's live version: a publish that finishes after
    /// a removal or a newer upload records nothing.
    pub async fn set_published(&self, id: &str, version: i64) -> Result<bool> {
        let done = sqlx::query("UPDATE games SET published_version = ? WHERE id = ? AND version = ? AND removed_at IS NULL")
            .bind(version)
            .bind(id)
            .bind(version)
            .execute(&self.pool)
            .await?;
        Ok(done.rows_affected() == 1)
    }

    /// Unmarks a game whose bucket copy is missing, with the same condition as `set_published`.
    pub async fn clear_published(&self, id: &str, version: i64) -> Result<()> {
        sqlx::query("UPDATE games SET published_version = NULL WHERE id = ? AND version = ? AND removed_at IS NULL")
            .bind(id)
            .bind(version)
            .execute(&self.pool)
            .await?;
        Ok(())
    }

    /// Removes a game: the row stays (its id is never used again), its ratings, open records and comments go, in
    /// one transaction. Owner, teacher and account removal all come through here.
    pub async fn tombstone_game(&self, id: &str, now: i64) -> Result<()> {
        let mut tx = self.pool.begin().await?;
        sqlx::query("UPDATE games SET removed_at = ? WHERE id = ? AND removed_at IS NULL")
            .bind(now)
            .bind(id)
            .execute(&mut *tx)
            .await?;
        sqlx::query("DELETE FROM comments WHERE game_id = ?").bind(id).execute(&mut *tx).await?;
        sqlx::query("DELETE FROM ratings WHERE game_id = ?").bind(id).execute(&mut *tx).await?;
        sqlx::query("DELETE FROM game_opens WHERE game_id = ?").bind(id).execute(&mut *tx).await?;
        tx.commit().await
    }

    // Ratings

    /// Records that this browser opened a live game, keeping its first open. Returns that first open, or `None`
    /// when the game isn't live (the insert checks it, so an open racing a removal leaves nothing).
    pub async fn record_open(&self, game_id: &str, rater_hash: &[u8], now: i64) -> Result<Option<i64>> {
        sqlx::query(
            "INSERT OR IGNORE INTO game_opens (game_id, rater_hash, opened_at) SELECT ?1, ?2, ?3 \
             WHERE EXISTS (SELECT 1 FROM games WHERE id = ?1 AND removed_at IS NULL)",
        )
        .bind(game_id)
        .bind(rater_hash)
        .bind(now)
        .execute(&self.pool)
        .await?;
        let row = sqlx::query(
            "SELECT o.opened_at FROM game_opens o JOIN games g ON g.id = o.game_id \
             WHERE o.game_id = ? AND o.rater_hash = ? AND g.removed_at IS NULL",
        )
        .bind(game_id)
        .bind(rater_hash)
        .fetch_optional(&self.pool)
        .await?;
        Ok(row.map(|r| r.get(0)))
    }

    /// This browser's first open of the game and its stars, if any. Read only (the play page's GET).
    pub async fn rating_state(&self, game_id: &str, rater_hash: &[u8]) -> Result<(Option<i64>, Option<i64>)> {
        let row = sqlx::query(
            "SELECT (SELECT opened_at FROM game_opens WHERE game_id = ?1 AND rater_hash = ?2), \
                    (SELECT stars FROM ratings WHERE game_id = ?1 AND rater_hash = ?2)",
        )
        .bind(game_id)
        .bind(rater_hash)
        .fetch_one(&self.pool)
        .await?;
        Ok((row.get(0), row.get(1)))
    }

    /// Sets this browser's stars on a live game, replacing any earlier ones, only if it first opened the game at or
    /// before `opened_by` (the gate, checked in the same statement). `false` means nothing was written.
    pub async fn rate(&self, game_id: &str, rater_hash: &[u8], stars: i64, opened_by: i64, now: i64) -> Result<bool> {
        let done = sqlx::query(
            "INSERT INTO ratings (game_id, rater_hash, stars, rated_at) SELECT ?1, ?2, ?3, ?4 \
             WHERE EXISTS (SELECT 1 FROM game_opens WHERE game_id = ?1 AND rater_hash = ?2 AND opened_at <= ?5) \
             AND EXISTS (SELECT 1 FROM games WHERE id = ?1 AND removed_at IS NULL) \
             ON CONFLICT (game_id, rater_hash) DO UPDATE SET stars = excluded.stars, rated_at = excluded.rated_at",
        )
        .bind(game_id)
        .bind(rater_hash)
        .bind(stars)
        .bind(now)
        .bind(opened_by)
        .execute(&self.pool)
        .await?;
        Ok(done.rows_affected() == 1)
    }

    /// Deletes open records from before `before` of browsers that never rated that game.
    pub async fn prune_opens(&self, before: i64) -> Result<u64> {
        let done = sqlx::query(
            "DELETE FROM game_opens WHERE opened_at < ? AND NOT EXISTS \
             (SELECT 1 FROM ratings r WHERE r.game_id = game_opens.game_id AND r.rater_hash = game_opens.rater_hash)",
        )
        .bind(before)
        .execute(&self.pool)
        .await?;
        Ok(done.rows_affected())
    }

    // Comments

    /// Writes a top-level comment, in one statement that checks everything: the game is live and the writer isn't
    /// its owner; a guest (`hash`, no `user`) first opened it at or before `opened_by` (the gate); the writer's last
    /// top-level comment (hidden ones included) is at least `gap` old; and, for a guest, fewer than `cap` guest
    /// comments in the last `window` seconds. A signed-in writer passes `hash: None`. `None` means nothing was written.
    #[allow(clippy::too_many_arguments)]
    pub async fn insert_comment(
        &self,
        game_id: &str,
        user: Option<i64>,
        hash: Option<&[u8]>,
        body: &str,
        now: i64,
        opened_by: i64,
        gap: i64,
        cap: i64,
        window: i64,
    ) -> Result<Option<i64>> {
        let row = sqlx::query(
            "INSERT INTO comments (game_id, user_id, guest_hash, body, created_at) SELECT ?1, ?2, ?3, ?4, ?5 \
             WHERE EXISTS (SELECT 1 FROM games WHERE id = ?1 AND removed_at IS NULL AND (?2 IS NULL OR owner_id IS NOT ?2)) \
             AND (?2 IS NOT NULL OR EXISTS \
                 (SELECT 1 FROM game_opens WHERE game_id = ?1 AND rater_hash = ?3 AND opened_at <= ?6)) \
             AND NOT EXISTS (SELECT 1 FROM comments WHERE parent_id IS NULL AND (user_id = ?2 OR guest_hash = ?3) \
                 AND created_at > ?5 - ?7) \
             AND (?2 IS NOT NULL OR \
                 (SELECT COUNT(*) FROM comments WHERE guest_hash IS NOT NULL AND created_at > ?5 - ?9) < ?8) \
             RETURNING id",
        )
        .bind(game_id)
        .bind(user)
        .bind(hash)
        .bind(body)
        .bind(now)
        .bind(opened_by)
        .bind(gap)
        .bind(cap)
        .bind(window)
        .fetch_optional(&self.pool)
        .await?;
        Ok(row.map(|r| r.get(0)))
    }

    /// Why `insert_comment` with the same inputs wrote nothing (read only), in the order the page explains them.
    /// `None` when every check passes now (a race that cleared meanwhile).
    #[allow(clippy::too_many_arguments)]
    pub async fn comment_refusal(
        &self,
        game_id: &str,
        user: Option<i64>,
        hash: Option<&[u8]>,
        now: i64,
        opened_by: i64,
        gap: i64,
        cap: i64,
        window: i64,
    ) -> Result<Option<Refusal>> {
        let row = sqlx::query(
            "SELECT EXISTS (SELECT 1 FROM games WHERE id = ?1 AND removed_at IS NULL), \
                    (SELECT owner_id FROM games WHERE id = ?1 AND removed_at IS NULL), \
                    (SELECT opened_at FROM game_opens WHERE game_id = ?1 AND rater_hash = ?3), \
                    (SELECT MAX(created_at) FROM comments WHERE parent_id IS NULL AND (user_id = ?2 OR guest_hash = ?3)), \
                    (SELECT COUNT(*) FROM comments WHERE guest_hash IS NOT NULL AND created_at > ?4)",
        )
        .bind(game_id)
        .bind(user)
        .bind(hash)
        .bind(now - window)
        .fetch_one(&self.pool)
        .await?;
        let live: bool = row.get(0);
        let owner: Option<i64> = row.get(1);
        let opened_at: Option<i64> = row.get(2);
        let last: Option<i64> = row.get(3);
        let recent_guests: i64 = row.get(4);
        if !live {
            return Ok(Some(Refusal::GameGone));
        }
        if user.is_some() && owner == user {
            return Ok(Some(Refusal::Owner));
        }
        if user.is_none() {
            let left = opened_at.map_or(now - opened_by, |opened| opened - opened_by);
            if left > 0 {
                return Ok(Some(Refusal::Gate(left as u64)));
            }
        }
        if let Some(last) = last.filter(|last| *last > now - gap) {
            return Ok(Some(Refusal::Gap((last + gap - now) as u64)));
        }
        if user.is_none() && recent_guests >= cap {
            return Ok(Some(Refusal::Busy));
        }
        Ok(None)
    }

    /// Writes the owner's reply under a visible top-level comment of their live game, in one statement. Replies
    /// skip the gap and don't start its clock. `None` means nothing was written.
    pub async fn insert_reply(&self, game_id: &str, parent_id: i64, owner_id: i64, body: &str, now: i64) -> Result<Option<i64>> {
        let row = sqlx::query(
            "INSERT INTO comments (game_id, parent_id, user_id, body, created_at) SELECT ?1, ?2, ?3, ?4, ?5 \
             WHERE EXISTS (SELECT 1 FROM comments WHERE id = ?2 AND game_id = ?1 AND parent_id IS NULL AND hidden_at IS NULL) \
             AND EXISTS (SELECT 1 FROM games WHERE id = ?1 AND removed_at IS NULL AND owner_id = ?3) \
             RETURNING id",
        )
        .bind(game_id)
        .bind(parent_id)
        .bind(owner_id)
        .bind(body)
        .bind(now)
        .fetch_optional(&self.pool)
        .await?;
        Ok(row.map(|r| r.get(0)))
    }

    /// Why `insert_reply` with the same inputs wrote nothing (read only). `None` when every check passes now.
    pub async fn reply_refusal(&self, game_id: &str, parent_id: i64, owner_id: i64) -> Result<Option<ReplyRefusal>> {
        let row = sqlx::query(
            "SELECT (SELECT owner_id FROM games WHERE id = ?1 AND removed_at IS NULL), \
                    EXISTS (SELECT 1 FROM games WHERE id = ?1 AND removed_at IS NULL), \
                    (SELECT parent_id IS NULL FROM comments WHERE id = ?2 AND game_id = ?1 AND hidden_at IS NULL)",
        )
        .bind(game_id)
        .bind(parent_id)
        .fetch_one(&self.pool)
        .await?;
        let owner: Option<i64> = row.get(0);
        let live: bool = row.get(1);
        let top_level: Option<bool> = row.get(2);
        Ok(if !live {
            Some(ReplyRefusal::GameGone)
        } else if owner != Some(owner_id) {
            Some(ReplyRefusal::NotOwner)
        } else {
            match top_level {
                None => Some(ReplyRefusal::ParentGone),
                Some(false) => Some(ReplyRefusal::NotTopLevel),
                Some(true) => None,
            }
        })
    }

    /// A page of a game's visible top-level comments, newest first, before the cursor `before` (an id), and the
    /// next cursor when there are more.
    pub async fn comments_page(
        &self,
        game_id: &str,
        before: Option<i64>,
        limit: i64,
        viewer_hash: Option<&[u8]>,
    ) -> Result<(Vec<CommentRow>, Option<i64>)> {
        let rows = sqlx::query(&format!(
            "SELECT {COMMENT_COLUMNS} WHERE c.game_id = ?2 AND c.parent_id IS NULL AND c.hidden_at IS NULL \
             AND (?3 IS NULL OR c.id < ?3) ORDER BY c.id DESC LIMIT ?4"
        ))
        .bind(viewer_hash)
        .bind(game_id)
        .bind(before)
        .bind(limit + 1)
        .fetch_all(&self.pool)
        .await?;
        Ok(paged(rows.iter().map(CommentRow::from_row).collect(), limit))
    }

    /// The visible replies under these comments of a game, oldest first. Replies of a hidden comment are never
    /// returned.
    pub async fn replies_of(&self, game_id: &str, parents: &[i64], viewer_hash: Option<&[u8]>) -> Result<Vec<CommentRow>> {
        let (Some(low), Some(high)) = (parents.iter().min(), parents.iter().max()) else { return Ok(Vec::new()) };
        let rows = sqlx::query(&format!(
            "SELECT {COMMENT_COLUMNS} WHERE c.game_id = ?4 AND c.parent_id BETWEEN ?2 AND ?3 \
             AND c.hidden_at IS NULL AND p.hidden_at IS NULL ORDER BY c.id"
        ))
        .bind(viewer_hash)
        .bind(low)
        .bind(high)
        .bind(game_id)
        .fetch_all(&self.pool)
        .await?;
        Ok(rows
            .iter()
            .map(CommentRow::from_row)
            .filter(|r| r.parent_id.is_some_and(|p| parents.contains(&p)))
            .collect())
    }

    /// One comment or reply, hidden ones included (`hidden`, `parent_hidden` say so).
    pub async fn comment_by_id(&self, id: i64, viewer_hash: Option<&[u8]>) -> Result<Option<CommentRow>> {
        let row = sqlx::query(&format!("SELECT {COMMENT_COLUMNS} WHERE c.id = ?2"))
            .bind(viewer_hash)
            .bind(id)
            .fetch_optional(&self.pool)
            .await?;
        Ok(row.as_ref().map(CommentRow::from_row))
    }

    /// 최근 댓글: every comment and reply on live games, newest first, before the cursor. A hidden one (or one under
    /// a hidden comment) is listed only while it was hidden at or after `hidden_since`.
    pub async fn recent_comments(&self, before: Option<i64>, limit: i64, hidden_since: i64) -> Result<(Vec<CommentRow>, Option<i64>)> {
        let rows = sqlx::query(&format!(
            "SELECT {COMMENT_COLUMNS} WHERE g.removed_at IS NULL AND (?2 IS NULL OR c.id < ?2) \
             AND COALESCE(c.hidden_at, p.hidden_at, ?3) >= ?3 ORDER BY c.id DESC LIMIT ?4"
        ))
        .bind(None::<Vec<u8>>)
        .bind(before)
        .bind(hidden_since)
        .bind(limit + 1)
        .fetch_all(&self.pool)
        .await?;
        Ok(paged(rows.iter().map(CommentRow::from_row).collect(), limit))
    }

    /// A writer's delete: hides their own visible comment (`user`, or a guest's `hash`; a browser can delete what
    /// it wrote as a guest even while signed in). `false` means nothing was hidden.
    pub async fn hide_comment(&self, id: i64, user: Option<i64>, hash: Option<&[u8]>, now: i64) -> Result<bool> {
        let done = sqlx::query(
            "UPDATE comments SET hidden_at = ?1 WHERE id = ?2 AND hidden_at IS NULL AND (user_id = ?3 OR guest_hash = ?4)",
        )
        .bind(now)
        .bind(id)
        .bind(user)
        .bind(hash)
        .execute(&self.pool)
        .await?;
        Ok(done.rows_affected() == 1)
    }

    /// The teacher's delete: erases the comment at once, with its replies (they cascade).
    pub async fn erase_comment(&self, id: i64) -> Result<bool> {
        let done = sqlx::query("DELETE FROM comments WHERE id = ?").bind(id).execute(&self.pool).await?;
        Ok(done.rows_affected() == 1)
    }

    /// Erases comments hidden before `before`, with their replies.
    pub async fn purge_hidden_comments(&self, before: i64) -> Result<u64> {
        let done = sqlx::query("DELETE FROM comments WHERE hidden_at < ?").bind(before).execute(&self.pool).await?;
        Ok(done.rows_affected())
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

    /// A database made before v0.20 (only the first migration) keeps each student's name as the real name.
    #[tokio::test]
    async fn the_real_name_migration_keeps_todays_names() {
        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("gallery.db");
        let options = SqliteConnectOptions::new().filename(&path).create_if_missing(true);
        let pool = SqlitePool::connect_with(options).await.unwrap();
        let mut first = sqlx::migrate!("./migrations");
        first.migrations = first.migrations[..1].to_vec().into();
        first.run(&pool).await.unwrap();
        for (login, name, role) in [("kim", "김민수", "student"), ("teach", "선생님", "teacher")] {
            sqlx::query("INSERT INTO users (login, display_name, role, password_hash, created_at) VALUES (?, ?, ?, 'h', 1)")
                .bind(login)
                .bind(name)
                .bind(role)
                .execute(&pool)
                .await
                .unwrap();
        }
        pool.close().await;
        let db = Db::open_existing(&path).await.unwrap();
        let kim = db.user_by_login("kim").await.unwrap().unwrap();
        assert_eq!((kim.real_name.as_str(), kim.display_name.as_str()), ("김민수", "김민수"));
        let teacher = db.user_by_login("teach").await.unwrap().unwrap();
        assert_eq!((teacher.real_name.as_str(), teacher.display_name.as_str()), ("", "선생님"));
    }

    /// A database from before v0.31 (three migrations) opens with migration 0004, and its games have no ratings.
    #[tokio::test]
    async fn the_ratings_migration_keeps_todays_games() {
        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("gallery.db");
        let options = SqliteConnectOptions::new().filename(&path).create_if_missing(true);
        let pool = SqlitePool::connect_with(options).await.unwrap();
        let mut first = sqlx::migrate!("./migrations");
        first.migrations = first.migrations[..3].to_vec().into();
        first.run(&pool).await.unwrap();
        sqlx::query("INSERT INTO users (login, display_name, role, password_hash, created_at) VALUES ('kim', '김', 'student', 'h', 1)")
            .execute(&pool)
            .await
            .unwrap();
        sqlx::query(
            "INSERT INTO games (id, owner_id, title, build_name, version, bytes, created_at, updated_at) \
             VALUES ('abc', 1, 'T', 'b', 1, 1, 1, 1)",
        )
        .execute(&pool)
        .await
        .unwrap();
        pool.close().await;
        let db = Db::open_existing(&path).await.unwrap();
        let g = db.live_game("abc").await.unwrap().unwrap();
        assert_eq!((g.ratings, g.stars_total), (0, 0));
    }

    async fn db_with_game() -> (tempfile::TempDir, Db) {
        let (dir, db) = temp_db().await;
        let id = db.create_user("kim", "김", Role::Student, "h", 1).await.unwrap();
        let game = NewGame { id: "abc", owner_id: id, title: "T", build_name: "b", bytes: 1, now: 1 };
        assert!(db.insert_game(&game, 10).await.unwrap());
        (dir, db)
    }

    #[tokio::test]
    async fn an_open_keeps_the_first_time_and_needs_a_live_game() {
        let (_dir, db) = db_with_game().await;
        assert_eq!(db.record_open("abc", b"r", 1000).await.unwrap(), Some(1000));
        assert_eq!(db.record_open("abc", b"r", 1100).await.unwrap(), Some(1000));
        assert_eq!(db.record_open("nope", b"r", 1100).await.unwrap(), None);
        db.tombstone_game("abc", 1200).await.unwrap();
        assert_eq!(db.record_open("abc", b"s", 1300).await.unwrap(), None);
    }

    #[tokio::test]
    async fn a_rating_needs_an_open_old_enough_and_replaces_the_last() {
        let (_dir, db) = db_with_game().await;
        assert!(!db.rate("abc", b"r", 4, 5000, 5000).await.unwrap(), "no open");
        db.record_open("abc", b"r", 1000).await.unwrap();
        // The gate is opened_at <= now - gate: at a 180 s gate, now 1179 is refused and 1180 accepted.
        assert!(!db.rate("abc", b"r", 4, 1179 - 180, 1179).await.unwrap());
        assert!(db.rate("abc", b"r", 4, 1180 - 180, 1180).await.unwrap());
        assert!(db.rate("abc", b"r", 2, 1200 - 180, 1200).await.unwrap());
        let g = db.live_game("abc").await.unwrap().unwrap();
        assert_eq!((g.ratings, g.stars_total), (1, 2));
        assert_eq!(db.rating_state("abc", b"r").await.unwrap(), (Some(1000), Some(2)));
        assert_eq!(db.rating_state("abc", b"x").await.unwrap(), (None, None));
    }

    #[tokio::test]
    async fn pruning_keeps_rated_and_recent_opens() {
        let (_dir, db) = db_with_game().await;
        db.record_open("abc", b"old", 100).await.unwrap();
        db.record_open("abc", b"rated", 100).await.unwrap();
        db.record_open("abc", b"new", 900).await.unwrap();
        assert!(db.rate("abc", b"rated", 5, 100, 400).await.unwrap());
        assert_eq!(db.prune_opens(500).await.unwrap(), 1);
        assert_eq!(db.rating_state("abc", b"old").await.unwrap().0, None);
        assert_eq!(db.rating_state("abc", b"rated").await.unwrap().0, Some(100));
        assert_eq!(db.rating_state("abc", b"new").await.unwrap().0, Some(900));
    }

    #[tokio::test]
    async fn removing_a_game_deletes_its_ratings_and_opens() {
        let (_dir, db) = db_with_game().await;
        db.record_open("abc", b"r", 100).await.unwrap();
        assert!(db.rate("abc", b"r", 5, 100, 400).await.unwrap());
        db.tombstone_game("abc", 500).await.unwrap();
        assert_eq!(db.rating_state("abc", b"r").await.unwrap(), (None, None));
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

    /// kim (id 1) owns "abc"; lee is another student.
    async fn db_for_comments() -> (tempfile::TempDir, Db, i64, i64) {
        let (dir, db) = db_with_game().await;
        let lee = db.create_user("lee", "이", Role::Student, "h", 1).await.unwrap();
        (dir, db, 1, lee)
    }

    #[tokio::test]
    async fn the_gap_is_checked_in_the_insert() {
        let (_dir, db, _kim, lee) = db_for_comments().await;
        assert!(db.insert_comment("abc", Some(lee), None, "hi", 1000, 1000, 30, 20, 600).await.unwrap().is_some());
        assert!(db.insert_comment("abc", Some(lee), None, "hi", 1029, 1029, 30, 20, 600).await.unwrap().is_none(), "29 s later");
        let refusal = db.comment_refusal("abc", Some(lee), None, 1029, 1029, 30, 20, 600).await.unwrap();
        assert_eq!(refusal, Some(Refusal::Gap(1)));
        assert!(db.insert_comment("abc", Some(lee), None, "hi", 1030, 1030, 30, 20, 600).await.unwrap().is_some(), "30 s later");
    }

    #[tokio::test]
    async fn the_guest_cap_counts_the_whole_gallery() {
        let (_dir, db, _kim, lee) = db_for_comments().await;
        for (n, hash) in [&b"a"[..], &b"b"[..], &b"c"[..]].into_iter().enumerate() {
            db.record_open("abc", hash, 0).await.unwrap();
            let written = db.insert_comment("abc", None, Some(hash), "hi", 1000, 1000, 30, 2, 600).await.unwrap();
            assert_eq!(written.is_some(), n < 2, "guest {n}");
        }
        let refusal = db.comment_refusal("abc", None, Some(b"c"), 1000, 1000, 30, 2, 600).await.unwrap();
        assert_eq!(refusal, Some(Refusal::Busy));
        let signed_in = db.insert_comment("abc", Some(lee), None, "hi", 1000, 1000, 30, 2, 600).await.unwrap();
        assert!(signed_in.is_some(), "signed-in writers are never capped");
        let later = db.insert_comment("abc", None, Some(b"c"), "hi", 1601, 1601, 30, 2, 600).await.unwrap();
        assert!(later.is_some(), "the window passed");
    }

    #[tokio::test]
    async fn a_guest_waits_for_the_gate_and_the_owner_writes_no_comment() {
        let (_dir, db, kim, _lee) = db_for_comments().await;
        assert!(db.insert_comment("abc", None, Some(b"g"), "hi", 1000, 1000, 30, 20, 600).await.unwrap().is_none(), "no open");
        let no_open = db.comment_refusal("abc", None, Some(b"g"), 1000, 820, 30, 20, 600).await.unwrap();
        assert_eq!(no_open, Some(Refusal::Gate(180)));
        db.record_open("abc", b"g", 900).await.unwrap();
        assert!(db.insert_comment("abc", None, Some(b"g"), "hi", 1000, 899, 30, 20, 600).await.unwrap().is_none());
        let early = db.comment_refusal("abc", None, Some(b"g"), 1000, 820, 30, 20, 600).await.unwrap();
        assert_eq!(early, Some(Refusal::Gate(80)));
        assert!(db.insert_comment("abc", None, Some(b"g"), "hi", 1000, 900, 30, 20, 600).await.unwrap().is_some());
        assert!(db.insert_comment("abc", Some(kim), None, "mine", 1000, 1000, 30, 20, 600).await.unwrap().is_none());
        let own = db.comment_refusal("abc", Some(kim), None, 1000, 1000, 30, 20, 600).await.unwrap();
        assert_eq!(own, Some(Refusal::Owner));
        let gone = db.comment_refusal("nope", Some(kim), None, 1000, 1000, 30, 20, 600).await.unwrap();
        assert_eq!(gone, Some(Refusal::GameGone));
    }

    #[tokio::test]
    async fn only_the_owner_replies_to_a_visible_top_level_comment() {
        let (_dir, db, kim, lee) = db_for_comments().await;
        let top = db.insert_comment("abc", Some(lee), None, "hi", 1000, 1000, 30, 20, 600).await.unwrap().unwrap();
        let reply = db.insert_reply("abc", top, kim, "thanks", 1001).await.unwrap().unwrap();
        assert!(db.insert_reply("abc", top, kim, "again", 1001).await.unwrap().is_some(), "replies don't wait");
        assert!(db.insert_reply("abc", reply, kim, "x", 1002).await.unwrap().is_none());
        assert_eq!(db.reply_refusal("abc", reply, kim).await.unwrap(), Some(ReplyRefusal::NotTopLevel));
        assert!(db.insert_reply("abc", top, lee, "x", 1002).await.unwrap().is_none());
        assert_eq!(db.reply_refusal("abc", top, lee).await.unwrap(), Some(ReplyRefusal::NotOwner));
        let other = NewGame { id: "def", owner_id: lee, title: "U", build_name: "b", bytes: 1, now: 1 };
        assert!(db.insert_game(&other, 10).await.unwrap());
        let theirs = db.insert_comment("def", Some(kim), None, "hi", 2000, 2000, 30, 20, 600).await.unwrap().unwrap();
        assert!(db.insert_reply("abc", theirs, kim, "x", 2001).await.unwrap().is_none(), "another game's comment");
        assert_eq!(db.reply_refusal("abc", theirs, kim).await.unwrap(), Some(ReplyRefusal::ParentGone));
        assert!(db.hide_comment(top, Some(lee), None, 3000).await.unwrap());
        assert!(db.insert_reply("abc", top, kim, "x", 3001).await.unwrap().is_none(), "a hidden comment");
        assert_eq!(db.reply_refusal("abc", top, kim).await.unwrap(), Some(ReplyRefusal::ParentGone));
    }

    #[tokio::test]
    async fn hiding_purging_and_removal() {
        let (_dir, db, kim, lee) = db_for_comments().await;
        let old = db.insert_comment("abc", Some(lee), None, "old", 1000, 1000, 0, 20, 600).await.unwrap().unwrap();
        let old_reply = db.insert_reply("abc", old, kim, "re", 1001).await.unwrap().unwrap();
        let recent = db.insert_comment("abc", Some(lee), None, "recent", 1002, 1002, 0, 20, 600).await.unwrap().unwrap();
        let kept = db.insert_comment("abc", Some(lee), None, "kept", 1003, 1003, 0, 20, 600).await.unwrap().unwrap();
        assert!(!db.hide_comment(old, Some(kim), None, 2000).await.unwrap(), "only its writer");
        assert!(db.hide_comment(old, Some(lee), None, 2000).await.unwrap());
        assert!(!db.hide_comment(old, Some(lee), None, 2001).await.unwrap(), "already hidden");
        assert!(db.hide_comment(recent, Some(lee), None, 9000).await.unwrap());
        let (page, more) = db.comments_page("abc", None, 20, None).await.unwrap();
        assert_eq!(page.iter().map(|c| c.id).collect::<Vec<_>>(), vec![kept]);
        assert_eq!(more, None);
        assert!(db.replies_of("abc", &[old], None).await.unwrap().is_empty(), "replies of a hidden comment");
        let reply = db.comment_by_id(old_reply, None).await.unwrap().unwrap();
        assert!(!reply.hidden && reply.parent_hidden);
        // Hidden since 5000: `old` (hidden at 2000) and its reply drop out of 최근 댓글; `recent` stays.
        let (listed, _) = db.recent_comments(None, 50, 5000).await.unwrap();
        assert_eq!(listed.iter().map(|c| c.id).collect::<Vec<_>>(), vec![kept, recent]);
        assert_eq!(db.purge_hidden_comments(5000).await.unwrap(), 1);
        assert!(db.comment_by_id(old_reply, None).await.unwrap().is_none(), "the reply cascades");
        assert!(db.comment_by_id(recent, None).await.unwrap().is_some());
        db.tombstone_game("abc", 9500).await.unwrap();
        assert!(db.comment_by_id(kept, None).await.unwrap().is_none());
        assert!(db.comment_by_id(recent, None).await.unwrap().is_none());
    }

    #[tokio::test]
    async fn a_comment_has_exactly_one_writer() {
        let (_dir, db, _kim, lee) = db_for_comments().await;
        for (user, hash) in [(Some(lee), Some(&b"g"[..])), (None, None)] {
            let written = sqlx::query(
                "INSERT INTO comments (game_id, user_id, guest_hash, body, created_at) VALUES ('abc', ?, ?, 'x', 1)",
            )
            .bind(user)
            .bind(hash)
            .execute(&db.pool)
            .await;
            assert!(written.is_err(), "{user:?} {hash:?}");
        }
    }

    #[tokio::test]
    async fn paging_hands_back_a_cursor() {
        let (_dir, db, _kim, lee) = db_for_comments().await;
        let mut ids = Vec::new();
        for n in 0..5 {
            ids.push(db.insert_comment("abc", Some(lee), None, "c", 1000 + n, 1000, 0, 20, 600).await.unwrap().unwrap());
        }
        let (first, more) = db.comments_page("abc", None, 2, None).await.unwrap();
        assert_eq!(first.iter().map(|c| c.id).collect::<Vec<_>>(), vec![ids[4], ids[3]]);
        let (rest, more) = db.comments_page("abc", more, 3, None).await.unwrap();
        assert_eq!(rest.iter().map(|c| c.id).collect::<Vec<_>>(), vec![ids[2], ids[1], ids[0]]);
        assert_eq!(more, None);
    }

    /// A database from before v0.35 (four migrations) opens with migration 0005; its games and ratings stay.
    #[tokio::test]
    async fn the_comments_migration_keeps_todays_games() {
        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("gallery.db");
        let options = SqliteConnectOptions::new().filename(&path).create_if_missing(true);
        let pool = SqlitePool::connect_with(options).await.unwrap();
        let mut first = sqlx::migrate!("./migrations");
        first.migrations = first.migrations[..4].to_vec().into();
        first.run(&pool).await.unwrap();
        sqlx::query("INSERT INTO users (login, display_name, role, password_hash, created_at) VALUES ('kim', '김', 'student', 'h', 1)")
            .execute(&pool)
            .await
            .unwrap();
        sqlx::query(
            "INSERT INTO games (id, owner_id, title, build_name, version, bytes, created_at, updated_at) \
             VALUES ('abc', 1, 'T', 'b', 1, 1, 1, 1)",
        )
        .execute(&pool)
        .await
        .unwrap();
        sqlx::query("INSERT INTO ratings (game_id, rater_hash, stars, rated_at) VALUES ('abc', x'01', 4, 1)")
            .execute(&pool)
            .await
            .unwrap();
        pool.close().await;
        let db = Db::open_existing(&path).await.unwrap();
        let g = db.live_game("abc").await.unwrap().unwrap();
        assert_eq!((g.ratings, g.stars_total), (1, 4));
        assert!(db.comments_page("abc", None, 20, None).await.unwrap().0.is_empty());
    }
}
