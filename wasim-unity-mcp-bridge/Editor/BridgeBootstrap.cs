using UnityEditor;
using UnityEditor.Compilation;

namespace WasimDevelopment.UnityMcpBridge
{
    [InitializeOnLoad]
    internal static class BridgeBootstrap
    {
        private const string ResumeSessionKey = "WasimUnityMcpBridge.ResumeAfterReload";
        private const int MaximumStartAttempts = 30;
        private const double RetryDelaySeconds = 0.20d;

        private static bool _startScheduled;
        private static int _startAttempt;
        private static double _nextStartTime;

        static BridgeBootstrap()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
            EditorApplication.quitting -= OnQuitting;
            EditorApplication.quitting += OnQuitting;
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            bool shouldStart = BridgePreferences.AutoStart || SessionState.GetBool(ResumeSessionKey, false);
            if (shouldStart)
                ScheduleStart(0.25d);
        }

        public static void StartRequested()
        {
            SessionState.SetBool(ResumeSessionKey, true);
            ScheduleStart(0d);
        }

        public static void StopRequested()
        {
            SessionState.SetBool(ResumeSessionKey, false);
            CancelScheduledStart();
            if (BridgePreferences.StopNgrokWithBridge) NgrokManager.Stop();
            McpHttpServer.StopAndWait(1500);
        }

        public static void RestartRequested()
        {
            SessionState.SetBool(ResumeSessionKey, true);
            CancelScheduledStart();
            McpHttpServer.StopAndWait(1500);
            ScheduleStart(0.15d);
        }

        private static void OnCompilationStarted(object context)
        {
            if (!McpHttpServer.IsRunning) return;

            // Stop before Unity reaches domain reload. This gives the listener and all
            // accepted sockets time to release the fixed loopback port.
            SessionState.SetBool(ResumeSessionKey, true);
            CancelScheduledStart();
            McpHttpServer.StopAndWait(1500);
        }

        private static void OnCompilationFinished(object context)
        {
            // If compilation fails and Unity does not reload assemblies, the current
            // domain is still alive. Restart here. If a reload follows, beforeAssemblyReload
            // will stop it again and the new domain will resume normally.
            if (SessionState.GetBool(ResumeSessionKey, false) && !McpHttpServer.IsRunning)
                ScheduleStart(0.75d);
        }

        private static void BeforeAssemblyReload()
        {
            bool shouldResume = SessionState.GetBool(ResumeSessionKey, false) || McpHttpServer.IsRunning;
            SessionState.SetBool(ResumeSessionKey, shouldResume);
            CancelScheduledStart();

            // Keep an owned ngrok process alive. It can tolerate the brief local-port gap
            // while Unity reloads; only the local TCP listener must be shut down fully.
            McpHttpServer.StopAndWait(2000);
        }

        private static void OnQuitting()
        {
            SessionState.SetBool(ResumeSessionKey, false);
            CancelScheduledStart();
            NgrokManager.StopForUnityQuit();
            McpHttpServer.StopAndWait(2000);
        }

        private static void ScheduleStart(double delaySeconds)
        {
            _startAttempt = 0;
            _nextStartTime = EditorApplication.timeSinceStartup + delaySeconds;
            if (_startScheduled) return;

            _startScheduled = true;
            EditorApplication.update -= TryScheduledStart;
            EditorApplication.update += TryScheduledStart;
        }

        private static void CancelScheduledStart()
        {
            if (!_startScheduled) return;
            _startScheduled = false;
            EditorApplication.update -= TryScheduledStart;
        }

        private static void TryScheduledStart()
        {
            if (!_startScheduled || EditorApplication.timeSinceStartup < _nextStartTime) return;

            if (!SessionState.GetBool(ResumeSessionKey, false) && !BridgePreferences.AutoStart)
            {
                CancelScheduledStart();
                return;
            }

            if (McpHttpServer.IsRunning)
            {
                CompleteSuccessfulStart();
                return;
            }

            _startAttempt++;
            McpHttpServer.Start();
            if (McpHttpServer.IsRunning)
            {
                CompleteSuccessfulStart();
                return;
            }

            if (!McpHttpServer.LastStartFailedAddressInUse || _startAttempt >= MaximumStartAttempts)
            {
                CancelScheduledStart();
                return;
            }

            _nextStartTime = EditorApplication.timeSinceStartup + RetryDelaySeconds;
        }

        private static void CompleteSuccessfulStart()
        {
            CancelScheduledStart();
            SessionState.SetBool(ResumeSessionKey, true);

            if (BridgePreferences.AutoStartNgrok)
            {
                if (NgrokManager.IsRunning)
                    NgrokManager.RefreshFromAgent();
                else
                    NgrokManager.Start();
            }
        }
    }
}
