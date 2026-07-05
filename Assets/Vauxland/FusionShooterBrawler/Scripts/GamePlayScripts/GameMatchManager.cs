/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Vauxland.FusionBrawler
{
    // our Game Manager script during a Match, this handles all gameplay logic, like match timers, scores, calling the spawner and changing the game state
    public class GameMatchManager : NetworkBehaviour
    {
        //each stage of the match
        public enum GameStage
        {
            MatchStart,
            Playing,
            MatchEnded
        }

        [Header("Match Timers")]
        [SerializeField] private float matchStartCountdown = 5f; // the starting countdown before the game begins
        [SerializeField] private float matchEndDelay = 10f; // the delay time before we automatically return to the lobby
        [SerializeField] private float matchTimeLength = 180f; // the length of time for the match

        [Header("Players Starting Stats")]
        public StatDictionary baseStats = new StatDictionary(); // these are the starting stats of every player regardless their weapon or character

        [Header("Game rules")]
        public MatchType matchType; // the type of game the match will be
        [SerializeField] private int maxScore; // the total allowed score for the match
        public float respawnDuration = 5f; // the duration before a player can respawn
        public GameObject teamDeathMatchObjects; // all of the visual game objects in the scene associated with team deathmatch gamemode (not team spawn points)
        public bool useBots; // bool to use bots or not
        public int botAmount; // amount of bots to use

        [HideInInspector][Networked] public bool FriendlyFire { get; set; } // sync bool to use friendly fire
        [Networked] private TickTimer GameTimer { get; set; } // the matches synced timer that controls the flow of the game
        [Networked] private GameStage GameState { get; set; } // the current game state of the match
        [Networked] private NetworkBehaviourId MatchWinner { get; set; } // the player match winner
        [Networked] private PlayerNetworkController BotMatchWinner { get; set; } // the bot match winner
        [Networked] private PlayerTeam TeamWinner { get; set; } // the team winner

        [Networked]
        [HideInInspector]
        public int TeamRedScore { get; private set; } // team reds score
        [Networked]
        [HideInInspector]
        public int TeamBlueScore { get; private set; } // team blues score

        public bool GameIsStarting => GameState == GameStage.MatchStart;
        public bool GameIsRunning => GameState == GameStage.Playing;
        public bool GameIsOver => GameState == GameStage.MatchEnded;

        [HideInInspector]
        public SpawnManager _spawnManager; // our spawn manager
        public MatchUIHandler _matchUiHandler; // the match ui handler that handles all of the matches UI

        private List<NetworkBehaviourId> _networkedPlayerIds = new List<NetworkBehaviourId>(); // list of all of our players
        private List<NetworkObject> _botPlayers = new List<NetworkObject>(); // list of all of our bots
        private ChangeDetector _changeDetector;

        private void Awake()
        {
            _spawnManager = FindObjectOfType<SpawnManager>(); // get our spawn manager
        }

        public override void Spawned()
        {      
            // set is Simulated so that FUN (fixed update network) runs on every client instead of just the Host
            Runner.SetIsSimulated(Object, true);
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            _matchUiHandler.matchStartInfoHolder.SetActive(true);
            _matchUiHandler.matchTimeHolder.SetActive(false);
            _matchUiHandler.teamDeathMatchUI.SetActive(false);
            _matchUiHandler.localPlayerUI.gameObject.SetActive(false);

            if (matchType == MatchType.DeathMatch)
            {
                teamDeathMatchObjects.SetActive(false);
            }

            // read our custom session properties
            if (Runner.SessionInfo.Properties != null)
            {
                if (Runner.SessionInfo.Properties.TryGetValue("MatchType", out var matchTypeProperty))
                {
                    matchType = (MatchType)(int)matchTypeProperty.PropertyValue;
                }
                else
                {
                    matchType = MatchType.DeathMatch; // default match type
                }

                if (Runner.SessionInfo.Properties.TryGetValue("UseBots", out var useBotsProperty))
                {
                    useBots = ((int)useBotsProperty.PropertyValue) == 1;
                }
                else
                {
                    useBots = true; // match default use bots or set false
                }

                if (Runner.SessionInfo.Properties.TryGetValue("BotAmount", out var botAmountProperty))
                {
                    botAmount = (int)botAmountProperty.PropertyValue;
                }
                else
                {
                    botAmount = 5; // match default bots is 5, change to whatever
                }

                if (Runner.SessionInfo.Properties.TryGetValue("FriendlyFire", out var friendlyFireProperty))
                {
                    FriendlyFire = ((int)friendlyFireProperty.PropertyValue) == 1;
                }
                else
                {
                    FriendlyFire = false; // match default is false for friendly fire or set it true
                }

                if (Runner.SessionInfo.Properties.TryGetValue("MaxScoreAmount", out var maxScoreAmountProperty))
                {
                    maxScore = (int)maxScoreAmountProperty.PropertyValue;
                }
                else
                {
                    maxScore = 10; // match default maxscore is 10, change to whatever
                }

                if (Runner.SessionInfo.Properties.TryGetValue("MatchTimeLength", out var matchTimeLengthProperty))
                {
                    int matchTimeLengthInt = (int)matchTimeLengthProperty.PropertyValue;
                    matchTimeLength = (float)matchTimeLengthInt;
                }
                else
                {
                    matchTimeLength = 180f; // default match time is 3 minutes, change to whatever
                }
            }

            // deactivate the team deathmatch map objects
            if (matchType == MatchType.TeamDeathMatch)
            {
                teamDeathMatchObjects.SetActive(true);
            }

            // add all of our current players that are joined
            if (!GameIsStarting)
            {
                foreach (var player in Runner.ActivePlayers)
                {
                    if (Runner.TryGetPlayerObject(player, out var playerObject) == false) continue;
                    AddNewPlayers(playerObject.GetComponent<PlayerNetworkController>().Id);
                }

                
            }
            
            // host stuff
            if (Object.HasStateAuthority)
            {               
                GameState = GameStage.MatchStart;
                GameTimer = TickTimer.CreateFromSeconds(Runner, matchStartCountdown);

                TeamRedScore = 0;
                TeamBlueScore = 0;
            }

        }

        // updating the teams score when a player gets a kill
        public void UpdateMatchScore(int team, int score)
        {
            if (matchType == MatchType.TeamDeathMatch)
            {
                if (HasStateAuthority)
                {
                    if (team == 1)
                    {
                        TeamRedScore += score;
                    }
                    else if (team == 2)
                    {
                        TeamBlueScore += score;
                    }
                }
            }           
        }

        // syncs changing of Ui for team score across all clients
        public override void Render()
        {
            if (_changeDetector == null) return;

            foreach (var change in _changeDetector.DetectChanges(this, out var prev, out var current))
            {
                var redTeam = 1;
                var blueTeam = 2;
                switch (change)
                {
                    case nameof(TeamRedScore):
                        _matchUiHandler.UpdateTeamScoreUI(redTeam);
                        break;
                    case nameof(TeamBlueScore):
                        _matchUiHandler.UpdateTeamScoreUI(blueTeam);
                        break;
                }
            }
        }

        // updates the match state
        public override void FixedUpdateNetwork()
        {
            switch (GameState)
            {
                case GameStage.MatchStart:
                    SetMatchStart();
                    break;
                case GameStage.Playing:
                    SetMatchPlaying();
                    if (GameTimer.ExpiredOrNotRunning(Runner))
                    {
                        CheckWinnerOnMatchTimeEnd();
                        MatchEnded();
                    }
                    break;
                case GameStage.MatchEnded:
                    EndGame();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();

            }

        }

        // set our match start settings, set UI and start our spawner
        private void SetMatchStart()
        {
            _matchUiHandler.gameInfoText.text = $"Match Starts In";
            _matchUiHandler.countDownTimerText.text = $"{Mathf.RoundToInt(GameTimer.RemainingTime(Runner) ?? 0)}";

            // host Handling
            if (!Object.HasStateAuthority) return;
            if (GameTimer.ExpiredOrNotRunning(Runner) == false) return;

            // starts spawning our players and powerups
            _spawnManager.StartSpawner(this);

            // switches the GameStage to Playing
            GameState = GameStage.Playing;

            // starts the actual match timer
            GameTimer = TickTimer.CreateFromSeconds(Runner, matchTimeLength);
        }

        // sets the match UI to playing state
        private void SetMatchPlaying()
        {
            // set the match timer UI active and the match start info off
            _matchUiHandler.matchStartInfoHolder.SetActive(false);
            _matchUiHandler.matchTimeHolder.SetActive(true);

            if(matchType == MatchType.TeamDeathMatch)
            {
                teamDeathMatchObjects.SetActive(true);
                _matchUiHandler.teamDeathMatchUI.SetActive(true);
            }                
            // show the timer now
            _matchUiHandler.matchTimerText.gameObject.SetActive(true);
            // update the timer for all players
            int totalSeconds = Mathf.RoundToInt(GameTimer.RemainingTime(Runner) ?? 0);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            _matchUiHandler.matchTimerText.text = $"{minutes}:{seconds:00}";

            // set our players local UI active
            _matchUiHandler.localPlayerUI.gameObject.SetActive(true);
        }

        // check can the match end or not based on the current score
        public void CheckCanMatchEnd()
        {
            if (matchType == MatchType.DeathMatch)
            {
                for (int i = 0; i < _networkedPlayerIds.Count; i++)
                {
                    if (Runner.TryFindBehaviour(_networkedPlayerIds[i], out PlayerNetworkController playerDataNetworkedComponent) == false)
                    {
                        _networkedPlayerIds.RemoveAt(i);
                        i--;
                        continue;
                    }

                    if (playerDataNetworkedComponent.Kills >= maxScore)
                    {
                        MatchWinner = playerDataNetworkedComponent.Id;
                        MatchEnded();
                        return;
                    }
                }

                foreach (var bot in _botPlayers)
                {
                    var botController = bot.GetComponent<PlayerNetworkController>();
                    int highestBotKills = 0;
                    if (botController.IsBot && botController.Kills > highestBotKills)
                    {
                        highestBotKills = botController.Kills;

                        if (highestBotKills >= maxScore)
                        {
                            BotMatchWinner = botController;
                            MatchEnded();
                            return;
                        }
                        
                    }
                }
            }
            else if (matchType == MatchType.TeamDeathMatch)
            {
                if (TeamRedScore >= maxScore)
                {
                    TeamWinner = PlayerTeam.Red;
                    MatchEnded();
                }
                else if (TeamBlueScore >= maxScore)
                {
                    TeamWinner = PlayerTeam.Blue;
                    MatchEnded();
                }
                else
                {
                    // in case of a tie, you can handle it accordingly or it defaults to team Red
                }
            }
        }

        // the match time has ended lets get our winner
        private void CheckWinnerOnMatchTimeEnd()
        {
            if (Object.HasStateAuthority == false) return; // only Host can check this

            if (matchType == MatchType.DeathMatch)
            {
                PlayerNetworkController winningPlayer = null;
                int highestKills = 0;

                // loop through all of the added player Ids
                foreach (var playerNetworkedId in _networkedPlayerIds)
                {
                    if (Runner.TryFindBehaviour(playerNetworkedId, out PlayerNetworkController playerNetworkedComponent) == false)
                    {
                        continue;
                    }

                    // if the player is not a bot and has highest kills out of players set the potential player match winner
                    if (!playerNetworkedComponent.IsBot && playerNetworkedComponent.Kills > highestKills)
                    {
                        highestKills = playerNetworkedComponent.Kills;
                        winningPlayer = playerNetworkedComponent;
                    }
                }

                if (winningPlayer != null)
                {
                    MatchWinner = winningPlayer.Id;
                }
                else
                {
                    MatchWinner = _networkedPlayerIds[0];
                }
            }
            else if (matchType == MatchType.TeamDeathMatch)
            {
                if (TeamRedScore >= maxScore || TeamRedScore > TeamBlueScore) // if team red score higher than blues score
                {
                    TeamWinner = PlayerTeam.Red;
                    MatchEnded();
                }
                else if (TeamBlueScore >= maxScore || TeamBlueScore > TeamRedScore) // if team blue score is higher than reds score
                {
                    TeamWinner = PlayerTeam.Blue;
                    MatchEnded();
                }
                else
                {
                    // its a tie, but it defaults to team Red
                }
            }
        }

        // match ended lets set the game state so the Fixed Update Network will call EndGame()
        private void MatchEnded()
        {
            GameTimer = TickTimer.CreateFromSeconds(Runner, matchEndDelay);
            GameState = GameStage.MatchEnded;        
        }

        // our end of the match method
        private void EndGame()
        {
            _matchUiHandler.matchTimerText.text = "";
            _matchUiHandler.localPlayerUI.gameObject.SetActive(false);

            // set our appropriate scoreboards active
            if (matchType == MatchType.DeathMatch)
            {
                _matchUiHandler.scoreBoardPanel.SetActive(true);

                int highestBotKills = 0;

                // loops through all the bots and find the one with the highest kills
                foreach (var bot in _botPlayers)
                {
                    var botController = bot.GetComponent<PlayerNetworkController>();
                    if (botController.IsBot && botController.Kills > highestBotKills)
                    {
                        highestBotKills = botController.Kills;
                        BotMatchWinner = botController;
                    }
                }

                //  we check here if the match winner is a player
                if (Runner.TryFindBehaviour(MatchWinner, out PlayerNetworkController playerController))
                {
                    // we compare bot kills with the player winner's kills
                    if (BotMatchWinner != null && BotMatchWinner.Kills > playerController.Kills)
                    {
                        // if bot kills are higher, show the bot as the winner
                        _matchUiHandler.ShowWinningPlayer(BotMatchWinner.PlayerNickName.ToString(), BotMatchWinner.Kills);
                    }
                    else
                    {
                        // otherwise show the player as the winner
                        _matchUiHandler.ShowWinningPlayer(playerController.PlayerNickName.ToString(), playerController.Kills);
                    }
                }
                else if (BotMatchWinner != null)
                {
                    // if no player is found but a bot has the highest kills, show the bot as the winner
                    _matchUiHandler.ShowWinningPlayer(this.BotMatchWinner.PlayerNickName.ToString(), this.BotMatchWinner.Kills);
                }
            }
            else if (matchType == MatchType.TeamDeathMatch)
            {
                // show the team deathmatch scoreboard not the deathmatch score board
                _matchUiHandler.teamScoreBoardPanel.SetActive(true);

                _matchUiHandler.ShowWinningTeam(TeamWinner);
            }

            if (GameTimer.ExpiredOrNotRunning(Runner))
            {
                ReturnToLobby();
            }
            
        }

        // return to lobby scene by shutting down the runner
        public void ReturnToLobby()
        {
            Runner.Shutdown();
        }

        // adds new player's IDs to our player id list as they join
        public void AddNewPlayers(NetworkBehaviourId playerDataNetworkedId)
        {
            _networkedPlayerIds.Add(playerDataNetworkedId);
        }

        // adds new bots using their names as Id's
        public void AddNewBots(NetworkObject botPlayer)
        {
            _botPlayers.Add(botPlayer);

        }

        // we get permission from the host/server to respawn
        public void CanRespawnPlayer(NetworkObject playerObject, PlayerRef player)
        {
            if (Runner.IsServer)
            {
                var playerManager = playerObject.GetComponent<PlayerManager>();
                if (!playerManager._playerController.IsBot)
                    playerManager._playerStats.StartRespawnTimer(respawnDuration);

               StartCoroutine(playerManager._playerStats.ServerRespawn(respawnDuration));
            }
        }
    }

    public enum PlayerTeam // the teams
    {
        Red,
        Blue
    }

    public enum MatchType // different Match Types
    {
        DeathMatch,
        TeamDeathMatch
    }
}


