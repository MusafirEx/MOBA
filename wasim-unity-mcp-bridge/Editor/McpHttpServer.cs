using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WasimDevelopment.UnityMcpBridge
{
    internal static class McpHttpServer
    {
        private const int MaximumHeaderBytes = 32 * 1024;
        private const int MaximumBodyBytes = 1024 * 1024;
        private const int DefaultShutdownWaitMilliseconds = 1500;

        private static readonly object Gate = new object();
        private static readonly HashSet<TcpClient> ActiveClients = new HashSet<TcpClient>();
        private static readonly HashSet<Task> ActiveClientTasks = new HashSet<Task>();

        private static TcpListener _listener;
        private static CancellationTokenSource _cancellation;
        private static Task _acceptTask;
        private static string _lastError = string.Empty;
        private static string _preferredPath = string.Empty;
        private static string _legacyPath = string.Empty;
        private static bool _lastStartFailedAddressInUse;
        private static int _generation;

        public static event Action StateChanged;

        public static bool IsRunning
        {
            get { lock (Gate) return _listener != null; }
        }

        public static string LastError
        {
            get { lock (Gate) return _lastError; }
        }

        public static bool LastStartFailedAddressInUse
        {
            get { lock (Gate) return _lastStartFailedAddressInUse; }
        }

        public static void Start()
        {
            lock (Gate)
            {
                if (_listener != null) return;

                TcpListener newListener = null;
                CancellationTokenSource newCancellation = null;
                try
                {
                    _lastError = string.Empty;
                    _lastStartFailedAddressInUse = false;

                    // Snapshot Unity Editor preference values on the main thread before
                    // request handling moves to worker threads.
                    string capabilityToken = BridgePreferences.CapabilityToken;
                    int configuredPort = BridgePreferences.Port;
                    _preferredPath = "/" + capabilityToken + "/mcp";
                    _legacyPath = "/mcp/" + capabilityToken;

                    newCancellation = new CancellationTokenSource();
                    newListener = new TcpListener(IPAddress.Loopback, configuredPort);
                    newListener.Start(20);

                    _generation++;
                    int generation = _generation;
                    _listener = newListener;
                    _cancellation = newCancellation;
                    CancellationToken token = newCancellation.Token;
                    _acceptTask = Task.Run(() => AcceptLoopAsync(newListener, token, generation), CancellationToken.None);
                }
                catch (SocketException ex)
                {
                    _lastStartFailedAddressInUse = ex.SocketErrorCode == SocketError.AddressAlreadyInUse;
                    _lastError = _lastStartFailedAddressInUse
                        ? "Unity MCP port " + BridgePreferences.Port + " is still in use. The bridge will retry after the previous domain finishes releasing its socket."
                        : ex.GetType().Name + ": " + ex.Message;
                    try { newListener?.Stop(); } catch { }
                    try { newCancellation?.Dispose(); } catch { }
                    _listener = null;
                    _cancellation = null;
                    _acceptTask = null;
                }
                catch (Exception ex)
                {
                    _lastError = ex.GetType().Name + ": " + ex.Message;
                    try { newListener?.Stop(); } catch { }
                    try { newCancellation?.Dispose(); } catch { }
                    _listener = null;
                    _cancellation = null;
                    _acceptTask = null;
                }
            }

            RaiseStateChanged();
        }

        public static void Stop()
        {
            StopAndWait(DefaultShutdownWaitMilliseconds);
        }

        public static void StopAndWait(int waitMilliseconds)
        {
            TcpListener listener;
            CancellationTokenSource cancellation;
            Task acceptTask;
            TcpClient[] clients;

            lock (Gate)
            {
                _generation++;
                listener = _listener;
                cancellation = _cancellation;
                acceptTask = _acceptTask;
                clients = ActiveClients.ToArray();
                ActiveClients.Clear();

                _listener = null;
                _cancellation = null;
                _acceptTask = null;
                _lastStartFailedAddressInUse = false;
            }

            // Cancel first, then close every socket that could keep an old request alive.
            try { cancellation?.Cancel(); } catch { }
            try { listener?.Stop(); } catch { }

            foreach (TcpClient client in clients)
            {
                try { client.Client?.Shutdown(SocketShutdown.Both); } catch { }
                try { client.Close(); } catch { }
                try { client.Dispose(); } catch { }
            }

            // The listening socket owns the port. Wait briefly for its accept loop to
            // observe Stop() before Unity unloads this assembly and a new domain rebinds.
            if (acceptTask != null && waitMilliseconds > 0)
            {
                try { acceptTask.Wait(waitMilliseconds); }
                catch (AggregateException) { }
                catch (ObjectDisposedException) { }
            }

            try { cancellation?.Dispose(); } catch { }
            RaiseStateChanged();
        }

        public static void Restart()
        {
            StopAndWait(DefaultShutdownWaitMilliseconds);
            Start();
        }

        private static async Task AcceptLoopAsync(TcpListener listener, CancellationToken token, int generation)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient acceptedClient = null;
                try
                {
                    acceptedClient = await listener.AcceptTcpClientAsync();

                    lock (Gate)
                    {
                        if (token.IsCancellationRequested || generation != _generation || listener != _listener)
                        {
                            try { acceptedClient.Dispose(); } catch { }
                            break;
                        }

                        ActiveClients.Add(acceptedClient);
                    }

                    TcpClient clientForTask = acceptedClient;
                    Task clientTask = Task.Run(() => HandleClientTrackedAsync(clientForTask, token), CancellationToken.None);
                    lock (Gate) ActiveClientTasks.Add(clientTask);
                    _ = clientTask.ContinueWith(task =>
                    {
                        lock (Gate) ActiveClientTasks.Remove(task);
                    }, TaskScheduler.Default);
                }
                catch (ObjectDisposedException) when (token.IsCancellationRequested || generation != _generation) { break; }
                catch (SocketException) when (token.IsCancellationRequested || generation != _generation) { break; }
                catch (Exception ex)
                {
                    try { acceptedClient?.Dispose(); } catch { }
                    lock (Gate) _lastError = ex.GetType().Name + ": " + ex.Message;
                    RaiseStateChanged();
                    if (!token.IsCancellationRequested)
                    {
                        try { await Task.Delay(250, token); }
                        catch (OperationCanceledException) { break; }
                    }
                }
            }
        }

        private static async Task HandleClientTrackedAsync(TcpClient client, CancellationToken token)
        {
            try
            {
                await HandleClientAsync(client, token);
            }
            finally
            {
                lock (Gate) ActiveClients.Remove(client);
                try { client.Dispose(); } catch { }
            }
        }

        private static async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            {
                client.NoDelay = true;
                client.ReceiveTimeout = 30000;
                client.SendTimeout = 30000;
                NetworkStream stream = client.GetStream();
                try
                {
                    HttpRequest request = await ReadRequestAsync(stream, token);
                    if (request == null) return;

                    bool validPath = string.Equals(request.Path, _preferredPath, StringComparison.Ordinal)
                                     || string.Equals(request.Path, _legacyPath, StringComparison.Ordinal);

                    if (request.Method == "OPTIONS")
                    {
                        await WriteResponseAsync(stream, 204, "No Content", string.Empty, null, token, CorsHeaders());
                        return;
                    }

                    if (!validPath)
                    {
                        BridgeRequestHistory.Add("http", string.Empty, false, request.Method + " " + request.Path + " -> 404");
                        await WriteResponseAsync(stream, 404, "Not Found", "Not found", "text/plain; charset=utf-8", token);
                        return;
                    }

                    if (!OriginAllowed(request.Headers))
                    {
                        BridgeRequestHistory.Add("http", string.Empty, false, request.Method + " " + request.Path + " -> 403 origin");
                        await WriteResponseAsync(stream, 403, "Forbidden", "Origin not allowed", "text/plain; charset=utf-8", token, CorsHeaders());
                        return;
                    }

                    if (request.Method != "POST")
                    {
                        BridgeRequestHistory.Add("http", string.Empty, false, request.Method + " " + request.Path + " -> 405");
                        var headers = CorsHeaders();
                        headers["Allow"] = "POST, GET, DELETE, OPTIONS";
                        await WriteResponseAsync(stream, 405, "Method Not Allowed", "Use POST for MCP requests.", "text/plain; charset=utf-8", token, headers);
                        return;
                    }

                    BridgeRequestHistory.Add("http", string.Empty, true, request.Method + " " + request.Path + " reached MCP handler");
                    JObject response = await McpProtocolHandler.HandleAsync(request.Body);
                    if (response == null)
                    {
                        await WriteResponseAsync(stream, 202, "Accepted", string.Empty, null, token);
                        return;
                    }

                    var responseHeaders = CorsHeaders();
                    responseHeaders["MCP-Protocol-Version"] = McpProtocolHandler.CurrentProtocolVersion;
                    await WriteResponseAsync(stream, 200, "OK", response.ToString(Formatting.None), "application/json; charset=utf-8", token, responseHeaders);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (ObjectDisposedException) when (token.IsCancellationRequested) { }
                catch (IOException) when (token.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    string diagnostic = ex.GetType().Name + ": " + ex.Message;
                    lock (Gate) _lastError = diagnostic;
                    try { BridgeRequestHistory.Add("http", string.Empty, false, diagnostic); } catch { }
                    try
                    {
                        JObject error = new JObject
                        {
                            ["jsonrpc"] = "2.0",
                            ["id"] = JValue.CreateNull(),
                            ["error"] = new JObject { ["code"] = -32603, ["message"] = "Server error", ["data"] = ex.Message }
                        };
                        await WriteResponseAsync(stream, 500, "Internal Server Error", error.ToString(Formatting.None), "application/json; charset=utf-8", token);
                    }
                    catch { }
                    RaiseStateChanged();
                }
            }
        }

        private static Dictionary<string, string> CorsHeaders()
        {
            return new Dictionary<string, string>
            {
                ["Access-Control-Allow-Origin"] = "*",
                ["Access-Control-Allow-Methods"] = "POST, GET, DELETE, OPTIONS",
                ["Access-Control-Allow-Headers"] = "Content-Type, Accept, MCP-Protocol-Version, MCP-Session-Id",
                ["Access-Control-Expose-Headers"] = "MCP-Protocol-Version, MCP-Session-Id"
            };
        }

        private static bool OriginAllowed(Dictionary<string, string> headers)
        {
            if (!headers.TryGetValue("Origin", out string origin) || string.IsNullOrWhiteSpace(origin)) return true;
            if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri uri)) return false;
            string host = uri.Host;
            return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                   || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                   || host.Equals("chatgpt.com", StringComparison.OrdinalIgnoreCase)
                   || host.EndsWith(".chatgpt.com", StringComparison.OrdinalIgnoreCase)
                   || host.Equals("openai.com", StringComparison.OrdinalIgnoreCase)
                   || host.EndsWith(".openai.com", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<HttpRequest> ReadRequestAsync(NetworkStream stream, CancellationToken token)
        {
            byte[] headerBytes = await ReadUntilAsync(stream, new byte[] { 13, 10, 13, 10 }, MaximumHeaderBytes, token);
            if (headerBytes == null || headerBytes.Length == 0) return null;
            string headerText = Encoding.ASCII.GetString(headerBytes);
            string[] lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] requestLine = lines[0].Split(' ');
            if (requestLine.Length < 2) throw new InvalidDataException("Malformed HTTP request line.");

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon <= 0) continue;
                headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
            }

            string body = string.Empty;
            if (headers.TryGetValue("Transfer-Encoding", out string transfer) && transfer.IndexOf("chunked", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                body = Encoding.UTF8.GetString(await ReadChunkedBodyAsync(stream, token));
            }
            else if (headers.TryGetValue("Content-Length", out string lengthText) && int.TryParse(lengthText, out int length))
            {
                if (length < 0 || length > MaximumBodyBytes) throw new InvalidDataException("HTTP body exceeds the 1 MB limit.");
                body = Encoding.UTF8.GetString(await ReadExactlyAsync(stream, length, token));
            }

            string rawPath = requestLine[1];
            int queryIndex = rawPath.IndexOf('?');
            if (queryIndex >= 0) rawPath = rawPath.Substring(0, queryIndex);
            return new HttpRequest
            {
                Method = requestLine[0].ToUpperInvariant(),
                Path = Uri.UnescapeDataString(rawPath),
                Headers = headers,
                Body = body
            };
        }

        private static async Task<byte[]> ReadUntilAsync(Stream stream, byte[] delimiter, int maximumBytes, CancellationToken token)
        {
            using (var output = new MemoryStream())
            {
                int matched = 0;
                while (output.Length < maximumBytes)
                {
                    byte[] single = await ReadExactlyAsync(stream, 1, token);
                    if (single.Length == 0) break;
                    byte value = single[0];
                    output.WriteByte(value);
                    if (value == delimiter[matched])
                    {
                        matched++;
                        if (matched == delimiter.Length)
                        {
                            byte[] all = output.ToArray();
                            return all.Take(all.Length - delimiter.Length).ToArray();
                        }
                    }
                    else
                    {
                        matched = value == delimiter[0] ? 1 : 0;
                    }
                }
                if (output.Length >= maximumBytes) throw new InvalidDataException("HTTP headers exceed the 32 KB limit.");
                return output.ToArray();
            }
        }

        private static async Task<byte[]> ReadChunkedBodyAsync(Stream stream, CancellationToken token)
        {
            using (var output = new MemoryStream())
            {
                while (true)
                {
                    string sizeLine = Encoding.ASCII.GetString(await ReadUntilAsync(stream, new byte[] { 13, 10 }, 1024, token));
                    int semicolon = sizeLine.IndexOf(';');
                    if (semicolon >= 0) sizeLine = sizeLine.Substring(0, semicolon);
                    if (!int.TryParse(sizeLine.Trim(), System.Globalization.NumberStyles.HexNumber, null, out int size))
                        throw new InvalidDataException("Invalid chunk size.");
                    if (size == 0)
                    {
                        await ReadUntilAsync(stream, new byte[] { 13, 10 }, 8192, token);
                        break;
                    }
                    if (output.Length + size > MaximumBodyBytes) throw new InvalidDataException("Chunked body exceeds the 1 MB limit.");
                    byte[] chunk = await ReadExactlyAsync(stream, size, token);
                    output.Write(chunk, 0, chunk.Length);
                    await ReadExactlyAsync(stream, 2, token);
                }
                return output.ToArray();
            }
        }

        private static async Task<byte[]> ReadExactlyAsync(Stream stream, int length, CancellationToken token)
        {
            byte[] buffer = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                int read = await stream.ReadAsync(buffer, offset, length - offset, token);
                if (read <= 0)
                {
                    if (offset == 0) return Array.Empty<byte>();
                    throw new EndOfStreamException("Unexpected end of HTTP stream.");
                }
                offset += read;
            }
            return buffer;
        }

        private static async Task WriteResponseAsync(Stream stream, int statusCode, string reason, string body, string contentType, CancellationToken token,
            Dictionary<string, string> extraHeaders = null)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
            var builder = new StringBuilder();
            builder.Append("HTTP/1.1 ").Append(statusCode).Append(' ').Append(reason).Append("\r\n");
            builder.Append("Content-Length: ").Append(bodyBytes.Length).Append("\r\n");
            builder.Append("Connection: close\r\n");
            builder.Append("Cache-Control: no-store\r\n");
            builder.Append("X-Content-Type-Options: nosniff\r\n");
            if (!string.IsNullOrWhiteSpace(contentType)) builder.Append("Content-Type: ").Append(contentType).Append("\r\n");
            if (extraHeaders != null)
            {
                foreach (KeyValuePair<string, string> header in extraHeaders)
                    builder.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
            }
            builder.Append("\r\n");
            byte[] headerBytes = Encoding.ASCII.GetBytes(builder.ToString());
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length, token);
            if (bodyBytes.Length > 0) await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length, token);
            await stream.FlushAsync(token);
        }

        private static void RaiseStateChanged()
        {
            MainThreadDispatcher.Post(() =>
            {
                try { StateChanged?.Invoke(); } catch { }
            });
        }

        private sealed class HttpRequest
        {
            public string Method;
            public string Path;
            public Dictionary<string, string> Headers;
            public string Body;
        }
    }
}
