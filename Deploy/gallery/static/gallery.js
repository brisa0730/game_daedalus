// The gallery's only script: the upload form, the play view's focus, fullscreen and stars, and the slips' print
// button. Full screen takes the game's box (#frame), not the iframe, so the game stays 16:9 (letterboxed) on any
// screen. Stars: the page records this browser's open with a POST, counts down to the rating gate, and saves a
// rating without leaving the page; a click hands the keyboard straight back to the game. Messages come from data
// attributes rendered from strings.rs. It never navigates for a rating and never listens for messages from games.
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
    var rate = document.getElementById("rate");
    if (rate) stars(rate, game);
  }

  function stars(rate, game) {
    var d = rate.dataset;
    var wait = document.getElementById("rate-wait");
    var shown = document.getElementById("rate-stars");
    var note = document.getElementById("rate-note");
    var avg = document.getElementById("rating-avg");
    var buttons = rate.querySelectorAll(".star");
    var deadline = 0;
    var timer = null;

    function light(mine) {
      for (var i = 0; i < buttons.length; i++) {
        var k = Number(buttons[i].dataset.stars);
        buttons[i].classList.toggle("on", k <= mine);
        buttons[i].setAttribute("aria-pressed", k === mine ? "true" : "false");
      }
    }

    function say(text, failed) {
      note.textContent = text;
      note.classList.toggle("error", !!failed);
    }

    // Minutes rounded up while a minute or more remains, then seconds: strings.rs rate_wait.
    function tick() {
      var left = Math.ceil((deadline - Date.now()) / 1000);
      if (left > 0) {
        wait.textContent = left >= 60 ? d.msgWaitMin.replace("{n}", Math.ceil(left / 60)) : d.msgWaitSec.replace("{n}", left);
        wait.hidden = false;
        shown.hidden = true;
        return;
      }
      wait.hidden = true;
      shown.hidden = false;
      clearInterval(timer);
      timer = null;
    }

    function countFrom(seconds) {
      deadline = Date.now() + seconds * 1000;
      if (!timer) timer = setInterval(tick, 1000);
      tick();
    }

    function post(url, body) {
      var options = { method: "POST", credentials: "same-origin" };
      if (body) {
        options.headers = { "Content-Type": "application/x-www-form-urlencoded" };
        options.body = body;
      }
      return fetch(url, options).then(function (r) {
        return r.json().then(function (answer) { return { status: r.status, answer: answer }; });
      });
    }

    // The only thing that records an open; it keeps the first, so sending it again is safe.
    function open() {
      return post(d.open).then(function (r) {
        if (r.status !== 200 || !r.answer.ok) throw new Error("open");
        if (r.answer.mine) light(r.answer.mine);
        countFrom(r.answer.wait);
      });
    }

    countFrom(Number(d.wait));
    open().catch(function () {});

    for (var i = 0; i < buttons.length; i++) buttons[i].addEventListener("click", function (event) {
      game.focus();
      var k = Number(event.currentTarget.dataset.stars);
      post(d.rate, "stars=" + k).then(function (r) {
        if (r.status === 200 && r.answer.ok) {
          light(r.answer.mine);
          say(d.msgSaved, false);
          avg.textContent = r.answer.summary || "";
          avg.hidden = !r.answer.summary;
          return;
        }
        if (r.status === 409) {
          // No open on record (it failed, or the cookie was lost): record one and count from its answer.
          say("", false);
          return open().catch(function () { say(d.msgFailed, true); });
        }
        say(d.msgFailed, true);
      }).catch(function () { say(d.msgFailed, true); });
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
