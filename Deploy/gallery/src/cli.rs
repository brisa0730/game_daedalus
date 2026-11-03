//! The command line: `serve`, `teacher create|reset` (the teacher's account is made only here, never on the web)
//! and `backup`. Its output is for the operator, in English; a password is printed once to stdout, never logged.

use std::path::PathBuf;

use crate::accounts::{Hasher, generate_password};
use crate::config::{Config, unix_now};
use crate::db::{Db, Role};
use crate::text::{clean_label, normalize_login};

pub const USAGE: &str = "usage:
  gallery [serve] --data <dir>
  gallery teacher create <id> <display name> --data <dir>
  gallery teacher reset <id> --data <dir>
  gallery backup --data <dir> --to <dir>";

#[derive(Debug, PartialEq, Eq)]
pub enum Command {
    Serve { data: PathBuf },
    TeacherCreate { data: PathBuf, id: String, name: String },
    TeacherReset { data: PathBuf, id: String },
    Backup { data: PathBuf, to: PathBuf },
}

/// Parses the arguments after the program name.
pub fn parse(args: &[String]) -> Result<Command, String> {
    let mut words = Vec::new();
    let mut data = None;
    let mut to = None;
    let mut it = args.iter();
    while let Some(arg) = it.next() {
        match arg.as_str() {
            "--data" => data = Some(PathBuf::from(it.next().ok_or("--data needs a folder")?)),
            "--to" => to = Some(PathBuf::from(it.next().ok_or("--to needs a folder")?)),
            flag if flag.starts_with("--") => return Err(format!("unknown option {flag}")),
            word => words.push(word.to_string()),
        }
    }
    let data = data.ok_or("--data is required")?;
    let words: Vec<&str> = words.iter().map(String::as_str).collect();
    match (words.as_slice(), to) {
        ([] | ["serve"], None) => Ok(Command::Serve { data }),
        (["teacher", "create", id, name], None) => Ok(Command::TeacherCreate { data, id: id.to_string(), name: name.to_string() }),
        (["teacher", "reset", id], None) => Ok(Command::TeacherReset { data, id: id.to_string() }),
        (["backup"], Some(to)) => Ok(Command::Backup { data, to }),
        _ => Err("unrecognized command".into()),
    }
}

pub async fn teacher_create(config: &Config, db: &Db, id: &str, name: &str) -> Result<String, String> {
    let login = normalize_login(id).ok_or("the id must be 3–20 characters of a-z 0-9 - _")?;
    let name = clean_label(name, config.name_max_chars).ok_or("the display name is empty")?;
    if db.teacher_exists().await.map_err(|e| e.to_string())? {
        return Err("a teacher account already exists; use `gallery teacher reset <id>`".into());
    }
    if db.user_by_login(&login).await.map_err(|e| e.to_string())?.is_some() {
        return Err(format!("the id {login} is taken"));
    }
    let password = generate_password();
    let hash = Hasher::new(config).hash(password.clone()).await.map_err(|e| format!("hashing failed: {e:?}"))?;
    db.create_user(&login, &name, Role::Teacher, &hash, unix_now() as i64).await.map_err(|e| e.to_string())?;
    Ok(password)
}

pub async fn teacher_reset(config: &Config, db: &Db, id: &str) -> Result<String, String> {
    let login = normalize_login(id).ok_or("not a valid id")?;
    let user = db
        .user_by_login(&login)
        .await
        .map_err(|e| e.to_string())?
        .filter(|u| u.role == Role::Teacher)
        .ok_or(format!("no teacher account {login}"))?;
    let password = generate_password();
    let hash = Hasher::new(config).hash(password.clone()).await.map_err(|e| format!("hashing failed: {e:?}"))?;
    db.set_password(user.id, &hash).await.map_err(|e| e.to_string())?;
    Ok(password)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn args(s: &str) -> Vec<String> {
        s.split_whitespace().map(String::from).collect()
    }

    #[test]
    fn commands_parse() {
        let d = PathBuf::from("/d");
        assert_eq!(parse(&args("--data /d")), Ok(Command::Serve { data: d.clone() }));
        assert_eq!(parse(&args("serve --data /d")), Ok(Command::Serve { data: d.clone() }));
        assert_eq!(
            parse(&args("teacher create kim 김선생 --data /d")),
            Ok(Command::TeacherCreate { data: d.clone(), id: "kim".into(), name: "김선생".into() })
        );
        assert_eq!(parse(&args("teacher reset kim --data /d")), Ok(Command::TeacherReset { data: d.clone(), id: "kim".into() }));
        assert_eq!(parse(&args("backup --data /d --to /b")), Ok(Command::Backup { data: d, to: "/b".into() }));
    }

    #[test]
    fn bad_input_is_refused() {
        assert!(parse(&args("serve")).is_err());
        assert!(parse(&args("teacher create kim --data /d")).is_err());
        assert!(parse(&args("backup --data /d")).is_err());
        assert!(parse(&args("frobnicate --data /d")).is_err());
        assert!(parse(&args("--data")).is_err());
        assert!(parse(&args("--port 1 --data /d")).is_err());
    }
}
