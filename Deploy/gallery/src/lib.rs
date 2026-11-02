//! The class gallery server's building blocks, settled by the class gallery spike
//! (Documents/Plans/archive/class-gallery-spike.md): sending a game's files, the grant that lets only signed-in users
//! load a game, and the guard that keeps games from acting through the gallery. The server itself arrives with the
//! `class-gallery` phase.

pub mod files;
pub mod guard;
pub mod play_token;
