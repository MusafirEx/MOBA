using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WasimDevelopment.UnityMcpBridge
{
    internal sealed class BridgeWindow : EditorWindow
    {
        private Vector2 _scroll;
        private bool _showHistory = true;
        private bool _showAdvancedTunnel;
        private bool _showPendingChanges = true;
        private string _operationMessage = string.Empty;
        private MessageType _operationMessageType = MessageType.None;

        [MenuItem("Window/Wasim Development/Unity MCP Bridge")]
        public static void Open()
        {
            GetWindow<BridgeWindow>("Unity MCP Bridge");
        }

        private void OnEnable()
        {
            minSize = new Vector2(600f, 650f);
            McpHttpServer.StateChanged += QueueRepaint;
            NgrokManager.StateChanged += QueueRepaint;
            BridgeRequestHistory.Changed += QueueRepaint;
            BridgeSelfTest.Changed += QueueRepaint;
            ScriptChangeManager.Changed += QueueRepaint;
        }

        private void OnDisable()
        {
            McpHttpServer.StateChanged -= QueueRepaint;
            NgrokManager.StateChanged -= QueueRepaint;
            BridgeRequestHistory.Changed -= QueueRepaint;
            BridgeSelfTest.Changed -= QueueRepaint;
            ScriptChangeManager.Changed -= QueueRepaint;
        }

        private void QueueRepaint()
        {
            Repaint();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawHeader();
            EditorGUILayout.Space(8);
            DrawConnectionHealth();
            EditorGUILayout.Space(10);
            DrawServerControls();
            EditorGUILayout.Space(10);
            DrawNgrokControls();
            EditorGUILayout.Space(10);
            DrawEndpoint();
            EditorGUILayout.Space(10);
            DrawPermissions();
            EditorGUILayout.Space(10);
            DrawPendingChanges();
            EditorGUILayout.Space(10);
            DrawHistory();
            EditorGUILayout.EndScrollView();
        }

        private static void DrawHeader()
        {
            EditorGUILayout.LabelField(BridgeVersion.DisplayName + "  v" + BridgeVersion.PackageVersion, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Unity 2022.3 LTS MCP bridge with automatic ngrok, richer project inspection and approval-gated C# script replacement. " +
                "ChatGPT can submit a proposal, but only an explicit approval in this Unity window can write to Assets.",
                MessageType.Info);
        }

        private void DrawConnectionHealth()
        {
            EditorGUILayout.LabelField("Connection health", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Local server", McpHttpServer.IsRunning ? "Running" : "Stopped");
            EditorGUILayout.LabelField("HTTPS tunnel", string.IsNullOrWhiteSpace(NgrokManager.PublicUrl) ? NgrokManager.State.ToString() : "Ready");

            DateTime? lastRequest = BridgeRequestHistory.LastRequestUtc;
            string activity = lastRequest.HasValue
                ? FormatAge(DateTime.UtcNow - lastRequest.Value) + " ago"
                : "No MCP requests received yet";
            EditorGUILayout.LabelField("Last MCP activity", activity);
            EditorGUILayout.LabelField("Pending script proposals", ScriptChangeManager.PendingCount.ToString());

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(!McpHttpServer.IsRunning || BridgeSelfTest.State == BridgeSelfTestState.Running);
            if (GUILayout.Button(BridgeSelfTest.State == BridgeSelfTestState.Running ? "Testing…" : "Run Local MCP Self-Test"))
                BridgeSelfTest.Run();
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("Refresh ngrok URL")) NgrokManager.RefreshFromAgent();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(BridgeSelfTest.LastResult))
            {
                MessageType type = BridgeSelfTest.State == BridgeSelfTestState.Passed
                    ? MessageType.Info
                    : BridgeSelfTest.State == BridgeSelfTestState.Failed ? MessageType.Error : MessageType.None;
                EditorGUILayout.HelpBox(BridgeSelfTest.LastResult, type);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawServerControls()
        {
            EditorGUILayout.LabelField("Local MCP server", EditorStyles.boldLabel);
            bool running = McpHttpServer.IsRunning;
            EditorGUILayout.LabelField("Status", running ? "Running" : "Stopped");
            if (!string.IsNullOrWhiteSpace(McpHttpServer.LastError))
                EditorGUILayout.HelpBox(McpHttpServer.LastError, MessageType.Error);

            EditorGUI.BeginDisabledGroup(running);
            int newPort = EditorGUILayout.IntField("Port", BridgePreferences.Port);
            newPort = Mathf.Clamp(newPort, 1024, 65535);
            if (newPort != BridgePreferences.Port) BridgePreferences.Port = newPort;
            EditorGUI.EndDisabledGroup();

            bool autoStart = EditorGUILayout.Toggle("Start bridge with Unity", BridgePreferences.AutoStart);
            if (autoStart != BridgePreferences.AutoStart) BridgePreferences.AutoStart = autoStart;

            EditorGUILayout.BeginHorizontal();
            if (!running)
            {
                if (GUILayout.Button("Start Bridge", GUILayout.Height(30))) BridgeBootstrap.StartRequested();
            }
            else
            {
                if (GUILayout.Button("Stop Bridge", GUILayout.Height(30))) BridgeBootstrap.StopRequested();
                if (GUILayout.Button("Restart Local Server", GUILayout.Height(30))) BridgeBootstrap.RestartRequested();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNgrokControls()
        {
            EditorGUILayout.LabelField("ngrok HTTPS tunnel", EditorStyles.boldLabel);
            bool autoStart = EditorGUILayout.Toggle("Start ngrok with bridge", BridgePreferences.AutoStartNgrok);
            if (autoStart != BridgePreferences.AutoStartNgrok) BridgePreferences.AutoStartNgrok = autoStart;

            bool stopWithBridge = EditorGUILayout.Toggle("Stop ngrok with bridge", BridgePreferences.StopNgrokWithBridge);
            if (stopWithBridge != BridgePreferences.StopNgrokWithBridge) BridgePreferences.StopNgrokWithBridge = stopWithBridge;

            string state = NgrokManager.State.ToString();
            if (NgrokManager.OwnsProcess) state += " (managed by Unity)";
            else if (NgrokManager.IsRunning) state += " (external ngrok)";
            EditorGUILayout.LabelField("Status", state);

            if (!string.IsNullOrWhiteSpace(NgrokManager.PublicUrl))
                DrawCopyableField("Detected HTTPS URL", NgrokManager.PublicUrl, NgrokManager.PublicUrl);

            if (!string.IsNullOrWhiteSpace(NgrokManager.LastError))
                EditorGUILayout.HelpBox(NgrokManager.LastError, MessageType.Error);

            _showAdvancedTunnel = EditorGUILayout.Foldout(_showAdvancedTunnel, "ngrok settings", true);
            if (_showAdvancedTunnel)
            {
                EditorGUI.BeginDisabledGroup(NgrokManager.IsRunning || NgrokManager.IsStarting);
                EditorGUILayout.BeginHorizontal();
                string executable = EditorGUILayout.TextField("ngrok executable", BridgePreferences.NgrokExecutablePath);
                if (executable != BridgePreferences.NgrokExecutablePath) BridgePreferences.NgrokExecutablePath = executable;
                if (GUILayout.Button("Browse", GUILayout.Width(70)))
                {
                    string selected = EditorUtility.OpenFilePanel("Select ngrok.exe", string.Empty, "exe");
                    if (!string.IsNullOrWhiteSpace(selected)) BridgePreferences.NgrokExecutablePath = selected;
                }
                EditorGUILayout.EndHorizontal();

                string stableUrl = EditorGUILayout.TextField(
                    new GUIContent("Optional stable URL", "Example: https://your-domain.ngrok.app. Leave blank for a random URL."),
                    BridgePreferences.NgrokPublicUrl);
                if (stableUrl != BridgePreferences.NgrokPublicUrl) BridgePreferences.NgrokPublicUrl = stableUrl;
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.HelpBox(
                    "A stable ngrok URL prevents the ChatGPT app address from changing between sessions. " +
                    "The URL must already belong to your ngrok account.", MessageType.None);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(!McpHttpServer.IsRunning || NgrokManager.IsRunning || NgrokManager.IsStarting);
            if (GUILayout.Button("Start ngrok", GUILayout.Height(27))) NgrokManager.Start();
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!NgrokManager.IsRunning && !NgrokManager.IsStarting && NgrokManager.State != NgrokConnectionState.Error);
            if (GUILayout.Button("Stop ngrok", GUILayout.Height(27))) NgrokManager.Stop();
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Refresh URL", GUILayout.Height(27))) NgrokManager.RefreshFromAgent();
            if (GUILayout.Button("Open Inspector", GUILayout.Height(27))) Application.OpenURL("http://127.0.0.1:4040");
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(NgrokManager.LastOutput))
                EditorGUILayout.LabelField("Last ngrok output", NgrokManager.LastOutput, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.HelpBox(
                "The automatic launcher only starts the configured ngrok executable. It does not run PowerShell or arbitrary commands.",
                MessageType.None);
        }

        private void DrawEndpoint()
        {
            EditorGUILayout.LabelField("ChatGPT connection", EditorStyles.boldLabel);
            bool reveal = EditorGUILayout.Toggle("Show private URL", BridgePreferences.RevealPrivateEndpoint);
            if (reveal != BridgePreferences.RevealPrivateEndpoint) BridgePreferences.RevealPrivateEndpoint = reveal;

            string local = BridgePreferences.LocalEndpoint;
            DrawCopyableField("Local MCP URL", reveal ? local : MaskEndpoint(local), local);

            string tunnelBase = EditorGUILayout.TextField(
                new GUIContent("HTTPS tunnel base", "Filled automatically when ngrok starts; may also be entered manually."),
                BridgePreferences.TunnelBaseUrl);
            if (tunnelBase != BridgePreferences.TunnelBaseUrl) BridgePreferences.TunnelBaseUrl = tunnelBase;

            if (!string.IsNullOrWhiteSpace(BridgePreferences.TunnelBaseUrl))
            {
                string publicEndpoint = BridgePreferences.PublicEndpoint;
                bool https = Uri.TryCreate(publicEndpoint, UriKind.Absolute, out Uri uri) && uri.Scheme == Uri.UriSchemeHttps;
                DrawCopyableField("ChatGPT MCP URL", reveal ? publicEndpoint : MaskEndpoint(publicEndpoint), publicEndpoint);
                if (!https) EditorGUILayout.HelpBox("The public MCP endpoint must use HTTPS.", MessageType.Warning);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Regenerate private token", GUILayout.Width(190)))
            {
                if (EditorUtility.DisplayDialog("Regenerate private token?",
                        "Existing MCP URLs will stop working immediately.", "Regenerate", "Cancel"))
                {
                    BridgePreferences.RegenerateCapabilityToken();
                    Repaint();
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Treat the complete MCP URL as a password. Do not commit or publish it.", MessageType.Warning);
        }

        private static void DrawPermissions()
        {
            EditorGUILayout.LabelField("Permissions", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Read Console, scenes, selections and assets", "Allowed");
            EditorGUILayout.LabelField("Read Assets C# scripts", "Allowed");

            bool packageScripts = EditorGUILayout.Toggle("Read Packages C# scripts", BridgePreferences.AllowPackageScripts);
            if (packageScripts != BridgePreferences.AllowPackageScripts) BridgePreferences.AllowPackageScripts = packageScripts;

            bool proposals = EditorGUILayout.Toggle(
                new GUIContent("Allow script change proposals", "Allows ChatGPT to submit a complete replacement for an existing Assets/**/*.cs file. Unity approval is still mandatory."),
                BridgePreferences.EnableScriptChangeProposals);
            if (proposals != BridgePreferences.EnableScriptChangeProposals)
            {
                if (proposals)
                {
                    proposals = EditorUtility.DisplayDialog(
                        "Enable script change proposals?",
                        "ChatGPT may submit proposed C# replacements. No script is written until you inspect and approve it in Unity. Backups are stored under Library/WasimUnityMcpBridge/Backups.",
                        "Enable", "Cancel");
                }
                BridgePreferences.EnableScriptChangeProposals = proposals;
            }

            EditorGUILayout.LabelField("Direct or automatic file writes", "Blocked");
            EditorGUILayout.LabelField("Delete files / arbitrary commands", "Blocked");
        }

        private void DrawPendingChanges()
        {
            ScriptChangeProposal[] pending = ScriptChangeManager.Snapshot().Where(p => p.Status == "Pending").ToArray();
            _showPendingChanges = EditorGUILayout.Foldout(_showPendingChanges, "Pending script changes (" + pending.Length + ")", true);
            if (!_showPendingChanges) return;

            if (!string.IsNullOrWhiteSpace(_operationMessage))
                EditorGUILayout.HelpBox(_operationMessage, _operationMessageType);

            if (pending.Length == 0)
            {
                EditorGUILayout.LabelField("No pending script proposals.", EditorStyles.miniLabel);
                return;
            }

            foreach (ScriptChangeProposal proposal in pending.Take(10))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(proposal.Path, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(proposal.Summary) ? "No summary supplied." : proposal.Summary, EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("Proposal " + proposal.Id + " • " + ParseDate(proposal.CreatedUtc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Original SHA-256", proposal.OriginalSha256, EditorStyles.miniLabel);

                string preview = ScriptChangeManager.GetDiffPreview(proposal);
                EditorGUILayout.LabelField("Diff preview", EditorStyles.miniBoldLabel);
                EditorGUILayout.TextArea(preview, GUILayout.MinHeight(120), GUILayout.MaxHeight(260));

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Open Current Script"))
                {
                    MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(proposal.Path);
                    if (script != null)
                    {
                        Selection.activeObject = script;
                        EditorGUIUtility.PingObject(script);
                    }
                }
                if (GUILayout.Button("Reject"))
                {
                    ScriptChangeManager.Reject(proposal.Id, out _operationMessage);
                    _operationMessageType = MessageType.Warning;
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button("Approve and Apply"))
                {
                    bool confirmed = EditorUtility.DisplayDialog(
                        "Apply proposed script replacement?",
                        proposal.Path + "\n\nA backup will be created before writing. Unity will then recompile scripts.",
                        "Apply", "Cancel");
                    if (confirmed)
                    {
                        bool applied = ScriptChangeManager.Approve(proposal.Id, out _operationMessage);
                        _operationMessageType = applied ? MessageType.Info : MessageType.Error;
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawHistory()
        {
            _showHistory = EditorGUILayout.Foldout(_showHistory, "Recent MCP requests", true);
            if (!_showHistory) return;

            BridgeRequestRecord[] records = BridgeRequestHistory.Snapshot();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(records.Length + " recorded", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", GUILayout.Width(70))) BridgeRequestHistory.Clear();
            EditorGUILayout.EndHorizontal();

            int shown = Mathf.Min(records.Length, 30);
            for (int i = 0; i < shown; i++)
            {
                BridgeRequestRecord record = records[i];
                string label = record.TimeUtc.ToLocalTime().ToString("HH:mm:ss") + "  "
                               + (record.Success ? "✓" : "✕") + "  "
                               + (string.IsNullOrEmpty(record.Tool) ? record.Method : record.Tool);
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
                if (!string.IsNullOrWhiteSpace(record.Detail))
                    EditorGUILayout.LabelField("    " + record.Detail, EditorStyles.miniLabel);
            }
            if (records.Length == 0) EditorGUILayout.LabelField("No requests yet.", EditorStyles.miniLabel);
        }

        private static void DrawCopyableField(string label, string displayed, string copyValue)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            EditorGUILayout.SelectableLabel(displayed, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUILayout.Button("Copy", GUILayout.Width(55))) GUIUtility.systemCopyBuffer = copyValue;
            EditorGUILayout.EndHorizontal();
        }

        private static string MaskEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) return string.Empty;
            string token = BridgePreferences.CapabilityToken;
            return endpoint.Replace("/" + token + "/mcp", "/••••••••••••••••/mcp")
                           .Replace("/mcp/" + token, "/mcp/••••••••••••••••");
        }

        private static string FormatAge(TimeSpan age)
        {
            if (age.TotalSeconds < 5) return "just now";
            if (age.TotalMinutes < 1) return Mathf.FloorToInt((float)age.TotalSeconds) + " seconds";
            if (age.TotalHours < 1) return Mathf.FloorToInt((float)age.TotalMinutes) + " minutes";
            if (age.TotalDays < 1) return Mathf.FloorToInt((float)age.TotalHours) + " hours";
            return Mathf.FloorToInt((float)age.TotalDays) + " days";
        }

        private static DateTime ParseDate(string value)
        {
            return DateTime.TryParse(value, out DateTime parsed) ? parsed : DateTime.UtcNow;
        }
    }
}
