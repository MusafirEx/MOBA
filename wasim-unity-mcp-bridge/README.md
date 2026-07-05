# Wasim Unity MCP Bridge

A Unity 2022.3 LTS Editor MCP bridge that lets a connected ChatGPT app inspect the current project and submit approval-gated C# script replacements without placing an OpenAI API key in Unity.

## Script recompilation and domain reloads

Version 0.4.1 shuts down the local listener before Unity reloads Editor assemblies, closes active MCP sockets, waits briefly for the accept loop to exit, and retries the configured port after reload. The owned ngrok process remains running, so the public MCP URL normally stays unchanged while Unity recompiles scripts.


## Version 0.4.0 highlights

- Connection-health dashboard and local MCP self-test.
- Automatic ngrok launch, URL detection and shutdown remain supported.
- Selected-hierarchy, selected-asset and Animator inspection tools.
- New bounded selected-folder analysis for scripts, prefabs, Animator Controllers, compiler messages and external dependencies.
- New script reference finder.
- `unity_read_script` now returns a SHA-256 hash.
- Optional complete-file replacement proposals and precise `oldText → newText` patch proposals for existing `Assets/**/*.cs` files.
- Every script proposal requires explicit review and approval inside Unity.
- A backup is created under `Library/WasimUnityMcpBridge/Backups` before an approved write.
- No direct edit, delete, rename, package installation, scene write, prefab write or arbitrary command MCP tools.

## Open the window

`Window > Wasim Development > Unity MCP Bridge`

## Recommended daily setup

1. Install and authenticate ngrok once.
2. Leave **Start ngrok with bridge** enabled.
3. Set **ngrok executable** to `ngrok`, or browse to `ngrok.exe`.
4. Press **Start Bridge**.
5. Run **Local MCP Self-Test**.
6. Use the complete **ChatGPT MCP URL** in the ChatGPT app connection.

## Inspection tools

- `unity_get_status`
- `unity_get_project_info`
- `unity_get_compilation_status`
- `unity_read_console`
- `unity_read_editor_log`
- `unity_search_scripts`
- `unity_find_script_references`
- `unity_read_script`
- `unity_get_selected_script`
- `unity_get_active_scene`
- `unity_inspect_selected_object`
- `unity_inspect_selected_hierarchy`
- `unity_inspect_selected_prefab`
- `unity_get_selected_asset_info`
- `unity_analyze_selected_folder`
- `unity_get_animator_info`
- `unity_get_packages`
- `unity_get_script_change_proposals`
- `unity_propose_script_text_patch`

## Approval-gated script changes

Script proposals are disabled by default. Enable **Allow script change proposals** in the Unity bridge window.

The safe workflow is:

1. ChatGPT reads the relevant current script lines and its SHA-256.
2. For a small change, ChatGPT calls `unity_propose_script_text_patch` with one uniquely matching old block and its replacement. For a full rewrite, it may call `unity_propose_script_replacement`.
3. Unity stores the proposal under `Library`; the script in `Assets` remains unchanged.
4. The Unity window shows a diff preview.
5. The user approves or rejects it.
6. On approval, Unity verifies the original hash again, creates a backup, writes the file, imports it and requests compilation.

The MCP tool itself cannot approve its own proposal.

## Security

- The MCP server binds only to `127.0.0.1`.
- The complete MCP URL contains a private capability token and must be treated as a password.
- Automatic launch is restricted to `ngrok`/`ngrok.exe`.
- Script proposals only target existing `Assets/**/*.cs` files.
- Package scripts can be read only when explicitly enabled and cannot be changed through the proposal workflow.
- No arbitrary shell or PowerShell execution is included.
- Stop the bridge when it is not being used.

## Network note

A mobile hotspot or another network that permits ngrok TLS tunnelling may be required. The plug-in does not bypass organisational firewalls, proxies or security controls.

## Verify the installed version

After installing, call `unity_get_status`. Version 0.4.0 reports:

- `bridgeVersion`: `0.4.0`
- `advertisedToolCount`
- `advertisedTools`

The tool list must include `unity_inspect_animator_controller`. If it does not, Unity is still referencing an older package directory. Remove the package, close Unity, delete the old extracted package folder, then add this versioned folder's `package.json`.

## Animator Controller graph inspection

Select either a GameObject with an Animator, an Animator Controller asset, or an Animator Override Controller asset, then call `unity_inspect_animator_controller`. It returns parameters, layers, nested state machines, states, Any State and Entry transitions, conditions, blend trees, motions, animation clips, behaviours, avatar masks, and override mappings.

## Selected-folder analysis

Select an `Assets` folder in Unity's Project window and call `unity_analyze_selected_folder`. The tool can include subfolders and returns bounded summaries for scripts, prefabs and Animator Controllers, current compiler messages in that folder, and direct dependencies outside the folder. It never modifies assets.

Suggested prompt:

```text
Use unity_analyze_selected_folder on the folder selected in Unity. Include subfolders,
analyze scripts, prefabs and Animator Controllers, then summarize the most important
issues and tell me which files I should inspect manually first.
```
