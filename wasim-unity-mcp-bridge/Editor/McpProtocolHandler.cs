using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WasimDevelopment.UnityMcpBridge
{
    internal static class McpProtocolHandler
    {
        public const string CurrentProtocolVersion = "2025-11-25";

        public static async Task<JObject> HandleAsync(string requestBody)
        {
            JObject request;
            try
            {
                request = JObject.Parse(requestBody);
            }
            catch (Exception ex)
            {
                return Error(null, -32700, "Parse error", ex.Message);
            }

            JToken id = request["id"];
            string method = request["method"]?.Value<string>();
            if (request["jsonrpc"]?.Value<string>() != "2.0" || string.IsNullOrWhiteSpace(method))
                return Error(id, -32600, "Invalid Request", "Expected a JSON-RPC 2.0 request with a method.");

            bool notification = id == null;
            try
            {
                switch (method)
                {
                    case "initialize":
                        BridgeRequestHistory.Add(method, string.Empty, true, "Client initialized");
                        return notification ? null : Success(id, InitializeResult(request["params"] as JObject));

                    case "notifications/initialized":
                    case "notifications/cancelled":
                        BridgeRequestHistory.Add(method, string.Empty, true, "Notification received");
                        return null;

                    case "ping":
                        BridgeRequestHistory.Add(method, string.Empty, true, "Ping");
                        return notification ? null : Success(id, new JObject());

                    case "tools/list":
                        BridgeRequestHistory.Add(method, string.Empty, true, "Tool catalogue returned");
                        return notification ? null : Success(id, new JObject { ["tools"] = McpToolCatalog.Build() });

                    case "tools/call":
                        return await HandleToolCallAsync(id, request["params"] as JObject, notification);

                    default:
                        BridgeRequestHistory.Add(method, string.Empty, false, "Method not found");
                        return notification ? null : Error(id, -32601, "Method not found", method);
                }
            }
            catch (Exception ex)
            {
                BridgeRequestHistory.Add(method, string.Empty, false, ex.Message);
                return notification ? null : Error(id, -32603, "Internal error", ex.Message);
            }
        }

        private static JObject InitializeResult(JObject parameters)
        {
            string requested = parameters?["protocolVersion"]?.Value<string>();
            string selected = requested == "2025-11-25" || requested == "2025-06-18" || requested == "2025-03-26"
                ? requested
                : CurrentProtocolVersion;

            return new JObject
            {
                ["protocolVersion"] = selected,
                ["capabilities"] = new JObject
                {
                    ["tools"] = new JObject { ["listChanged"] = true }
                },
                ["serverInfo"] = new JObject
                {
                    ["name"] = "wasim-unity-mcp-bridge",
                    ["title"] = "Wasim Unity MCP Bridge",
                    ["version"] = BridgeVersion.PackageVersion
                },
                ["instructions"] = "Unity Editor bridge for inspection and approval-gated script changes. Read tools may inspect the project, Console, scripts, scene, assets, Animator and selection. unity_propose_script_replacement only creates a pending proposal; it does not edit Assets. Never claim a script changed until the user approves it inside Unity and a later compilation/status check confirms the result."
            };
        }

        private static async Task<JObject> HandleToolCallAsync(JToken id, JObject parameters, bool notification)
        {
            string name = parameters?["name"]?.Value<string>();
            JObject arguments = parameters?["arguments"] as JObject ?? new JObject();
            if (string.IsNullOrWhiteSpace(name))
                return notification ? null : Error(id, -32602, "Invalid params", "tools/call requires a tool name.");

            try
            {
                JToken result = await MainThreadDispatcher.InvokeAsync(() => UnityToolExecutor.Execute(name, arguments));
                string text = result.ToString(Formatting.Indented);
                BridgeRequestHistory.Add("tools/call", name, true, "Completed");
                if (notification) return null;
                return Success(id, new JObject
                {
                    ["content"] = new JArray
                    {
                        new JObject { ["type"] = "text", ["text"] = text }
                    },
                    ["isError"] = false
                });
            }
            catch (Exception ex)
            {
                Exception actual = ex.GetBaseException();
                BridgeRequestHistory.Add("tools/call", name, false, actual.Message);
                if (notification) return null;
                return Success(id, new JObject
                {
                    ["content"] = new JArray
                    {
                        new JObject { ["type"] = "text", ["text"] = "Unity tool failed: " + actual.Message }
                    },
                    ["isError"] = true
                });
            }
        }

        private static JObject Success(JToken id, JToken result)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id?.DeepClone() ?? JValue.CreateNull(),
                ["result"] = result ?? new JObject()
            };
        }

        private static JObject Error(JToken id, int code, string message, string data)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id?.DeepClone() ?? JValue.CreateNull(),
                ["error"] = new JObject
                {
                    ["code"] = code,
                    ["message"] = message,
                    ["data"] = data ?? string.Empty
                }
            };
        }
    }
}
