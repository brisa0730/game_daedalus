//! The gallery binary: a thin entry over the library. See `cli::USAGE`.

use std::os::unix::fs::PermissionsExt;
use std::path::Path;
use std::process::ExitCode;

use gallery::app::{AppState, router};
use gallery::cli::{self, Command};
use gallery::config::{BIND, Config, unix_now};
use gallery::db::Db;
use gallery::{backup, upload};

#[tokio::main]
async fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().skip(1).collect();
    let command = match cli::parse(&args) {
        Ok(command) => command,
        Err(error) => {
            eprintln!("{error}\n{}", cli::USAGE);
            return ExitCode::from(2);
        }
    };
    let result = match command {
        Command::Serve { data } => serve(&data).await,
        Command::TeacherCreate { data, id, name } => with_db(&data, |config, db| async move {
            let password = cli::teacher_create(&config, &db, &id, &name).await?;
            println!("teacher account created: {id}\npassword (shown once): {password}");
            Ok(())
        })
        .await,
        Command::TeacherReset { data, id } => with_db(&data, |config, db| async move {
            let password = cli::teacher_reset(&config, &db, &id).await?;
            println!("new password for {id} (shown once): {password}");
            Ok(())
        })
        .await,
        Command::Backup { data, to } => match backup::run(&data, &to, unix_now()).await {
            Ok(report) => {
                eprintln!(
                    "backup done: {} ({} folders copied, {} pruned)",
                    report.db_copy.display(),
                    report.copied,
                    report.pruned
                );
                Ok(())
            }
            Err(error) => Err(format!("backup failed: {error}")),
        },
    };
    match result {
        Ok(()) => ExitCode::SUCCESS,
        Err(error) => {
            eprintln!("{error}");
            ExitCode::FAILURE
        }
    }
}

async fn with_db<F, Fut>(data: &Path, work: F) -> Result<(), String>
where
    F: FnOnce(Config, Db) -> Fut,
    Fut: std::future::Future<Output = Result<(), String>>,
{
    prepare_folders(data)?;
    let config = Config::new(data);
    let db = Db::open(&config.db_path()).await.map_err(|e| format!("open database: {e}"))?;
    let result = work(config, db.clone()).await;
    db.close().await;
    result
}

/// The data, games and staging folders, private to the operator.
fn prepare_folders(data: &Path) -> Result<(), String> {
    for dir in [data.to_path_buf(), data.join("games"), data.join("staging")] {
        std::fs::create_dir_all(&dir).map_err(|e| format!("create {}: {e}", dir.display()))?;
        std::fs::set_permissions(&dir, std::fs::Permissions::from_mode(0o700))
            .map_err(|e| format!("chmod {}: {e}", dir.display()))?;
    }
    Ok(())
}

async fn serve(data: &Path) -> Result<(), String> {
    tracing_subscriber::fmt().with_writer(std::io::stderr).with_ansi(false).init();
    prepare_folders(data)?;
    let config = Config::new(data);
    let db = Db::open(&config.db_path()).await.map_err(|e| format!("open database: {e}"))?;
    let now = unix_now() as i64;
    db.prune_sessions(now - config.session_idle_secs as i64, now - config.session_max_secs as i64)
        .await
        .map_err(|e| format!("prune sessions: {e}"))?;
    let (live, removed) = db.game_folders().await.map_err(|e| format!("read games: {e}"))?;
    // A sweep problem must not keep the gallery down (launchd would restart it forever): log it and serve.
    if let Err(error) = upload::sweep(&config, &live, &removed) {
        tracing::warn!(%error, "startup sweep of game folders failed");
    }
    let state = AppState::new(config, db.clone());
    let listener = tokio::net::TcpListener::bind(BIND).await.map_err(|e| format!("bind {BIND}: {e}"))?;
    tracing::info!(address = %BIND, data = %data.display(), "gallery listening");
    let served = axum::serve(listener, router(state)).with_graceful_shutdown(shutdown()).await;
    db.close().await;
    tracing::info!("gallery stopped");
    served.map_err(|e| format!("serve: {e}"))
}

/// SIGTERM (launchd) or Ctrl-C: stop taking requests and close the database cleanly.
async fn shutdown() {
    let mut term = tokio::signal::unix::signal(tokio::signal::unix::SignalKind::terminate()).expect("SIGTERM handler");
    tokio::select! {
        _ = term.recv() => {}
        _ = tokio::signal::ctrl_c() => {}
    }
}
