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
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Fusion.Photon.Realtime;


namespace Vauxland.FusionBrawler
{
    public class MainMenuManager : MonoBehaviour
    {
        [SerializeField] private TMP_InputField playerNickname; // where we change the players name
        [SerializeField] public GameNetworkRunnerManager runnerManager; // the network manager to spawn the network runner

        [SerializeField] public GameObject lobbyUI; // the lobby UI
        [SerializeField] private GameObject dropDownCanvas; // the canvas for the dropdown region

        [Header("CreateGame Panel Setup")]
        [SerializeField] public TMP_InputField gameSessionName;
        [SerializeField] private RawImage mapIcon;
        [SerializeField] private TMP_Text _mapNameText;
        [SerializeField] private Button selectMapButtonLeft;
        [SerializeField] private Button selectMapButtonRight;
        [SerializeField] private Button _deathMatchButton;
        [SerializeField] private Button _teamDeathMatchButton;
        [SerializeField] private TMP_Text _deathMatchText;
        [SerializeField] private TMP_Text _teamDeathMatchText;
        [SerializeField] private Toggle useBotsToggle;
        [SerializeField] private Toggle friendlyFireToggle;
        [SerializeField] private TMP_InputField botAmountInputField;
        [SerializeField] private TMP_InputField maxScoreAmountInputField;
        [SerializeField] private TMP_InputField matchTimeInputField;
        private bool useBots = true;
        private bool friendlyFire = false;
        private int botAmount = 5;
        private int maxScoreAmount = 10;
        private float matchTimeLength = 180f;

        [Header("Game Session Lobby Panel Setup")]
        public GameObject gameSessionListPanel;
        public GameSessionLobbyList sessionLobbyList;
        public GameObject joinGamePanel;
        [SerializeField] public TMP_Text loadingGameText;
        [SerializeField] private TMP_Dropdown regionDropdown;
        private readonly string[] regionDisplayNames = { "USA", "Europe", "Asia", "Japan"};
        private readonly string[] regionCodes = { "us", "eu", "asia", "jp" };
        private string selectedRegion;

        // setting up and accessing Player saved characters and weapons
        [Header("Player Visual")]
        public Transform playerModelSetter;

        private CharacterModelConfig characterModelConfig;
        protected WeaponConfig weaponConfig; // legacy primary reference
        protected WeaponConfig topWeaponConfig;
        protected WeaponConfig leftWeaponConfig;
        protected WeaponConfig rightWeaponConfig;
        protected CharacterConfig characterConfig;
        protected CosmeticConfig cosmeticConfig;
        private MatchType gameMode;
        private int selectedCharacterConfig = 0;
        private int selectedWeaponConfig = 0; // legacy primary selection
        private int selectedTopWeaponConfig = -1;
        private int selectedLeftWeaponConfig = -1;
        private int selectedRightWeaponConfig = -1;
        private int selectedCosmeticConfig = 0;
        private int selectedMapIndex = 0;
        private int matchType = 0;
         
        // the selected character config
        public int SelectedCharacterConfig
        {
            get => selectedCharacterConfig;
            set
            {
                selectedCharacterConfig = ClampSelection(value, MaxCharacterConfig);
                UpdatePlayerCharacter();
            }
        }

        // Legacy property. Equips the weapon into the mount declared by WeaponConfig.
        public int SelectedWeaponConfig
        {
            get => selectedWeaponConfig;
            set => SetSelectedWeaponForMount(value);
        }

        public int SelectedTopWeaponConfig => selectedTopWeaponConfig;
        public int SelectedLeftWeaponConfig => selectedLeftWeaponConfig;
        public int SelectedRightWeaponConfig => selectedRightWeaponConfig;

        public int GetSelectedWeaponForMount(WeaponMountType mountType)
        {
            switch (mountType)
            {
                case WeaponMountType.Top: return selectedTopWeaponConfig;
                case WeaponMountType.Left: return selectedLeftWeaponConfig;
                case WeaponMountType.Right: return selectedRightWeaponConfig;
                default: return -1;
            }
        }

        public void SetSelectedWeaponForMount(int weaponId)
        {
            WeaponConfig config = PlayerGameData.GetWeapon(weaponId);
            if (config == null)
                return;

            selectedWeaponConfig = weaponId;
            switch (config.requiredMount)
            {
                case WeaponMountType.Top:
                    selectedTopWeaponConfig = weaponId;
                    topWeaponConfig = config;
                    break;
                case WeaponMountType.Left:
                    selectedLeftWeaponConfig = weaponId;
                    leftWeaponConfig = config;
                    break;
                case WeaponMountType.Right:
                    selectedRightWeaponConfig = weaponId;
                    rightWeaponConfig = config;
                    break;
            }

            UpdatePlayerWeapons();
        }

        // the selected cosmetic config
        public int SelectedCosmeticConfig
        {
            get => selectedCosmeticConfig;
            set
            {
                selectedCosmeticConfig = ClampSelection(value, MaxCosmeticConfig);
                UpdatePlayerCosmetic();
            }
        }

        // the helper method used for the selected configs
        private int ClampSelection(int value, int max)
        {
            if (value < 0)
                return max;
            if (value > max)
                return 0;
            return value;
        }

        public int MaxCharacterConfig => PlayerGameData.Characters.Count - 1;
        public int MaxWeaponConfig => PlayerGameData.Weapons.Count - 1;
        public int MaxCosmeticConfig => PlayerGameData.Cosmetics.Count - 1;

        // loads all of our settings and last used player setup when we first open the game
        private void Start()
        {
            StartCoroutine(LoadPlayerRoutine());

            runnerManager.SetUpRunner();

            InitializeRegionDropdown();

            StartCoroutine(InitializeMapDisplay());

            // load previous settings of our bots or set new ones
            useBots = PlayerPrefs.GetInt("UseBots", 1) == 1;
            botAmount = PlayerPrefs.GetInt("BotAmount", 5);
            friendlyFire = PlayerPrefs.GetInt("FriendlyFire", 0) == 1;
            maxScoreAmount = PlayerPrefs.GetInt("MaxScoreAmount", 10);
            matchTimeLength = PlayerPrefs.GetFloat("MatchTimeLength", 180f);

            useBotsToggle.isOn = useBots;
            botAmountInputField.text = botAmount.ToString();

            useBotsToggle.onValueChanged.AddListener(OnUseBotsToggleChanged);
            botAmountInputField.onEndEdit.AddListener(OnBotAmountInputChanged);

            friendlyFireToggle.isOn = friendlyFire;
            friendlyFireToggle.onValueChanged.AddListener(OnFriendlyFireToggleChanged);

            maxScoreAmountInputField.text = maxScoreAmount.ToString();
            maxScoreAmountInputField.onEndEdit.AddListener(OnMaxScoreAmountInputChanged);

            matchTimeInputField.text = matchTimeLength.ToString();
            matchTimeInputField.onEndEdit.AddListener(OnMatchTimeInputChanged);
        }

        // just wait a tad before loading the player data
        private IEnumerator LoadPlayerRoutine()
        {
            yield return new WaitForSeconds(.5f);
            LoadLocalData();
        }

        // populates the region list
        private void InitializeRegionDropdown()
        {
            if (regionDropdown != null)
            {
                regionDropdown.ClearOptions();
                List<string> options = new List<string>(regionDisplayNames);
                regionDropdown.AddOptions(options);

                // load our saved region or set default
                selectedRegion = PlayerLocalSave.GetSelectedRegion();
                int index = System.Array.IndexOf(regionCodes, selectedRegion);
                if (index >= 0)
                {
                    regionDropdown.value = index;
                }
                else
                {
                    // default to first region if not found
                    regionDropdown.value = 0;
                    selectedRegion = regionCodes[0];
                }

                regionDropdown.onValueChanged.AddListener(OnRegionDropdownValueChanged);
                regionDropdown.RefreshShownValue();
            }
        }

        // changes to the region selected
        public void OnRegionDropdownValueChanged(int index)
        {
            if (index >= 0 && index < regionCodes.Length)
            {
                selectedRegion = regionCodes[index];
                Debug.Log($"Selected region: {regionDisplayNames[index]} ({selectedRegion})");

                // save the selected region
                PlayerLocalSave.SetSelectedRegion(selectedRegion);

                // update the Fusion AppSettings
                PhotonAppSettings.Global.AppSettings.FixedRegion = selectedRegion;
            }
        }

        private IEnumerator InitializeMapDisplay()
        {
            // wait until PlayerGameData has initialized
            yield return new WaitUntil(() => PlayerGameData.Maps.Count > 0);

            // load the selected map index from saved data
            selectedMapIndex = PlayerLocalSave.GetMap();

            UpdateMapDisplay();
        }

        // sets our use bots or not in create game panel
        private void OnUseBotsToggleChanged(bool value)
        {
            useBots = value;
            PlayerPrefs.SetInt("UseBots", useBots ? 1 : 0);
        }

        // sets our friendly fire toggle on or off when pressed
        private void OnFriendlyFireToggleChanged(bool value)
        {
            friendlyFire = value;
            PlayerPrefs.SetInt("FriendlyFire", friendlyFire ? 1 : 0);
        }

        // changes the amount of bots to set to spawn
        private void OnBotAmountInputChanged(string value)
        {
            if (int.TryParse(value, out int amount))
            {
                botAmount = amount;
                PlayerPrefs.SetInt("BotAmount", botAmount);
            }
        }

        // changes the max score value for the match
        private void OnMaxScoreAmountInputChanged(string value)
        {
            if (int.TryParse(value, out int amount))
            {
                maxScoreAmount = amount;
                PlayerPrefs.SetInt("MaxScoreAmount", maxScoreAmount);
            }
        }

        // changes the match time value for the match
        private void OnMatchTimeInputChanged(string value)
        {
            if (float.TryParse(value, out float amount))
            {
                matchTimeLength = amount;
                PlayerPrefs.SetFloat("MatchTimeLength", matchTimeLength);
            }
        }
        
        // navigates through list of maps
        public void SelectPreviousMap()
        {
            var maps = PlayerGameData.Maps;
            if (maps != null && maps.Count > 0)
            {
                selectedMapIndex = (selectedMapIndex - 1 + maps.Count) % maps.Count;
                UpdateMapDisplay();
            }
        }

        // navigates through list of maps
        public void SelectNextMap()
        {
            var maps = PlayerGameData.Maps;
            if (maps != null && maps.Count > 0)
            {
                selectedMapIndex = (selectedMapIndex + 1) % maps.Count;
                UpdateMapDisplay();
            }
        }

        // keep the UI buttons updated
        private void Update()
        {
            if (_deathMatchButton != null)
                _deathMatchButton.interactable = gameMode != MatchType.DeathMatch;

            if (_teamDeathMatchButton != null)
                _teamDeathMatchButton.interactable = gameMode != MatchType.TeamDeathMatch;

            if (_deathMatchText != null)
                _deathMatchText.gameObject.SetActive(gameMode == MatchType.DeathMatch);

            if (_teamDeathMatchText != null)
                _teamDeathMatchText.gameObject.SetActive(gameMode == MatchType.TeamDeathMatch);
        }

        // sets the match type to DeathMatch
        public void SetGameModeDeathMatch()
        {
            gameMode = MatchType.DeathMatch;
            matchType = 0;
            SaveLocalData();
        }

        // sets the match type to Team Deathmatch
        public void SetGameModeTeamDeathMatch()
        {
            gameMode = MatchType.TeamDeathMatch;
            matchType = 1;
            SaveLocalData();
        }

        // creates our game from the create game panel and loads the selected rules
        public void CreateGame()
        {
            lobbyUI.SetActive(false);
            dropDownCanvas.SetActive(false);
            joinGamePanel.SetActive(true);
            loadingGameText.text = "Creating Match...";

            SetPlayerGameData();

            MatchType selectedMatchType = (matchType == 0) ? MatchType.DeathMatch : MatchType.TeamDeathMatch;

            // get the selected map
            MapConfig selectedMap = PlayerGameData.GetMap(selectedMapIndex);
            if (selectedMap == null)
            {
                Debug.LogError("Selected map is null.");
                return;
            }
            string sceneName = selectedMap.sceneName;

            string sessionName = gameSessionName.text;
            if (string.IsNullOrEmpty(sessionName))
            {
                sessionName = GenerateDefaultSessionName(selectedMatchType);
            }

            int matchTimeLengthInt = Mathf.RoundToInt(matchTimeLength);

            // create our custom session properties dictionary // any other custom match properties you would like to add put here
            var sessionProperties = new Dictionary<string, SessionProperty>()
            {
                { "MatchType", (int)selectedMatchType },
                { "UseBots", useBots ? 1 : 0 },
                { "BotAmount", botAmount },
                { "FriendlyFire", friendlyFire ? 1 : 0 },
                { "MaxScoreAmount", maxScoreAmount },
                { "MatchTimeLength", matchTimeLengthInt },
                { "MapScene", sceneName },
                { "MapName" , selectedMap.mapName},
            };

            runnerManager.CreateNewGame(sessionName, selectedMatchType, selectedRegion, sessionProperties);
        }

        // opens the session browser to show list of matches joinable
        public void FindGame()
        {
            SetPlayerGameData();
            runnerManager.OnJoinMatchLobby();
            gameSessionListPanel.SetActive(true);
            sessionLobbyList.LookingForGameSession();
        }

        // the quick game method
        public void JoinQuickGame()
        {
            lobbyUI.SetActive(false);
            dropDownCanvas.SetActive(false);
            joinGamePanel.SetActive(true);
            loadingGameText.text = "Finding a match...";

            SetPlayerGameData();

            /*MatchType selectedMatchType = (matchType == 0) ? MatchType.DeathMatch : MatchType.TeamDeathMatch;

            runnerManager.JoinRandomGameOrCreate(selectedRegion, selectedMatchType);*/ // uncomment this if you want to join a match right away instead of searching for one

            StartCoroutine(SimulateSearchingForGame()); // you do not have to use this method, this is just to produce a more natural searching look lets say if your real player count is low
                                                        // just comment this out and uncomment the 2 commented out lines above
        }

        private IEnumerator SimulateSearchingForGame()
        {
            // generate a random search duration between 2 and 7 seconds
            float searchDuration = UnityEngine.Random.Range(2f, 7f);
            float elapsedTime = 0f;

            // display "Finding match..." during the search
            loadingGameText.text = "Finding match...";

            while (elapsedTime < searchDuration)
            {
                elapsedTime += Time.deltaTime;

                yield return null;
            }

            // after the search duration, change the text to "Match found"
            loadingGameText.text = "Match found";

            //wait a short time to let players see "Match found"
            yield return new WaitForSeconds(1f);

            // after the delay, attempt to join or create a game
            MatchType selectedMatchType = (matchType == 0) ? MatchType.DeathMatch : MatchType.TeamDeathMatch;

            runnerManager.JoinRandomGameOrCreate(selectedRegion, selectedMatchType);
        }

        // creates a name for our match session if we didn't add one when creating the match
        private string GenerateDefaultSessionName(MatchType matchType)
        {
            string baseName = "Open";//matchType == MatchType.DeathMatch ? "DeathMatchGame" : "TeamDeathMatchGame"; // change to this if you want a default name to say the name of match type
            int randomNum = Random.Range(1000, 9999);
            return $"{baseName}_{randomNum}";
        }

        // sets our current set up in the player game data script that will carry over into the match scene
        private void SetPlayerGameData()
        {
            var playerData = PlayerGameData.PlayerData;
            playerNickname.text = PlayerLocalSave.GetPlayerName();
            playerData.SetPlayerNickName(playerNickname.text);
            playerData.SetSelectedWeaponForMount(WeaponMountType.Top, selectedTopWeaponConfig);
            playerData.SetSelectedWeaponForMount(WeaponMountType.Left, selectedLeftWeaponConfig);
            playerData.SetSelectedWeaponForMount(WeaponMountType.Right, selectedRightWeaponConfig);
            playerData.SetSelectedCharacter(SelectedCharacterConfig);   
            playerData.SetSelectedCosmetic(SelectedCosmeticConfig);
        }

        // changes players name
        public void OnInputNameChanged(string eventInput)
        {
            PlayerLocalSave.SetPlayerName(playerNickname.text);
        }

        // updates the selected map image
        private void UpdateMapDisplay()
        {
            var maps = PlayerGameData.Maps;
            if (maps != null && maps.Count > 0)
            {
                selectedMapIndex = Mathf.Clamp(selectedMapIndex, 0, maps.Count - 1);
                MapConfig currentMap = PlayerGameData.GetMap(selectedMapIndex);
                if (currentMap != null)
                {
                    _mapNameText.text = currentMap.mapName;
                    mapIcon.texture = currentMap.mapImage;
                }
            }
        }

        // changes the character when we select a new one
        private void UpdatePlayerCharacter()
        {
            if (characterModelConfig != null)
                Destroy(characterModelConfig.gameObject);

            characterConfig = PlayerGameData.GetCharacter(SelectedCharacterConfig);
            if (characterConfig == null || characterConfig.characterModel == null)
                return;

            characterModelConfig = Instantiate(characterConfig.characterModel, playerModelSetter);
            UpdatePlayerWeapons();

            if (cosmeticConfig != null)
                characterModelConfig.SetCosmeticModel(cosmeticConfig.cosmeticModel);

            characterModelConfig.gameObject.SetActive(true);
        }

        public void UpdatePlayerWeapon()
        {
            SetSelectedWeaponForMount(selectedWeaponConfig);
        }

        public void UpdatePlayerWeapons()
        {
            if (characterModelConfig == null)
                return;

            characterModelConfig.ClearAllWeapons();
            AttachPreviewWeapon(selectedTopWeaponConfig, WeaponMountType.Top);
            AttachPreviewWeapon(selectedLeftWeaponConfig, WeaponMountType.Left);
            AttachPreviewWeapon(selectedRightWeaponConfig, WeaponMountType.Right);
        }

        private void AttachPreviewWeapon(int weaponId, WeaponMountType expectedMount)
        {
            if (weaponId < 0)
                return;

            WeaponConfig config = PlayerGameData.GetWeapon(weaponId);
            if (config == null || config.requiredMount != expectedMount)
                return;

            characterModelConfig.SetWeaponConfig(config, characterConfig);
        }

        // changes our players cosmetic when we select a new one from loadout
        public void UpdatePlayerCosmetic()
        {
            cosmeticConfig = PlayerGameData.GetCosmetic(SelectedCosmeticConfig);
            if (characterModelConfig != null && cosmeticConfig != null)
            {
                characterModelConfig.SetCosmeticModel(cosmeticConfig.cosmeticModel);
            }
        }

        // saves our players current set up
        public void SaveLocalData()
        {
            PlayerLocalSave.SetCharacter(SelectedCharacterConfig);
            PlayerLocalSave.SetTopWeapon(selectedTopWeaponConfig);
            PlayerLocalSave.SetLeftWeapon(selectedLeftWeaponConfig);
            PlayerLocalSave.SetRightWeapon(selectedRightWeaponConfig);
            PlayerLocalSave.SetCosmetic(SelectedCosmeticConfig);
            PlayerLocalSave.SetPlayerName(playerNickname.text);
            PlayerLocalSave.SetMatchType(gameMode);
            PlayerLocalSave.SetSelectedRegion(selectedRegion);
            PlayerLocalSave.SetMap(selectedMapIndex);
        }

        // loads our players current set up
        public void LoadLocalData()
        {
            playerNickname.text = PlayerLocalSave.GetPlayerName();
            SelectedCharacterConfig = PlayerLocalSave.GetCharacter();
            selectedTopWeaponConfig = PlayerLocalSave.GetTopWeapon();
            selectedLeftWeaponConfig = PlayerLocalSave.GetLeftWeapon();
            selectedRightWeaponConfig = PlayerLocalSave.GetRightWeapon();
            selectedWeaponConfig = selectedTopWeaponConfig >= 0 ? selectedTopWeaponConfig : (selectedLeftWeaponConfig >= 0 ? selectedLeftWeaponConfig : selectedRightWeaponConfig);
            topWeaponConfig = PlayerGameData.GetWeapon(selectedTopWeaponConfig);
            leftWeaponConfig = PlayerGameData.GetWeapon(selectedLeftWeaponConfig);
            rightWeaponConfig = PlayerGameData.GetWeapon(selectedRightWeaponConfig);
            SelectedCosmeticConfig = PlayerLocalSave.GetCosmetic();
            matchType = PlayerLocalSave.GetMatchType();

            if (matchType == 0)
                gameMode = MatchType.DeathMatch;
            else if (matchType == 1)
                gameMode = MatchType.TeamDeathMatch;

            // load selected region
            selectedRegion = PlayerLocalSave.GetSelectedRegion();
            if (string.IsNullOrEmpty(selectedRegion))
            {
                selectedRegion = regionCodes[0]; // default region
            }

            // set dropdown value
            int index = System.Array.IndexOf(regionCodes, selectedRegion);
            if (regionDropdown != null && index >= 0)
            {
                regionDropdown.value = index;
                regionDropdown.RefreshShownValue();
            }

            selectedMapIndex = PlayerLocalSave.GetMap();
            UpdateMapDisplay();

            // SelectedCharacterConfig creates the preview before the three weapon IDs
            // are loaded. Refresh once more after all saved slots are available.
            UpdatePlayerWeapons();
        }
    }
}