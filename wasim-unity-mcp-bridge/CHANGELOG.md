# Changelog

## 0.4.1 - 2026-06-23

- Fixed local MCP port collisions after Unity recompiles or reloads Editor assemblies.
- Stops the bridge at compilation start, before domain reload, and on Unity quit.
- Cancels the accept loop, closes active client sockets, and waits for the listener task to exit before unloading.
- Preserves the auto-resume flag when compilation has already stopped the listener.
- Adds bounded delayed retries when the previous domain has not released the configured port yet.
- Restarts automatically when compilation fails without an assembly reload.
- Keeps the existing ngrok process alive across script reloads and refreshes its local forwarding state after the bridge rebinds.

## 0.4.0 - 2026-06-23

- Added read-only `unity_analyze_selected_folder`.
- Scans the folder selected in Unity's Project window, optionally including subfolders.
- Returns a bounded asset inventory, extension/type counts and subfolder list.
- Summarizes C# namespaces, declarations, line counts, sizes, hashes, TODO/FIXME counts and duplicate type names.
- Inspects bounded prefab hierarchies for component counts, missing scripts and missing object references.
- Summarizes Animator Controllers and Override Controllers, including parameters, layers, states, transitions, blend trees, motions, clips and missing overrides.
- Includes current compiler messages for scripts inside the selected folder.
- Reports direct dependencies outside the selected folder with reference counts and sample referring assets.
- Adds strict limits and truncation flags so large folders do not return unbounded data.
- Folder analysis remains read-only and is restricted to `Assets/`.

## 0.3.2 - 2026-06-23

- Added `unity_propose_script_text_patch` for small approval-gated code changes without sending an entire source file.
- Requires the supplied `oldText` to match exactly one location, preventing ambiguous edits.
- Retains SHA-256 stale-file protection, Unity-side diff review, explicit approval, backup creation and post-approval compilation.
- Handles CRLF/LF differences while preserving the script's preferred line ending.
- Keeps complete-file `unity_propose_script_replacement` for intentional full rewrites.

## 0.3.1

- Fixed stale/mixed package diagnosis by reporting the running bridge version and advertised tool names from `unity_get_status`.
- Added explicit `unity_inspect_animator_controller` MCP tool and retained `unity_get_animator_info` as a compatibility alias.
- Expanded Animator graph inspection to include nested state machines, default states, Any State transitions, Entry transitions, state-machine transitions, conditions, behaviours, blend trees, motions, animation clips, avatar masks and override mappings.
- Advertises MCP tool-list changes so ChatGPT can refresh the connector catalogue.
- Distributed in a versioned folder to prevent Unity from continuing to reference an older extracted package directory.

## 0.3.0 - 2026-06-23

- Added a connection-health dashboard and local MCP initialize/tools self-test.
- Added `unity_find_script_references`.
- Added recursive `unity_inspect_selected_hierarchy`.
- Added `unity_get_selected_asset_info` with bounded dependency reporting.
- Added `unity_get_animator_info` for controller, avatar, parameters, layers, states and transitions.
- Added SHA-256 values to script reads.
- Added optional `unity_propose_script_replacement` with strict `Assets/**/*.cs` scope, stale-hash protection and no direct write.
- Added Unity-side pending proposal review, diff preview, approval/rejection and automatic backups.
- Added `unity_get_script_change_proposals`.
- Updated MCP server metadata to report the actual package version.
- Direct file writes, deletion, asset renaming, package installation, scene changes and arbitrary commands remain blocked.

## 0.2.2 - 2026-06-23

- Fixed CS0311 in `NgrokManager.cs` on Unity 2022.3 / Newtonsoft.Json 3.2.1.
- Replaced the incompatible generic `DescendantsAndSelf` call with a version-safe iterative token-tree traversal.

## 0.2.1 - 2026-06-23

- Fixed automatic ngrok startup timing out when the public URL was present in ngrok output but unavailable from the local agent API.
- Added public URL detection from both `/api/endpoints`, `/api/tunnels`, and JSON process output.
- Increased the hotspot startup wait period from 30 seconds to 90 seconds.
- Added a clear error when another ngrok agent already owns local inspector port 4040.
- Included the last ngrok output in timeout diagnostics.
- Improved detection of existing tunnels targeting the Unity MCP port.

## 0.2.0 - 2026-06-23

- Added optional automatic ngrok startup when the local MCP bridge starts.
- Added optional automatic ngrok shutdown when the bridge stops or Unity closes.
- Added automatic discovery of the ngrok public HTTPS URL through the local ngrok agent API.
- Added support for a reusable ngrok URL with `--url` so the ChatGPT app address can remain stable.
- Added ngrok status, error output, refresh, inspector, and executable selection controls in the Unity window.
- Kept ngrok running through Unity script recompilation to avoid unnecessary connector interruption.
- Added `unity_inspect_selected_prefab` for bounded recursive inspection of prefab assets and prefab instances.
- Added prefab hierarchy, component properties, missing scripts, missing object references, and property override reporting.
- Moved bridge and request-history UI notifications onto the Unity main thread.
- Remains read-only; no asset, script, scene, or prefab modification tools are included.

## 0.1.2 - 2026-06-23

- Fixed worker-thread access to Unity Editor preferences.
- Fixed request-history repaint calls occurring outside Unity's main thread.
- Improved diagnostics for MCP HTTP 500 responses.

## 0.1.1 - 2026-06-23

- Updated the preferred endpoint route to `/{capability-token}/mcp`.
- Retained compatibility with the earlier route.
- Added MCP endpoint test script and improved setup guidance.

## 0.1.0 - 2026-06-23

- Initial read-only Unity 2022.3 LTS MCP bridge.
