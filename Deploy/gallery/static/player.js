// The games host's player page: loads the build named in the canvas's data attributes with Unity's loader,
// shows the page's loading box until Unity starts, and keeps keyboard focus on the game. Messages come from data
// attributes (strings.rs). The box's bar is indeterminate: Unity's own progress counts unpacked bytes against the
// packed (Brotli) file size, so it reports 0.9 long before a slow download ends and then stays there.
// A published game loads from the Naver bucket (`data-fallback`): any load failure reloads the page once with
// `?local=1`, which names the Mac's copies. Unity's loader reports a framework that fails to load and a data file
// without its Content-Encoding only through `showBanner(…, "error")` and never settles its promise, so the banner
// counts as a failure too.
(function () {
  var canvas = document.getElementById("unity-canvas");
  var d = canvas.dataset;
  var loading = document.getElementById("loading");
  var running = false;
  function started() {
    running = true;
    loading.hidden = true;
    focus();
  }
  function fail() {
    if (running) return;
    if (d.fallback) {
      running = true;
      location.replace(location.pathname + "?local=1");
      return;
    }
    loading.hidden = true;
    var e = document.getElementById("error");
    e.textContent = d.error;
    e.hidden = false;
  }
  function banner(message, type) {
    if (type === "error") { fail(); } else { console.warn(message); }
  }
  function focus() { canvas.focus(); }
  var script = document.createElement("script");
  script.src = d.loader;
  script.onload = function () {
    if (typeof createUnityInstance !== "function") { fail(); return; }
    createUnityInstance(canvas, {
      dataUrl: d.data,
      frameworkUrl: d.framework,
      codeUrl: d.code,
      companyName: "daedalus",
      productName: "game",
      productVersion: "1",
      showBanner: banner
    }).then(started).catch(fail);
  };
  script.onerror = fail;
  document.body.appendChild(script);
  canvas.addEventListener("click", focus);
  window.addEventListener("focus", focus);
  focus();
})();
