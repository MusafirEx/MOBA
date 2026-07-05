/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using Fusion;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vauxland.FusionBrawler
{
    // our player score data used in the top player updater
    class PlayerScoreData
    {
        public string Name;
        public int Kills;
        public int Deaths;
        public bool IsBot;
        public PlayerRef PlayerRef;
        public string BotID;
        public int TeamInt;
    }
    public class MatchUIHandler : MonoBehaviour
    {
        [Header("Match Timers UI")]
        public TextMeshProUGUI gameInfoText; // the the beginning of the match text saying "Match starts in"
        public TextMeshProUGUI countDownTimerText; // the match countdown text
        public TextMeshProUGUI matchTimerText; // the match timer text
        public TextMeshProUGUI respawnTimerText; // the respawn timer text
        public GameObject matchStartInfoHolder; // the holder parent transform of the match start text
        public GameObject matchTimeHolder; // the holder parent transform of the match timer text

        [Header("Match UI Set Up")]
        public GameObject teamDeathMatchUI; // team deatch match specific UI
        public GameObject scoreBoardPanel; // the death match score board transform holder
        public GameObject teamScoreBoardPanel; // the team death match score board panel
        public GameObject respawnPanel; // the respawn UI Panel
        public GameObject localPlayerUI; // the local players UI panel. (the ammo text and weapon Icon and Kill count text)
        public GameObject mobileControls; // mobile controls UI
        public Joystick leftJoystick; // left joystick UI
        public Joystick rightJoystick; // right joystick UI
        public Button shootButton; // shoot button UI (right joystick)
        [SerializeField] private PlayerScoreEntry _playerEntryPrefab = null;
        [SerializeField] private PlayerScoreEntry _playerMainEntryPrefab = null;
        [SerializeField] private PlayerScoreEntry _playerTeamRedEntryPrefab = null;
        [SerializeField] private PlayerScoreEntry _playerTeamBlueEntryPrefab = null;
        [SerializeField] private PlayerKillPopup _playerKillPopupPrefab = null;
        [SerializeField] private float killPopupDuration; // how long you want the kill feed pop up to show
        [SerializeField] private GameObject killPopupContainer; // where the kill pop ups will spawn
        [SerializeField] private GameObject playerEntryContainer; // where the top 3 players score update entries will spawn
        [SerializeField] private GameObject scoreBoardContainer; // the score board for deathmatch gamemode
        [SerializeField] private GameObject teamRedScoreBoardContainer; // team red's scoreboard
        [SerializeField] private GameObject teamBlueScoreBoardContainer; // team blue's scorebaord.
        [SerializeField] private TextMeshProUGUI teamRedScoreText; // the main score text for team red
        [SerializeField] private TextMeshProUGUI teamBlueScoreText; // main score text for team blue
        [SerializeField] private TextMeshProUGUI winningTeamText; // wining player text to show winning player
        [SerializeField] private TextMeshProUGUI winningPlayerText; // winning team text to show winning team

        [Header("Local Player UI")]
        public TMP_Text localHealthAmount;
        public Image localHealthFill;
        public Image localBodyArmorFill;

        public TMP_Text localAmmoAmount; // the equipped weapons ammo amount text
        public Image localWeaponIcon; // legacy primary weapon icon
        [Header("Mounted Weapon UI (Optional)")]
        public TMP_Text topAmmoAmount;
        public TMP_Text leftAmmoAmount;
        public TMP_Text rightAmmoAmount;
        public Image topWeaponIcon;
        public Image leftWeaponIcon;
        public Image rightWeaponIcon;

        [Header("Mounted Weapon Reload UI (Optional)")]
        public GameObject topReloadHolder;
        public GameObject leftReloadHolder;
        public GameObject rightReloadHolder;
        public Image topReloadFill;
        public Image leftReloadFill;
        public Image rightReloadFill;
        public TextMeshProUGUI killCount; // the kill count text

        private int teamRedScore; // team red score UI text
        private int teamBlueScore; // team blue scores UI text
        public GameMatchManager matchManager; // the game match manager in the scene

        // Data dictionaries
        private Dictionary<PlayerRef, string> _playerNickNames = new Dictionary<PlayerRef, string>();
        private Dictionary<PlayerRef, int> _playerKills = new Dictionary<PlayerRef, int>();
        private Dictionary<PlayerRef, int> _playerDeaths = new Dictionary<PlayerRef, int>();
        private Dictionary<PlayerRef, int> _playerTeams = new Dictionary<PlayerRef, int>();

        private Dictionary<string, string> _botNickNames = new Dictionary<string, string>();
        private Dictionary<string, int> _botKills = new Dictionary<string, int>();
        private Dictionary<string, int> _botDeaths = new Dictionary<string, int>();
        private Dictionary<string, int> _botTeams = new Dictionary<string, int>();

        private void Start()
        {
            // clear all of our entries containers so theyre empty when match starts
            ClearContainerList(playerEntryContainer);
            ClearContainerList(scoreBoardContainer);
            ClearContainerList(teamRedScoreBoardContainer);
            ClearContainerList(teamBlueScoreBoardContainer);
            ClearContainerList(killPopupContainer);
        }

        private void ClearContainerList(GameObject container)
        {
            foreach (Transform child in container.transform)
            {
                Destroy(child.gameObject);
            }
        }


        public void UpdateLocalPlayerHealth(
            int currentHealth,
            int maximumHealth,
            int currentBodyArmor,
            int maximumBodyArmor)
        {
            if (localHealthAmount != null)
                localHealthAmount.text = currentHealth.ToString();

            if (localHealthFill != null)
            {
                localHealthFill.fillAmount = maximumHealth > 0
                    ? Mathf.Clamp01(currentHealth / (float)maximumHealth)
                    : 0f;
            }

            if (localBodyArmorFill != null)
            {
                localBodyArmorFill.fillAmount = maximumBodyArmor > 0
                    ? Mathf.Clamp01(currentBodyArmor / (float)maximumBodyArmor)
                    : 0f;
            }
        }

        public void UpdateMountedWeaponUI(
            WeaponConfig topWeapon, int topLoaded, int topReserve, bool topReloading, float topReloadProgress,
            WeaponConfig leftWeapon, int leftLoaded, int leftReserve, bool leftReloading, float leftReloadProgress,
            WeaponConfig rightWeapon, int rightLoaded, int rightReserve, bool rightReloading, float rightReloadProgress)
        {
            SetMountedSlotUI(
                topAmmoAmount, topWeaponIcon, topReloadHolder, topReloadFill,
                topWeapon, topLoaded, topReserve, topReloading, topReloadProgress);

            SetMountedSlotUI(
                leftAmmoAmount, leftWeaponIcon, leftReloadHolder, leftReloadFill,
                leftWeapon, leftLoaded, leftReserve, leftReloading, leftReloadProgress);

            SetMountedSlotUI(
                rightAmmoAmount, rightWeaponIcon, rightReloadHolder, rightReloadFill,
                rightWeapon, rightLoaded, rightReserve, rightReloading, rightReloadProgress);
        }

        private static void SetMountedSlotUI(
            TMP_Text ammoText,
            Image icon,
            GameObject reloadHolder,
            Image reloadFill,
            WeaponConfig weapon,
            int loaded,
            int reserve,
            bool isReloading,
            float reloadProgress)
        {
            if (ammoText != null)
                ammoText.text = weapon != null ? $"{loaded} / {reserve}" : "--";

            if (icon != null)
            {
                icon.sprite = weapon != null ? weapon.weaponIcon : null;
                icon.enabled = weapon != null && weapon.weaponIcon != null;
            }

            if (reloadHolder != null)
                reloadHolder.SetActive(weapon != null && isReloading);

            if (reloadFill != null)
                reloadFill.fillAmount = isReloading ? Mathf.Clamp01(reloadProgress) : 0f;
        }

        // adds our players entries as they join
        public void AddPlayerEntry(PlayerRef playerRef, PlayerNetworkController playerDataNetworked, int teamInt)
        {
            if (_playerKills.ContainsKey(playerRef) || playerDataNetworked == null) return;

            string nickName = playerDataNetworked.PlayerNickName.ToString();
            int deaths = 0;
            int kills = 0;

            _playerNickNames[playerRef] = nickName;
            _playerKills[playerRef] = kills;
            _playerDeaths[playerRef] = deaths;
            _playerTeams[playerRef] = teamInt;

            UpdateTopPlayerEntries();
        }

        // adds our bot entries
        public void AddBotPlayerEntry(PlayerNetworkController playerDataNetworked, int teamInt)
        {
            if (playerDataNetworked == null)
                return;

            string botID = playerDataNetworked.PlayerNickName.ToString();
            int kills = 0;
            int deaths = 0;
            string botName = botID;

            if (_botKills.ContainsKey(botID))
                return;

            _botNickNames[botID] = botName;
            _botKills[botID] = kills;
            _botDeaths[botID] = deaths;
            _botTeams[botID] = teamInt;

            UpdateTopPlayerEntries();
        }

        // removes players entries when they leave
        public void RemovePlayerEntry(PlayerRef playerRef)
        {
            // remove the player's data from the dictionaries
            _playerNickNames.Remove(playerRef);
            _playerKills.Remove(playerRef);
            _playerDeaths.Remove(playerRef);
            _playerTeams.Remove(playerRef);

            UpdateTopPlayerEntries();
        }

        // if you remove bots this will remove their entries
        public void RemoveBotPlayerEntry(string botID)
        {
            _botNickNames.Remove(botID);
            _botKills.Remove(botID);
            _botDeaths.Remove(botID);
            _botTeams.Remove(botID);

            UpdateTopPlayerEntries();
        }

        // player update entry methods
        // update the player Kills
        public void UpdatePlayerKills(PlayerRef player, int kills)
        {
            if (!_playerKills.ContainsKey(player)) return;

            _playerKills[player] = kills;

            UpdateTopPlayerEntries();
        }

        // update the player deaths
        public void UpdatePlayerDeaths(PlayerRef player, int deaths)
        {
            if (!_playerDeaths.ContainsKey(player)) return;

            _playerDeaths[player] = deaths;

            UpdateTopPlayerEntries();
        }

        // update the players Nickname
        public void UpdatePlayerNickName(PlayerRef player, string nickName)
        {
            if (!_playerNickNames.ContainsKey(player)) return;

            _playerNickNames[player] = nickName;

            UpdateTopPlayerEntries();
        }

        //bot Update entry methods
        // update bot kills
        public void UpdateBotKills(string botID, int kills)
        {
            if (!_botKills.ContainsKey(botID)) return;

            _botKills[botID] = kills;

            UpdateTopPlayerEntries();
        }

        // update bot deaths
        public void UpdateBotDeaths(string botID, int deaths)
        {
            if (!_botDeaths.ContainsKey(botID)) return;

            _botDeaths[botID] = deaths;

            UpdateTopPlayerEntries();
        }

        // update the bots nickname
        public void UpdateBotNickName(string nickName)
        {
            string botID = nickName;

            if (!_botNickNames.ContainsKey(botID)) return;

            _botNickNames[botID] = nickName;

            UpdateTopPlayerEntries();
        }

        // update the Teams Scores UI
        public void UpdateTeamScoreUI(int team)
        {
            if (team == 1)
            {
                teamRedScoreText.text = matchManager.TeamRedScore.ToString();
            }
            else if (team == 2)
            {
                teamBlueScoreText.text = matchManager.TeamBlueScore.ToString();
            }
        }

        // set the teams Score UI when new player joins
        public void SetTeamScoreUI()
        {
            teamRedScoreText.text = matchManager.TeamRedScore.ToString();
            teamBlueScoreText.text = matchManager.TeamBlueScore.ToString();
        }

        // shows our kill feed pop ups in the kill feed transform
        public void ShowKillPopup(string attacker, string victim, string weaponName, int weaponId, int playerTeam)
        {
            WeaponConfig weaponConfig = PlayerGameData.GetWeapon(weaponId);
            var killEntry = Instantiate(_playerKillPopupPrefab, killPopupContainer.transform);

            var attackerNameText = killEntry.attackerNameText;
            var victimNameText = killEntry.victimNameText;
            var weaponNameText = killEntry.weaponNameText;
            var weaponImage = killEntry.weaponIcon;

            killEntry.teamRedImage.SetActive(playerTeam == 1);
            killEntry.teamBlueImage.SetActive(playerTeam == 2);

            if (attackerNameText != null)
                attackerNameText.text = attacker;

            if (victimNameText != null)
                victimNameText.text = victim;

            if (weaponNameText != null)
                weaponNameText.text = weaponName;

            if (weaponImage != null)
                weaponImage.sprite = weaponConfig.weaponIcon;

            Destroy(killEntry.gameObject, killPopupDuration);
        }

        // shows the winning player at the end of the match
        public void ShowWinningPlayer(string playerName, int kills)
        {
            winningPlayerText.gameObject.SetActive(true);
            winningPlayerText.text = $"{playerName} won with {kills} kills";
        }

        // shows the winning team at the end of a match
        public void ShowWinningTeam(PlayerTeam winningTeam)
        {
            winningTeamText.gameObject.SetActive(true);
            winningTeamText.text = winningTeam == PlayerTeam.Red ? "Red Team Wins!" : "Blue Team Wins!";
        }

        public void OnEmoteButtonPressed(int emoteIndex)
        {
            // get the local players visual controller
            var localPlayer = PlayerNetworkController.LocalPlayer;
            if (localPlayer != null)
            {
                var playerVisuals = localPlayer.GetComponent<PlayerVisualsController>();
                if (playerVisuals != null)
                {
                    playerVisuals.ShowEmote(emoteIndex);
                }
            }
        }

        // dynamically updates players and bots scores based on their kills
        private void UpdateTopPlayerEntries()
        {
            List<PlayerScoreData> allPlayers = new List<PlayerScoreData>();

            // add players to our player score data
            foreach (var playerRef in _playerKills.Keys)
            {
                var kills = _playerKills[playerRef];
                var deaths = _playerDeaths[playerRef];
                var name = _playerNickNames[playerRef];
                var teamInt = _playerTeams[playerRef];

                allPlayers.Add(new PlayerScoreData
                {
                    Name = name,
                    Kills = kills,
                    Deaths = deaths,
                    IsBot = false,
                    PlayerRef = playerRef,
                    TeamInt = teamInt
                });
            }

            // add the bots to our player score data
            foreach (var botID in _botKills.Keys)
            {
                var kills = _botKills[botID];
                var deaths = _botDeaths[botID];
                var name = _botNickNames[botID];
                var teamInt = _botTeams[botID];

                allPlayers.Add(new PlayerScoreData
                {
                    Name = name,
                    Kills = kills,
                    Deaths = deaths,
                    IsBot = true,
                    BotID = botID,
                    TeamInt = teamInt
                });
            }

            if (matchManager.matchType == MatchType.DeathMatch)
            {
                // sort all players by kills descending
                allPlayers.Sort((a, b) => b.Kills.CompareTo(a.Kills));

                // take top 3 (or however many you would like)
                var topPlayers = allPlayers.Take(3).ToList();

                // clear the containers on the scorebaord transforms
                ClearContainerList(playerEntryContainer);
                ClearContainerList(scoreBoardContainer);

                foreach (var playerData in topPlayers)
                {
                    PlayerScoreEntry entry = Instantiate(_playerEntryPrefab, playerEntryContainer.transform);

                    entry.playerNameText.text = playerData.Name;
                    entry.playerKillsText.text = playerData.Kills.ToString();
                    if (entry.playerDeathsText != null)
                        entry.playerDeathsText.text = playerData.Deaths.ToString();
                }

                // update the main scoreboard
                foreach (var playerData in allPlayers)
                {
                    PlayerScoreEntry mainEntry = Instantiate(_playerMainEntryPrefab, scoreBoardContainer.transform);

                    mainEntry.playerNameText.text = playerData.Name;
                    mainEntry.playerKillsText.text = playerData.Kills.ToString();
                    if (mainEntry.playerDeathsText != null)
                        mainEntry.playerDeathsText.text = playerData.Deaths.ToString();

                }
            }
            else if (matchManager.matchType == MatchType.TeamDeathMatch)
            {
                // clear the team score board containers
                ClearContainerList(teamRedScoreBoardContainer);
                ClearContainerList(teamBlueScoreBoardContainer);

                // get the players for each team
                var teamRedPlayers = allPlayers.Where(p => p.TeamInt == 1).ToList();
                var teamBluePlayers = allPlayers.Where(p => p.TeamInt == 2).ToList();

                // sort each team's players by kills descending
                teamRedPlayers.Sort((a, b) => b.Kills.CompareTo(a.Kills));
                teamBluePlayers.Sort((a, b) => b.Kills.CompareTo(a.Kills));

                // update tema Red UI
                foreach (var playerData in teamRedPlayers)
                {
                    PlayerScoreEntry entry = Instantiate(_playerTeamRedEntryPrefab, teamRedScoreBoardContainer.transform);

                    entry.playerNameText.text = playerData.Name;
                    entry.playerKillsText.text = playerData.Kills.ToString();
                    if (entry.playerDeathsText != null)
                        entry.playerDeathsText.text = playerData.Deaths.ToString();
                }

                // update team Blue UI
                foreach (var playerData in teamBluePlayers)
                {
                    PlayerScoreEntry entry = Instantiate(_playerTeamBlueEntryPrefab, teamBlueScoreBoardContainer.transform);

                    entry.playerNameText.text = playerData.Name;
                    entry.playerKillsText.text = playerData.Kills.ToString();
                    if (entry.playerDeathsText != null)
                        entry.playerDeathsText.text = playerData.Deaths.ToString();
                }
            }
        }
    }
}


