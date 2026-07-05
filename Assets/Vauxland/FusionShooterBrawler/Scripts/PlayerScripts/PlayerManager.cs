/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using Fusion;
using UnityEngine;

namespace Vauxland.FusionBrawler
{
    public class PlayerManager : NetworkBehaviour
    {
        // all in one script setter of all the player systems attached to the player prefab and game scripts 
        // instead of setting players scripts components needed to work together in each script instead can just call this script.
        [HideInInspector] public PlayerNetworkController _playerController = null;
        [HideInInspector] public PlayerStatsManager _playerStats = null;
        [HideInInspector] public PlayerVisualsController _playerVisuals = null;
        [HideInInspector] public PlayerMovementManager _playerMovement = null;
        [HideInInspector] public ProjectileController _projectileController = null;
        [HideInInspector] public MountedWeaponCombatController _mountedWeaponCombat = null;
        [HideInInspector] public NetworkObject _networkObject = null;
        [HideInInspector] public CharacterController _characterController = null;
        [HideInInspector] public NetworkCharacterController _networkCharacterController = null;
        [HideInInspector] public GameMatchManager _matchManager = null;
        [HideInInspector] public MatchUIHandler _matchUI = null;
        [HideInInspector] public PlayerInputManager _playerInput = null;
        [HideInInspector] public MobileControls _mobileControls = null;
        [HideInInspector] public BotController _botController = null;
        [Networked] public NetworkBool PlayerSetUp { get; set; }

        // called when the player object is spawned into the game
        public override void Spawned()
        {
            PlayerSetUp = false; // before other scripts can access this script

            // assign all of player components
            _playerController = GetComponent<PlayerNetworkController>();
            _playerStats = GetComponent<PlayerStatsManager>();
            _playerVisuals = GetComponent<PlayerVisualsController>();
            _playerMovement = GetComponent<PlayerMovementManager>();
            _projectileController = GetComponent<ProjectileController>();
            _mountedWeaponCombat = GetComponent<MountedWeaponCombatController>();
            _networkObject = GetComponent<NetworkObject>();
            _characterController = GetComponent<CharacterController>();
            _networkCharacterController = GetComponent<NetworkCharacterController>();
            _matchManager = FindObjectOfType<GameMatchManager>();
            _matchUI = FindObjectOfType<MatchUIHandler>();

            // handle setup for the local player (with input authority)
            if (Object.HasInputAuthority)
            {
                // set up UI elements and mobile controls
                _matchUI.SetTeamScoreUI();
                _mobileControls = GetComponent<MobileControls>();
                _mobileControls.enabled = true;
                _mobileControls.movementJoystick = _matchUI.leftJoystick;
                _mobileControls.aimJoystick = _matchUI.rightJoystick;
                _mobileControls.shootButton = _matchUI.shootButton;
                _mobileControls.SetUpShootButtonEventTriggers();

                // enable mobile controls UI if on mobile or testing mobile controls
                if (PlayerGameData.PlayerData.testMobileControls || Application.isMobilePlatform)
                    _matchUI.mobileControls.SetActive(true);

                // initialize player input
                _playerInput = GetComponent<PlayerInputManager>();
                _playerInput.isSet = true;
            }
            else
            {
                // handle setup for bots if state authority and player is a bot
                if (Object.HasStateAuthority && _playerController.IsBot)
                {
                    _playerStats.SetUpBot(); // set up bot-specific stats
                    _playerController.SetBotName(); // assign bot name
                    _botController = GetComponent<BotController>();
                    _botController.enabled = true; // enable bot controller
                }
            }

            // handle match UI setup for players and bots in DeathMatch mode
            if (_matchManager.matchType == MatchType.DeathMatch && !_playerController.IsBot)
            {
                // add player entry in the match UI
                _matchUI.AddPlayerEntry(Object.InputAuthority, _playerController, 0);

                // refresh Match UI texts in Spawned to set to initial values.
                _matchUI.UpdatePlayerNickName(Object.InputAuthority, _playerController.PlayerNickName.ToString());
                _matchUI.UpdatePlayerKills(Object.InputAuthority, _playerController.Kills);
                _matchUI.UpdatePlayerDeaths(Object.InputAuthority, _playerController.Deaths);
            }
            else if (_matchManager.matchType == MatchType.DeathMatch && _playerController.IsBot)
            {
                // add bot entry in the match UI
                _matchUI.AddBotPlayerEntry(_playerController, 0);

                // update UI with initial bot stats
                _matchUI.UpdateBotNickName(_playerController.PlayerNickName.ToString());
                _matchUI.UpdateBotKills(_playerController.PlayerNickName.ToString(), _playerController.Kills);
                _matchUI.UpdateBotDeaths(_playerController.PlayerNickName.ToString(), _playerController.Deaths);
            }

            // update player nickname in the UI
            if (!_playerController.IsBot)
                _playerController.UpdatePlayerNickname(Object.InputAuthority, _playerController.PlayerNickName.ToString());
            else if (_playerController.IsBot)
                _playerVisuals.playerNickNameText.text = _playerController.PlayerNickName.ToString();

            PlayerSetUp = true; // player setup is now complete and ready
        }
    }
}
