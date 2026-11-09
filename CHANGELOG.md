# Changelog / 更新日志

All notable changes to this project are documented here. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/).

本项目的重要改动都记录在这里。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循[语义化版本](https://semver.org/lang/zh-CN/)。

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
