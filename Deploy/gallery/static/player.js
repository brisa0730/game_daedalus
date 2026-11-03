// The games host's player page: loads the build named in the canvas's data attributes with Unity's loader,
// and keeps keyboard focus on the game. Messages come from data attributes (strings.rs).
(function () {
  var canvas = document.getElementById("unity-canvas");
  var d = canvas.dataset;
  function fail() {
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
    }).then(focus).catch(fail);
  };
  script.onerror = fail;
  document.body.appendChild(script);
  canvas.addEventListener("click", focus);
  window.addEventListener("focus", focus);
  focus();
})();
