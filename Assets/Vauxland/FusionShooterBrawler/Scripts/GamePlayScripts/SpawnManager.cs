/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vauxland.FusionBrawler
{
    public class SpawnManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
    {
        public Dictionary<int, NetworkObject> CurrentPlayers { get; private set; } = new Dictionary<int, NetworkObject>(); // keep track of our current players
        private List<NetworkObject> botPlayers = new List<NetworkObject>(); // keep track of our bots
        public Dictionary<int, PlayerTeam> PlayerTeams { get; private set; } = new Dictionary<int, PlayerTeam>(); // keep track of how many players on each team.

        [SerializeField] private NetworkPrefabRef playerPrefab = NetworkPrefabRef.Empty; // the players prefab to spawn
        [Header("Pickup Settings")]
        [SerializeField]
        private NetworkPrefabRef[] pickupPrefabs; // the pickups you want to spawn
        [SerializeField] private int maxPickups = 5; // maximum number of pick-ups to be active
        public bool delayPickups; // bool to delay spawning the pickups, set false to not delay them and spawn another right after one is picked up
        public float pickupSpawnDelay = 5f; // pickups spawn delay
        private List<NetworkObject> activePickups = new List<NetworkObject>(); // keep track of our current pick ups in the match

        private bool matchReady = false;

        [SerializeField] private SpawnPoint[] playerSpawnPoints = null; // players spawn points
        [SerializeField] private SpawnPoint[] redTeamSpawnPoints = null; // red teams specific spawn points
        [SerializeField] private SpawnPoint[] blueTeamSpawnPoints = null; // blue teams specific spawn points
        [SerializeField] private SpawnPoint[] pickupSpawnPoints = null; // the pickup spawn points

        private GameMatchManager matchManager = null;
        public GameMatchManager _matchManager;
        private GameNetworkRunnerManager _runnerManager;

        private Vector3 spawnPosition; // we use this to set our spawn position after we calculate it
        private bool botsSpawned = false;

        public override void Spawned()
        {
            _runnerManager = FindObjectOfType<GameNetworkRunnerManager>();
        }

        // add players to the current players as they join
        public void AddToEntry(int id, NetworkObject obj)
        {
            if (!CurrentPlayers.ContainsKey(id))
            {
                CurrentPlayers.Add(id, obj);
            }
        }

        // starts our spawner and spawns our players
        public void StartSpawner(GameMatchManager gameMatchManager)
        {
            matchReady = true;
            matchManager = gameMatchManager;
            foreach (var player in Runner.ActivePlayers)
            {
                if (_matchManager.matchType == MatchType.TeamDeathMatch)
                {
                    AssignTeam(player);
                }

                SpawnPlayers(player);

            }

            // if we are using bots spawn the bots
            if (!botsSpawned && _matchManager.useBots)
                SpawnBots();

            // spawn pickups
            SpawnPickups();
        }

        // when a player joins our ongoing match
        public void PlayerJoined(PlayerRef player)
        {
            if (matchReady == false) return;

            if (_matchManager.matchType == MatchType.TeamDeathMatch)
            {
                AssignTeam(player);
            }

            SpawnPlayers(player);


        }

        private void AssignTeam(PlayerRef player)
        {
            int playerId = player.PlayerId; // we use PlayerId for the player and bots name for bots

            if (PlayerTeams.Count(p => p.Value == PlayerTeam.Red) <= PlayerTeams.Count(p => p.Value == PlayerTeam.Blue))
            {
                PlayerTeams[playerId] = PlayerTeam.Red;
            }
            else
            {
                PlayerTeams[playerId] = PlayerTeam.Blue;
            }
        }

        // is the spawn spot already occupied?
        private bool IsSpawnPointOccupied(Vector3 position, float radius, string tag)
        {
            Collider[] colliders = Physics.OverlapSphere(position, radius);
            foreach (var collider in colliders)
            {
                if (collider.CompareTag(tag))
                {
                    return true;
                }
            }
            return false;
        }

        // gets a random spawn position in a spawn point
        private Vector3 GetRandomPointInSpawnArea(SpawnPoint spawnPoint)
        {
            if (spawnPoint == null)
            {
                Debug.LogError("SpawnManager received a null SpawnPoint reference.", this);
                return transform.position;
            }

            Vector2 randomPoint = new Vector2(
                UnityEngine.Random.Range(-spawnPoint.spawnAreaSize.x / 2f, spawnPoint.spawnAreaSize.x / 2f),
                UnityEngine.Random.Range(-spawnPoint.spawnAreaSize.y / 2f, spawnPoint.spawnAreaSize.y / 2f)
            );

            return spawnPoint.transform.position + new Vector3(randomPoint.x, 0f, randomPoint.y);
        }

        // try to get an unoccupied spawn point
        public Vector3 GetUnoccupiedSpawnPoint(SpawnPoint[] spawnPoints, float avoidRadius, string tag)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogError($"No spawn points are assigned for '{tag}'.", this);
                return transform.position;
            }

            SpawnPoint[] validSpawnPoints = spawnPoints.Where(point => point != null).ToArray();
            if (validSpawnPoints.Length == 0)
            {
                Debug.LogError($"All spawn point entries for '{tag}' are null.", this);
                return transform.position;
            }

            const int maxAttempts = 15;
            for (int i = 0; i < maxAttempts; i++)
            {
                SpawnPoint spawnPoint = validSpawnPoints[UnityEngine.Random.Range(0, validSpawnPoints.Length)];
                Vector3 candidatePosition = GetRandomPointInSpawnArea(spawnPoint);

                if (!IsSpawnPointOccupied(candidatePosition, avoidRadius, tag))
                    return candidatePosition;
            }

            SpawnPoint fallbackPoint = validSpawnPoints[UnityEngine.Random.Range(0, validSpawnPoints.Length)];
            return GetRandomPointInSpawnArea(fallbackPoint);
        }

        // spawn our bots
        private void SpawnBots()
        {
            if (botsSpawned) return;

            int botsToSpawn = _matchManager.botAmount;

            // assign bot to a team
            PlayerTeam assignedTeam;

            for (int i = 0; i < botsToSpawn; i++)
            {
                if (_matchManager.matchType == MatchType.DeathMatch)
                {
                    spawnPosition = GetUnoccupiedSpawnPoint(playerSpawnPoints, 5f, "Player");

                    // spawn the bot
                    var botObject = Runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, null, SetBot);
                    botPlayers.Add(botObject); // track the bot separately
                    _matchManager.AddNewBots(botObject);
                }
                else if (_matchManager.matchType == MatchType.TeamDeathMatch)
                {

                    if (PlayerTeams.Count(p => p.Value == PlayerTeam.Red) <= PlayerTeams.Count(p => p.Value == PlayerTeam.Blue))
                    {
                        assignedTeam = PlayerTeam.Red;
                    }
                    else
                    {
                        assignedTeam = PlayerTeam.Blue;
                    }

                    // get spawn position based on team
                    if (assignedTeam == PlayerTeam.Red)
                    {
                        spawnPosition = GetUnoccupiedSpawnPoint(redTeamSpawnPoints, 5f, "Player");
                    }
                    else
                    {
                        spawnPosition = GetUnoccupiedSpawnPoint(blueTeamSpawnPoints, 5f, "Player");
                    }

                    // spawn the bot
                    var botObject = Runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, null, SetBot); // use null as PlayerRef for bots
                    botPlayers.Add(botObject); // keep track of the bots

                    // set team on the bot's network controller
                    var botController = botObject.GetComponent<PlayerNetworkController>();
                    int teamInt = (assignedTeam == PlayerTeam.Red) ? 1 : 2;
                    botController.SetTeamInt(teamInt);

                    // add bot to team tracking dictionary with a negative ID for bots
                    int botId = -(i + 1); // use a negative ID to differentiate from real players
                    PlayerTeams[botId] = assignedTeam;
                }



            }
            botsSpawned = true;
        }

        // set up the player prefab to be a bot
        private void SetBot(NetworkRunner runner, NetworkObject networkObject)
        {
            networkObject.GetComponent<PlayerNetworkController>().IsBot = true;
        }

        // spawn our players based on the match type
        private void SpawnPlayers(PlayerRef player)
        {
            if (Runner == null || _matchManager == null)
                return;

            // StartSpawner and PlayerJoined can both be invoked for the same participant.
            // Do not create another network player prefab when Fusion already has one assigned.
            if (Runner.TryGetPlayerObject(player, out NetworkObject existingPlayerObject) &&
                existingPlayerObject != null)
            {
                return;
            }

            int playerId = player.PlayerId;

            if (_matchManager.matchType == MatchType.TeamDeathMatch && !PlayerTeams.ContainsKey(playerId))
            {
                Debug.LogWarning($"Player {player} does not have a team assigned.");
                return;
            }

            if (_matchManager.matchType == MatchType.DeathMatch)
            {
                spawnPosition = GetUnoccupiedSpawnPoint(playerSpawnPoints, 5f, "Player");
            }
            else if (_matchManager.matchType == MatchType.TeamDeathMatch)
            {
                if (PlayerTeams[playerId] == PlayerTeam.Red)
                {
                    spawnPosition = GetUnoccupiedSpawnPoint(redTeamSpawnPoints, 5f, "Player");
                }
                else
                {
                    spawnPosition = GetUnoccupiedSpawnPoint(blueTeamSpawnPoints, 5f, "Player");
                }
            }

            // spawn the player
            var playerObject = Runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);
            if (playerObject == null)
            {
                Debug.LogError($"Failed to spawn player prefab for {player}.", this);
                return;
            }

            Runner.SetPlayerObject(player, playerObject);

            // set the players team int based on what team they were assigned to when spawning
            var playerController = playerObject.GetComponent<PlayerNetworkController>();
            if (playerController == null)
            {
                Debug.LogError("Spawned player prefab is missing PlayerNetworkController.", playerObject);
                return;
            }
            if (matchManager.matchType == MatchType.TeamDeathMatch)
            {
                int teamInt = (PlayerTeams[playerId] == PlayerTeam.Red) ? 1 : 2;
                playerController.SetTeamInt(teamInt);
            }
            else if (matchManager.matchType == MatchType.DeathMatch)
            {
                playerController.SetTeamInt(0);
            }
            matchManager.AddNewPlayers(playerController.Id); // keep track of new players in the match manager
        }

        // spawn the pickups
        public void SpawnPickups()
        {
            if (pickupSpawnPoints == null || pickupSpawnPoints.Length == 0)
            {
                Debug.LogWarning("No pickup spawn points assigned.");
                return;
            }

            if (pickupPrefabs == null || pickupPrefabs.Length == 0)
            {
                Debug.LogWarning("No pickup prefabs assigned.");
                return;
            }

            for (int i = 0; i < maxPickups; i++)
            {
                SpawnSinglePickup();
            }
        }

        private void SpawnSinglePickup()
        {
            // randomly select a power-up prefab
            int randomPowerUpIndex = Random.Range(0, pickupPrefabs.Length);
            NetworkPrefabRef powerUpPrefab = pickupPrefabs[randomPowerUpIndex];

            // try and get an unoccupied spawn position within the spawn area
            Vector3 spawnPosition = GetUnoccupiedSpawnPoint(pickupSpawnPoints, 4f, "PickUp");

            spawnPosition.y = 0.5f;
            // spawn the pickup
            NetworkObject powerUpObject = Runner.Spawn(powerUpPrefab, spawnPosition, Quaternion.identity);

            // keep track of the spawned pick-up
            activePickups.Add(powerUpObject);

            // set the SpawnManager as the owner for callbacks
            var networkPickUp = powerUpObject.GetComponent<NetworkPickup>();
            if (networkPickUp != null)
            {
                networkPickUp.SetSpawnManager(this);
            }
        }

        // when a player picks up a pick up
        public void PickupPickedUp(NetworkObject powerUpObject)
        {
            if (Object.HasStateAuthority)
            {
                // remove the pick-up from the active list
                activePickups.Remove(powerUpObject);

                // optionally, wait for a delay before spawning a new pick-up
                if (delayPickups)
                {
                    StartCoroutine(SpawnPickupWithDelay(pickupSpawnDelay));
                }
                else
                {
                    SpawnSinglePickup();
                }
            }
        }

        // when spawning with delayed pick ups
        private IEnumerator SpawnPickupWithDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            SpawnSinglePickup();
        }

        // gte the spawn position
        public Vector3 GetSpawnPosition(PlayerRef player)
        {
            int playerId = player.PlayerId;
            Vector3 spawnPosition = Vector3.zero;

            if (_matchManager.matchType == MatchType.DeathMatch)
            {
                spawnPosition = GetUnoccupiedSpawnPoint(playerSpawnPoints, 5f, "Player");
            }
            else if (_matchManager.matchType == MatchType.TeamDeathMatch)
            {
                if (PlayerTeams[playerId] == PlayerTeam.Red)
                {
                    spawnPosition = GetUnoccupiedSpawnPoint(redTeamSpawnPoints, 5f, "Player");
                }
                else
                {
                    spawnPosition = GetUnoccupiedSpawnPoint(blueTeamSpawnPoints, 5f, "Player");
                }
            }
            return spawnPosition;
        }

        // get the bots spawn position
        public Vector3 GetBotSpawnPosition(int teamInt)
        {
            Vector3 spawnPosition = Vector3.zero;

            if (_matchManager.matchType == MatchType.DeathMatch)
            {
                spawnPosition = GetUnoccupiedSpawnPoint(playerSpawnPoints, 5f, "Player");
            }
            else if (_matchManager.matchType == MatchType.TeamDeathMatch)
            {
                if (teamInt == 1)
                {
                    spawnPosition = GetUnoccupiedSpawnPoint(redTeamSpawnPoints, 5f, "Player");
                }
                else
                {
                    spawnPosition = GetUnoccupiedSpawnPoint(blueTeamSpawnPoints, 5f, "Player");
                }
            }
            return spawnPosition;
        }

        // when a player leaves the match
        public void PlayerLeft(PlayerRef player)
        {
            int playerId = player.PlayerId;

            if (CurrentPlayers.TryGetValue(player.PlayerId, out var playerObject))
            {
                // remove the player from the tracked players
                CurrentPlayers.Remove(player.PlayerId);
                if (playerObject != null)
                {
                    if (Runner.TryGetPlayerObject(player, out var playerNetworkObject))
                        Runner.Despawn(playerNetworkObject);
                }

                // reset Player Object
                Runner.SetPlayerObject(player, null);
            }

            // remove the player from the PlayerTeams dictionary if its team deathmatch
            if (PlayerTeams.ContainsKey(playerId))
            {
                PlayerTeams.Remove(playerId);
            }
        }
    }
}

