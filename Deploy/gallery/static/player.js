// The games host's player page: loads the build named in the canvas's data attributes with Unity's loader,
// shows the page's loading box until Unity starts, and keeps keyboard focus on the game. Messages come from data
// attributes (strings.rs). The box's bar is indeterminate: Unity's own progress counts unpacked bytes against the
// packed (Brotli) file size, so it reports 0.9 long before a slow download ends and then stays there.
(function () {
  var canvas = document.getElementById("unity-canvas");
  var d = canvas.dataset;
  var loading = document.getElementById("loading");
  function started() {
    loading.hidden = true;
    focus();
  }
  function fail() {
    loading.hidden = true;
    var e = document.getElementById("error");
    e.textContent = d.error;
    e.hidden = false;
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
      productVersion: "1"
    }).then(started).catch(fail);
  };
  script.onerror = fail;
  document.body.appendChild(script);
  canvas.addEventListener("click", focus);
  window.addEventListener("focus", focus);
  focus();
})();
