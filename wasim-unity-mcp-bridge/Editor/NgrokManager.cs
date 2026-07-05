using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace WasimDevelopment.UnityMcpBridge
{
    internal enum NgrokConnectionState
    {
        Stopped,
        Starting,
        Running,
        Error
    }

    internal static class NgrokManager
    {
        private const string OwnedProcessSessionKey = "WasimUnityMcpBridge.NgrokOwnedProcessId";
        private const string AgentApiBaseUrl = "http://127.0.0.1:4040/api";
        private const int PollDelayMilliseconds = 500;
        private const int MaximumPollAttempts = 180; // 90 seconds for slower hotspot connections.

        private static readonly object Gate = new object();
        private static Process _process;
        private static CancellationTokenSource _pollCancellation;
        private static NgrokConnectionState _state = NgrokConnectionState.Stopped;
        private static string _publicUrl = string.Empty;
        private static string _lastError = string.Empty;
        private static string _lastOutput = string.Empty;
        private static bool _ownsProcess;

        public static event Action StateChanged;

        public static NgrokConnectionState State
        {
            get { lock (Gate) return _state; }
        }

        public static bool IsRunning => State == NgrokConnectionState.Running;
        public static bool IsStarting => State == NgrokConnectionState.Starting;

        public static string PublicUrl
        {
            get { lock (Gate) return _publicUrl; }
        }

        public static string LastError
        {
            get { lock (Gate) return _lastError; }
        }

        public static string LastOutput
        {
            get { lock (Gate) return _lastOutput; }
        }

        public static bool OwnsProcess
        {
            get { lock (Gate) return _ownsProcess; }
        }

        public static void Start()
        {
            if (!McpHttpServer.IsRunning)
            {
                SetError("Start the local MCP bridge before starting ngrok.");
                return;
            }

            if (IsRunning || IsStarting) return;
            SetState(NgrokConnectionState.Starting, string.Empty, string.Empty, false);

            int localPort = BridgePreferences.Port;
            AgentProbeResult existingAgent = ProbeAgentApi(500, localPort);
            if (!string.IsNullOrWhiteSpace(existingAgent.PublicUrl))
            {
                AttachToExistingProcessIfOwned();
                SetRunning(existingAgent.PublicUrl, OwnsProcess);
                return;
            }

            if (existingAgent.AgentReachable)
            {
                SetError(
                    "Another ngrok agent is already using its local inspector at 127.0.0.1:4040, " +
                    "but it has no HTTPS tunnel for Unity MCP port " + localPort + ". " +
                    "Stop the other ngrok process, then press Start ngrok again.");
                return;
            }

            string executable = ResolveExecutable(BridgePreferences.NgrokExecutablePath);
            if (string.IsNullOrWhiteSpace(executable))
            {
                SetError("ngrok.exe was not found. Browse to ngrok.exe or add ngrok to the Windows PATH.");
                return;
            }

            try
            {
                string arguments = BuildArguments();
                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                if (Path.IsPathRooted(executable))
                {
                    string directory = Path.GetDirectoryName(executable);
                    if (!string.IsNullOrWhiteSpace(directory)) startInfo.WorkingDirectory = directory;
                }

                var process = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };
                process.OutputDataReceived += OnProcessOutput;
                process.ErrorDataReceived += OnProcessOutput;
                process.Exited += OnProcessExited;

                if (!process.Start())
                    throw new InvalidOperationException("Windows did not start ngrok.exe.");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                lock (Gate)
                {
                    _process = process;
                    _ownsProcess = true;
                }
                SessionState.SetInt(OwnedProcessSessionKey, process.Id);
                StartPolling(process, localPort);
            }
            catch (Exception ex)
            {
                SetError(ex.GetType().Name + ": " + ex.Message);
            }
        }

        public static void Stop()
        {
            AttachToExistingProcessIfOwned();
            CancellationTokenSource cancellation;
            Process process;
            bool owns;
            lock (Gate)
            {
                cancellation = _pollCancellation;
                _pollCancellation = null;
                process = _process;
                owns = _ownsProcess;
                _process = null;
                _ownsProcess = false;
            }

            try { cancellation?.Cancel(); } catch { }

            if (owns && process != null)
            {
                try
                {
                    if (!process.HasExited) process.Kill();
                }
                catch { }
                try { process.Dispose(); } catch { }
            }

            SessionState.SetInt(OwnedProcessSessionKey, 0);
            SetState(NgrokConnectionState.Stopped, string.Empty, string.Empty, false);
        }

        public static void RefreshFromAgent()
        {
            AgentProbeResult result = ProbeAgentApi(1000, BridgePreferences.Port);
            if (string.IsNullOrWhiteSpace(result.PublicUrl))
            {
                if (result.AgentReachable)
                {
                    SetError("ngrok is running, but no HTTPS tunnel targets Unity MCP port " + BridgePreferences.Port + ".");
                }
                else if (!IsStarting)
                {
                    SetError("No active ngrok agent API was found at 127.0.0.1:4040.");
                }
                return;
            }

            AttachToExistingProcessIfOwned();
            SetRunning(result.PublicUrl, OwnsProcess);
        }

        public static void StopForUnityQuit()
        {
            if (BridgePreferences.StopNgrokWithBridge) Stop();
        }

        private static string BuildArguments()
        {
            string target = "http://127.0.0.1:" + BridgePreferences.Port;
            string arguments = "http " + Quote(target) + " --log=stdout --log-format=json";
            string publicUrl = BridgePreferences.NgrokPublicUrl;
            if (!string.IsNullOrWhiteSpace(publicUrl))
                arguments += " --url " + Quote(publicUrl);
            return arguments;
        }

        private static void StartPolling(Process process, int localPort)
        {
            CancellationTokenSource cancellation = new CancellationTokenSource();
            lock (Gate)
            {
                try { _pollCancellation?.Cancel(); } catch { }
                _pollCancellation = cancellation;
            }

            Task.Run(() =>
            {
                for (int attempt = 0; attempt < MaximumPollAttempts && !cancellation.IsCancellationRequested; attempt++)
                {
                    if (IsRunning) return;

                    AgentProbeResult result = ProbeAgentApi(700, localPort);
                    if (!string.IsNullOrWhiteSpace(result.PublicUrl))
                    {
                        MainThreadDispatcher.Post(() => SetRunning(result.PublicUrl, true));
                        return;
                    }

                    try
                    {
                        if (process.HasExited)
                        {
                            string output = LastOutput;
                            MainThreadDispatcher.Post(() => SetError(
                                string.IsNullOrWhiteSpace(output)
                                    ? "ngrok exited before creating a tunnel. Run 'ngrok diagnose' in PowerShell."
                                    : "ngrok exited before creating a tunnel: " + output));
                            return;
                        }
                    }
                    catch { }

                    Thread.Sleep(PollDelayMilliseconds);
                }

                if (!cancellation.IsCancellationRequested && !IsRunning)
                {
                    string output = LastOutput;
                    MainThreadDispatcher.Post(() => SetError(
                        "Timed out while waiting for ngrok to create an HTTPS tunnel." +
                        (string.IsNullOrWhiteSpace(output) ? string.Empty : " Last ngrok output: " + output) +
                        " Confirm the mobile hotspot is active, then press Stop ngrok and Start ngrok."));
                }
            }, cancellation.Token);
        }

        private static void OnProcessOutput(object sender, DataReceivedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.Data)) return;
            string line = args.Data.Trim();
            lock (Gate)
            {
                _lastOutput = line.Length <= 2000 ? line : line.Substring(0, 2000);
            }

            string detectedUrl = TryExtractPublicUrlFromLog(line);
            if (!string.IsNullOrWhiteSpace(detectedUrl))
            {
                MainThreadDispatcher.Post(() => SetRunning(detectedUrl, true));
            }
            else
            {
                RaiseStateChanged();
            }
        }

        private static string TryExtractPublicUrlFromLog(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return string.Empty;

            try
            {
                JToken token = JToken.Parse(line);
                foreach (JValue value in EnumerateTokenTree(token).OfType<JValue>())
                {
                    if (value.Type != JTokenType.String) continue;
                    string candidate = value.Value<string>();
                    if (IsNgrokHttpsUrl(candidate)) return candidate;
                }
            }
            catch
            {
                // Some agent versions may emit a non-JSON startup line despite the requested format.
            }

            int start = line.IndexOf("https://", StringComparison.OrdinalIgnoreCase);
            while (start >= 0)
            {
                int end = start;
                while (end < line.Length && !char.IsWhiteSpace(line[end]) && line[end] != '"' && line[end] != '\'' && line[end] != ',') end++;
                string candidate = line.Substring(start, end - start).TrimEnd('}', ']', ')', '.', ';');
                if (IsNgrokHttpsUrl(candidate)) return candidate;
                start = line.IndexOf("https://", end, StringComparison.OrdinalIgnoreCase);
            }

            return string.Empty;
        }

        private static IEnumerable<JToken> EnumerateTokenTree(JToken root)
        {
            if (root == null) yield break;

            var pending = new Stack<JToken>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                JToken current = pending.Pop();
                yield return current;

                JContainer container = current as JContainer;
                if (container == null) continue;

                IList<JToken> children = container.Children().ToList();
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    pending.Push(children[i]);
                }
            }
        }

        private static bool IsNgrokHttpsUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri)) return false;
            if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;
            string host = uri.Host ?? string.Empty;
            return host.EndsWith(".ngrok.app", StringComparison.OrdinalIgnoreCase)
                   || host.EndsWith(".ngrok-free.app", StringComparison.OrdinalIgnoreCase)
                   || host.EndsWith(".ngrok.dev", StringComparison.OrdinalIgnoreCase)
                   || host.IndexOf("ngrok", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void OnProcessExited(object sender, EventArgs args)
        {
            MainThreadDispatcher.Post(() =>
            {
                if (State == NgrokConnectionState.Stopped) return;
                string output = LastOutput;
                SetError(string.IsNullOrWhiteSpace(output) ? "ngrok stopped." : "ngrok stopped: " + output);
            });
        }

        private static void AttachToExistingProcessIfOwned()
        {
            int processId = SessionState.GetInt(OwnedProcessSessionKey, 0);
            if (processId <= 0) return;
            try
            {
                Process process = Process.GetProcessById(processId);
                if (process.HasExited) return;
                lock (Gate)
                {
                    _process = process;
                    _ownsProcess = true;
                }
            }
            catch
            {
                SessionState.SetInt(OwnedProcessSessionKey, 0);
            }
        }

        private static AgentProbeResult ProbeAgentApi(int timeoutMilliseconds, int localPort)
        {
            AgentProbeResult endpoints = ProbeAgentResource(AgentApiBaseUrl + "/endpoints", timeoutMilliseconds, localPort, true);
            if (!string.IsNullOrWhiteSpace(endpoints.PublicUrl)) return endpoints;

            AgentProbeResult tunnels = ProbeAgentResource(AgentApiBaseUrl + "/tunnels", timeoutMilliseconds, localPort, false);
            return new AgentProbeResult
            {
                AgentReachable = endpoints.AgentReachable || tunnels.AgentReachable,
                PublicUrl = tunnels.PublicUrl
            };
        }

        private static AgentProbeResult ProbeAgentResource(string url, int timeoutMilliseconds, int localPort, bool endpointShape)
        {
            var result = new AgentProbeResult();
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = timeoutMilliseconds;
                request.ReadWriteTimeout = timeoutMilliseconds;
                request.Proxy = null;
                using (WebResponse response = request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream ?? Stream.Null, Encoding.UTF8))
                {
                    result.AgentReachable = true;
                    JObject root = JObject.Parse(reader.ReadToEnd());
                    JArray items = root[endpointShape ? "endpoints" : "tunnels"] as JArray;
                    if (items == null) return result;

                    foreach (JToken item in items)
                    {
                        string publicUrl = endpointShape
                            ? item["url"]?.Value<string>() ?? string.Empty
                            : item["public_url"]?.Value<string>() ?? string.Empty;
                        if (!publicUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) continue;

                        string upstream = endpointShape
                            ? item["upstream"]?["url"]?.Value<string>() ?? string.Empty
                            : item["config"]?["addr"]?.Value<string>() ?? string.Empty;

                        if (AddressTargetsPort(upstream, localPort))
                        {
                            result.PublicUrl = publicUrl;
                            return result;
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                // A reachable ngrok API may return 404 for a resource unsupported by an older agent.
                if (ex.Response is HttpWebResponse response)
                {
                    result.AgentReachable = response.StatusCode == HttpStatusCode.NotFound
                                            || response.StatusCode == HttpStatusCode.MethodNotAllowed;
                    try { response.Dispose(); } catch { }
                }
            }
            catch
            {
                // Not reachable or not valid JSON.
            }
            return result;
        }

        private static bool AddressTargetsPort(string address, int localPort)
        {
            string value = (address ?? string.Empty).Trim().TrimEnd('/');
            if (value.Length == 0) return false;
            string port = localPort.ToString();
            if (value.Equals(port, StringComparison.OrdinalIgnoreCase)) return true;
            if (value.EndsWith(":" + port, StringComparison.OrdinalIgnoreCase)) return true;
            if (value.IndexOf(":" + port + "/", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static string ResolveExecutable(string configured)
        {
            string value = string.IsNullOrWhiteSpace(configured) ? "ngrok" : configured.Trim().Trim('"');
            if (Path.IsPathRooted(value))
                return IsAllowedNgrokExecutable(value) && File.Exists(value) ? value : string.Empty;

            if (value.IndexOf(Path.DirectorySeparatorChar) >= 0 || value.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                string full = Path.GetFullPath(value);
                return IsAllowedNgrokExecutable(full) && File.Exists(full) ? full : string.Empty;
            }

            var candidates = new List<string>();
            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string directory in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory)) continue;
                candidates.Add(Path.Combine(directory.Trim().Trim('"'), "ngrok.exe"));
                candidates.Add(Path.Combine(directory.Trim().Trim('"'), "ngrok"));
            }

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                candidates.Add(Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "ngrok.exe"));
                candidates.Add(Path.Combine(localAppData, "ngrok", "ngrok.exe"));
            }

            return candidates.FirstOrDefault(candidate => IsAllowedNgrokExecutable(candidate) && File.Exists(candidate)) ?? string.Empty;
        }

        private static bool IsAllowedNgrokExecutable(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path ?? string.Empty);
            return name.Equals("ngrok", StringComparison.OrdinalIgnoreCase);
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static void SetRunning(string publicUrl, bool ownsProcess)
        {
            string normalized = (publicUrl ?? string.Empty).Trim().TrimEnd('/');
            lock (Gate)
            {
                _state = NgrokConnectionState.Running;
                _publicUrl = normalized;
                _lastError = string.Empty;
                _ownsProcess = ownsProcess;
            }
            BridgePreferences.TunnelBaseUrl = normalized;
            RaiseStateChanged();
        }

        private static void SetError(string error)
        {
            lock (Gate)
            {
                _state = NgrokConnectionState.Error;
                _lastError = error ?? string.Empty;
                _publicUrl = string.Empty;
            }
            RaiseStateChanged();
        }

        private static void SetState(NgrokConnectionState state, string publicUrl, string error, bool ownsProcess)
        {
            lock (Gate)
            {
                _state = state;
                _publicUrl = publicUrl ?? string.Empty;
                _lastError = error ?? string.Empty;
                _ownsProcess = ownsProcess;
            }
            RaiseStateChanged();
        }

        private static void RaiseStateChanged()
        {
            MainThreadDispatcher.Post(() =>
            {
                try { StateChanged?.Invoke(); } catch { }
            });
        }

        private sealed class AgentProbeResult
        {
            public bool AgentReachable;
            public string PublicUrl = string.Empty;
        }
    }
}
