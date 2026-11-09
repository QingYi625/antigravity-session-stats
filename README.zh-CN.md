# Antigravity 会话统计（session-stats）

[English](README.md) | **简体中文**

在 Google Antigravity 里实时查看当前对话的轮数、步数、模型用时、工具用时、首 token 近似时间、输出速度、Token 缓存用量、上下文估算和压缩次数。

本项目包含两部分，可以单独使用：

- **Antigravity 插件**：打开 MCP App 统计面板，每 3 秒刷新一次。如果某个 Antigravity 版本不渲染 UI 资源，工具的文本结果仍可作为降级内容使用。
- **底部统计条（仅 Windows）**：透明小条，贴在 Antigravity 输入框下方并跟随窗口移动；点击摘要弹出详情，默认可以随 Windows 登录启动。

所有数据都只在本机读取，不会上传。

> 界面文字保持简体中文，并尽量复用原 Codex 会话统计项目的布局、字段和样式。

## 显示的数据

| 分组 | 项目 |
| --- | --- |
| 会话统计 | 轮数、步数、模型用时、工具调用用时、首 token 平均（近似 TTFT）、输出速度（TPS） |
| Token 用量 | Token 总量、缓存命中率、未缓存输入、缓存读取、输出 |
| 上下文估算 | 已用 Token / 256,000 的估算上限，以及压缩次数 |

面板会把上下文上限明确标成估算值，并且不会把占用率截断到 100%。

## 数据来源

解析器读取每个对话的完整本地记录：

```text
%USERPROFILE%\.gemini\antigravity\brain\<conversation-id>\.system_generated\logs\transcript_full.jsonl
```

使用 `transcript_full.jsonl`，不使用较短的 `transcript.jsonl`。对话标题和工作区 URI 来自：

```text
%USERPROFILE%\.gemini\antigravity\conversation_summaries.db
```

Antigravity 会持续占用这个 SQLite 数据库。服务会先把主数据库以及可能存在的 `-wal`、`-shm` 文件复制到临时目录，再以只读方式打开副本；不会写入 Antigravity 的数据目录。

## 数据口径

- **轮数**：`USER_INPUT` 记录数。
- **步数**：`PLANNER_RESPONSE` 记录数。
- **工具调用次数**：所有 `PLANNER_RESPONSE.tool_calls` 数组中的条目数。
- **Token 总量**：同时统计 `PLANNER_RESPONSE` 和 `CHECKPOINT`。`CHECKPOINT` 是上下文压缩调用，也会消耗 Token。
- Antigravity 的 `input_tokens` 与 `cache_read_tokens` 分开记录，`input_tokens` 不包含缓存读取。因此：

  ```text
  总输入 = input_tokens + cache_read_tokens
  未缓存输入 = input_tokens
  缓存命中率 = cache_read_tokens / (input_tokens + cache_read_tokens)
  Token 总量 = 总输入 + output_tokens
  ```

- **模型用时**：从每轮 `USER_INPUT` 到该轮最后一个事件的墙钟时间，减去其中的工具调用区间，是估算值。仍活跃的最后一轮会计到当前时间，并沿用原项目的 15 分钟空闲保护。
- **工具调用用时**：从 `GENERIC` 工具结果文本中的 `Created At` 和 `Completed At` 行计算；同一轮内重叠的区间会合并。被取消或缺少时间行的工具结果不贡献时长，但仍计入调用次数。
- **TTFT 是近似值**：Antigravity 没有记录首 token 时间，这里用每轮 `USER_INPUT` 到第一个 `PLANNER_RESPONSE` 的时间差。日志时间戳只有秒级精度，因此不足一秒的差值可能显示为 0。
- **输出速度**：输出 Token 总量除以估算的模型用时，不是服务商直接返回的 TPS。
- **当前上下文是估算值**：Antigravity 不提供真实上下文窗口大小，也没有模型名称，本地数据只有 `MODEL_PLACEHOLDER_...` 一类编号。本项目统一按 256,000 Token 估算，并返回 `windowEstimated: true`。
- **`context.used`** 取最近一条 `PLANNER_RESPONSE` 的 `input_tokens + cache_read_tokens`。`CHECKPOINT` 的用量不算作压缩后的上下文；如果压缩后还没有新的 `PLANNER_RESPONSE`，`used` 为 `null`，界面提示“刚压缩，等待下一步”。
- **`context.compactions`** 是本对话的 `CHECKPOINT` 条数。`CHECKPOINT` 的 Token 仍计入总 Token 用量，但不计入上下文 `used`。
- **占用率不截断**：可以超过 100%。现有长对话的压缩前上下文曾达到约 25.4 万到 27.9 万 Token，因此固定估算上限可能低于实际。
- 如需覆盖估算上限，可在插件的 MCP 环境变量中把 `SESSION_STATS_CONTEXT_WINDOW` 设为正整数：

  ```json
  "env": {
    "SESSION_STATS_CONTEXT_WINDOW": "200000"
  }
  ```

  无效值、0 或非整数会被忽略，回退到 256,000。修改后需要重启 MCP 服务或 Antigravity 才会重新读取。
- **运行中 / 空闲**：根据最近 15 分钟是否有新记录推断，不是 Antigravity 的实时任务状态。

Token 统计采用增量读取。第一次读取后，每次只解析新增字节，因此每 3 秒刷新一次也不会反复扫描整个大日志。

## 环境要求

- Google Antigravity 2.x
- Node.js 24 或更高版本，`node` 在 `PATH` 中可用（需要 `node:sqlite`）
- 底部统计条额外需要 Windows 10/11，以及系统自带的 .NET Framework 4.x 编译器

本次实现基于 Antigravity 2.29.0 验证。Antigravity 的日志与插件格式属于内部实现，后续版本可能变化。

## 安装插件

1. 下载项目，并复制到 Antigravity 的用户插件目录，目录名用 `session-stats`：

   ```powershell
   git clone https://github.com/QingYi625/antigravity-session-stats.git
   $dest = Join-Path $env:USERPROFILE '.gemini\config\plugins\session-stats'
   New-Item -ItemType Directory -Path $dest -Force | Out-Null
   Copy-Item -Path .\antigravity-session-stats\* -Destination $dest -Recurse -Force
   ```

2. 在 `%USERPROFILE%\.gemini\config\mcp_config.json` 里注册 MCP 服务。先备份这个文件，再在 `mcpServers` 下加一项 `session-stats`，原有的服务保留不动：

   ```json
   {
     "mcpServers": {
       "session-stats": {
         "command": "node",
         "args": ["C:\\Users\\<你的用户名>\\.gemini\\config\\plugins\\session-stats\\server\\index.mjs"]
       }
     }
   }
   ```

   把 `<你的用户名>` 换成你的 Windows 用户名。如果 Antigravity 找不到 `node`，就改成 `node.exe` 的完整路径。

3. 重启 Antigravity，让它重新读取 MCP 配置。

插件包含：

```text
plugin.json
mcp_config.json
server/index.mjs
server/stats.mjs
server/bar-feed.mjs
ui/panel.html
skills/session-stats/SKILL.md
```

插件自带的 `mcp_config.json` 用的是 `${PLUGIN_ROOT}`。Antigravity 的语言服务器里有这个占位符，但通过它加载本地插件还没有验证过，所以第 2 步直接注册服务是验证过的做法。

新建一个对话，输入“打开会话统计面板”或调用 `session_stats_panel` 工具即可。面板每 3 秒刷新一次，工具的文本结果始终作为不渲染 UI 时的降级内容。

卸载时，从 `mcp_config.json` 里删掉 `session-stats` 这一项，删除 `session-stats` 插件目录，再重启 Antigravity。

## 底部统计条（Windows）

先在本机编译一次：

```bat
bar\build.cmd
```

编译后生成 `bar\AntigravitySessionStatsBar.exe`，双击运行。

首次运行会为当前用户写入 Windows 登录启动项，以便打开 Antigravity 时统计条已经就绪。它平时在后台等待，只在 Antigravity 窗口可见时显示。

- 用 Win32 窗口事件跟随 Antigravity 窗口移动。
- 用 UI Automation 从 Antigravity 的“消息输入框” Edit/ComboBox 控件读取实际位置；如果输入框没有暴露给辅助功能，就按窗口宽度比例估算。
- 同时从 Antigravity 的辅助功能文档标题读取当前对话名，再用侧边栏中匹配的对话链接去掉项目后缀；标题匹配不到时，退回到最近活动的会话。
- 点击任一组摘要会弹出详情，点同一组或其他位置会关闭。
- 背景透明，文字会根据背后像素自动切换深色或浅色。
- 右键可以切换开机自启或退出。

### 开机自启

- **默认开启**：首次运行写入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，值名 `AntigravitySessionStatsBar`。
- **设置位置**：`HKCU\Software\AntigravitySessionStats`。
- **关闭**：右键取消“开机自启”。首次运行也可以加参数：

  ```bat
  bar\AntigravitySessionStatsBar.exe --no-autostart
  ```

- **完全跳过注册表诊断**：再加 `--no-registry`，程序不会读取或写入任何开机自启注册表项。

- **卸载**：取消开机自启、退出统计条、删除目录；需要时再删除 `AntigravitySessionStats` 设置键。

统计条使用独立的注册表值名、设置键、日志文件、可执行文件名和互斥体：

```text
AntigravitySessionStatsBar
HKCU\Software\AntigravitySessionStats
%TEMP%\antigravity-session-stats-bar.log
Local\AntigravitySessionStatsBar
```

因此不会和已经安装的 Codex 版冲突。

## MCP Apps 支持

Antigravity 2.29 的 language server 中存在本面板使用的 MCP Apps 协议字符串和 RPC：

```text
text/html;profile=mcp-app
io.modelcontextprotocol/ui
ui/resourceUri
GetMcpAppResource
LaunchMcpApp
CallMcpAppTool
```

Antigravity 2.29 实际提供的前端 bundle 还包含 MCP Apps AppBridge，并处理 `ui/initialize`、`ui/notifications/tool-input`、`ui/notifications/tool-result`、`ui/notifications/host-context-changed` 和 `ui/resource-teardown`；其协商协议版本正好是 `2026-01-26`，与本面板一致。

因此本项目保留标准 MCP App 资源（`ui://...`，MIME 类型为 `text/html;profile=mcp-app`），同时提供标准 `ui` 元数据和 `ui/resourceUri`。最终是否渲染仍取决于 Antigravity 的版本和 MCP Apps 功能开关；本项目不会强制开启该开关。

如果宿主没有显示资源，`session_stats_panel` 和 `session_stats_refresh` 仍会返回完整的结构化结果和可读文本摘要。

## 项目结构

```text
plugin.json                         Antigravity 插件清单
mcp_config.json                     stdio MCP 服务配置
CHANGELOG.md                        版本更新记录
server/stats.mjs                    解析记录、计算统计
server/index.mjs                    零依赖 stdio MCP 服务
server/bar-feed.mjs                 给底部统计条输出数据
ui/panel.html                       MCP App 面板
skills/session-stats/SKILL.md       让 Antigravity 知道何时打开面板
bar/StatsBar.cs                     底部统计条源码
bar/build.cmd                       统计条编译脚本
```

## 注意事项

- Antigravity 的记录和摘要数据库属于内部实现，不是公开接口。升级后统计可能失效，需要跟着调整字段映射。
- 读取标题依赖 `node:sqlite`，请使用 Node.js 24+。摘要数据库不可用时，解析器仍能读取记录，但标题会退回对话 ID 或工作区目录名。
- 模型名称不可用，256,000 Token 的上下文上限是明确标注的估算值，不是服务商元数据。
- 部分 `GENERIC` 工具结果没有完整的开始/完成时间，此时工具用时是偏低的估算值，但调用次数仍会计入。
- 底部统计条是独立顶层窗口，不是 Antigravity 渲染进程的一部分；快速拖动窗口时可能有轻微延迟。
- Electron 聊天输入框不一定会以 Edit 或 ComboBox 控件暴露给 UI Automation；这种情况下会使用按宽度估算的兜底位置，水平位置可能和真实输入框有偏差。
- 本项目与 Google 无关，不是 Antigravity 官方插件。

## 许可证

[MIT](LICENSE)
