//! Passwords: Argon2id hashing with a bounded queue, and readable generated passwords (`word-word-ddd`).

use std::sync::Arc;
use std::sync::atomic::{AtomicU64, Ordering};
use std::time::Duration;

use argon2::password_hash::{PasswordHash, PasswordHasher, PasswordVerifier, SaltString};
use argon2::{Algorithm, Argon2, Params, Version};
use rand::Rng;
use tokio::sync::Semaphore;

use crate::config::Config;

#[derive(Debug, PartialEq, Eq)]
pub enum HashError {
    /// No hashing slot came free in time; nothing was hashed.
    Busy,
    Internal,
}

/// Runs Argon2id off the async threads, at most `argon2_permits` at once. A caller that can't get a slot within
/// `argon2_wait` gets `Busy`, so a flood of sign-ins can't queue real students for minutes.
pub struct Hasher {
    params: Params,
    wait: Duration,
    pub permits: Arc<Semaphore>,
    /// How many hashes or verifies have run (tests read it).
    pub runs: AtomicU64,
}

impl Hasher {
    pub fn new(config: &Config) -> Hasher {
        let params = Params::new(config.argon2.memory_kib, config.argon2.iterations, 1, None)
            .expect("Argon2 parameters in config.rs are valid");
        Hasher {
            params,
            wait: config.argon2_wait,
            permits: Arc::new(Semaphore::new(config.argon2_permits)),
            runs: AtomicU64::new(0),
        }
    }

    pub async fn hash(&self, password: String) -> Result<String, HashError> {
        let params = self.params.clone();
        self.run(move || {
            let salt = SaltString::encode_b64(&rand::random::<[u8; 16]>()).map_err(|_| HashError::Internal)?;
            Argon2::new(Algorithm::Argon2id, Version::V0x13, params)
                .hash_password(password.as_bytes(), &salt)
                .map(|h| h.to_string())
                .map_err(|_| HashError::Internal)
        })
        .await
    }

    /// The stored PHC string carries its own parameters, so hashes made with older settings still verify.
    pub async fn verify(&self, hash: String, password: String) -> Result<bool, HashError> {
        self.run(move || {
            let parsed = PasswordHash::new(&hash).map_err(|_| HashError::Internal)?;
            Ok(Argon2::default().verify_password(password.as_bytes(), &parsed).is_ok())
        })
        .await
    }

    async fn run<T: Send + 'static>(
        &self,
        work: impl FnOnce() -> Result<T, HashError> + Send + 'static,
    ) -> Result<T, HashError> {
        let permit = tokio::time::timeout(self.wait, self.permits.clone().acquire_owned())
            .await
            .map_err(|_| HashError::Busy)?
            .map_err(|_| HashError::Internal)?;
        self.runs.fetch_add(1, Ordering::Relaxed);
        // The permit lives in the blocking task: if the client goes away, the hash still holds its slot until done.
        tokio::task::spawn_blocking(move || {
            let _permit = permit;
            work()
        })
        .await
        .map_err(|_| HashError::Internal)?
    }
}

/// `word-word-ddd`: two short words without `l` (easily confused with `1` and `I`) and three digits 2–9.
/// About 25 bits, enough against online guessing behind the sign-in delay.
pub fn generate_password() -> String {
    let mut rng = rand::rng();
    let a = WORDS[rng.random_range(0..WORDS.len())];
    let b = WORDS[rng.random_range(0..WORDS.len())];
    let digits: String = (0..3).map(|_| char::from(b'0' + rng.random_range(2..=9u8))).collect();
    format!("{a}-{b}-{digits}")
}

/// 3–6 lowercase letters each, no `l`, no duplicates (tested).
pub const WORDS: &[&str] = &[
    "ant", "ape", "bat", "bear", "bee", "bird", "boar", "buck", "bug", "cat", "cow", "crab", "crow", "deer", "dog",
    "dove", "duck", "fish", "fox", "frog", "goat", "goose", "hare", "hawk", "hen", "horse", "kiwi", "mink", "moth",
    "mouse", "newt", "otter", "panda", "pig", "pony", "pug", "puma", "ram", "rat", "raven", "robin", "shark",
    "sheep", "snake", "swan", "tiger", "toad", "trout", "tuna", "wasp", "worm", "yak", "zebra", "bison", "hippo",
    "hyena", "moose", "okapi", "orca", "prawn", "rhino", "squid", "stork", "viper", "wren", "emu", "gnu", "cub",
    "pup", "bean", "beet", "berry", "bread", "bun", "cake", "candy", "carrot", "corn", "cream", "curry", "date", "egg",
    "fig", "grape", "ham", "honey", "jam", "juice", "kimchi", "mango", "mint", "nut", "oat", "onion", "pasta",
    "peach", "pear", "pea", "pepper", "pie", "pizza", "rice", "soup", "sugar", "taco", "tea", "toast", "tofu", "yam",
    "nacho", "donut", "mocha", "cocoa", "crepe", "curd", "dough", "gravy", "herb", "nori", "radish", "ramen", "sushi",
    "syrup", "tart", "wafer", "acorn", "bay", "beach", "brook", "bush", "cave", "coast", "creek", "dawn", "dew",
    "dune", "dusk", "earth", "fern", "fire", "frost", "grass", "grove", "gust", "haze", "ice", "iris", "ivy", "moon",
    "moss", "mist", "ocean", "oak", "pine", "pond", "rain", "reef", "river", "rock", "rose", "sand", "sea", "seed",
    "shore", "sky", "snow", "star", "stone", "storm", "sun", "thorn", "tide", "tree", "wave", "wind", "wood",
    "sprout", "root", "twig", "vine", "bud", "daisy", "poppy", "cedar", "birch", "aspen", "comet", "orbit", "nova",
    "mars", "venus", "atom", "spark", "ember", "torch", "anchor", "arrow", "badge", "bag", "banner", "barn", "basket",
    "bead", "bench", "bike", "boat", "bone", "book", "boot", "box", "brick", "broom", "brush", "bucket", "button",
    "cabin", "canoe", "cap", "card", "cart", "chair", "chess", "coin", "comb", "cone", "crown", "cup", "desk", "dice",
    "door", "drum", "fan", "fork", "gate", "gear", "gift", "harp", "hat", "hook", "horn", "house", "jar", "jet",
    "key", "kite", "knot", "map", "mask", "mirror", "mug", "nest", "net", "note", "oar", "pan", "paper", "pen",
    "piano", "pipe", "pot", "radio", "rope", "ruby", "sack", "saw", "ship", "shoe", "sock", "sofa", "spoon", "stamp",
    "sword", "tent", "tower", "toy", "train", "tray", "truck", "tub", "van", "vase", "wagon", "watch", "whip", "yoyo",
    "zip", "robot", "rocket", "magnet", "ticket", "tuba", "banjo", "guitar", "organ", "crayon", "marker", "eraser",
    "domino", "amber", "azure", "beige", "brave", "brown", "cozy", "cyan", "dark", "eager", "fancy", "fast", "green",
    "happy", "jade", "kind", "magic", "merry", "neat", "noisy", "ochre", "pink", "proud", "quick", "quiet", "rapid",
    "red", "rosy", "sandy", "shiny", "smart", "sunny", "super", "sweet", "swift", "tan", "tiny", "warm", "wise",
    "witty", "young", "zesty", "bright", "crisp", "fresh", "giant", "grand", "hardy", "keen", "mighty", "nifty",
    "peppy", "perky", "spicy", "sporty", "tidy", "upbeat", "vivid", "wavy", "zany",
];

#[cfg(test)]
mod tests {
    use super::*;
    use std::collections::HashSet;

    fn cheap_config() -> Config {
        let mut config = Config::new(std::path::Path::new("/nonexistent"));
        config.argon2.memory_kib = 64;
        config.argon2.iterations = 1;
        config.argon2_wait = Duration::from_millis(100);
        config
    }

    #[tokio::test]
    async fn round_trip_and_wrong_password() {
        let hasher = Hasher::new(&cheap_config());
        let hash = hasher.hash("bear-moon-234".into()).await.unwrap();
        assert!(hash.starts_with("$argon2id$"));
        assert!(hasher.verify(hash.clone(), "bear-moon-234".into()).await.unwrap());
        assert!(!hasher.verify(hash, "bear-moon-235".into()).await.unwrap());
    }

    #[tokio::test]
    async fn a_full_queue_answers_busy_without_hashing() {
        let hasher = Hasher::new(&cheap_config());
        let held = hasher.permits.clone().acquire_many_owned(2).await.unwrap();
        let before = hasher.runs.load(Ordering::Relaxed);
        assert_eq!(hasher.hash("x".into()).await, Err(HashError::Busy));
        assert_eq!(hasher.runs.load(Ordering::Relaxed), before);
        drop(held);
        assert!(hasher.hash("x".into()).await.is_ok());
    }

    #[test]
    fn generated_passwords_are_readable() {
        let words: HashSet<&str> = WORDS.iter().copied().collect();
        let mut seen = HashSet::new();
        for _ in 0..100 {
            let p = generate_password();
            let parts: Vec<&str> = p.split('-').collect();
            assert_eq!(parts.len(), 3, "{p}");
            assert!(words.contains(parts[0]) && words.contains(parts[1]), "{p}");
            assert!(parts[2].len() == 3 && parts[2].bytes().all(|b| (b'2'..=b'9').contains(&b)), "{p}");
            seen.insert(p);
        }
        // P(any repeat in 100) ≈ 0.015% with ≥ 256 words; 1,000 would fail ~1.5% of correct runs.
        assert_eq!(seen.len(), 100);
    }

    #[test]
    fn the_word_list_is_clean() {
        let unique: HashSet<&str> = WORDS.iter().copied().collect();
        assert_eq!(unique.len(), WORDS.len(), "duplicate words");
        assert!(WORDS.len() >= 256, "{} words", WORDS.len());
        for w in WORDS {
            assert!((3..=6).contains(&w.len()) && w.bytes().all(|b| b.is_ascii_lowercase()) && !w.contains('l'), "{w}");
        }
    }
}
