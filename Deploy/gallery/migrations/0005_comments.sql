-- Comments under a game (v0.35). Exactly one writer: a user (nickname joined at render time) or a guest browser
-- (the rater cookie's SHA-256, never shown to anyone). A signed-in comment never stores the hash.
-- Games are tombstoned, never deleted: removing a game deletes its comments explicitly (db.rs `tombstone_game`).
-- A writer's delete only sets `hidden_at`; the start-up purge erases hidden rows after 30 days.
CREATE TABLE comments (
    id INTEGER PRIMARY KEY,
    game_id TEXT NOT NULL REFERENCES games(id),
    parent_id INTEGER REFERENCES comments(id) ON DELETE CASCADE,
    user_id INTEGER REFERENCES users(id) ON DELETE CASCADE,
    guest_hash BLOB,
    body TEXT NOT NULL CHECK (length(body) BETWEEN 1 AND 300),
    created_at INTEGER NOT NULL,
    hidden_at INTEGER,
    CHECK ((user_id IS NULL) <> (guest_hash IS NULL))
);
CREATE INDEX comments_game ON comments(game_id, parent_id, id);
CREATE INDEX comments_parent ON comments(parent_id);
CREATE INDEX comments_user ON comments(user_id);
CREATE INDEX comments_guest_recent ON comments(created_at) WHERE guest_hash IS NOT NULL;
