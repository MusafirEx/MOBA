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
using Fusion;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using System.Linq;

namespace Vauxland.FusionBrawler
{
    public class GameNetworkRunnerManager : MonoBehaviour
    {
        [SerializeField] public NetworkRunner networkRunnerPrefab = null; // the network runner (the heartbeat of fusion simulation)

        protected NetworkRunner _cacheNetworkRunner;

        public static GameNetworkRunnerManager Instance { get; private set; }

        private void Awake()
        {
            // find our network runner if in the scene already
            _cacheNetworkRunner = FindObjectOfType<NetworkRunner>();

            if (Instance != null)
            {
                Destroy(gameObject);
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

        }

        private void OnDestroy()
        {
            Instance = null;
        }

        // spawns our runner if its null and not in the scene already
        public void SetUpRunner()
        {
            if (_cacheNetworkRunner == null)
            {
                _cacheNetworkRunner = Instantiate(networkRunnerPrefab);
            }
        }

        // gets or adds the scene manager on the runner
        INetworkSceneManager GetSceneManager(NetworkRunner runner)
        {
            var sceneManager = runner.GetComponents(typeof(MonoBehaviour)).OfType<INetworkSceneManager>().FirstOrDefault();

            if (sceneManager == null)
            {
                //handle networked objects that already exits in the scene
                sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            return sceneManager;
        }

        // tells the runner to load the match scene with the selected rules and start the match
        public async void StartGame(GameMode mode, string roomName, string region, Dictionary<string, SessionProperty> sessionProperties = null)
        {
            _cacheNetworkRunner = FindObjectOfType<NetworkRunner>();
            if (_cacheNetworkRunner == null)
            {
                _cacheNetworkRunner = Instantiate(networkRunnerPrefab);
            }

            // let the Runner know that we will be providing user input
            _cacheNetworkRunner.ProvideInput = true;

            // set the FixedRegion in AppSettings
            var appSettings = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings;
            appSettings.FixedRegion = region;

            // retrieve the sceneName from sessionProperties
            string sceneName = "BrawlScene"; // the default scene to load
            if (sessionProperties != null && sessionProperties.TryGetValue("MapScene", out var sceneNameProperty))
            {
                sceneName = (string)sceneNameProperty;
            }

            var startGameArgs = new StartGameArgs()
            {
                GameMode = mode, // we set the game mode we selected on the main menu
                SessionName = roomName, //we assign our room name we set in the main menu
                CustomLobbyName = "LobbyID",
                IsOpen = true, // we set the game to open to be joinable
                PlayerCount = 10, // we set the player count
                ObjectProvider = _cacheNetworkRunner.GetComponent<NetworkObjectPooler>(),
                SceneManager = GetSceneManager(_cacheNetworkRunner),
                SessionProperties = sessionProperties, // we assign custom session properties based on our preferences in the main menu
            };

            var result = await _cacheNetworkRunner.StartGame(startGameArgs); // starts and loads the match scene

            if (_cacheNetworkRunner.IsServer)
            {
                if (result.Ok)
                {
                    _ = _cacheNetworkRunner.LoadScene(sceneName);
                }
                else
                {
                    Debug.LogError($"Failed to Start: {result.ShutdownReason}");
                }
            }
        }

        // starts a random game 
        public async void StartRandomGame(GameMode mode, string region, MatchType matchType)
        {
            _cacheNetworkRunner = FindObjectOfType<NetworkRunner>();
            if (_cacheNetworkRunner == null)
            {
                _cacheNetworkRunner = Instantiate(networkRunnerPrefab); // spawns and starts the runner
            }

            // let the Runner know that we will be providing user input
            _cacheNetworkRunner.ProvideInput = true;

            // set the FixedRegion in AppSettings
            var appSettings = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings;
            appSettings.FixedRegion = region;

            var sessionPropertyFilters = new Dictionary<string, SessionProperty>() // looks for a random game based on match type set in main menu
            {
                { "MatchType", (int)matchType }
            };

            // retrieve the sceneName from sessionProperties
            string sceneName = "BrawlScene"; // the default scene to choose
            if (sessionPropertyFilters != null && sessionPropertyFilters.TryGetValue("MapName", out var sceneNameProperty))
            {
                sceneName = (string)sceneNameProperty;
            }

            var startGameArgs = new StartGameArgs()
            {
                GameMode = mode,
                IsOpen = true,
                CustomLobbyName = "LobbyID",
                ObjectProvider = _cacheNetworkRunner.GetComponent<NetworkObjectPooler>(),
                SceneManager = GetSceneManager(_cacheNetworkRunner),
                SessionProperties = sessionPropertyFilters
            };

            var result = await _cacheNetworkRunner.StartGame(startGameArgs); // starts and loads the match scene

            if (_cacheNetworkRunner.IsServer)
            {
                if (result.Ok)
                {
                    _ = _cacheNetworkRunner.LoadScene(sceneName);
                }
                else
                {
                    Debug.LogError($"Failed to Start: {result.ShutdownReason}");
                }
            }
        }

        // joins the Fusions lobby system to find available games
        public void OnJoinMatchLobby()
        {
            _cacheNetworkRunner = FindObjectOfType<NetworkRunner>();
            var currenTask = JoinMatchLobby();
        }

        // the task to join the lobby
        private async Task JoinMatchLobby()
        {
            string sessionId = "LobbyID";

            var result = await _cacheNetworkRunner.JoinSessionLobby(SessionLobby.Custom, sessionId);

            if (!result.Ok)
            {
                Debug.LogError($"Unable to Join Match {sessionId}");
            }
            else
            {
                Debug.Log($"Success Join Match {sessionId}");
            }
        }

        // sends the selected match properties from create game to the start game method
        public void CreateNewGame(string sessionName, MatchType matchType, string region, Dictionary<string, SessionProperty> sessionProperties)
        {
            StartGame(GameMode.Host, sessionName, region, sessionProperties);
        }

        // joins a match from the session list
        public void JoinGame(SessionInfo sessionInfo)
        {
            // use the region of the session we're joining
            string region = sessionInfo.Region;

            StartGame(GameMode.Client, sessionInfo.Name, region, null);
        }

        // creates or joins a random match based on selected game mode
        public void JoinRandomGameOrCreate(string region, MatchType matchType)
        {
            StartRandomGame(GameMode.AutoHostOrClient, region, matchType); 
        }

        // TODO: Add Host Migration. It's in the works

    }
  
}
