# Antigravity Session Stats (session-stats)

**English** | [简体中文](README.zh-CN.md)

Live statistics for the current Google Antigravity conversation: turns, steps, model and tool time, approximate time to first token, output speed, token cache usage, estimated context usage, and context compactions.

The project has two parts, and either one can be used on its own:

- **Antigravity plugin**: opens an MCP App statistics panel that refreshes every 3 seconds. If an Antigravity build does not render the UI resource, the tool's text result remains available as a fallback.
- **Bottom bar (Windows only)**: a small transparent bar centered below the Antigravity composer. It follows the Antigravity window, opens detail cards when clicked, and can start with Windows.

All data is read locally. Nothing is uploaded.

> The UI labels are in Simplified Chinese. The panel and bar intentionally keep the same layout and labels as the original Codex session-stats project.

## What it shows

| Group | Items |
| --- | --- |
| Session stats | Turns, steps, model time, tool time, approximate average TTFT, output speed (TPS) |
| Token usage | Total tokens, cache hit rate, uncached input, cached input, output |
| Context estimate | Used tokens against a 256,000-token estimate, plus the number of compactions |

The panel marks the context limit as an estimate and never caps usage at 100%.

## Data sources

The parser reads Antigravity's local transcript for each conversation:

```text
%USERPROFILE%\.gemini\antigravity\brain\<conversation-id>\.system_generated\logs\transcript_full.jsonl
```

It uses `transcript_full.jsonl`, not the shorter `transcript.jsonl`. Conversation titles and workspace URIs come from:

```text
%USERPROFILE%\.gemini\antigravity\conversation_summaries.db
```

Antigravity keeps that SQLite database open. The server copies the database (plus any `-wal` and `-shm` files) to a temporary directory before opening it in read-only mode. It never writes to Antigravity's data directory.

## How the numbers are calculated

- **Turns** are the number of `USER_INPUT` records.
- **Steps** are the number of `PLANNER_RESPONSE` records.
- **Tool calls** are the number of entries in all `PLANNER_RESPONSE.tool_calls` arrays.
- **Token totals** include both `PLANNER_RESPONSE` and `CHECKPOINT` records. A checkpoint is a context-compaction call and consumes tokens too.
- Antigravity reports `input_tokens` and `cache_read_tokens` separately; `input_tokens` does not include cache reads. Therefore:

  ```text
  total input = input_tokens + cache_read_tokens
  uncached input = input_tokens
  cache hit rate = cache_read_tokens / (input_tokens + cache_read_tokens)
  total tokens = total input + output_tokens
  ```

- **Model time** is estimated from each user input to the last observed event in that turn, minus the union of tool intervals. A recently active final turn counts up to the current time, with the same 15-minute idle guard used by the original project.
- **Tool time** comes from the `Created At` and `Completed At` lines in `GENERIC` tool-result content. Overlapping intervals in one turn are merged. Some cancelled or malformed tool results do not have both timestamps and therefore contribute no duration, although they are still counted as tool calls.
- **TTFT is approximate.** Antigravity does not log a first-token timestamp. This project measures each `USER_INPUT` to the first following `PLANNER_RESPONSE`. Timestamps have one-second precision, so a sub-second value can appear as zero.
- **Output speed** is total output tokens divided by estimated model time. It is not a provider-reported TPS value.
- **Context usage is estimated.** Antigravity does not expose the real context-window size or model name; the local data only contains placeholder model IDs. This project therefore assumes a 256,000-token limit for every conversation and returns `windowEstimated: true`.
- **Context `used`** is `input_tokens + cache_read_tokens` from the most recent `PLANNER_RESPONSE`. `CHECKPOINT` usage is not treated as the post-compaction context. After a checkpoint and before the next planner response, `used` is `null` and the UI reports that it is waiting for the next step.
- **`compactions`** counts `CHECKPOINT` records. Checkpoint tokens still count toward total token usage, but not toward context `used`.
- **Usage is never clamped.** A value above 100% is valid because the assumed limit may be lower than the real limit; in the available long-conversation history, pre-compaction context reached roughly 254,000 to 279,000 tokens.
- To override the estimate, set `SESSION_STATS_CONTEXT_WINDOW` to a positive integer in the plugin's MCP environment:

  ```json
  "env": {
    "SESSION_STATS_CONTEXT_WINDOW": "200000"
  }
  ```

  Invalid, zero, or non-integer values are ignored and fall back to 256,000. Restart the MCP server or Antigravity after changing the environment so the new value is loaded.
- **Running / idle** is a 15-minute activity heuristic, not a real-time task-state field from Antigravity.

Token accounting is incremental. After the first read, only appended bytes are parsed, so a large transcript does not need to be rescanned every 3 seconds.

## Requirements

- Google Antigravity 2.x
- Node.js 24 or later, with `node` available on `PATH` (`node:sqlite` is used)
- For the bottom bar: Windows 10/11 and the built-in .NET Framework 4.x compiler

The implementation was developed and checked against Antigravity 2.29.0. Antigravity's transcript and plugin formats are internal interfaces and may change in later releases.

## Install the plugin

1. Download the project and copy it into Antigravity's user plugin directory as `session-stats`:

   ```powershell
   git clone https://github.com/QingYi625/antigravity-session-stats.git
   $dest = Join-Path $env:USERPROFILE '.gemini\config\plugins\session-stats'
   New-Item -ItemType Directory -Path $dest -Force | Out-Null
   Copy-Item -Path .\antigravity-session-stats\* -Destination $dest -Recurse -Force
   ```

2. Register the MCP server in `%USERPROFILE%\.gemini\config\mcp_config.json`. Back the file up first, then add a `session-stats` entry under `mcpServers` and keep your existing servers:

   ```json
   {
     "mcpServers": {
       "session-stats": {
         "command": "node",
         "args": ["C:\\Users\\<you>\\.gemini\\config\\plugins\\session-stats\\server\\index.mjs"]
       }
     }
   }
   ```

   Replace `<you>` with your Windows user name. If Antigravity cannot find `node`, use the full path to `node.exe`.

3. Restart Antigravity so it reloads the MCP configuration.

The plugin consists of:

```text
plugin.json
mcp_config.json
server/index.mjs
server/stats.mjs
server/bar-feed.mjs
ui/panel.html
skills/session-stats/SKILL.md
```

The plugin's own `mcp_config.json` uses `${PLUGIN_ROOT}`. The Antigravity language server contains that placeholder, but loading local plugins through it has not been verified, so registering the server directly in step 2 is the tested path.

Start a new conversation and ask to open the session statistics panel, or invoke the `session_stats_panel` tool. The panel refreshes every 3 seconds and always keeps the tool's text result as a non-UI fallback.

To uninstall, remove the `session-stats` entry from `mcp_config.json`, delete the `session-stats` plugin directory, and restart Antigravity.

## Bottom bar (Windows)

Compile the bar once:

```bat
bar\build.cmd
```

This creates `bar\AntigravitySessionStatsBar.exe`. Double-click the executable to run it.

On first run it registers itself for the current user's Windows sign-in, so it can be ready when Antigravity opens. The bar waits in the background and appears only while the Antigravity window is visible.

- It follows the Antigravity window using Win32 window events.
- UI Automation is used to read the actual composer position from Antigravity's `消息输入框` control (Edit or ComboBox). If the composer is not exposed, the bar falls back to an estimate based on the window width.
- It also reads the active conversation title from Antigravity's accessible document title and trims project suffixes using the matching sidebar hyperlink. If no title matches a known conversation, it follows the most recently active transcript.
- Click either summary to open its detail card. Click the same summary again or anywhere else to close it.
- The background is transparent and the text switches between dark and light based on the pixels behind the bar.
- Right-click to toggle start-on-sign-in or quit.

### Start on sign-in

- **Default:** the first run writes the executable path to `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `AntigravitySessionStatsBar`.
- **Settings:** the user's choice is stored at `HKCU\Software\AntigravitySessionStats`.
- **Disable:** right-click the bar and uncheck 开机自启. To disable it for the first run, pass:

  ```bat
  bar\AntigravitySessionStatsBar.exe --no-autostart
  ```

- **Registry-free diagnostic run:** add `--no-registry`; this skips every startup-registry read and write.

- **Uninstall:** uncheck 开机自启, quit the bar, delete the folder, then remove the `AntigravitySessionStats` settings key if desired.

The bar uses its own registry value, settings key, log filename, executable name, and mutex:

```text
AntigravitySessionStatsBar
HKCU\Software\AntigravitySessionStats
%TEMP%\antigravity-session-stats-bar.log
Local\AntigravitySessionStatsBar
```

This keeps it independent from any installed Codex version of the bar.

## MCP Apps support

Antigravity 2.29's language server contains the MCP Apps protocol strings and RPCs used by this panel:

```text
text/html;profile=mcp-app
io.modelcontextprotocol/ui
ui/resourceUri
GetMcpAppResource
LaunchMcpApp
CallMcpAppTool
```

The frontend bundle served by Antigravity 2.29 also contains the MCP Apps AppBridge and handles `ui/initialize`, `ui/notifications/tool-input`, `ui/notifications/tool-result`, `ui/notifications/host-context-changed`, and `ui/resource-teardown`. It negotiates protocol `2026-01-26`, which is the version used by this panel.

The panel therefore keeps the standard MCP App resource (`ui://...` with MIME type `text/html;profile=mcp-app`). The backend advertises both the standard `ui` metadata and `ui/resourceUri`. Runtime rendering still depends on the Antigravity build and its MCP Apps feature flag; this repository does not force-enable that flag.

If the host does not display the resource, `session_stats_panel` and `session_stats_refresh` still return the complete structured result and a readable text summary.

## Project layout

```text
plugin.json                         Antigravity plugin manifest
mcp_config.json                     Stdio MCP server configuration
CHANGELOG.md                        Release history
server/stats.mjs                    Transcript parser and statistics
server/index.mjs                    Zero-dependency stdio MCP server
server/bar-feed.mjs                 Feeds data to the bottom bar
ui/panel.html                       MCP App panel
skills/session-stats/SKILL.md       Tells Antigravity when to open the panel
bar/StatsBar.cs                     Bottom bar source
bar/build.cmd                       Bottom bar build script
```

## Caveats

- Antigravity's transcript and summary database are internal implementation details, not a public API. A future Antigravity update can break the parser until the field mapping is updated.
- `node:sqlite` is required to read conversation titles. Use Node.js 24+; the parser can still read transcripts if the summary database is unavailable, but titles fall back to conversation IDs or workspace names.
- The selected model is unavailable, and the 256,000-token context limit is an explicit estimate rather than provider metadata.
- Antigravity's `GENERIC` tool results do not always contain complete timing lines. Tool time is therefore a lower-bound estimate in such conversations.
- The bottom bar is a separate top-level window, not part of Antigravity's renderer. It can lag slightly while the window is being dragged.
- A native Electron chat composer may not expose an Edit or ComboBox control through UI Automation. In that case the width-based fallback is used, and its horizontal position may differ from the true composer.
- This project is not affiliated with Google and is not an official Antigravity plugin.

## License

[MIT](LICENSE)
