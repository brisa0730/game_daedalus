//! Signed, expiring grants to load one game. The gallery puts an `enter` grant in the game frame's address;
//! the games host trades it for a `gate` grant kept in a cookie. Neither carries who the user is.

use hmac::{Hmac, Mac};
use sha2::Sha256;
use subtle::ConstantTimeEq;

type HmacSha256 = Hmac<Sha256>;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Purpose {
    Enter,
    Gate,
}

pub struct Key([u8; 32]);

impl Key {
    /// A fresh random key: every grant signed before a restart stops working (the gallery phase decides whether to
    /// keep the key across restarts or let players simply reopen the game).
    pub fn generate() -> Self {
        Key(rand::random())
    }

    /// `"<expiry>.<mac hex>"`: valid for `game` and `purpose` until `expires` (Unix seconds).
    pub fn sign(&self, purpose: Purpose, game: &str, expires: u64) -> String {
        format!("{expires}.{}", hex(&self.mac(purpose, game, expires)))
    }

    pub fn verify(&self, purpose: Purpose, game: &str, token: &str, now: u64) -> bool {
        let Some((expires, mac)) = token.split_once('.') else { return false };
        let Ok(expires) = expires.parse::<u64>() else { return false };
        if now >= expires {
            return false;
        }
        let expected = hex(&self.mac(purpose, game, expires));
        expected.as_bytes().ct_eq(mac.as_bytes()).into()
    }

    fn mac(&self, purpose: Purpose, game: &str, expires: u64) -> Vec<u8> {
        let mut mac = HmacSha256::new_from_slice(&self.0).expect("HMAC takes any key length");
        let tag = match purpose {
            Purpose::Enter => "enter",
            Purpose::Gate => "gate",
        };
        mac.update(format!("{tag}\n{game}\n{expires}").as_bytes());
        mac.finalize().into_bytes().to_vec()
    }
}

fn hex(bytes: &[u8]) -> String {
    bytes.iter().map(|b| format!("{b:02x}")).collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn a_grant_opens_its_game_until_it_expires() {
        let key = Key::generate();
        let token = key.sign(Purpose::Enter, "brotli", 100);
        assert!(key.verify(Purpose::Enter, "brotli", &token, 99));
        assert!(!key.verify(Purpose::Enter, "brotli", &token, 100));
    }

    #[test]
    fn a_grant_opens_nothing_else() {
        let key = Key::generate();
        let token = key.sign(Purpose::Enter, "brotli", 100);
        assert!(!key.verify(Purpose::Enter, "gzip", &token, 50));
        assert!(!key.verify(Purpose::Gate, "brotli", &token, 50));
        assert!(!Key::generate().verify(Purpose::Enter, "brotli", &token, 50));
    }

    #[test]
    fn a_tampered_grant_is_refused() {
        let key = Key::generate();
        let token = key.sign(Purpose::Enter, "brotli", 100);
        let (_, mac) = token.split_once('.').unwrap();
        assert!(!key.verify(Purpose::Enter, "brotli", &format!("999999.{mac}"), 50));
        assert!(!key.verify(Purpose::Enter, "brotli", "100", 50));
        assert!(!key.verify(Purpose::Enter, "brotli", "", 50));
        assert!(!key.verify(Purpose::Enter, "brotli", "x.y", 50));
    }
}
