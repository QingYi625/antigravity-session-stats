import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { DatabaseSync } from "node:sqlite";

const GEMINI_HOME = process.env.GEMINI_HOME || path.join(os.homedir(), ".gemini");
const ANTIGRAVITY_HOME = process.env.ANTIGRAVITY_HOME || path.join(GEMINI_HOME, "antigravity");
const BRAIN_DIR = path.join(ANTIGRAVITY_HOME, "brain");
const SUMMARIES_DB = path.join(ANTIGRAVITY_HOME, "conversation_summaries.db");
const TRANSCRIPT_RELATIVE = path.join(".system_generated", "logs", "transcript_full.jsonl");
const RECENT_LIMIT = 30;
const LIVE_IDLE_MS = 15 * 60 * 1000;
const TOKEN_RECORD_TYPES = new Set(["PLANNER_RESPONSE", "CHECKPOINT"]);
const ESTIMATED_CONTEXT_WINDOW = 256000;

function configuredContextWindow(value) {
  if (typeof value !== "string" || !/^\d+$/.test(value.trim())) return ESTIMATED_CONTEXT_WINDOW;
  const parsed = Number(value.trim());
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : ESTIMATED_CONTEXT_WINDOW;
}

const CONTEXT_WINDOW = configuredContextWindow(process.env.SESSION_STATS_CONTEXT_WINDOW);

const fileStates = new Map();
const activityCache = new Map();
let summariesCache = { signature: "", values: new Map() };

function statOrNull(filePath) {
  try {
    return fs.statSync(filePath);
  } catch {
    return null;
  }
}

function fileSignature(filePath) {
  const stat = statOrNull(filePath);
  return stat ? `${stat.mtimeMs}:${stat.size}` : "-";
}

function decodeWorkspaceUri(value) {
  if (typeof value !== "string") return "";
  try {
    const uris = JSON.parse(value);
    for (const uri of Array.isArray(uris) ? uris : []) {
      if (typeof uri !== "string" || !uri.startsWith("file:")) continue;
      try {
        return fileURLToPath(uri);
      } catch {}
    }
  } catch {}
  return "";
}

function readSummaries() {
  const signature = [
    fileSignature(SUMMARIES_DB),
    fileSignature(`${SUMMARIES_DB}-wal`),
    fileSignature(`${SUMMARIES_DB}-shm`),
  ].join("|");
  if (signature === summariesCache.signature) return summariesCache.values;

  let tempDir = "";
  let db = null;
  try {
    tempDir = fs.mkdtempSync(path.join(os.tmpdir(), "antigravity-session-stats-db-"));
    const tempDb = path.join(tempDir, "conversation_summaries.db");
    fs.copyFileSync(SUMMARIES_DB, tempDb);
    for (const suffix of ["-wal", "-shm"]) {
      const source = `${SUMMARIES_DB}${suffix}`;
      if (fs.existsSync(source)) fs.copyFileSync(source, `${tempDb}${suffix}`);
    }

    try {
      db = new DatabaseSync(tempDb, { readOnly: true });
    } catch {
      db = new DatabaseSync(tempDb);
      db.exec("PRAGMA query_only = ON");
    }

    const values = new Map();
    const rows = db
      .prepare(
        `SELECT conversation_id, title, workspace_uris, parent_conversation_id, nesting_depth
         FROM conversation_summaries
         ORDER BY last_modified_time DESC`,
      )
      .all();
    for (const row of rows) {
      if (!row.conversation_id) continue;
      values.set(row.conversation_id, {
        title: typeof row.title === "string" ? row.title.trim() : "",
        cwd: decodeWorkspaceUri(row.workspace_uris),
        parentConversationId: row.parent_conversation_id || "",
        nestingDepth: Number(row.nesting_depth) || 0,
      });
    }
    summariesCache = { signature, values };
    return values;
  } catch {
    return summariesCache.values;
  } finally {
    try {
      if (db) db.close();
    } catch {}
    if (tempDir) {
      try {
        fs.rmSync(tempDir, { recursive: true, force: true });
      } catch {}
    }
  }
}

function readTail(filePath, size, length) {
  const start = Math.max(0, size - length);
  const buffer = Buffer.alloc(size - start);
  const fd = fs.openSync(filePath, "r");
  try {
    let read = 0;
    while (read < buffer.length) {
      const count = fs.readSync(fd, buffer, read, buffer.length - read, start + read);
      if (count <= 0) break;
      read += count;
    }
    return buffer.subarray(0, read);
  } finally {
    fs.closeSync(fd);
  }
}

function lastActivityMs(filePath, stat) {
  const cached = activityCache.get(filePath);
  if (cached && cached.size === stat.size && cached.mtimeMs === stat.mtimeMs) return cached.ms;

  let activityMs = stat.mtimeMs;
  try {
    for (const window of [64 * 1024, 1024 * 1024, 16 * 1024 * 1024]) {
      const length = Math.min(stat.size, window);
      if (length <= 0) break;
      const text = readTail(filePath, stat.size, length).toString("utf8");
      const lines = text.split(/\r?\n/);
      for (let index = lines.length - 1; index >= 0; index -= 1) {
        const line = lines[index].trim();
        if (!line) continue;
        try {
          const record = JSON.parse(line);
          const parsed = Date.parse(record.created_at);
          if (Number.isFinite(parsed)) activityMs = parsed;
          break;
        } catch {}
      }
      if (activityMs !== stat.mtimeMs || length === stat.size) break;
    }
  } catch {}

  activityCache.set(filePath, { size: stat.size, mtimeMs: stat.mtimeMs, ms: activityMs });
  return activityMs;
}

function transcriptFiles() {
  let names = [];
  try {
    names = fs.readdirSync(BRAIN_DIR, { withFileTypes: true });
  } catch {
    return [];
  }

  const files = [];
  for (const entry of names) {
    if (!entry.isDirectory()) continue;
    const filePath = path.join(BRAIN_DIR, entry.name, TRANSCRIPT_RELATIVE);
    const stat = statOrNull(filePath);
    if (!stat || !stat.isFile()) continue;
    files.push({
      id: entry.name,
      filePath,
      updatedMs: lastActivityMs(filePath, stat),
    });
  }
  files.sort((left, right) => right.updatedMs - left.updatedMs);
  return files;
}

function titleForThread(id, summaries) {
  const meta = summaries.get(id) || {};
  const cwd = meta.cwd || "";
  return {
    title: meta.title || (cwd ? path.basename(cwd) : id),
    cwd,
    parentConversationId: meta.parentConversationId || "",
    nestingDepth: meta.nestingDepth || 0,
  };
}

export function listRecentThreads() {
  const summaries = readSummaries();
  const threads = [];
  for (const file of transcriptFiles()) {
    const meta = titleForThread(file.id, summaries);
    if (meta.parentConversationId || meta.nestingDepth > 0) continue;
    threads.push({
      id: file.id,
      title: meta.title,
      cwd: meta.cwd,
      updatedAt: new Date(file.updatedMs).toISOString(),
      filePath: file.filePath,
    });
    if (threads.length >= RECENT_LIMIT) break;
  }
  return threads;
}

function validThreadId(value) {
  return typeof value === "string" && /^[A-Za-z0-9._-]+$/.test(value) && value !== "." && value !== "..";
}

function findThreadFile(threadId) {
  if (!validThreadId(threadId)) return null;
  const filePath = path.join(BRAIN_DIR, threadId, TRANSCRIPT_RELATIVE);
  const stat = statOrNull(filePath);
  return stat && stat.isFile() ? filePath : null;
}

function newState(filePath) {
  return {
    filePath,
    offset: 0,
    leftover: Buffer.alloc(0),
    turns: new Map(),
    turnOrder: [],
    currentTurnKey: null,
    userTurns: 0,
    steps: 0,
    toolCount: 0,
    inputTokens: 0,
    cachedTokens: 0,
    outputTokens: 0,
    lastPlannerUsage: null,
    compactions: 0,
    model: null,
    lastEventMs: 0,
  };
}

function ensureTurn(state, turnKey, startedMs) {
  let turn = state.turns.get(turnKey);
  if (!turn) {
    turn = {
      startedMs: startedMs || 0,
      lastEventMs: startedMs || 0,
      ttftMs: null,
      toolIntervals: [],
    };
    state.turns.set(turnKey, turn);
    state.turnOrder.push(turnKey);
  }
  if (startedMs && (!turn.startedMs || startedMs < turn.startedMs)) turn.startedMs = startedMs;
  if (startedMs > turn.lastEventMs) turn.lastEventMs = startedMs;
  return turn;
}

function turnKeyFor(record, state) {
  if (record.step_index !== undefined && record.step_index !== null) return `step-${record.step_index}`;
  return `event-${state.turnOrder.length}`;
}

function tokenValue(value) {
  const numeric = Number(value);
  return Number.isFinite(numeric) && numeric > 0 ? numeric : 0;
}

function parseToolInterval(content) {
  if (typeof content !== "string") return null;
  const startMatch = /(?:^|\n)Created At:\s*([^\r\n]+)/.exec(content);
  const endMatch = /(?:^|\n)Completed At:\s*([^\r\n]+)/.exec(content);
  if (!startMatch || !endMatch) return null;
  const startMs = Date.parse(startMatch[1].trim());
  const endMs = Date.parse(endMatch[1].trim());
  if (!Number.isFinite(startMs) || !Number.isFinite(endMs) || endMs < startMs) return null;
  return [startMs, endMs];
}

function handleRecord(state, line) {
  let record;
  try {
    record = JSON.parse(line);
  } catch {
    return;
  }

  const type = typeof record.type === "string" ? record.type : "";
  const timestampMs = Date.parse(record.created_at) || 0;
  if (timestampMs > state.lastEventMs) state.lastEventMs = timestampMs;
  if (typeof record.model === "string" && record.model) state.model = record.model;

  let turn;
  if (type === "USER_INPUT") {
    const key = turnKeyFor(record, state);
    turn = ensureTurn(state, key, timestampMs);
    state.currentTurnKey = key;
    state.userTurns += 1;
  } else {
    if (state.currentTurnKey === null) state.currentTurnKey = `implicit-${state.turnOrder.length}`;
    turn = ensureTurn(state, state.currentTurnKey, timestampMs);
    if (timestampMs > turn.lastEventMs) turn.lastEventMs = timestampMs;
  }

  if (type === "PLANNER_RESPONSE") {
    state.steps += 1;
    if (turn.ttftMs === null && turn.startedMs && timestampMs) {
      turn.ttftMs = Math.max(0, timestampMs - turn.startedMs);
    }
    state.toolCount += Array.isArray(record.tool_calls) ? record.tool_calls.length : 0;
  }

  if (TOKEN_RECORD_TYPES.has(type)) {
    const input = tokenValue(record.input_tokens);
    const cached = tokenValue(record.cache_read_tokens);
    const output = tokenValue(record.output_tokens);
    state.inputTokens += input;
    state.cachedTokens += cached;
    state.outputTokens += output;
    if (type === "PLANNER_RESPONSE") {
      state.lastPlannerUsage = { totalInput: input + cached, timestampMs };
    } else {
      state.compactions += 1;
      state.lastPlannerUsage = null;
    }
  } else if (type === "GENERIC") {
    const interval = parseToolInterval(record.content);
    if (interval) turn.toolIntervals.push(interval);
  }
}

function updateState(filePath) {
  let state = fileStates.get(filePath);
  const stat = fs.statSync(filePath);
  if (!state || stat.size < state.offset) {
    state = newState(filePath);
    fileStates.set(filePath, state);
  }
  if (stat.size === state.offset) return state;

  const fd = fs.openSync(filePath, "r");
  try {
    const length = stat.size - state.offset;
    const fresh = Buffer.alloc(length);
    let read = 0;
    while (read < length) {
      const count = fs.readSync(fd, fresh, read, length - read, state.offset + read);
      if (count <= 0) break;
      read += count;
    }
    state.offset += read;

    const buffer = state.leftover.length ? Buffer.concat([state.leftover, fresh.subarray(0, read)]) : fresh.subarray(0, read);
    const lastNewline = buffer.lastIndexOf(10);
    if (lastNewline < 0) {
      state.leftover = Buffer.from(buffer);
      return state;
    }
    state.leftover = Buffer.from(buffer.subarray(lastNewline + 1));
    const text = buffer.subarray(0, lastNewline).toString("utf8");
    let start = 0;
    while (start < text.length) {
      let end = text.indexOf("\n", start);
      if (end < 0) end = text.length;
      if (end > start) handleRecord(state, text.slice(start, end));
      start = end + 1;
    }
  } finally {
    fs.closeSync(fd);
  }
  return state;
}

function unionLength(intervals, lower, upper) {
  const clipped = intervals
    .map(([start, end]) => [Math.max(start, lower), Math.min(end, upper)])
    .filter(([start, end]) => end > start)
    .sort((left, right) => left[0] - right[0]);
  let total = 0;
  let currentStart = null;
  let currentEnd = null;
  for (const [start, end] of clipped) {
    if (currentEnd === null || start > currentEnd) {
      if (currentEnd !== null) total += currentEnd - currentStart;
      currentStart = start;
      currentEnd = end;
    } else if (end > currentEnd) {
      currentEnd = end;
    }
  }
  if (currentEnd !== null) total += currentEnd - currentStart;
  return total;
}

function summarize(state, now) {
  let modelTimeMs = 0;
  let toolTimeMs = 0;
  let ttftSum = 0;
  let ttftCount = 0;
  let running = false;
  const lastTurnKey = state.turnOrder[state.turnOrder.length - 1];

  for (const turnKey of state.turnOrder) {
    const turn = state.turns.get(turnKey);
    if (!turn || !turn.startedMs) continue;
    const live = turnKey === lastTurnKey && now - state.lastEventMs < LIVE_IDLE_MS;
    const endMs = live ? Math.max(turn.lastEventMs, now) : turn.lastEventMs || turn.startedMs;
    if (live) running = true;
    const wallMs = Math.max(0, endMs - turn.startedMs);
    const turnToolMs = unionLength(turn.toolIntervals, turn.startedMs, endMs);
    toolTimeMs += turnToolMs;
    modelTimeMs += Math.max(0, wallMs - turnToolMs);
    if (turn.ttftMs !== null) {
      ttftSum += turn.ttftMs;
      ttftCount += 1;
    }
  }

  const totalInput = state.inputTokens + state.cachedTokens;
  const contextUsed = state.lastPlannerUsage ? state.lastPlannerUsage.totalInput : null;
  return {
    turns: state.userTurns,
    steps: state.steps,
    toolCalls: state.toolCount,
    running,
    modelTimeMs,
    toolTimeMs,
    avgTtftMs: ttftCount ? Math.round(ttftSum / ttftCount) : null,
    outputTps: modelTimeMs > 0 ? state.outputTokens / (modelTimeMs / 1000) : null,
    tokens: {
      total: totalInput + state.outputTokens,
      input: totalInput,
      uncachedInput: state.inputTokens,
      cachedInput: state.cachedTokens,
      output: state.outputTokens,
      reasoningOutput: 0,
      cacheHitRate: totalInput > 0 ? state.cachedTokens / totalInput : null,
    },
    context: {
      window: CONTEXT_WINDOW,
      windowEstimated: true,
      used: contextUsed,
      usedRate: contextUsed === null ? null : contextUsed / CONTEXT_WINDOW,
      compactions: state.compactions,
    },
    model: state.model,
    lastEventAt: state.lastEventMs ? new Date(state.lastEventMs).toISOString() : null,
  };
}

function normalizeTitle(value) {
  return String(value || "")
    .replace(/\s+/g, " ")
    .replace(/\s+[-–—]\s+Antigravity(?:\s*[-–—].*)?$/i, "")
    .trim();
}

function titleMatchScore(wanted, candidate) {
  if (!wanted || !candidate) return -1;
  if (wanted === candidate) return 100000 + candidate.length;
  for (const separator of [" - ", " – ", " — ", " · "]) {
    if (wanted.startsWith(candidate + separator)) return candidate.length;
  }
  const clipped = wanted.replace(/(?:\.{3}|…)$/, "").trim();
  if (clipped.length >= 8 && clipped.length < wanted.length && candidate.startsWith(clipped)) return clipped.length;
  return -1;
}

function findThreadByTitle(title, recent, summaries) {
  const wanted = normalizeTitle(title);
  if (!wanted) return null;
  const candidates = recent.filter((thread) => normalizeTitle(thread.title) === wanted);
  if (candidates.length) return candidates[0];

  let best = null;
  let bestScore = -1;
  for (const [id, meta] of summaries) {
    if (meta.parentConversationId || meta.nestingDepth > 0) continue;
    const score = titleMatchScore(wanted, normalizeTitle(meta.title));
    if (score < 0 || score <= bestScore) continue;
    const filePath = findThreadFile(id);
    if (!filePath) continue;
    best = { id, title: meta.title || title, cwd: meta.cwd || "", filePath };
    bestScore = score;
  }
  return best;
}

export function getSessionStats({ threadId, hintIds = [], title } = {}) {
  const recent = listRecentThreads();
  const summaries = readSummaries();
  let target = null;
  let selection = "auto";

  if (threadId) {
    target = recent.find((thread) => thread.id === threadId) || null;
    if (!target && validThreadId(threadId)) {
      const filePath = findThreadFile(threadId);
      if (filePath) {
        const meta = titleForThread(threadId, summaries);
        target = { id: threadId, title: meta.title, cwd: meta.cwd, filePath };
      }
    }
    selection = "pinned";
  }

  if (!target) {
    for (const hint of hintIds) {
      const match = recent.find((thread) => thread.id === hint);
      if (match) {
        target = match;
        selection = "host";
        break;
      }
    }
  }

  if (!target && title) {
    target = findThreadByTitle(title, recent, summaries);
    if (target) selection = "window";
  }

  if (!target) {
    target = recent[0] || null;
    selection = "auto";
  }

  const threads = recent.map(({ id, title, updatedAt }) => ({ id, title, updatedAt }));
  if (!target) {
    return {
      ok: false,
      error: `没有在 ${BRAIN_DIR} 找到最近的 Antigravity 会话日志`,
      threads,
      generatedAt: new Date().toISOString(),
    };
  }

  const now = Date.now();
  const state = updateState(target.filePath);
  return {
    ok: true,
    selection,
    thread: { id: target.id, title: target.title, cwd: target.cwd || "" },
    stats: summarize(state, now),
    threads,
    generatedAt: new Date(now).toISOString(),
  };
}
