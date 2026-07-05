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
using System;

namespace Vauxland.FusionBrawler
{
    public class PlayerNetworkController : NetworkBehaviour
    {
        // our local player
        public static PlayerNetworkController LocalPlayer { get; set; }

        // static list to keep track of all players
        public static List<PlayerNetworkController> AllPlayers = new List<PlayerNetworkController>();

        // when the player enters hiding area
        public event Action<PlayerNetworkController> OnVisibilityChanged;

        [HideInInspector]
        [Networked]
        public NetworkString<_16> PlayerNickName { get; set; } // players name displayed

        // Player components
        protected PlayerManager _playerManager;
        private ChangeDetector _cacheChangeDetector;

        [HideInInspector]
        [Networked]
        public int Kills { get; private set; } // networked kills

        [HideInInspector]
        [Networked]
        public int Deaths { get; private set; } // netowrked deaths

        [HideInInspector]
        [Networked]
        public int TeamInt { get; private set; } // networked team

        [HideInInspector]
        [Networked] public NetworkBool IsHiding { get; set; } //networked state if hiding

        [HideInInspector]
        [Networked] public NetworkBool IsBot { get; set; } // networked bool to let players know if this is a bot

        [HideInInspector]
        public List<PlayerManager> playersInHidingZone = new List<PlayerManager>(); // amount of player sin the hiding zone


        public override void Spawned()
        {
            TeamInt = 0;
            IsHiding = false;

            var spawnManager = FindObjectOfType<SpawnManager>();

            spawnManager.AddToEntry(Object.InputAuthority.PlayerId, this.Object);
            _cacheChangeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            _playerManager = GetComponent<PlayerManager>();

            // --- local player
            if (Object.HasInputAuthority)
            {
                LocalPlayer = this;
                SetPlayerComponents();
            }

            // --- host player
            if (Object.HasStateAuthority)
            {
                Kills = 0;
                Deaths = 0;
            }

            // adds this player to the list of all players when spawned
            AllPlayers.Add(this);

            OnVisibilityChanged += HandleVisibilityChanged;

        }

        // sets our players nickname and then calls rpc to let all players know the nickname
        private void SetPlayerComponents()
        {
            var nickName = PlayerGameData.PlayerData.GetPlayerNickName();
            RpcSetPlayerNickName(nickName);
        }

        // sets the bots nickname
        public void SetBotName()
        {
            string botName = PlayerGameData.PlayerData.GetBotName();
            _playerManager._playerVisuals.playerNickNameText.text = botName;
            PlayerNickName = botName;           
        }

        // syncs all of the values to all players
        public override void Render()
        {
            if (_cacheChangeDetector == null) return;

            foreach (var change in _cacheChangeDetector.DetectChanges(this, out var prev, out var current))
            {
                switch (change)
                {
                    case nameof(PlayerNickName):
                        UpdatePlayerNickname(Object.InputAuthority, PlayerNickName.ToString());
                        _playerManager._matchUI.UpdatePlayerNickName(Object.InputAuthority, PlayerNickName.ToString());
                        break;
                    case nameof(Kills):
                        _playerManager._matchUI.UpdatePlayerKills(Object.InputAuthority, Kills);
                        if (Object.HasInputAuthority)
                            _playerManager._matchUI.killCount.text = Kills.ToString();
                        if (IsBot)
                            _playerManager._matchUI.UpdateBotKills(PlayerNickName.ToString(), Kills);
                        var killsReader = GetPropertyReader<int>(nameof(Kills));
                        var (oldKills, newKills) = killsReader.Read(prev, current);
                        break;
                    case nameof(Deaths):
                        _playerManager._matchUI.UpdatePlayerDeaths(Object.InputAuthority, Deaths);
                        if (IsBot)
                            _playerManager._matchUI.UpdateBotDeaths(PlayerNickName.ToString(), Deaths);
                        break;
                    case nameof(TeamInt):
                        var teamReader = GetPropertyReader<int>(nameof(TeamInt));
                        var (oldTeam, newTeam) = teamReader.Read(prev, current);
                        ChangeTeamVisuals(oldTeam, newTeam);
                        break;
                }

            }
        }

        // changes the players nickname text 
        public void UpdatePlayerNickname(PlayerRef player, string playerName)
        {
            Debug.Log("Name was changed");
            _playerManager._playerVisuals.playerNickNameText.text = playerName;
        }

        // records the players kills
        public void AddKills(int kills)
        {
            Kills += kills;
            _playerManager._matchManager.UpdateMatchScore(TeamInt, kills); // updates the teams scores on a kill              
        }

        // records the players deaths
        public void AddDeaths(int deaths)
        {
            Deaths += deaths;
        }

        // sets our players team and calls rpc to let all players know our team
        public void SetTeamInt(int teamInt)
        {
            if (Object.HasInputAuthority)
            {
                RpcSetTeamInt(teamInt);
            }

            TeamInt = teamInt;
        }

        // sets our state to hiding
        public void SetIsHiding(bool isHiding)
        {
            IsHiding = isHiding;

            NotifyVisibilityChange();
            
            OnVisibilityChanged?.Invoke(this);
        }

        private void NotifyVisibilityChange()
        {
            // notify all other players
            foreach (var player in AllPlayers)
            {
                if (player != this)
                {
                    player.HandleVisibilityChanged(this);
                }
            }
        }

        private void HandleVisibilityChanged(PlayerNetworkController player)
        {
            // only update visibility if this player is the local player
            if (Object.HasInputAuthority)
            {
                UpdateOtherPlayersVisibility();
            }
        }

        // let other players know if this player should be seen
        void UpdateOtherPlayersVisibility()
        {
            foreach (var otherPlayer in AllPlayers)
            {
                if (otherPlayer == this)
                    continue;

                bool shouldSee = ShouldSeePlayer(otherPlayer);
                otherPlayer._playerManager._playerVisuals.SetVisible(shouldSee);
            }
        }

        // if we can see a player hiding or not
        bool ShouldSeePlayer(PlayerNetworkController otherPlayer)
        {
            if (this.TeamInt == otherPlayer.TeamInt && _playerManager._matchManager.matchType != MatchType.DeathMatch)
            {
                // team members can always see each other
                return true;
            }
            else
            {
                if (otherPlayer.IsHiding && !this.IsHiding)
                {
                    // enemy is hiding and we are not hiding, so we cannot see them
                    return false;
                }
                else
                {
                    // else, we can see them
                    return true;
                }
            }
        }

        // change our team UI and color visuals on player object
        public void ChangeTeamVisuals(int oldTeam, int newTeam)
        {
            if (_playerManager._matchManager.matchType == MatchType.TeamDeathMatch)
            {
                if (IsBot)
                {
                    _playerManager._matchUI.AddBotPlayerEntry(this, newTeam);

                    _playerManager._matchUI.UpdateBotNickName(PlayerNickName.ToString());
                    _playerManager._matchUI.UpdateBotKills(PlayerNickName.ToString(), Kills);
                    _playerManager._matchUI.UpdateBotDeaths(PlayerNickName.ToString(), Deaths);
                }
                else
                {
                    _playerManager._matchUI.AddPlayerEntry(Object.InputAuthority, this, newTeam);

                    _playerManager._matchUI.UpdatePlayerNickName(Object.InputAuthority, PlayerNickName.ToString());
                    _playerManager._matchUI.UpdatePlayerKills(Object.InputAuthority, Kills);
                    _playerManager._matchUI.UpdatePlayerDeaths(Object.InputAuthority, Deaths);
                }
                
            }

            switch (newTeam)
            {
                case 1:
                    SetVisuals(_playerManager._playerVisuals.teamRedVisuals, true);
                    SetVisuals(_playerManager._playerVisuals.teamBlueVisuals, false);
                    _playerManager._playerVisuals.noTeamVisual.SetActive(false);
                    break;
                case 2:
                    SetVisuals(_playerManager._playerVisuals.teamRedVisuals, false);
                    SetVisuals(_playerManager._playerVisuals.teamBlueVisuals, true);
                    _playerManager._playerVisuals.noTeamVisual.SetActive(false);
                    break;
                case 0:
                    SetVisuals(_playerManager._playerVisuals.teamRedVisuals, false);
                    SetVisuals(_playerManager._playerVisuals.teamBlueVisuals, false);
                    _playerManager._playerVisuals.noTeamVisual.SetActive(true);
                    break;
            }
        }

        // helper method for setting the team visuals
        private void SetVisuals(GameObject[] visuals, bool isActive)
        {
            foreach (var visual in visuals)
            {
                visual.SetActive(isActive);
            }
        }

        // when the player leaves
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _playerManager._matchUI.RemovePlayerEntry(Object.InputAuthority);
            // remove this player from the list when despawned
            AllPlayers.Remove(this);
            OnVisibilityChanged -= HandleVisibilityChanged;

        }

        // rpc used to send player nickname to the Host
        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        private void RpcSetPlayerNickName(string nickName)
        {
            if (string.IsNullOrEmpty(nickName)) return;
            PlayerNickName = nickName;
        }

        // rpc used to send player team to the Host
        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        private void RpcSetTeamInt(int teamInt)
        {
            TeamInt = teamInt;
        }
    }

}
