---
name: session-stats
description: Use when the user asks to see, open, or check Antigravity conversation statistics, token usage, cache hit rate, turn or step counts, model or tool time, TTFT, output speed, estimated context usage, or compaction count for the current chat.
---

Call the `session_stats_panel` tool to open the live statistics panel. It reads the local Antigravity transcript, so it needs no arguments; pass `threadId` only when the user names a specific conversation ID.

After the panel opens, report the headline numbers from the tool's text result in one or two sentences. If the Antigravity build does not render MCP App resources, use the same text result as the fallback.

TTFT is only an approximation: Antigravity does not record the first-token timestamp, so this project measures each user input to the first planner response and the transcript has second-level precision.

Output speed uses the summed output tokens and elapsed windows of completed planner steps. Invalid, missing-next, and over-threshold windows are excluded; if none remain, it falls back to the legacy output-tokens-per-model-time calculation.

Context usage is also an estimate: Antigravity does not expose the real context-window size, so this plugin uses 256,000 tokens unless `SESSION_STATS_CONTEXT_WINDOW` overrides it. A result above 100% is valid; the project does not clamp it.
