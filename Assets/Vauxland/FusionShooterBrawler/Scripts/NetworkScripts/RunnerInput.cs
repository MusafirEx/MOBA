/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using Fusion.Sockets;
using System;

namespace Vauxland.FusionBrawler
{
    public class RunnerInput : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private string lobbySceneName = string.Empty;
        private PlayerInputManager playerInput;
        private PlayerNetworkInputData lastInput;

        private GameSessionLobbyList gameSessionLobbyList; // the game session lobby list in the scene

        private void Awake()
        {
            gameSessionLobbyList = FindObjectOfType<GameSessionLobbyList>(true);
        }

        // lets our runners simulation know we are pressing buttons
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (playerInput == null && PlayerNetworkController.LocalPlayer != null)
                playerInput = PlayerNetworkController.LocalPlayer.GetComponent<PlayerInputManager>();

            if (playerInput != null)
            {
                lastInput = playerInput.SetPlayerNetworkInput();
                input.Set(lastInput);
            }

        }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            SceneManager.LoadScene(lobbySceneName); // load our lobby scene on shutdown which happens at the end of a match
        }

        public void OnConnectedToServer(NetworkRunner runner) { }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            // populates our session list from the active matches found
            if (gameSessionLobbyList == null)
                return;

            if (sessionList != null && sessionList.Count == 0)
            {
                Debug.Log("No sessions found in Lobby");

                gameSessionLobbyList.NoGameSessionFound();
            }
            else
            {
                gameSessionLobbyList.ClearContainerList();

                foreach (SessionInfo sessionInfo in sessionList)
                {
                    gameSessionLobbyList.AddGameSession(sessionInfo);
                }
            }
        }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

        public void OnSceneLoadDone(NetworkRunner runner) { }

        public void OnSceneLoadStart(NetworkRunner runner) { }
    }
}