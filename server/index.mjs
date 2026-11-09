import fs from "node:fs";
import path from "node:path";
import readline from "node:readline";
import { fileURLToPath } from "node:url";
import { getSessionStats } from "./stats.mjs";

const SERVER_INFO = { name: "antigravity-session-stats", version: "0.3.0" };
const UI_URI = "ui://session-stats/panel.html";
const UI_MIME = "text/html;profile=mcp-app";
const SUPPORTED_PROTOCOLS = ["2025-11-25", "2025-06-18", "2025-03-26", "2024-11-05"];
const here = path.dirname(fileURLToPath(import.meta.url));
const panelPath = path.join(here, "..", "ui", "panel.html");

const statsInputSchema = {
  type: "object",
  properties: {
    threadId: { type: "string", description: "要统计的 Antigravity 对话 ID。省略时统计最近活动的对话。" },
    hintIds: { type: "array", items: { type: "string" }, description: "面板提供的候选对话 ID。" },
  },
  additionalProperties: false,
};

const tools = [
  {
    name: "session_stats_panel",
    title: "会话统计",
    description: "打开会话统计面板，实时显示当前 Antigravity 对话的轮数、步数、用时、首 token 近似时间、输出速度、Token 用量、上下文估算和压缩次数。",
    inputSchema: statsInputSchema,
    annotations: { title: "会话统计", readOnlyHint: true, openWorldHint: false },
    _meta: {
      ui: { resourceUri: UI_URI, visibility: ["model", "app"] },
      "ui/resourceUri": UI_URI,
      "openai/outputTemplate": UI_URI,
      "openai/widgetAccessible": true,
      "openai/ui": { entrypoints: [{ type: "thread" }] },
    },
  },
  {
    name: "session_stats_refresh",
    title: "刷新会话统计",
    description: "面板内部使用：重新读取 Antigravity 对话日志并返回最新统计。",
    inputSchema: statsInputSchema,
    annotations: { title: "刷新会话统计", readOnlyHint: true, openWorldHint: false },
    _meta: { ui: { resourceUri: UI_URI, visibility: ["app"] } },
  },
];

function formatDuration(ms) {
  const seconds = Math.round(ms / 1000);
  const minutes = Math.floor(seconds / 60);
  return minutes ? `${minutes}分${seconds % 60}秒` : `${seconds}秒`;
}

function formatInteger(value) {
  return Number(value).toLocaleString("en-US");
}

function formatContext(context) {
  if (!context || context.used === null || context.used === undefined) {
    return context && context.compactions > 0 ? "刚压缩，等待下一步" : "—";
  }
  if (!context.window) return `${formatInteger(context.used)} tok`;
  const rate = Number.isFinite(context.usedRate) ? context.usedRate : context.used / context.window;
  return `${formatInteger(context.used)} / ${formatInteger(context.window)}（${Math.round(rate * 100)}%）`;
}

function textSummary(result) {
  if (!result.ok) return result.error;
  const { stats, thread } = result;
  const tokens = stats.tokens;
  const percent = (value) => (value === null ? "—" : `${(value * 100).toFixed(1)}%`);
  return [
    `会话：${thread.title}`,
    `${stats.turns} 轮 · ${stats.steps} 步 · ${stats.toolCalls} 次工具调用`,
    `模型用时 ${formatDuration(stats.modelTimeMs)}，工具调用用时 ${formatDuration(stats.toolTimeMs)}`,
    `首 token 平均（近似） ${stats.avgTtftMs === null ? "—" : (stats.avgTtftMs / 1000).toFixed(1) + "秒"}，输出速度 ${stats.outputTps === null ? "—" : stats.outputTps.toFixed(1) + " tok/s"}`,
    `Token 总量 ${tokens.total}，缓存命中 ${percent(tokens.cacheHitRate)}，未缓存输入 ${tokens.uncachedInput}，缓存读取 ${tokens.cachedInput}，输出 ${tokens.output}`,
    `上下文（估算上限） ${formatContext(stats.context)}，已压缩 ${stats.context.compactions || 0} 次`,
  ].join("\n");
}

function callStats(args, includeText) {
  const result = getSessionStats({
    threadId: typeof args.threadId === "string" && args.threadId ? args.threadId : undefined,
    hintIds: Array.isArray(args.hintIds) ? args.hintIds.filter((id) => typeof id === "string").slice(0, 20) : [],
  });
  return {
    content: includeText ? [{ type: "text", text: textSummary(result) }] : [],
    structuredContent: result,
    isError: !result.ok,
  };
}

function handle(method, params) {
  switch (method) {
    case "initialize": {
      const requested = params && params.protocolVersion;
      return {
        protocolVersion: SUPPORTED_PROTOCOLS.includes(requested) ? requested : SUPPORTED_PROTOCOLS[0],
        capabilities: {
          tools: { listChanged: false },
          resources: { listChanged: false },
          extensions: { "io.modelcontextprotocol/ui": {} },
        },
        serverInfo: SERVER_INFO,
      };
    }
    case "ping":
      return {};
    case "logging/setLevel":
      return {};
    case "tools/list":
      return { tools };
    case "tools/call": {
      const name = params && params.name;
      const args = (params && params.arguments) || {};
      if (name === "session_stats_panel") return callStats(args, true);
      if (name === "session_stats_refresh") return callStats(args, false);
      throw Object.assign(new Error(`Unknown tool: ${name}`), { code: -32602 });
    }
    case "resources/list":
      return {
        resources: [
          { uri: UI_URI, name: "session-stats-panel", title: "会话统计面板", mimeType: UI_MIME, _meta: { ui: { prefersBorder: false } } },
        ],
      };
    case "resources/templates/list":
      return { resourceTemplates: [] };
    case "resources/read": {
      if (!params || params.uri !== UI_URI) throw Object.assign(new Error("Unknown resource"), { code: -32002 });
      return {
        contents: [
          { uri: UI_URI, mimeType: UI_MIME, text: fs.readFileSync(panelPath, "utf8"), _meta: { ui: { prefersBorder: false } } },
        ],
      };
    }
    case "prompts/list":
      return { prompts: [] };
    default:
      throw Object.assign(new Error(`Method not found: ${method}`), { code: -32601 });
  }
}

function send(message) {
  process.stdout.write(JSON.stringify(message) + "\n");
}

const input = readline.createInterface({ input: process.stdin, crlfDelay: Infinity });
input.on("line", (line) => {
  if (!line.trim()) return;
  let message;
  try {
    message = JSON.parse(line);
  } catch {
    send({ jsonrpc: "2.0", id: null, error: { code: -32700, message: "Parse error" } });
    return;
  }
  if (message.id === undefined || message.id === null) return;
  try {
    send({ jsonrpc: "2.0", id: message.id, result: handle(message.method, message.params) });
  } catch (error) {
    process.stderr.write(`[session-stats] ${message.method}: ${error.stack || error}\n`);
    send({ jsonrpc: "2.0", id: message.id, error: { code: error.code || -32603, message: String(error.message || error) } });
  }
});
input.on("close", () => process.exit(0));
