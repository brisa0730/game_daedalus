"""Loads a page that runs a Unity WebGL build (a class-gallery play page, the games host's player page, or a bare
test page) in Chrome over the DevTools protocol and reports how it went.

  python Tools/gallery_load.py URL [--throttle KBPS] [--size WxH] [--shots DIR] [--limit S] [--settle S] [--headless]

Prints the seconds until Unity starts (a canvas `#unity-canvas` leaves its 300 x 150 default, in any frame), every
Build file's status, host and Content-Encoding, every navigation to a `?local=1` page (the player's fallback) and
console errors (Cloudflare's blocked beacon is skipped). Exit 0 when Unity started within --limit, else 1.
Needs Chrome and the `websockets` package; a fresh profile each run, so nothing is cached.
"""
import argparse, asyncio, base64, json, os, subprocess, sys, tempfile, time, urllib.parse, urllib.request

try:
    import websockets
except ImportError:
    sys.exit("needs the websockets package (pip install websockets)")

CHROME = os.environ.get("CHROME", "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
                        if sys.platform == "darwin" else r"C:\Program Files\Google\Chrome\Application\chrome.exe")
PORT = 9339
PROBE = ("(()=>{const c=document.getElementById('unity-canvas');if(!c)return null;"
         "const l=document.getElementById('loading'),e=document.getElementById('error');"
         "return JSON.stringify({w:c.width,url:location.href,loading:l?!l.hidden:null,error:e&&!e.hidden?e.textContent:null})})()")


async def run(args):
    width, height = (int(v) for v in args.size.split("x"))
    flags = [CHROME, f"--remote-debugging-port={PORT}", f"--user-data-dir={tempfile.mkdtemp(prefix='gallery-load-')}",
             "--no-first-run", "--no-default-browser-check", f"--window-size={width},{height}", "about:blank"]
    if args.headless:
        flags.insert(1, "--headless=new")
    chrome = subprocess.Popen(flags, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        return await drive(args, width, height)
    finally:
        chrome.kill()


async def drive(args, width, height):
    for _ in range(100):
        try:
            pages = json.load(urllib.request.urlopen(f"http://127.0.0.1:{PORT}/json"))
            break
        except Exception:
            await asyncio.sleep(0.1)
    page = next(p for p in pages if p["type"] == "page")
    async with websockets.connect(page["webSocketDebuggerUrl"], max_size=2**28) as ws:
        counter, pending, contexts, urls = [0], {}, set(), {}

        async def send(method, params=None):
            counter[0] += 1
            future = asyncio.get_event_loop().create_future()
            pending[counter[0]] = future
            await ws.send(json.dumps({"id": counter[0], "method": method, "params": params or {}}))
            return await future

        async def reader():
            async for raw in ws:
                m = json.loads(raw)
                if "id" in m and m["id"] in pending:
                    pending.pop(m["id"]).set_result(m.get("result", m.get("error")))
                    continue
                method, p = m.get("method"), m.get("params", {})
                if method == "Runtime.executionContextCreated":
                    contexts.add(p["context"]["id"])
                elif method == "Runtime.executionContextDestroyed":
                    contexts.discard(p.get("executionContextId"))
                elif method == "Network.responseReceived" and "/Build/" in p["response"]["url"]:
                    r = p["response"]
                    h = {k.lower(): v for k, v in r["headers"].items()}
                    url = urllib.parse.urlsplit(r["url"])
                    print(f"  {r['status']} {url.netloc} {url.path.rsplit('/', 1)[-1]} "
                          f"encoding={h.get('content-encoding')}", flush=True)
                elif method == "Network.requestWillBeSent":
                    urls[p["requestId"]] = p["request"]["url"]
                elif method == "Network.loadingFailed" and "/Build/" in urls.get(p["requestId"], ""):
                    name = urls[p["requestId"]].rsplit("/", 1)[-1]
                    print(f"  failed: {name} {p.get('errorText')} {p.get('blockedReason') or ''}", flush=True)
                elif method == "Page.frameNavigated" and "local=1" in p["frame"].get("url", ""):
                    print(f"  fallback reload: {p['frame']['url']}", flush=True)
                elif method == "Log.entryAdded" and p["entry"]["level"] == "error" \
                        and "cloudflareinsights" not in p["entry"]["text"]:
                    print(f"  console error: {p['entry']['text'][:300]}", flush=True)

        asyncio.ensure_future(reader())
        for method in ("Runtime.enable", "Network.enable", "Page.enable", "Log.enable"):
            await send(method)
        await send("Emulation.setDeviceMetricsOverride",
                   {"width": width, "height": height, "deviceScaleFactor": 1, "mobile": False})
        if args.throttle:
            rate = args.throttle * 1024
            await send("Network.emulateNetworkConditions",
                       {"offline": False, "latency": 40, "downloadThroughput": rate, "uploadThroughput": rate})
        if args.shots:
            os.makedirs(args.shots, exist_ok=True)
        start, shot = time.time(), 0
        await send("Page.navigate", {"url": args.url})
        state = None
        while time.time() - start < args.limit:
            await asyncio.sleep(1)
            for ctx in list(contexts):
                r = await send("Runtime.evaluate", {"expression": PROBE, "contextId": ctx})
                value = (r or {}).get("result", {}).get("value")
                if value:
                    state = json.loads(value)
            if args.shots:
                await screenshot(send, os.path.join(args.shots, f"{width}x{height}-{shot:03d}.png"))
                shot += 1
            if state and state.get("error"):
                print(f"PLAYER ERROR after {time.time() - start:.1f} s: {state['error']}")
                return 1
            if state and state["w"] != 300:
                print(f"UNITY STARTED after {time.time() - start:.1f} s ({state['url']})")
                await asyncio.sleep(args.settle)
                if args.shots:
                    await screenshot(send, os.path.join(args.shots, f"{width}x{height}-started.png"))
                return 0
        print(f"NOT STARTED within {args.limit:.0f} s: {state}")
        return 1


async def screenshot(send, path):
    shot = await send("Page.captureScreenshot", {"format": "png"})
    with open(path, "wb") as f:
        f.write(base64.b64decode(shot["data"]))


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("url")
    p.add_argument("--throttle", type=float, help="download and upload KB/s")
    p.add_argument("--size", default="1366x768")
    p.add_argument("--shots", help="folder for a screenshot each second and one after the start")
    p.add_argument("--limit", type=float, default=300)
    p.add_argument("--settle", type=float, default=2, help="seconds after the start before the last screenshot")
    p.add_argument("--headless", action="store_true")
    sys.exit(asyncio.run(run(p.parse_args())))


if __name__ == "__main__":
    main()
