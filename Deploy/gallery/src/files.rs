//! Serving a game's build files: the response headers each file needs.

/// The headers a game file is sent with.
#[derive(Debug, PartialEq, Eq)]
pub struct FileHeaders {
    pub content_type: &'static str,
    /// `Some("br")` for Unity's precompressed `.br` files (uploads are Brotli only); the browser decodes them.
    pub content_encoding: Option<&'static str>,
}

pub fn headers_for(path: &str) -> FileHeaders {
    let (inner, content_encoding) = if let Some(inner) = path.strip_suffix(".br") {
        (inner, Some("br"))
    } else {
        (path, None)
    };
    let extension = inner.rsplit_once('.').map(|(_, e)| e.to_ascii_lowercase()).unwrap_or_default();
    let content_type = match extension.as_str() {
        "js" => "application/javascript",
        "wasm" => "application/wasm",
        "json" => "application/json",
        "css" => "text/css",
        "png" => "image/png",
        "ico" => "image/x-icon",
        _ => "application/octet-stream",
    };
    FileHeaders { content_type, content_encoding }
}

/// Every header a game file is sent with, as name/value pairs.
pub fn response_headers(path: &str) -> Vec<(&'static str, &'static str)> {
    let file = headers_for(path);
    let mut headers = vec![
        ("content-type", file.content_type),
        // `private`: Cloudflare never caches a gated file; `no-cache`: the browser keeps it but checks back (the
        // player's ETag answers 304), so a re-upload at the same address is picked up; `no-transform`: Cloudflare
        // passes Unity's files through as they are.
        ("cache-control", "private, no-cache, no-transform"),
        ("x-content-type-options", "nosniff"),
        ("referrer-policy", "no-referrer"),
    ];
    if let Some(encoding) = file.content_encoding {
        headers.push(("content-encoding", encoding));
        headers.push(("vary", "Accept-Encoding"));
    }
    headers
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn types_and_encodings() {
        assert_eq!(headers_for("Build/x.wasm.br"), FileHeaders { content_type: "application/wasm", content_encoding: Some("br") });
        assert_eq!(headers_for("Build/x.wasm"), FileHeaders { content_type: "application/wasm", content_encoding: None });
        assert_eq!(headers_for("Build/x.framework.js.br").content_type, "application/javascript");
        assert_eq!(headers_for("Build/x.loader.js").content_type, "application/javascript");
        assert_eq!(headers_for("Build/x.data.br").content_type, "application/octet-stream");
        assert_eq!(headers_for("Build/x.data").content_type, "application/octet-stream");
    }

    #[test]
    fn encoded_files_vary_and_are_encoded_once() {
        let headers = response_headers("Build/x.data.br");
        assert_eq!(headers.iter().filter(|(n, _)| *n == "content-encoding").count(), 1);
        assert!(headers.contains(&("vary", "Accept-Encoding")));
        assert!(headers.contains(&("cache-control", "private, no-cache, no-transform")));
        let plain = response_headers("Build/x.data");
        assert!(!plain.iter().any(|(n, _)| *n == "content-encoding" || *n == "vary"));
        assert!(plain.contains(&("x-content-type-options", "nosniff")));
    }
}
