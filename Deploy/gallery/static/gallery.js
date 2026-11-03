// The gallery's only script: the upload form, the play view's focus and fullscreen, and the slips' print button.
// Messages come from data attributes rendered from strings.rs. It never listens for messages from games.
(function () {
  var game = document.getElementById("game");
  if (game) {
    game.addEventListener("load", function () { game.focus(); });
    game.focus();
    var fs = document.getElementById("fullscreen");
    if (fs) fs.addEventListener("click", function () {
      if (game.requestFullscreen) game.requestFullscreen();
      game.focus();
    });
  }

  var print = document.getElementById("print");
  if (print) print.addEventListener("click", function () { window.print(); });

  var form = document.getElementById("upload");
  if (!form) return;
  var d = form.dataset;
  var maxBytes = Number(d.maxBytes);
  var input = document.getElementById("zip");
  var bar = document.getElementById("progress");
  var message = document.getElementById("upload-message");
  var button = document.getElementById("upload-button");

  function say(text, loginLink) {
    message.textContent = text;
    if (loginLink) {
      var a = document.createElement("a");
      a.href = "/login";
      a.textContent = " " + d.msgLogin;
      message.appendChild(a);
    }
  }

  form.addEventListener("submit", function (event) {
    event.preventDefault();
    var file = input.files && input.files[0];
    if (!file || !/\.zip$/i.test(file.name)) { say(d.msgPickZip); return; }
    if (file.size > maxBytes) { say(d.msgTooBig); return; }
    var target = form.querySelector("input[name=target]:checked");
    var gameId = "new";
    if (target && target.value === "update") {
      var picked = form.querySelector("input[name=game]:checked");
      if (!picked) { say(d.msgPickGame); return; }
      gameId = picked.value;
    }
    var xhr = new XMLHttpRequest();
    xhr.open("POST", "/upload?game=" + encodeURIComponent(gameId));
    xhr.setRequestHeader("Content-Type", "application/zip");
    xhr.upload.onprogress = function (e) {
      if (e.lengthComputable) bar.value = Math.round((e.loaded / e.total) * 100);
    };
    xhr.onload = function () {
      button.disabled = false;
      bar.hidden = true;
      var answer = null;
      try { answer = JSON.parse(xhr.responseText); } catch (e) { answer = null; }
      if (answer && answer.ok && answer.play) { window.location.href = answer.play; return; }
      say(answer && answer.message ? answer.message : d.msgNetwork, answer && answer.login);
    };
    xhr.onerror = function () {
      button.disabled = false;
      bar.hidden = true;
      say(d.msgNetwork);
    };
    button.disabled = true;
    bar.hidden = false;
    bar.value = 0;
    say(d.msgUploading);
    xhr.send(file);
  });
})();
