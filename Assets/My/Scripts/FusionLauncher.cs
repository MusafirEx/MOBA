using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Minimum Photon Fusion launcher + player spawner for Host/Client prototype.
/// Put this on a GameObject in the arena scene. Assign Player Prefab with NetworkObject.
/// </summary>
public class FusionLauncher : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Fusion")]
    public NetworkRunner runnerPrefab;
    public NetworkObject playerMonsterPrefab;
    public string sessionName = "MonsterArena_Test";
    public GameMode gameMode = GameMode.AutoHostOrClient;

    [Header("Spawn")]
    public Transform[] teamASpawnPoints;
    public Transform[] teamBSpawnPoints;

    private NetworkRunner _runner;
    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new();

    public async void StartGame()
    {
        if (_runner != null) return;

        _runner = runnerPrefab != null ? Instantiate(runnerPrefab) : gameObject.AddComponent<NetworkRunner>();
        _runner.name = "NetworkRunner";
        _runner.ProvideInput = true;
        _runner.AddCallbacks(this);

        var sceneManager = _runner.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null) sceneManager = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        var startArgs = new StartGameArgs
        {
            GameMode = gameMode,
            SessionName = sessionName,
            Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
            SceneManager = sceneManager
        };

        var result = await _runner.StartGame(startArgs);
        if (!result.Ok)
        {
            Debug.LogError($"Fusion start failed: {result.ShutdownReason}");
        }
    }

    public void ShutdownGame()
    {
        if (_runner != null) _runner.Shutdown();
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer || playerMonsterPrefab == null) return;

        int teamId = AssignTeamId(player);
        Transform spawn = GetSpawnPoint(teamId, player.RawEncoded % 8);
        Vector3 pos = spawn != null ? spawn.position : Vector3.zero;
        Quaternion rot = spawn != null ? spawn.rotation : Quaternion.identity;

        NetworkObject obj = runner.Spawn(playerMonsterPrefab, pos, rot, player);
        _spawnedPlayers[player] = obj;

        var stats = obj.GetComponent<MonsterStatsNetwork>();
        if (stats != null) stats.InitOnServer(teamId);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedPlayers.TryGetValue(player, out NetworkObject obj))
        {
            if (obj != null) runner.Despawn(obj);
            _spawnedPlayers.Remove(player);
        }
    }

    private int AssignTeamId(PlayerRef player)
    {
        // Minimum: alternate by player ref. Later replace with proper lobby team assignment.
        return Mathf.Abs(player.RawEncoded) % 2;
    }

    private Transform GetSpawnPoint(int teamId, int index)
    {
        Transform[] list = teamId == 0 ? teamASpawnPoints : teamBSpawnPoints;
        if (list == null || list.Length == 0) return null;
        return list[Mathf.Abs(index) % list.Length];
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        MonsterInputData data = MonsterInputProvider.CollectInput();
        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { _runner = null; }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { request.Accept(); }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}
