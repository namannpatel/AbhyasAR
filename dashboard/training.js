// Training domain model: how the Unity app records attempts, and what "certified" means.
// Kept in one place so the dashboard's rules match the app's (see TrainingResultsUI.cs and
// MachineTrainingResultsUI.cs): a module is passed only when its practical part is passed AND
// its quiz is passed.

export const MODULES = {
  fire_safety: { key: "fire_safety", label: "Fire Safety", practicalLabel: "Fire scenarios" },
  machine_training: { key: "machine_training", label: "Machine Safety", practicalLabel: "Conveyor practice" },
};
export const MODULE_KEYS = Object.keys(MODULES);

// The four Fire Training scenarios, in the app's campaign order.
export const FIRE_SCENARIOS = [
  { key: "trashcan", label: "Trash can fire", fireClass: "A" },
  { key: "outlet", label: "Overloaded outlet", fireClass: "BC" },
  { key: "cabinet", label: "Electrical cabinet", fireClass: "BC" },
  { key: "furnace", label: "Gas furnace", fireClass: "BC" },
];

// Scored steps in a fire scenario's details JSON (FireResponseResult). `bad` = true means the
// flag being true is a mistake, not a completed step.
export const FIRE_STEPS = [
  { key: "alarmActivated", label: "Sound the alarm" },
  { key: "wrongExtinguisherUsed", label: "Choose the right extinguisher", bad: true },
  { key: "pinPulled", label: "Pull the pin" },
  { key: "aimedAtBase", label: "Aim at the base" },
  { key: "squeezed", label: "Squeeze the handle" },
  { key: "swept", label: "Sweep side to side" },
  { key: "fireFullyOut", label: "Put the fire fully out" },
  { key: "forcedFailure", label: "Shut off the gas (furnace)", bad: true },
];

/**
 * Classifies an attempt's scenario tag.
 *   fire_safety      "FireTraining/Quiz" | "FireTraining/BC/furnace" | legacy "FireTraining/BC"
 *   machine_training "ConveyorTest/Practice" | "ConveyorTest/Quiz"
 * -> { kind: "quiz" | "scenario" | "practice" | "other", key, label }
 */
export function parseScenario(module, scenario) {
  const parts = String(scenario || "").split("/");
  const tail = parts[parts.length - 1];
  if (tail === "Quiz") return { kind: "quiz", key: "quiz", label: "Quiz" };
  if (module === "machine_training" && tail === "Practice") return { kind: "practice", key: "practice", label: "Conveyor practice" };
  if (module === "fire_safety" && parts.length >= 2) {
    const fireClass = parts[1];
    const known = parts.length >= 3 && FIRE_SCENARIOS.find((s) => s.key === parts[2]);
    if (known) return { kind: "scenario", key: known.key, label: known.label, fireClass };
    return { kind: "scenario", key: `class_${fireClass}`, label: `Class ${fireClass} fire`, fireClass, legacy: true };
  }
  return { kind: "other", key: scenario || "unknown", label: scenario || "—" };
}

/** Quiz result from its details JSON ({correct, total, answered, passMark, completed}); falls back to the % score. */
export function quizResult(attempt) {
  const d = attempt.details || {};
  if (Number.isFinite(d.correct) && Number.isFinite(d.total) && d.total > 0) {
    return { correct: d.correct, total: d.total, pct: Math.round((d.correct / d.total) * 100), completed: d.completed !== false, passMark: d.passMark };
  }
  return { correct: null, total: null, pct: attempt.score ?? null, completed: d.completed !== false, passMark: null };
}

/**
 * Certification status of one worker in one module, from that worker's attempts (any order).
 * Certified at the first passed quiz that came at or after a passed practical attempt -- the
 * app only runs the quiz once the practical part is finished, and only then issues a certificate.
 */
export function certification(attempts, module) {
  const mine = attempts.filter((a) => a.module === module);
  const practicalKind = module === "machine_training" ? "practice" : "scenario";
  const practicals = mine.filter((a) => parseScenario(module, a.scenario).kind === practicalKind);
  const quizzes = mine.filter((a) => parseScenario(module, a.scenario).kind === "quiz");
  const firstPracticalPass = minTime(practicals.filter((a) => a.passed));
  const passedQuizTimes = quizzes.filter((a) => a.passed).map((a) => Date.parse(a.completed_at)).sort((x, y) => x - y);
  // every passed quiz that came after a passed practical -- each one is a moment a certificate could be issued
  const qualifyingTimes = firstPracticalPass == null ? [] : passedQuizTimes.filter((t) => t >= firstPracticalPass - 60_000);
  const certifiedMs = qualifyingTimes[0] ?? null;

  const bestQuiz = quizzes.map(quizResult).filter((q) => q.pct != null).sort((x, y) => y.pct - x.pct)[0] || null;
  return {
    status: certifiedMs != null ? "certified" : mine.length ? "in_progress" : "not_started",
    certifiedAt: certifiedMs != null ? new Date(certifiedMs).toISOString() : null,
    practicalPassed: firstPracticalPass != null,
    practicalAttempts: practicals.length,
    quizPassed: passedQuizTimes.length > 0,
    quizAttempts: quizzes.length,
    bestQuiz,
    qualifyingTimes,
  };
}

function minTime(list) {
  let best = null;
  for (const a of list) {
    const t = Date.parse(a.completed_at);
    if (best == null || t < best) best = t;
  }
  return best;
}

// ------------------------------------------------------------------ certificates
// QR payload written by CertificateService.cs: "AR-CERT|v1|<name>|<module>|<score>|<unix>|<checksum>".
// The checksum is HMAC-SHA256 with a key embedded in the app, truncated to 8 hex chars. The key is
// in the (public) source code, so a valid checksum only proves the text wasn't casually edited --
// the dashboard therefore also checks the certificate against the worker's synced records.
const CERT_KEY = "ARBT-SmartEducation-2026-CertKey-v1";

// Module names as the app prints them on certificates, in every app language.
const CERT_MODULE_NAMES = {
  fire_safety: ["Fire Safety Training", "अग्नि सुरक्षा प्रशिक्षण", "ᱥᱮᱸᱜᱮᱞ ᱥᱟᱦᱟᱛ ᱥᱤᱠᱷᱲᱟ"],
  machine_training: ["Conveyor Machine Safety Training", "कन्वेयर मशीन सुरक्षा प्रशिक्षण", "ᱠᱚᱱᱵᱷᱮᱭᱟᱨ ᱢᱮᱥᱤᱱ ᱨᱮᱭᱟᱜ ᱦᱚᱨᱦᱚᱭ ᱛᱟᱹᱞᱤᱢ"],
};

export function parseCertificate(text) {
  const parts = String(text || "").trim().split("|");
  if (parts.length !== 7 || parts[0] !== "AR-CERT" || parts[1] !== "v1") return null;
  const unix = Number(parts[5]);
  const score = Number(parts[4]);
  if (!Number.isFinite(unix) || !Number.isFinite(score)) return null;
  const moduleName = parts[3];
  const module = MODULE_KEYS.find((m) => CERT_MODULE_NAMES[m].includes(moduleName)) || null;
  return {
    name: parts[2], moduleName, module, score,
    issuedAt: new Date(unix * 1000).toISOString(),
    body: parts.slice(0, 6).join("|"),
    checksum: parts[6].toLowerCase(),
  };
}

export async function checksumMatches(cert) {
  const enc = new TextEncoder();
  const key = await crypto.subtle.importKey("raw", enc.encode(CERT_KEY), { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
  const mac = new Uint8Array(await crypto.subtle.sign("HMAC", key, enc.encode(cert.body)));
  const hex = Array.from(mac.slice(0, 4), (b) => b.toString(16).padStart(2, "0")).join("");
  return hex === cert.checksum;
}

/**
 * Looks for synced records backing a certificate: a worker with that name who passed that
 * module's practical and quiz, with a qualifying quiz pass within a day of the issue time.
 */
export function matchCertificate(cert, workers, attemptsByWorker) {
  const norm = (s) => String(s || "").trim().toLowerCase().replace(/\s+/g, " ");
  const candidates = workers.filter((w) => norm(w.display_name) === norm(cert.name));
  const issued = Date.parse(cert.issuedAt);
  const DAY = 864e5;
  for (const w of candidates) {
    if (!cert.module) continue;
    const c = certification(attemptsByWorker.get(w.id) || [], cert.module);
    const near = c.qualifyingTimes.filter((t) => Math.abs(t - issued) <= DAY).sort((a, b) => Math.abs(a - issued) - Math.abs(b - issued))[0];
    if (near != null) {
      return { worker: w, certification: c, exact: true, matchedAt: new Date(near).toISOString(), nameMatches: candidates.length };
    }
  }
  return { worker: candidates[0] || null, certification: null, exact: false, nameMatches: candidates.length };
}
