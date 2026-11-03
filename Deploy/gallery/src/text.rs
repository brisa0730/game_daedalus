//! Cleaning text that comes from users: game titles, display names and login IDs.

/// Trimmed, without control or bidi-override characters, whitespace runs collapsed, at most `max_chars`
/// characters; `None` when nothing is left.
pub fn clean_label(raw: &str, max_chars: usize) -> Option<String> {
    let mut out = String::new();
    let mut pending_space = false;
    for c in raw.chars() {
        if c.is_control() && !c.is_whitespace() || is_bidi_control(c) {
            continue;
        }
        if c.is_whitespace() {
            pending_space = !out.is_empty();
            continue;
        }
        if pending_space {
            out.push(' ');
            pending_space = false;
        }
        out.push(c);
    }
    let capped: String = out.chars().take(max_chars).collect();
    let capped = capped.trim_end().to_string();
    (!capped.is_empty()).then_some(capped)
}

fn is_bidi_control(c: char) -> bool {
    matches!(c, '\u{202A}'..='\u{202E}' | '\u{2066}'..='\u{2069}' | '\u{200E}' | '\u{200F}' | '\u{061C}')
}

/// A login ID: lowercase ASCII `a-z 0-9 - _`, 3–20 characters. Upper case is folded; anything else is refused.
pub fn normalize_login(raw: &str) -> Option<String> {
    let id = raw.trim().to_ascii_lowercase();
    let ok = (3..=20).contains(&id.len())
        && id.bytes().all(|b| b.is_ascii_lowercase() || b.is_ascii_digit() || b == b'-' || b == b'_');
    ok.then_some(id)
}

/// Why a new password was refused.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum PasswordProblem {
    Mismatch,
    TooShort,
    TooLong,
    SameAsLogin,
    SameAsCurrent,
}

/// The rule for a password a user chooses: both copies equal, 8–128 characters, not the login ID (ASCII case
/// ignored; IDs are ASCII) and not the current password. Kept exactly as typed. Every input is typed text, so it
/// is checked before any hashing and reveals nothing.
pub fn check_new_password(new: &str, confirm: &str, current: &str, login: &str) -> Result<(), PasswordProblem> {
    let chars = new.chars().count();
    if new != confirm {
        Err(PasswordProblem::Mismatch)
    } else if chars < 8 {
        Err(PasswordProblem::TooShort)
    } else if chars > 128 {
        Err(PasswordProblem::TooLong)
    } else if new.eq_ignore_ascii_case(login) {
        Err(PasswordProblem::SameAsLogin)
    } else if new == current {
        Err(PasswordProblem::SameAsCurrent)
    } else {
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn labels_are_cleaned() {
        assert_eq!(clean_label("  Spike   Hell \n", 40).as_deref(), Some("Spike Hell"));
        assert_eq!(clean_label("a\u{0007}b\u{202E}c\u{2066}d", 40).as_deref(), Some("abcd"));
        assert_eq!(clean_label("가나다라마바사", 3).as_deref(), Some("가나다"));
        assert_eq!(clean_label("ab cd", 3).as_deref(), Some("ab"));
        assert_eq!(clean_label(" \t\u{202E} ", 40), None);
        assert_eq!(clean_label("", 40), None);
    }

    #[test]
    fn new_passwords_follow_the_rule() {
        use PasswordProblem::*;
        let check = |new: &str, confirm: &str| check_new_password(new, confirm, "bear-moon-234", "kim01");
        assert_eq!(check("abcdefgh", "abcdefgi"), Err(Mismatch));
        assert_eq!(check("abcdefg", "abcdefg"), Err(TooShort));
        assert_eq!(check("abcdefgh", "abcdefgh"), Ok(()));
        let longest = "a".repeat(128);
        assert_eq!(check(&longest, &longest), Ok(()));
        let too_long = "a".repeat(129);
        assert_eq!(check(&too_long, &too_long), Err(TooLong));
        assert_eq!(check("가나다라마바사아", "가나다라마바사아"), Ok(()));
        assert_eq!(check("가나다라마바사", "가나다라마바사"), Err(TooShort));
        assert_eq!(check_new_password("KIM01ABC", "KIM01ABC", "x", "kim01abc"), Err(SameAsLogin));
        assert_eq!(check("bear-moon-234", "bear-moon-234"), Err(SameAsCurrent));
        assert_eq!(check("        ", "        "), Ok(()), "kept as typed, spaces count");
    }

    #[test]
    fn logins_are_normalized() {
        assert_eq!(normalize_login(" Kim_01 ").as_deref(), Some("kim_01"));
        assert_eq!(normalize_login("a-b").as_deref(), Some("a-b"));
        assert_eq!(normalize_login("ab"), None);
        assert_eq!(normalize_login("a".repeat(21).as_str()), None);
        assert_eq!(normalize_login("kim min"), None);
        assert_eq!(normalize_login("김민수"), None);
        assert_eq!(normalize_login("x.y.z"), None);
    }
}
