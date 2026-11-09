# Changelog / 更新日志

All notable changes to this project are documented here. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/).

本项目的重要改动都记录在这里。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循[语义化版本](https://semver.org/lang/zh-CN/)。

## [0.4.0] - 2026-11-09

### Changed / 变更

- **Refreshes every second.** The stats panel and the bottom bar now update every second instead of every 3 seconds, matching the Codex version. Each refresh still reads only newly appended log bytes.
- **面板和底部统计条改为每秒刷新**，以前是每 3 秒，和 Codex 版保持一致。每次刷新仍然只读取日志新增的部分。

## [0.3.0] - 2026-11-09

### Changed / 变更

- Output speed (TPS) now uses the summed output tokens and generation windows of completed `PLANNER_RESPONSE` steps. Each window is the next record's `created_at` minus the planner's `created_at`. Steps followed by `USER_INPUT` or `SYSTEM_MESSAGE` (waiting for the user or a background message), zero/negative windows, missing-next steps, and windows over 600 seconds are excluded. If no usable step remains, the legacy output-tokens-per-model-time calculation is used. Model-time display is unchanged.
- Sampled Cloudflare conversation: 69.4 → 47.6 tok/s (36 included; 3 waiting, 0 same-second, 1 no-next, 0 over-threshold).
- Sampled largest Steel Workshop conversation: 34.6 → 39.7 tok/s (3125 included; 66 waiting, 0 same-second, 1 no-next, 0 over-threshold).
- Bumped the plugin, MCP server, and panel app version to 0.3.0.
- 输出速度（TPS）改为使用完整的 `PLANNER_RESPONSE` 步：输出 Token 合计 ÷ 生成窗口合计。窗口为下一条记录的 `created_at` 减去 planner 的 `created_at`。下一条是 `USER_INPUT` 或 `SYSTEM_MESSAGE`（等待用户或后台消息）的步骤，以及同秒/负窗口、无下一条、超过 600 秒的步骤均排除。无可用步骤时回退到旧的“输出 Token ÷ 模型用时”算法。“模型用时”显示口径不变。
- 抽查 Cloudflare 对话：69.4 → 47.6 tok/s（计入 36 步；等待 3、同秒 0、无下一条 1、超阈值 0）。
- 抽查最大钢铁工坊对话：34.6 → 39.7 tok/s（计入 3125 步；等待 66、同秒 0、无下一条 1、超阈值 0）。
- 插件、MCP 服务和面板版本统一更新为 0.3.0。

### Notes / 说明

- The per-step method excludes a final planner response whose generation end is unknown. In short conversations this can produce a lower value than the legacy formula, even though the legacy denominator also ends at the last observed step start.
- Structured results include TPS step counters for diagnostics; the panel and bottom-bar summaries continue to show only the TPS value.
- 逐步算法会排除生成结束时间未知的最后一条 planner response；在短对话中，这可能使结果低于旧公式，即使旧公式的模型用时也只统计到最后一个已观察步骤的开始时间。
- 结构化结果包含 TPS 步数诊断字段；面板和底部统计条摘要仍只显示 TPS 数值。

## [0.2.0] - 2026-11-09

### Added / 新增

- Estimated context usage against a configurable 256,000-token window, plus context-compaction counts.
- `context.window`, `context.windowEstimated`, `context.used`, `context.usedRate`, and `context.compactions` in the structured MCP result.
- A visible “estimate” marker and compaction row in the MCP App panel, and two context rows in the bottom bar detail card.
- `SESSION_STATS_CONTEXT_WINDOW` for overriding the estimated window with a positive integer.
- 按可配置的 256,000 Token 窗口估算上下文占用，并显示上下文压缩次数。
- MCP 结构化结果新增 `context.window`、`context.windowEstimated`、`context.used`、`context.usedRate` 和 `context.compactions`。
- MCP App 面板新增“估算”标记和压缩次数行；底部统计条详情卡新增两行上下文信息。
- 支持用 `SESSION_STATS_CONTEXT_WINDOW` 覆盖估算窗口，只接受正整数。

### Changed / 变更

- Context usage now comes from the latest `PLANNER_RESPONSE`, not the latest token-bearing record. If a `CHECKPOINT` is the latest context event, usage is `null` until the next planner response.
- Context usage is no longer capped at 100%.
- Bumped the plugin, MCP server, and panel app version to 0.2.0.
- 上下文已用 Token 改为取最近一条 `PLANNER_RESPONSE`；如果最近一次上下文事件是 `CHECKPOINT`，则在下一条 planner response 前显示为未知。
- 上下文占用率不再截断到 100%。
- 插件、MCP 服务和面板版本统一更新为 0.2.0。

## [0.1.0] - 2026-11-09

### Added / 新增

- First public release of the Antigravity plugin and Windows bottom bar.
- Turn, step, timing, TTFT, throughput, and token-cache statistics.
- Antigravity 插件和 Windows 底部统计条的首个公开版本。
- 支持轮数、步数、用时、TTFT、吞吐和 Token 缓存统计。
