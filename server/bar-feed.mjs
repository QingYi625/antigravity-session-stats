import { getSessionStats } from "./stats.mjs";
import readline from "node:readline";

const INTERVAL_MS = Number(process.env.SESSION_STATS_INTERVAL_MS) || 3000;
let activeTitle = "";

function compact(value) {
  if (value === null || value === undefined) return "—";
  if (value >= 1e9) return (value / 1e9).toFixed(2) + "B";
  if (value >= 1e6) return (value / 1e6).toFixed(1) + "M";
  if (value >= 1e4) return (value / 1e3).toFixed(1) + "K";
  return String(value);
}

const full = (value) => (value === null || value === undefined ? "—" : Number(value).toLocaleString("en-US"));

function contextUsage(context) {
  if (!context || context.used === null || context.used === undefined) {
    return context && context.compactions > 0 ? "刚压缩" : "—";
  }
  if (!context.window) return `${full(context.used)} tok`;
  const rate = Number.isFinite(context.usedRate) ? context.usedRate : context.used / context.window;
  return `${full(context.used)} / ${full(context.window)} (${Math.round(rate * 100)}%)`;
}

function duration(ms) {
  const total = Math.round((ms || 0) / 1000);
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const seconds = total % 60;
  if (hours) return `${hours}小时${minutes}分${seconds}秒`;
  if (minutes) return `${minutes}分${seconds}秒`;
  return `${seconds}秒`;
}

function payload() {
  try {
    const result = getSessionStats({ title: activeTitle });
    if (!result.ok) return { ok: false, message: "暂无会话" };
    const s = result.stats;
    const t = s.tokens;
    const c = s.context || {};
    const hit = t.cacheHitRate === null ? "—" : `${Math.round(t.cacheHitRate * 100)}%`;
    const tps = s.outputTps === null ? "—" : `${Math.round(s.outputTps)} tok/s`;
    return {
      ok: true,
      title: result.thread.title,
      running: s.running,
      session: {
        summary: `${s.turns} 轮 ${s.steps} 步 · ${tps}`,
        headline: `${s.turns} 轮 ${s.steps} 步`,
        rows: [
          ["模型用时", duration(s.modelTimeMs)],
          ["工具调用用时", duration(s.toolTimeMs)],
          ["首 token 平均（TTFT）", s.avgTtftMs === null ? "—" : `${(s.avgTtftMs / 1000).toFixed(1)}秒`],
          ["输出速度（TPS）", tps],
        ],
      },
      tokens: {
        summary: `${compact(t.total)} tok · 缓存命中 ${hit}`,
        headline: `${full(t.total)} tok`,
        rows: [
          ["缓存命中", hit],
          ["未缓存输入", `${full(t.uncachedInput)} tok`],
          ["缓存读取", `${full(t.cachedInput)} tok`],
          ["输出", `${full(t.output)} tok`],
          ["上下文（估算上限）", contextUsage(c)],
          ["已压缩", `${Number(c.compactions) || 0} 次`],
        ],
      },
    };
  } catch (error) {
    return { ok: false, message: "读取失败：" + String(error.message || error) };
  }
}

function emit() {
  process.stdout.write(JSON.stringify(payload()) + "\n");
}

let timer = null;
function schedule() {
  clearInterval(timer);
  emit();
  timer = setInterval(emit, INTERVAL_MS);
}

const input = readline.createInterface({ input: process.stdin, crlfDelay: Infinity });
input.on("line", (line) => {
  let message;
  try {
    message = JSON.parse(line);
  } catch {
    return;
  }
  if (message && typeof message.title === "string" && message.title !== activeTitle) {
    activeTitle = message.title;
    schedule();
  }
});
input.on("close", () => process.exit(0));
schedule();
