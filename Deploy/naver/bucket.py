"""The class gallery's Naver Object Storage bucket: one-time setup and the Step 1 test (Documents/Plans/gallery-naver-storage.md).

Reads the key from the gallery's storage.json and never prints the secret. Stdlib only (SigV4 by hand), so it runs
with the Mac's python3 and nothing installed.

  python3 Deploy/naver/bucket.py put-test BUILD_DIR   uploads a Share build's four files + a test page under test/
  python3 Deploy/naver/bucket.py headers KEY [--range] GETs a key over its public URL and prints the status and headers
  python3 Deploy/naver/bucket.py setup --origin O ...  sets the bucket's CORS: GET/HEAD from each origin
  python3 Deploy/naver/bucket.py remove-test          deletes everything under test/
"""
import argparse, base64, datetime, hashlib, hmac, json, os, sys, time, urllib.error, urllib.parse, urllib.request

DATA = os.path.expanduser("~/Library/Application Support/daedalus-gallery")
IMMUTABLE = "public, max-age=31536000, immutable"


def load_config(data):
    with open(os.path.join(data, "storage.json")) as f:
        return json.load(f)


class Bucket:
    def __init__(self, config):
        self.region = config["region"]
        self.bucket = config["bucket"]
        self.access_key = config["access_key"]
        self.secret_key = config["secret_key"]
        endpoint = urllib.parse.urlsplit(config["endpoint"])
        self.host = f"{self.bucket}.{endpoint.hostname}"  # virtual-host style, as the gallery's public_origin()
        self.origin = f"https://{self.host}"

    def request(self, method, key, body=b"", headers=None, query=""):
        headers = dict(headers or {})
        now = datetime.datetime.now(datetime.timezone.utc)
        amz_date, day = now.strftime("%Y%m%dT%H%M%SZ"), now.strftime("%Y%m%d")
        payload = hashlib.sha256(body).hexdigest()
        headers.update({"host": self.host, "x-amz-date": amz_date, "x-amz-content-sha256": payload})
        path = "/" + urllib.parse.quote(key, safe="/~")
        lower = {k.lower(): str(v).strip() for k, v in headers.items()}
        signed = ";".join(sorted(lower))
        canonical = "\n".join([method, path, query, "".join(f"{k}:{lower[k]}\n" for k in sorted(lower)), signed, payload])
        scope = f"{day}/{self.region}/s3/aws4_request"
        to_sign = "\n".join(["AWS4-HMAC-SHA256", amz_date, scope, hashlib.sha256(canonical.encode()).hexdigest()])
        k = ("AWS4" + self.secret_key).encode()
        for part in (day, self.region, "s3", "aws4_request"):
            k = hmac.new(k, part.encode(), hashlib.sha256).digest()
        signature = hmac.new(k, to_sign.encode(), hashlib.sha256).hexdigest()
        headers["Authorization"] = (f"AWS4-HMAC-SHA256 Credential={self.access_key}/{scope}, "
                                    f"SignedHeaders={signed}, Signature={signature}")
        url = f"{self.origin}{path}" + (f"?{query}" if query else "")
        req = urllib.request.Request(url, data=body if method in ("PUT", "POST") else None, method=method,
                                     headers={k: v for k, v in headers.items() if k != "host"})
        try:
            with urllib.request.urlopen(req, timeout=60) as r:
                return r.status, r.read()
        except urllib.error.HTTPError as e:
            return e.code, e.read()

    def put(self, key, body, content_type, encoding=None, cache=IMMUTABLE):
        headers = {"content-type": content_type, "cache-control": cache, "x-amz-acl": "public-read"}
        if encoding:
            headers["content-encoding"] = encoding
        return self.request("PUT", key, body, headers)

    def list_keys(self, prefix):
        keys, token = [], None
        while True:
            query = "list-type=2&prefix=" + urllib.parse.quote(prefix, safe="")
            if token:
                query += "&continuation-token=" + urllib.parse.quote(token, safe="")
            query = "&".join(sorted(query.split("&")))
            status, body = self.request("GET", "", query=query)
            if status != 200:
                sys.exit(f"list failed: HTTP {status}")
            text = body.decode()
            keys += [part.split("</Key>")[0] for part in text.split("<Key>")[1:]]
            if "<NextContinuationToken>" not in text:
                return keys
            token = text.split("<NextContinuationToken>")[1].split("</NextContinuationToken>")[0]


def content_type(name):
    base = name[:-3] if name.endswith(".br") else name
    return {"js": "application/javascript", "wasm": "application/wasm", "html": "text/html; charset=utf-8"}.get(
        base.rsplit(".", 1)[-1], "application/octet-stream")


TEST_PAGE = """<!doctype html><html lang="ko"><head><meta charset="utf-8"><title>Naver test</title>
<style>html,body{margin:0;height:100%;background:#0b0f1c}#unity-canvas{width:100%;height:100%;display:block}
#t{position:fixed;top:8px;left:8px;color:#c9d0e8;font:16px sans-serif}</style></head>
<body><canvas id="unity-canvas"></canvas><p id="t">불러오는 중…</p><script src="Build/NAME.loader.js"></script><script>
var t0=performance.now();createUnityInstance(document.getElementById("unity-canvas"),{dataUrl:"Build/NAME.data.br",
frameworkUrl:"Build/NAME.framework.js.br",codeUrl:"Build/NAME.wasm.br",showBanner:function(m,k){if(k=="error")
document.getElementById("t").textContent="실패: "+m;}}).then(function(){document.getElementById("t")
.textContent="시작: "+((performance.now()-t0)/1000).toFixed(1)+"초";}).catch(function(e){document.getElementById("t")
.textContent="실패: "+e;});</script></body></html>
"""


def put_test(b, build_dir):
    files = sorted(os.listdir(build_dir))
    loaders = [f for f in files if f.endswith(".loader.js")]
    if len(loaders) != 1:
        sys.exit(f"expected one *.loader.js in {build_dir}")
    name = loaders[0][: -len(".loader.js")]
    for f in (f"{name}.loader.js", f"{name}.data.br", f"{name}.framework.js.br", f"{name}.wasm.br"):
        body = open(os.path.join(build_dir, f), "rb").read()
        start = time.time()
        status, _ = b.put(f"test/Build/{f}", body, content_type(f), "br" if f.endswith(".br") else None)
        print(f"put {f} {len(body)} B: HTTP {status} in {time.time() - start:.2f} s")
        if status != 200:
            sys.exit(1)
    status, _ = b.put("test/index.html", TEST_PAGE.replace("NAME", name).encode(), content_type("index.html"),
                      cache="no-cache")
    print(f"put index.html: HTTP {status}")
    print(f"test page: {b.origin}/test/index.html")


def headers(b, key, with_range):
    req = urllib.request.Request(f"{b.origin}/{key}", headers={"Accept-Encoding": "br",
                                 **({"Range": "bytes=0-99"} if with_range else {}),
                                 "Origin": "https://suwon-play.orcarin.com"})
    try:
        r = urllib.request.urlopen(req, timeout=60)
    except urllib.error.HTTPError as e:
        r = e
    print("HTTP", r.status)
    for k, v in r.headers.items():
        print(f"  {k}: {v}")


def setup(b, origins):
    rules = "".join(f"<AllowedOrigin>{o}</AllowedOrigin>" for o in origins)
    body = ("<CORSConfiguration><CORSRule>" + rules + "<AllowedMethod>GET</AllowedMethod><AllowedMethod>HEAD"
            "</AllowedMethod><AllowedHeader>*</AllowedHeader><MaxAgeSeconds>3600</MaxAgeSeconds></CORSRule>"
            "</CORSConfiguration>").encode()
    md5 = base64.b64encode(hashlib.md5(body).digest()).decode()
    status, resp = b.request("PUT", "", body, {"content-md5": md5, "content-type": "application/xml"}, query="cors=")
    print(f"CORS for {', '.join(origins)}: HTTP {status}", "" if status == 200 else resp.decode()[:300])


def remove_test(b):
    keys = b.list_keys("test/")
    for k in keys:
        status, _ = b.request("DELETE", k)
        print(f"delete {k}: HTTP {status}")
    print(f"{len(keys)} keys under test/")


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--data", default=DATA)
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("put-test").add_argument("build_dir")
    h = sub.add_parser("headers")
    h.add_argument("key")
    h.add_argument("--range", action="store_true")
    sub.add_parser("setup").add_argument("--origin", action="append", required=True)
    sub.add_parser("remove-test")
    a = p.parse_args()
    b = Bucket(load_config(a.data))
    if a.cmd == "put-test":
        put_test(b, a.build_dir)
    elif a.cmd == "headers":
        headers(b, a.key, a.range)
    elif a.cmd == "setup":
        setup(b, a.origin)
    else:
        remove_test(b)


if __name__ == "__main__":
    main()
