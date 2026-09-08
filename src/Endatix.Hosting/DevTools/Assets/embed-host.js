(function () {
  var MAX_ENTRIES = 200;
  var EVENTS = [
    "endatix:form-loaded",
    "endatix:form-complete",
    "endatix:form-error",
  ];
  var pending = [];
  var els = null;
  var unseen = 0;

  function pad(value, width) {
    return String(value).padStart(width, "0");
  }

  EVENTS.forEach(function (type) {
    window.addEventListener(type, function (event) {
      var item = { time: new Date(), type: type, detail: event.detail };
      if (els) {
        render(item);
      } else {
        pending.push(item);
      }
    });
  });

  function store(key, value) {
    try {
      if (value === undefined) {
        return localStorage.getItem(key);
      }
      localStorage.setItem(key, value);
    } catch (err) {
      /* storage blocked */
    }
    return null;
  }

  function stamp(date) {
    return (
      pad(date.getHours(), 2) +
      ":" +
      pad(date.getMinutes(), 2) +
      ":" +
      pad(date.getSeconds(), 2) +
      "." +
      pad(date.getMilliseconds(), 3)
    );
  }

  function emptyNote() {
    var note = document.createElement("div");
    note.className = "log-empty";
    note.textContent = "Waiting for form-loaded / form-complete / form-error…";
    return note;
  }

  function render(item) {
    var name = item.type.replace("endatix:", "");
    var entry = document.createElement("div");
    entry.className = "entry entry--" + name.replace("form-", "");

    var head = document.createElement("div");
    head.className = "entry-head";
    var label = document.createElement("span");
    label.textContent = name;
    var time = document.createElement("time");
    time.textContent = stamp(item.time);
    head.appendChild(label);
    head.appendChild(time);

    var body = document.createElement("pre");
    try {
      body.textContent = JSON.stringify(item.detail, null, 2);
    } catch (err) {
      body.textContent = String(item.detail);
    }

    entry.appendChild(head);
    entry.appendChild(body);

    var placeholder = els.log.querySelector(".log-empty");
    if (placeholder) {
      placeholder.remove();
    }

    var atBottom =
      els.log.scrollHeight - els.log.scrollTop - els.log.clientHeight < 24;
    els.log.appendChild(entry);
    while (els.log.childElementCount > MAX_ENTRIES) {
      els.log.firstElementChild.remove();
    }
    if (atBottom) {
      els.log.scrollTop = els.log.scrollHeight;
    }

    if (name === "form-error") {
      setDrawer(true);
    }
    if (!els.drawer.classList.contains("open")) {
      unseen += 1;
      setBadge(unseen, name === "form-error");
    }
  }

  function setConfig(open) {
    els.config.hidden = !open;
    els.configToggle.setAttribute("aria-expanded", open ? "true" : "false");
    store("edx.embedhost.config", open ? "1" : "0");
  }

  function setBadge(count, alert) {
    els.badges.forEach(function (badge) {
      badge.textContent = String(count);
      badge.classList.toggle("badge--alert", Boolean(alert));
    });
  }

  function setDrawer(open) {
    els.drawer.classList.toggle("open", open);
    els.toggles.forEach(function (toggle) {
      toggle.setAttribute("aria-expanded", open ? "true" : "false");
    });
    if (open) {
      unseen = 0;
      setBadge(0, false);
      els.log.scrollTop = els.log.scrollHeight;
    }
    store("edx.embedhost.log", open ? "1" : "0");
  }

  function setWidth(value) {
    document.documentElement.style.setProperty(
      "--frame-w",
      value === "full" ? "none" : value + "px",
    );
    els.widths.forEach(function (button) {
      button.setAttribute(
        "aria-pressed",
        button.dataset.width === value ? "true" : "false",
      );
    });
    store("edx.embedhost.width", value);
  }

  function copy(text, button) {
    var label = button.querySelector(".btn-label") || button;
    var original = label.textContent;
    function done(ok) {
      label.textContent = ok ? "Copied" : "Copy failed";
      setTimeout(function () {
        label.textContent = original;
      }, 1400);
    }
    if (!navigator.clipboard) {
      done(false);
      return;
    }
    navigator.clipboard.writeText(text).then(
      function () {
        done(true);
      },
      function () {
        done(false);
      },
    );
  }

  function init() {
    els = {
      config: document.getElementById("config"),
      configToggle: document.getElementById("toggle-config"),
      drawer: document.getElementById("log-drawer"),
      toggles: Array.prototype.slice.call(
        document.querySelectorAll("[data-log-toggle]"),
      ),
      badges: Array.prototype.slice.call(
        document.querySelectorAll("[data-log-count]"),
      ),
      log: document.getElementById("embed-event-log"),
      widths: Array.prototype.slice.call(
        document.querySelectorAll("[data-width]"),
      ),
    };

    if (!els.config || !els.configToggle || !els.drawer || !els.log) {
      return;
    }

    els.configToggle.addEventListener("click", function () {
      setConfig(els.config.hidden);
    });
    els.toggles.forEach(function (toggle) {
      toggle.addEventListener("click", function () {
        setDrawer(!els.drawer.classList.contains("open"));
      });
    });
    els.widths.forEach(function (button) {
      button.addEventListener("click", function () {
        setWidth(button.dataset.width);
      });
    });

    var clear = document.getElementById("log-clear");
    if (clear) {
      clear.addEventListener("click", function () {
        els.log.textContent = "";
        els.log.appendChild(emptyNote());
        unseen = 0;
        setBadge(0, false);
      });
    }

    var reload = document.getElementById("reload-embed");
    if (reload) {
      reload.addEventListener("click", function () {
        window.location.reload();
      });
    }

    var snippet = document.getElementById("copy-snippet");
    if (snippet) {
      snippet.addEventListener("click", function () {
        copy(snippet.dataset.snippet, snippet);
      });
    }

    setConfig(
      els.config.dataset.forceOpen === "true" ||
        store("edx.embedhost.config") === "1",
    );
    setDrawer(store("edx.embedhost.log") === "1");
    setWidth(store("edx.embedhost.width") || "full");

    els.log.appendChild(emptyNote());
    pending.forEach(render);
    pending.length = 0;
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
})();
