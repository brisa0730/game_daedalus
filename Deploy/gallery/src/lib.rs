//! The daedalus class gallery server (suwon.orcarin.com): accounts, the teacher page, Share zip uploads, the game
//! list and the games host that plays them. Its isolation rules come from the class gallery spike
//! (Documents/Plans/archive/class-gallery-spike.md); the phase that built it is Documents/Phases/phase5-gallery-server.md.

pub mod accounts;
pub mod app;
pub mod backup;
pub mod cli;
pub mod config;
pub mod db;
pub mod files;
pub mod gallery;
pub mod guard;
pub mod pages;
pub mod play;
pub mod play_token;
pub mod session;
pub mod share_zip;
pub mod strings;
pub mod teacher;
pub mod text;
pub mod upload;
