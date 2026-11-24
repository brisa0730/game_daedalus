-- When a browser (the rater cookie's SHA-256; nothing else about it) first opened a game's play page (v0.31).
-- Games are tombstoned, never deleted: removing a game deletes its rows here explicitly (db.rs `tombstone_game`).
CREATE TABLE game_opens (
    game_id TEXT NOT NULL REFERENCES games(id),
    rater_hash BLOB NOT NULL,
    opened_at INTEGER NOT NULL,
    PRIMARY KEY (game_id, rater_hash)
);

-- One rating per browser per game; a new one replaces it. No user id, ever (v0.31).
CREATE TABLE ratings (
    game_id TEXT NOT NULL REFERENCES games(id),
    rater_hash BLOB NOT NULL,
    stars INTEGER NOT NULL CHECK (stars BETWEEN 1 AND 5),
    rated_at INTEGER NOT NULL,
    PRIMARY KEY (game_id, rater_hash)
);
