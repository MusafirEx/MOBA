# Setup

## Requirements

- Unity 2022.3.x LTS
- Windows for the built-in ngrok launcher workflow
- ChatGPT account with custom MCP app support
- ngrok Agent installed and authenticated
- Project backup or version control

## Install or update

1. Stop the old bridge and ngrok process.
2. Remove the earlier package in Unity Package Manager.
3. Extract this package outside the Unity project's `Assets` folder.
4. In Package Manager choose **Add package from disk**.
5. Select this package's `package.json`.
6. Open `Window > Wasim Development > Unity MCP Bridge`.

The private token and settings are stored per project in Editor preferences, so upgrading normally keeps the existing MCP URL when the ngrok domain is unchanged.

## Start and verify

1. Connect to a network on which ngrok works.
2. Press **Start Bridge**.
3. Wait for the tunnel to show **Ready**.
4. Press **Run Local MCP Self-Test**.
5. A passing test reports the negotiated MCP protocol and number of tools.
6. Copy the complete **ChatGPT MCP URL** into the ChatGPT app.
7. Refresh or reconnect the app after upgrading so the new tools are discovered.

## Useful prompts

```text
Use the Unity bridge to read the latest Console errors, find the related scripts,
and explain the most likely cause.
```

```text
Use the Unity bridge to recursively inspect my selected GameObject hierarchy and
report missing scripts, missing object references and suspicious component values.
```

```text
Use the Unity bridge to inspect the Animator on my selected object. List its
parameters, layers, states, transitions and animation assets.
```

```text
Use unity_analyze_selected_folder on the folder selected in the Project window.
Include subfolders and summarize scripts, prefabs, Animator Controllers, compiler
messages and external dependencies.
```

## Script proposal workflow

1. Enable **Allow script change proposals** in Unity.
2. Ask ChatGPT to read the entire target script first.
3. Ask it to propose a corrected complete replacement.
4. Review the pending proposal and diff in Unity.
5. Press **Approve and Apply** or **Reject**.

Approved backups are stored in:

```text
Library/WasimUnityMcpBridge/Backups/
```

Because `Library` is normally excluded from source control, keep normal Git or project backups as the primary recovery method.

## Manual ngrok fallback

Disable **Start ngrok with bridge** and run:

```powershell
ngrok http 38421
```

Paste the resulting HTTPS base URL into **HTTPS tunnel base**.

## Troubleshooting

- Socket address already in use: close old bridge/ngrok processes or change the local port.
- Automatic ngrok timeout: confirm the ngrok config works manually and inspect `http://127.0.0.1:4040`.
- Local self-test fails: restart the local server and review the bridge's Last Error.
- ChatGPT app cannot connect while local self-test passes: verify the public URL and ngrok request inspector.
- New tools not visible: refresh, reconnect or recreate the ChatGPT app connection.
- Script proposal rejected as stale: read the script again and create a new proposal using the new SHA-256.

## Clean upgrade

The ZIP contains a versioned folder named `wasim-unity-mcp-bridge-0.4.0`. Remove the previous package in Package Manager, close Unity, and add this exact folder's `package.json`. After reconnecting, refresh the ChatGPT app and verify that `unity_get_status` reports version `0.4.0` and advertises `unity_analyze_selected_folder`.

## After editing C# scripts

Unity reloads Editor assemblies after script compilation. The bridge stops its loopback listener before the reload, keeps ngrok alive, and rebinds the same local port automatically. A brief disconnect is expected, but the ChatGPT app URL should remain unchanged. If the port cannot be rebound immediately, version 0.4.1 retries automatically for up to about six seconds.

