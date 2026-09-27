"use strict";

const cfg = window.SURAKSHA_CONFIG;
const db = supabase.createClient(cfg.supabaseUrl, cfg.supabaseAnonKey);

const $ = (id) => document.getElementById(id);
const state = { workers: [], currentWorker: null, attempts: [] };

// ------------------------------------------------------------ helpers

function el(tag, attrs = {}, ...children) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (k === "class") node.className = v;
    else if (k.startsWith("on")) node.addEventListener(k.slice(2), v);
    else node.setAttribute(k, v);
  }
  for (const child of children) {
    if (child == null) continue;
    node.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return node;
}

function fmtDate(iso) {
  if (!iso) return "—";
  return new Date(iso).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
}

function fmtDuration(seconds) {
  if (seconds == null) return "—";
  const s = Math.round(seconds);
  return s < 60 ? `${s}s` : `${Math.floor(s / 60)}m ${String(s % 60).padStart(2, "0")}s`;
}

function pct(part, whole) {
  return whole ? `${Math.round((part / whole) * 100)}%` : "—";
}

function humanize(key) {
  return key.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/_/g, " ").replace(/^./, (c) => c.toUpperCase());
}

function toast(message) {
  const t = $("toast");
  t.textContent = message;
  t.hidden = false;
  clearTimeout(toast.timer);
  toast.timer = setTimeout(() => (t.hidden = true), 2600);
}

function tile(label, value) {
  return el("div", { class: "card tile" }, el("div", { class: "label" }, label), el("div", { class: "value" }, value));
}

function showView(name) {
  for (const v of document.querySelectorAll(".view")) v.hidden = v.id !== `view-${name}`;
}

async function rpc(fn, args) {
  const { data, error } = await db.rpc(fn, args);
  if (error) throw new Error(error.message);
  return data;
}

// ------------------------------------------------------------ auth

async function requireAdmin() {
  const { data } = await db.auth.getSession();
  const session = data.session;
  $("session-box").hidden = !session;
  if (!session) return false;
  $("admin-email").textContent = session.user.email;
  return true;
}

$("login-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  $("login-error").textContent = "";
  const { error } = await db.auth.signInWithPassword({
    email: $("login-email").value.trim(),
    password: $("login-password").value,
  });
  if (error) {
    $("login-error").textContent = error.message;
    return;
  }
  const isAdmin = await rpc("is_admin").catch(() => false);
  if (!isAdmin) {
    await db.auth.signOut();
    $("login-error").textContent = "This account is not a SurakshaAR admin.";
    return;
  }
  location.hash = "#/workers";
  route();
});

$("signout-btn").addEventListener("click", async () => {
  await db.auth.signOut();
  location.hash = "";
  route();
});

// ------------------------------------------------------------ workers list

async function loadWorkers() {
  state.workers = (await rpc("admin_list_workers")) || [];
}

function renderWorkers() {
  const q = $("worker-search").value.trim().toLowerCase();
  const rows = state.workers.filter(
    (w) => !q || w.display_name.toLowerCase().includes(q) || w.worker_code.toLowerCase().includes(q)
  );

  const totalAttempts = state.workers.reduce((n, w) => n + Number(w.attempts), 0);
  const totalPassed = state.workers.reduce((n, w) => n + Number(w.passed), 0);
  const weekAgo = Date.now() - 7 * 864e5;
  const activeWeek = state.workers.filter((w) => w.last_activity && new Date(w.last_activity) > weekAgo).length;
  $("overview-tiles").replaceChildren(
    tile("Workers", state.workers.length),
    tile("Active this week", activeWeek),
    tile("Attempts synced", totalAttempts),
    tile("Overall pass rate", pct(totalPassed, totalAttempts))
  );

  $("workers-body").replaceChildren(
    ...rows.map((w) =>
      el(
        "tr",
        { class: "clickable", onclick: () => (location.hash = `#/worker/${w.id}`) },
        el("td", { class: "mono" }, w.worker_code),
        el("td", {}, w.display_name),
        el("td", {}, el("span", { class: w.active ? "badge pass" : "badge off" }, w.active ? "Active" : "Disabled")),
        el("td", { class: "num" }, w.attempts),
        el("td", { class: "num" }, pct(Number(w.passed), Number(w.attempts))),
        el("td", {}, fmtDate(w.last_activity)),
        el("td", { class: "num" }, el("button", { class: "link" }, "View →"))
      )
    )
  );
  $("workers-empty").hidden = state.workers.length > 0;
}

$("worker-search").addEventListener("input", renderWorkers);

// ------------------------------------------------------------ create worker / credentials

function openDialog(id) {
  $(id).showModal();
}

for (const btn of document.querySelectorAll("[data-close]")) {
  btn.addEventListener("click", () => btn.closest("dialog").close());
}

$("new-worker-btn").addEventListener("click", () => {
  $("new-worker-name").value = "";
  $("new-worker-error").textContent = "";
  openDialog("new-worker-dialog");
});

$("new-worker-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const submit = e.submitter;
  submit.disabled = true;
  try {
    const created = await rpc("admin_create_worker", { p_name: $("new-worker-name").value });
    $("new-worker-dialog").close();
    showCredentials("New worker created", created);
    await loadWorkers();
    renderWorkers();
  } catch (err) {
    $("new-worker-error").textContent = err.message;
  } finally {
    submit.disabled = false;
  }
});

function showCredentials(title, c) {
  $("cred-title").textContent = title;
  $("cred-name").textContent = c.display_name;
  $("cred-code").textContent = c.worker_code;
  $("cred-password").textContent = c.password;
  openDialog("credentials-dialog");
}

$("cred-copy").addEventListener("click", async () => {
  const text = `SurakshaAR login\nName: ${$("cred-name").textContent}\nWorker ID: ${$("cred-code").textContent}\nPassword: ${$("cred-password").textContent}`;
  await navigator.clipboard.writeText(text);
  toast("Copied to clipboard");
});

$("cred-print").addEventListener("click", () => window.print());

$("credentials-dialog").addEventListener("close", () => {
  $("cred-password").textContent = "";
});

// ------------------------------------------------------------ worker detail

async function loadWorker(id) {
  if (!state.workers.length) await loadWorkers();
  state.currentWorker = state.workers.find((w) => w.id === id) || null;
  if (!state.currentWorker) return;
  const { data, error } = await db
    .from("training_attempts")
    .select("*")
    .eq("worker_id", id)
    .order("completed_at", { ascending: false });
  if (error) throw new Error(error.message);
  state.attempts = data;
}

function renderWorker() {
  const w = state.currentWorker;
  if (!w) {
    $("worker-name").textContent = "Worker not found";
    $("worker-meta").textContent = "";
    $("worker-tiles").replaceChildren();
    $("attempts-body").replaceChildren();
    return;
  }
  $("worker-name").textContent = w.display_name;
  $("worker-meta").replaceChildren(
    el("span", { class: "mono" }, w.worker_code),
    ` · ${w.active ? "Active" : "Disabled"} · added ${fmtDate(w.created_at)}`
  );
  $("toggle-active-btn").textContent = w.active ? "Disable login" : "Enable login";

  const a = state.attempts;
  const passed = a.filter((x) => x.passed).length;
  const modulesPassed = new Set(a.filter((x) => x.passed).map((x) => `${x.module}/${x.scenario}`)).size;
  const best = a.length ? Math.max(...a.map((x) => x.score ?? 0)) : null;
  $("worker-tiles").replaceChildren(
    tile("Attempts", a.length),
    tile("Pass rate", pct(passed, a.length)),
    tile("Scenarios passed", modulesPassed),
    tile("Best score", best ?? "—"),
    tile("Last active", a.length ? new Date(a[0].completed_at).toLocaleDateString() : "—")
  );

  const rows = [];
  for (const x of a) {
    const detailsRow = el("tr", { class: "details-row", hidden: "" }, el("td", { colspan: "7" }, renderDetails(x.details)));
    const toggle = el("button", { class: "link" }, "Details");
    toggle.addEventListener("click", () => (detailsRow.hidden = !detailsRow.hidden));
    rows.push(
      el(
        "tr",
        {},
        el("td", {}, fmtDate(x.completed_at)),
        el("td", {}, humanize(x.module)),
        el("td", {}, x.scenario || "—"),
        el("td", {}, el("span", { class: x.passed ? "badge pass" : "badge fail" }, x.passed ? "PASS" : "FAIL")),
        el("td", { class: "num" }, x.score ?? "—"),
        el("td", { class: "num" }, fmtDuration(x.elapsed_seconds)),
        el("td", { class: "num" }, x.details ? toggle : null)
      ),
      detailsRow
    );
  }
  $("attempts-body").replaceChildren(...rows);
  $("attempts-empty").hidden = a.length > 0;
}

const SKIP_DETAIL_KEYS = new Set(["passed", "score", "elapsedSeconds"]);
// Keys where "true" is a mistake rather than a completed step.
const NEGATIVE_KEYS = new Set(["wrongExtinguisherUsed", "forcedFailure"]);

function renderDetails(details) {
  if (!details) return "—";
  const items = Object.entries(details)
    .filter(([k, v]) => typeof v === "boolean" && !SKIP_DETAIL_KEYS.has(k))
    .map(([k, v]) => {
      const good = NEGATIVE_KEYS.has(k) ? !v : v;
      return el("li", { class: good ? "yes" : "no" }, humanize(k));
    });
  return el("ul", { class: "checklist" }, ...items);
}

$("reset-pw-btn").addEventListener("click", async () => {
  const w = state.currentWorker;
  if (!w || !confirm(`Reset the password for ${w.display_name}? Their old password stops working, and their devices will need to log in online once.`)) return;
  try {
    showCredentials("Password reset", await rpc("admin_reset_password", { p_worker_id: w.id }));
  } catch (err) {
    toast(err.message);
  }
});

$("toggle-active-btn").addEventListener("click", async () => {
  const w = state.currentWorker;
  if (!w) return;
  if (w.active && !confirm(`Disable login for ${w.display_name}? Progress already synced is kept.`)) return;
  try {
    await rpc("admin_set_active", { p_worker_id: w.id, p_active: !w.active });
    await loadWorkers();
    state.currentWorker = state.workers.find((x) => x.id === w.id);
    renderWorker();
    toast(state.currentWorker.active ? "Login enabled" : "Login disabled");
  } catch (err) {
    toast(err.message);
  }
});

$("export-csv-btn").addEventListener("click", () => {
  const w = state.currentWorker;
  if (!w) return;
  const header = ["completed_at", "module", "scenario", "passed", "score", "elapsed_seconds"];
  const cell = (v) => `"${String(v ?? "").replace(/"/g, '""')}"`;
  const lines = [header.join(",")].concat(state.attempts.map((x) => header.map((h) => cell(x[h])).join(",")));
  const blob = new Blob([lines.join("\n")], { type: "text/csv" });
  const a = el("a", { href: URL.createObjectURL(blob), download: `${w.worker_code}-progress.csv` });
  a.click();
  URL.revokeObjectURL(a.href);
});

// ------------------------------------------------------------ routing

async function route() {
  if (!(await requireAdmin())) {
    showView("login");
    return;
  }
  const hash = location.hash;
  try {
    if (hash.startsWith("#/worker/")) {
      showView("worker");
      await loadWorker(hash.slice("#/worker/".length));
      renderWorker();
    } else {
      showView("workers");
      await loadWorkers();
      renderWorkers();
    }
  } catch (err) {
    toast(err.message);
  }
}

window.addEventListener("hashchange", route);
route();
