// The gallery's only script: the upload form, the play view's focus, fullscreen and copy-link button, and the
// slips' print button.
// Full screen takes the game's box (#frame), not the iframe, so the game stays 16:9 (letterboxed) on any screen.
// Messages come from data attributes rendered from strings.rs. It never listens for messages from games.
(function () {
  var game = document.getElementById("game");
  if (game) {
    game.addEventListener("load", function () { game.focus(); });
    game.focus();
    var box = document.getElementById("frame");
    var fs = document.getElementById("fullscreen");
    if (fs && box) fs.addEventListener("click", function () {
      if (box.requestFullscreen) box.requestFullscreen();
      game.focus();
    });
  }

  // Copy link: the play view's own address. If the clipboard says no (or isn't there), the link is selected instead.
  var copy = document.getElementById("copy-link");
  var shareUrl = document.getElementById("share-url");
  var copyNote = document.getElementById("copy-note");
  if (copy && shareUrl && copyNote) {
    var timer = 0;
    var shown = function (text) {
      copyNote.textContent = text;
      clearTimeout(timer);
      timer = setTimeout(function () { copyNote.textContent = ""; }, 4000);
    };
    var selected = function () {
      shareUrl.focus();
      shareUrl.select();
      shown(copy.dataset.msgSelected);
    };
    copy.addEventListener("click", function () {
      if (!navigator.clipboard) { selected(); return; }
      navigator.clipboard.writeText(shareUrl.value).then(function () { shown(copy.dataset.msgCopied); }, selected);
    });
    shareUrl.addEventListener("focus", function () { shareUrl.select(); });
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
  var pct = document.getElementById("progress-pct");
  var zipName = document.getElementById("zip-name");
  var noFile = zipName.textContent;

  input.addEventListener("change", function () {
    var file = input.files && input.files[0];
    zipName.textContent = file ? file.name : noFile;
  });

  function done() {
    button.disabled = false;
    bar.hidden = true;
    pct.textContent = "";
  }

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
      if (!e.lengthComputable) return;
      bar.value = Math.round((e.loaded / e.total) * 100);
      pct.textContent = bar.value + "%";
    };
    xhr.onload = function () {
      done();
      var answer = null;
      try { answer = JSON.parse(xhr.responseText); } catch (e) { answer = null; }
      if (answer && answer.ok && answer.play) { window.location.href = answer.play; return; }
      say(answer && answer.message ? answer.message : d.msgNetwork, answer && answer.login);
    };
    xhr.onerror = function () {
      done();
      say(d.msgNetwork);
    };
    button.disabled = true;
    bar.hidden = false;
    bar.value = 0;
    say(d.msgUploading);
    xhr.send(file);
  });
})();
