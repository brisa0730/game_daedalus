// The gallery's only script: the upload form, the play view's focus, fullscreen, stars and comments, the teacher's
// 최근 댓글, and the slips' print button. Full screen takes the game's box (#frame), not the iframe, so the game stays
// 16:9 (letterboxed) on any screen. Stars: the page records this browser's open with a POST, counts down to the
// rating gate, and saves a rating without leaving the page; a click hands the keyboard straight back to the game.
// Comments: a guest's button counts down on the same clock; posting, replying, deleting and "more" are all fetches,
// and new items are markup the server rendered (never user text put into HTML here), as are the average's stars
// after a vote. The game gets focus at start and after its frame loads only when nothing else has it, so typing a
// comment is never interrupted. Messages come from data attributes rendered from strings.rs. It never navigates
// for a rating or a comment and never listens for messages from games.
(function () {
  // The rating gate's clock, shared by the stars and a guest's comment button.
  var gate = { deadline: 0, listeners: [], timer: null, reopen: null };
  function gateLeft() { return Math.max(0, Math.ceil((gate.deadline - Date.now()) / 1000)); }
  function gateTick() {
    var left = gateLeft();
    for (var i = 0; i < gate.listeners.length; i++) gate.listeners[i](left);
    if (left <= 0 && gate.timer) { clearInterval(gate.timer); gate.timer = null; }
  }
  function gateFrom(seconds) {
    gate.deadline = Date.now() + seconds * 1000;
    if (!gate.timer) gate.timer = setInterval(gateTick, 1000);
    gateTick();
  }

  // Minutes rounded up while a minute or more remains, then seconds: strings.rs rate_wait and comment_wait.
  function waitText(left, minutes, seconds) {
    return left >= 60 ? minutes.replace("{n}", Math.ceil(left / 60)) : seconds.replace("{n}", left);
  }

  function answerOf(r) {
    return r.json().then(function (answer) { return { status: r.status, answer: answer }; });
  }

  function post(url, body) {
    var options = { method: "POST", credentials: "same-origin" };
    if (body) {
      options.headers = { "Content-Type": "application/x-www-form-urlencoded" };
      options.body = body;
    }
    return fetch(url, options).then(answerOf);
  }

  function tell(el, text, failed) {
    if (!el) return;
    el.textContent = text;
    el.classList.toggle("error", !!failed);
  }

  var game = document.getElementById("game");
  if (game) {
    // Nothing else has focus: the page itself, or the game already.
    var idle = function () {
      var a = document.activeElement;
      return !a || a === document.body || a === game;
    };
    game.addEventListener("load", function () { if (idle()) game.focus({ preventScroll: true }); });
    game.focus({ preventScroll: true });
    var box = document.getElementById("frame");
    var fs = document.getElementById("fullscreen");
    if (fs && box) fs.addEventListener("click", function () {
      if (box.requestFullscreen) box.requestFullscreen();
      game.focus({ preventScroll: true });
    });
    var rate = document.getElementById("rate");
    if (rate) stars(rate, game);
  }
  var section = document.getElementById("comments");
  if (section) comments(section);
  var recent = document.getElementById("recent-comments");
  if (recent) {
    deletions(recent, recent.dataset, null);
    more(document.getElementById("recent-more"), recent, recent.dataset);
  }

  function stars(rate, game) {
    var d = rate.dataset;
    var wait = document.getElementById("rate-wait");
    var shown = document.getElementById("rate-stars");
    var note = document.getElementById("rate-note");
    var avg = document.getElementById("rating-avg");
    var buttons = rate.querySelectorAll(".star");

    function light(mine) {
      for (var i = 0; i < buttons.length; i++) {
        var k = Number(buttons[i].dataset.stars);
        buttons[i].classList.toggle("on", k <= mine);
        buttons[i].setAttribute("aria-pressed", k === mine ? "true" : "false");
      }
    }

    gate.listeners.push(function (left) {
      if (left > 0) wait.textContent = waitText(left, d.msgWaitMin, d.msgWaitSec);
      wait.hidden = left <= 0;
      shown.hidden = left > 0;
    });

    // The only thing that records an open; it keeps the first, so sending it again is safe.
    function open() {
      return post(d.open).then(function (r) {
        if (r.status !== 200 || !r.answer.ok) throw new Error("open");
        if (r.answer.mine) light(r.answer.mine);
        gateFrom(r.answer.wait);
      });
    }
    gate.reopen = open;

    gateFrom(Number(d.wait));
    open().catch(function () {});

    for (var i = 0; i < buttons.length; i++) buttons[i].addEventListener("click", function (event) {
      game.focus({ preventScroll: true });
      var k = Number(event.currentTarget.dataset.stars);
      post(d.rate, "stars=" + k).then(function (r) {
        if (r.status === 200 && r.answer.ok) {
          light(r.answer.mine);
          tell(note, d.msgSaved, false);
          avg.innerHTML = r.answer.summary || "";
          avg.hidden = !r.answer.summary;
          return;
        }
        if (r.status === 409) {
          // No open on record (it failed, or the cookie was lost): record one and count from its answer.
          tell(note, "", false);
          return open().catch(function () { tell(note, d.msgFailed, true); });
        }
        tell(note, d.msgFailed, true);
      }).catch(function () { tell(note, d.msgFailed, true); });
    });
  }

  // The characters typed, as the server counts them (NFC). A guide only: the server decides after cleaning.
  function counter(textarea, count, max) {
    function update() {
      var n = Array.from(textarea.value.normalize("NFC")).length;
      count.textContent = n + "/" + max;
      count.classList.toggle("over", n > max);
    }
    textarea.addEventListener("input", update);
    update();
  }

  function comments(section) {
    var d = section.dataset;
    var max = Number(d.max);
    var list = document.getElementById("comment-list");
    var empty = document.getElementById("comments-empty");
    var msg = document.getElementById("comment-msg");
    deletions(list, d, function () { if (!list.children.length) empty.hidden = false; }, msg);
    more(document.getElementById("comments-more"), list, d);

    var text = document.getElementById("comment-text");
    if (text) {
      var send = document.getElementById("comment-send");
      var guest = d.guest === "1";
      var busy = false;
      var cooldown = 0;
      var gapTimer = null;
      var label = function () {
        var left = Math.max(guest ? gateLeft() : 0, Math.max(0, Math.ceil((cooldown - Date.now()) / 1000)));
        send.disabled = busy || left > 0;
        send.textContent = left > 0 ? waitText(left, d.msgWaitMin, d.msgWaitSec) : d.labelSend;
      };
      var coolFor = function (seconds) {
        cooldown = Date.now() + seconds * 1000;
        if (!gapTimer) gapTimer = setInterval(function () {
          label();
          if (Date.now() >= cooldown) { clearInterval(gapTimer); gapTimer = null; }
        }, 1000);
        label();
      };
      counter(text, document.getElementById("comment-count"), max);
      gate.listeners.push(label);
      if (guest && !gate.deadline) gateFrom(Number(d.wait));
      label();
      // A button, no form: Enter makes a new line (and confirms Korean syllables) and never sends.
      send.addEventListener("click", function () {
        var value = text.value.normalize("NFC");
        if (!value.trim()) { tell(msg, d.msgEmpty, true); return; }
        busy = true;
        label();
        post(d.post, new URLSearchParams({ as: d.as, text: value }).toString()).then(function (r) {
          busy = false;
          // The disabled button dropped focus to the page; put it back on the box (never the game).
          text.focus({ preventScroll: true });
          var a = r.answer || {};
          if (r.status === 200 && a.ok) {
            list.insertAdjacentHTML("afterbegin", a.html);
            empty.hidden = true;
            text.value = "";
            text.dispatchEvent(new Event("input"));
            tell(msg, d.msgPosted, false);
            coolFor(a.wait || 0);
            return;
          }
          tell(msg, a.message || d.msgFailed, true);
          if (r.status === 409 && a.wait !== undefined) {
            // The gate: count from this answer, and record an open in case there was none.
            gateFrom(a.wait);
            if (gate.reopen) gate.reopen().catch(function () {});
          } else if (r.status === 429 && a.wait) {
            // The gap counts across the gallery, so it can come from a comment on another game.
            coolFor(a.wait);
          }
          label();
        }).catch(function () {
          busy = false;
          label();
          tell(msg, d.msgFailed, true);
        });
      });
    }

    if (document.getElementById("reply-form")) section.addEventListener("click", function (event) {
      var button = event.target.closest(".comment-reply");
      if (button && section.contains(button)) reply(button);
    });

    // The owner's reply box under a comment, at most one open at a time.
    function reply(button) {
      var li = button.closest("li.comment");
      var open = section.querySelector(".reply-form");
      if (open) open.remove();
      var form = document.getElementById("reply-form").content.firstElementChild.cloneNode(true);
      var replies = li.querySelector("ul.replies");
      replies.before(form);
      var area = form.querySelector(".reply-text");
      var note = form.querySelector(".reply-msg");
      var sendReply = form.querySelector(".reply-send");
      counter(area, form.querySelector(".reply-count"), max);
      area.focus({ preventScroll: true });
      form.querySelector(".reply-cancel").addEventListener("click", function () { form.remove(); });
      sendReply.addEventListener("click", function () {
        var value = area.value.normalize("NFC");
        if (!value.trim()) { tell(note, d.msgEmpty, true); return; }
        sendReply.disabled = true;
        var body = new URLSearchParams({ as: d.as, text: value, parent: button.dataset.parent }).toString();
        post(d.post, body).then(function (r) {
          sendReply.disabled = false;
          if (r.status === 200 && r.answer.ok) {
            replies.insertAdjacentHTML("beforeend", r.answer.html);
            form.remove();
            button.focus({ preventScroll: true });
            return;
          }
          tell(note, (r.answer && r.answer.message) || d.msgFailed, true);
        }).catch(function () {
          sendReply.disabled = false;
          tell(note, d.msgFailed, true);
        });
      });
    }
  }

  // 삭제 on any item in `container`: an inline confirm built with textContent, then the POST.
  function deletions(container, d, after, note) {
    container.addEventListener("click", function (event) {
      var del = event.target.closest(".comment-del");
      if (!del || !container.contains(del)) return;
      var li = del.closest("li.comment");
      var actions = del.parentElement;
      if (actions.nextElementSibling && actions.nextElementSibling.classList.contains("comment-confirm")) return;
      var confirm = document.createElement("div");
      confirm.className = "comment-confirm";
      var question = document.createElement("p");
      question.textContent = d.msgConfirm;
      var yes = document.createElement("button");
      yes.type = "button";
      yes.className = "danger";
      yes.textContent = d.labelDelete;
      var no = document.createElement("button");
      no.type = "button";
      no.textContent = d.labelCancel;
      confirm.append(question, yes, no);
      actions.hidden = true;
      actions.after(confirm);
      no.addEventListener("click", function () {
        confirm.remove();
        actions.hidden = false;
      });
      yes.addEventListener("click", function () {
        yes.disabled = true;
        post(del.dataset.delete, "as=" + encodeURIComponent(d.as)).then(function (r) {
          if (r.status === 200 && r.answer.ok) {
            li.remove();
            if (after) after();
            return;
          }
          if (r.status === 410) {
            // Already deleted (in another tab, or its parent was): say so where the list's messages go, and drop it.
            if (note) tell(note, d.msgGone, true);
            li.remove();
            if (after) after();
            return;
          }
          yes.disabled = false;
          question.textContent = (r.answer && r.answer.message) || d.msgFailed;
        }).catch(function () {
          yes.disabled = false;
          question.textContent = d.msgFailed;
        });
      });
    });
  }

  // "More": the next page as server markup in JSON. A failed fetch, or a page that isn't JSON (a teacher whose
  // sign-in ended is sent to /login), keeps the button and says so.
  function more(button, list, d) {
    if (!button) return;
    button.addEventListener("click", function () {
      button.disabled = true;
      fetch(button.dataset.more, { credentials: "same-origin" }).then(answerOf).then(function (r) {
        if (r.status !== 200 || !r.answer.ok) throw new Error("more");
        list.insertAdjacentHTML("beforeend", r.answer.html);
        if (r.answer.more) {
          button.dataset.more = r.answer.more;
          button.disabled = false;
        } else {
          button.remove();
        }
      }).catch(function () {
        button.disabled = false;
        button.textContent = d.msgFailed;
      });
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
