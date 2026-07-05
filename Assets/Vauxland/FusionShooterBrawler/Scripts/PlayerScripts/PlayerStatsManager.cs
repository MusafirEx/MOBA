/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Vauxland.FusionBrawler
{
    // here we will handle all of our players stat changes including ammo, body armor, etc.
    public class PlayerStatsManager : NetworkBehaviour
    {
        [HideInInspector]
        public bool AcceptPlayerInput => Object.IsValid && IsAlive && !_playerManager._matchManager.GameIsOver;
        public bool IsAlive => Object.IsValid && Hp > 0f;
        public bool CanShoot => ReserveAmmo > 0 && PlayerIsReady || LoadedAmmo > 0 && PlayerIsReady;

        private CancellationTokenSource _reloadCancellationTokenSource;

        [HideInInspector][Networked] public NetworkBool IsReloading { get; set; } // networked if reloading
        [HideInInspector][Networked] public NetworkBool IsShooting { get; set; } // networked if shooting
        [HideInInspector][Networked] public NetworkBool IsRespawning { get; set; } // networked if respawning
        [HideInInspector][Networked] public NetworkBool HasShield { get; set; } // networked if shield is up
        [HideInInspector][Networked] public NetworkBool PlayerIsReady { get; set; } // networked if the player is ready
        [Networked] private TickTimer RespawnTimer { get; set; } // the networked respawn timer
        [HideInInspector][Networked] public int WeaponID { get; set; } // our chosen weapon ID
        [HideInInspector][Networked] public int CharacterID { get; set; } // our chosen character ID
        [HideInInspector][Networked] public int CosmeticID { get; set; } // our chosen cosmetic ID

        [HideInInspector] public WeaponConfig playerWeapon;
        [HideInInspector] public CharacterConfig playerCharacter;
        [HideInInspector] public CosmeticConfig playerCosmetic;

        private int defaultWeapon; // weapon we started with
        protected HitboxRoot _hitboxRoot;
        protected PlayerManager _playerManager;
        private ChangeDetector _changeDetector;
        private float shieldTimer = 5f;
        bool respawnPlayer = false;

        #region StatHandlingLogic
        protected bool reloadTotalPlayerStats = true;
        // this is the sum of all the players current stats after all additions and modifications
        protected PlayerStats totalStats { get; set; }
        public virtual PlayerStats TotalStats
        {
            get
            {
                if (reloadTotalPlayerStats)
                {
                    var addStats = new PlayerStats { stats = new Dictionary<StatType, int>() };

                    // add base stats
                    if (_playerManager._matchManager.baseStats != null)
                        addStats.AddStats(_playerManager._matchManager.baseStats.ToDictionary());

                    // add the loaded character's stats
                    if (playerCharacter != null)
                        addStats.AddStats(playerCharacter.characterStats.ToDictionary());

                    // add the loaded weapon's stats
                    if (playerWeapon != null)
                    {
                        addStats.AddStats(playerWeapon.weaponStats.ToDictionary());
                    }

                    // if you give the cosmetic stats it add's the loaded cosmetic stats here
                    if (playerCosmetic != null)
                    {
                        addStats.AddStats(playerCosmetic.cosmeticStats.ToDictionary());
                    }

                    // add any active boosts stats
                    if (activeBoosts != null)
                    {
                        foreach (var boost in activeBoosts)
                        {
                            foreach (var value in boost.Value)
                            {
                                addStats.ModifyStat(boost.Key, value);
                            }
                        }
                    }
                    totalStats = addStats;
                    reloadTotalPlayerStats = false;
                }
                return totalStats;
            }
        }

        private Dictionary<StatType, Func<int>> statGetters;
        private Dictionary<StatType, Action<int>> statSetters;
        private Dictionary<StatType, List<int>> activeBoosts;
        private Dictionary<StatType, Coroutine> boostCoroutines;

        private void InitializeStatDictionaries() // also add any new stat you create to here
        {
            statGetters = new Dictionary<StatType, Func<int>>
        {
            { StatType.Hp, () => Hp },
            { StatType.BodyArmor, () => BodyArmor },
            { StatType.ReserveAmmo, () => ReserveAmmo },
            { StatType.LoadedAmmo, () => LoadedAmmo },
            { StatType.AttackDamage, () => AttackDamage },
            { StatType.MoveSpeed, () => MoveSpeed }
        };

            statSetters = new Dictionary<StatType, Action<int>>
        {
            { StatType.Hp, value => Hp = value },
            { StatType.BodyArmor, value => BodyArmor = value },
            { StatType.ReserveAmmo, value => ReserveAmmo = value },
            { StatType.LoadedAmmo, value => LoadedAmmo = value },
            { StatType.AttackDamage, value => AttackDamage = value },
            { StatType.MoveSpeed, value => MoveSpeed = value }
        };

            activeBoosts = new Dictionary<StatType, List<int>>();
            boostCoroutines = new Dictionary<StatType, Coroutine>();
            foreach (StatType statType in System.Enum.GetValues(typeof(StatType)))
            {
                activeBoosts[statType] = new List<int>();
            }
        }

        // gets our total stat value from the total stats
        public virtual int GetTotalStat(StatType statType)
        {
            reloadTotalPlayerStats = true;
            int totalValue = TotalStats.GetStat(statType);

            //int baseValue = _playerManager._matchManager.baseStats.GetStatValue(statType); // use for debugging
            //int baseValue = _matchManager.baseStats.TryGetValue(statType, out int value) ? value : 0; // use for debugging
            //Debug.Log($"GetTotalStat: StatType: {statType}, Value: {baseValue}, New Value: {totalValue}");

            return totalValue;
        }

        // gets our current stat value
        public virtual int GetCurrentStat(StatType statType)
        {
            if (statGetters.TryGetValue(statType, out var getter))
            {
                return getter();
            }
            return 0;
        }

        // here we modify the stat
        public void ModifyStat(StatType statType, int value)
        {
            if (IsAlive)
            {
                int currentValue = GetCurrentStat(statType);
                SetNetworkedStat(statType, currentValue + value);
            }
        }

        // set the stat value
        public void SetNetworkedStat(StatType statType, int value)
        {

            int maxValue = TotalStats.GetStat(statType);
            value = Mathf.Clamp(value, 0, maxValue);

            if (statSetters.TryGetValue(statType, out var setter))
            {
                setter(value);
            }

        }

        // here we temoporarily boost the stat or permanently if revert after is false
        public void TemporaryBoostStat(StatType statType, int value, float duration, bool revertAfter, bool canStack, bool stackDuration)
        {
            StartCoroutine(ApplyTemporaryBoost(statType, value, duration, revertAfter, canStack, stackDuration));
        }

        private IEnumerator ApplyTemporaryBoost(StatType statType, int value, float duration, bool revertAfter, bool canStack, bool stackDuration)
        {
            bool alreadyBoosted = activeBoosts[statType].Contains(value);

            if (!canStack)
            {
                if (alreadyBoosted)
                {
                    if (stackDuration)
                    {
                        // extend the duration by restarting the coroutine
                        if (boostCoroutines.TryGetValue(statType, out Coroutine existingCoroutine))
                        {
                            StopCoroutine(existingCoroutine);

                        }
                        Coroutine coroutine = StartCoroutine(TemporaryBoostCoroutine(statType, value, duration, revertAfter));
                        boostCoroutines[statType] = coroutine;
                    }
                    else
                    {
                        // do nothing if stacking is not allowed and duration should not be extended
                        yield break;
                    }
                }
                else
                {
                    // apply the boost and start the coroutine
                    activeBoosts[statType].Add(value);
                    reloadTotalPlayerStats = true;
                    int newStat = GetTotalStat(statType);
                    statSetters[statType](newStat);

                    Coroutine coroutine = StartCoroutine(TemporaryBoostCoroutine(statType, value, duration, revertAfter));
                    boostCoroutines[statType] = coroutine;
                }
            }
            else
            {
                // apply the boost and start the coroutine
                activeBoosts[statType].Add(value);
                reloadTotalPlayerStats = true;
                int newStat = GetTotalStat(statType);
                statSetters[statType](newStat);

                Coroutine coroutine = StartCoroutine(TemporaryBoostCoroutine(statType, value, duration, revertAfter));
                boostCoroutines[statType] = coroutine;
            }
        }

        private IEnumerator TemporaryBoostCoroutine(StatType statType, int value, float duration, bool revertAfter)
        {
            yield return new WaitForSeconds(duration);

            if (revertAfter)
            {
                activeBoosts[statType].Remove(value);
                reloadTotalPlayerStats = true;
                int revertStat = GetTotalStat(statType);
                statSetters[statType](revertStat);
            }

            boostCoroutines.Remove(statType);
        }
        #endregion

        // all of our networked stats from PlayerStats script, add here if you add to PlayerStats script
        [Networked]
        public int Hp { get; set; }
        [Networked]
        public int BodyArmor { get; set; }
        [Networked]
        public int Heavy { get; set; }
        [Networked]
        public int ReserveAmmo { get; set; }
        [Networked]
        public int LoadedAmmo { get; set; }
        [Networked]
        public int AttackDamage { get; set; }
        [Networked]
        public int MoveSpeed { get; set; }

        // for health fill bar purposes
        public virtual int TotalHp => TotalStats.GetStat(StatType.Hp);
        // for body armor fill bar purposes
        public virtual int TotalBodyArmor => TotalStats.GetStat(StatType.BodyArmor);

        private void Awake()
        {
            InitializeStatDictionaries();
        }

        public override void Spawned()
        {
            // set all of our networked variables
            PlayerIsReady = false;
            IsShooting = false;
            CharacterID = -1;
            WeaponID = -1;
            CosmeticID = -1;
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            _playerManager = GetComponent<PlayerManager>();
            _hitboxRoot = GetComponent<HitboxRoot>();

            // if local player, sets up our local players loadout and then tells all of the host to tell all of the other players our loadout
            if (Object.HasInputAuthority)
            {
                int selectedCharacterId = PlayerGameData.PlayerData.GetSelectedCharacter();
                int selectedWeaponId = PlayerGameData.PlayerData.GetSelectedWeapon();
                int selectedCosmeticId = PlayerGameData.PlayerData.GetSelectedCosmetic();
                CharacterID = selectedCharacterId;
                WeaponID = selectedWeaponId;
                CosmeticID = selectedCosmeticId;
                defaultWeapon = WeaponID;
                RpcSetPlayerCharacter(CharacterID);
                RpcSetPlayerWeapon(WeaponID);
                RpcSetPlayerCosmetic(CosmeticID);
            }

            // if host
            if (Object.HasStateAuthority)
            {
                IsRespawning = false;
            }

        }

        public void SetUpBot()
        {
            // give the bots random characters and weapons from the list of characters and weapons we've added in the lobby scene
            CharacterID = UnityEngine.Random.Range(0, PlayerGameData.Characters.Count);
            WeaponID = UnityEngine.Random.Range(0, PlayerGameData.Weapons.Count);
            CosmeticID += UnityEngine.Random.Range(0, PlayerGameData.Cosmetics.Count);

            // set the default weapon for respawning logic
            defaultWeapon = WeaponID;
        }


        private void InitializeVitals()
        {
            // set the networked variables to their corresponding totals at the start of the match
            Hp = GetTotalStat(StatType.Hp);
            BodyArmor = GetTotalStat(StatType.BodyArmor);
            ReserveAmmo = GetTotalStat(StatType.ReserveAmmo);
            LoadedAmmo = GetTotalStat(StatType.LoadedAmmo);
            AttackDamage = GetTotalStat(StatType.AttackDamage);
            MoveSpeed = GetTotalStat(StatType.MoveSpeed);

            // we set this so that the players stats loads correctly when a new player joins and our stats are correct before we move or shoot.
            PlayerIsReady = true;
        }

        // handles all changes to each stat and loadout over the network and calling their methods when a stat is changed or loadout is changed
        public override void Render()
        {
            if (_changeDetector == null) return;

            foreach (var change in _changeDetector.DetectChanges(this, out var prev, out var current))
            {
                switch (change)
                {
                    case nameof(CharacterID):
                        if (CharacterID > -1)
                            UpdatePlayerCharacter(Object.InputAuthority, CharacterID);
                        break;
                    case nameof(WeaponID):
                        var weaponReader = GetPropertyReader<int>(nameof(WeaponID));
                        var (oldWeapon, newWeapon) = weaponReader.Read(prev, current);
                        ChangeWeapon(oldWeapon, newWeapon);
                        break;
                    case nameof(CosmeticID):
                        if (CosmeticID > -1)
                            UpdatePlayerCosmetic(Object.InputAuthority, CosmeticID);
                        break;
                    case nameof(Hp):
                        var hpReader = GetPropertyReader<int>(nameof(Hp));
                        var (oldHealth, currentHealth) = hpReader.Read(prev, current);
                        _playerManager._playerVisuals.PlayerHpEffected(oldHealth, currentHealth, GetTotalStat(StatType.Hp));
                        break;
                    case nameof(BodyArmor):
                        var armorReader = GetPropertyReader<int>(nameof(BodyArmor));
                        var (oldArmor, currentArmor) = armorReader.Read(prev, current);
                        PlayerBodyArmorEffected(oldArmor, currentArmor);
                        break;
                    case nameof(ReserveAmmo):
                        var ammoReader = GetPropertyReader<int>(nameof(ReserveAmmo));
                        var (oldAmmo, currentAmmo) = ammoReader.Read(prev, current);
                        UpdateAmmoUI(oldAmmo, currentAmmo);
                        CheckReload(oldAmmo, currentAmmo);
                        break;
                    case nameof(LoadedAmmo):
                        var loadedAmmoReader = GetPropertyReader<int>(nameof(LoadedAmmo));
                        var (oldLoadedAmmo, currentLoadedAmmo) = loadedAmmoReader.Read(prev, current);
                        UpdateAmmoUI(oldLoadedAmmo, currentLoadedAmmo);
                        break;
                    case nameof(IsRespawning):
                        var respawnReader = GetPropertyReader<NetworkBool>(nameof(IsRespawning));
                        var (notRespawning, isRespawning) = respawnReader.Read(prev, current);
                        HandleRespawn(notRespawning, isRespawning);
                        
                        break;
                    case nameof(HasShield):
                        var shieldReader = GetPropertyReader<NetworkBool>(nameof(HasShield));
                        var (noShield, hasShield) = shieldReader.Read(prev, current);
                        ApplyShield(noShield, hasShield);
                        break;
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!CanShoot || Hp <= 0)
                IsShooting = false;

            if (IsRespawning && !RespawnTimer.ExpiredOrNotRunning(Runner))
            {
                _playerManager._matchManager._matchUiHandler.respawnTimerText.text = $"{Mathf.RoundToInt(RespawnTimer.RemainingTime(Runner) ?? 0)}";
            }

            if (IsReloading && Object.HasInputAuthority)
            {
                _playerManager._playerVisuals.reloadBar.SetActive(true);
            }
            else
            {
                if (Object.HasInputAuthority)
                {
                    _playerManager._playerVisuals.reloadBar.SetActive(false);
                    _playerManager._playerVisuals.reloadFill.fillAmount = 0f;
                }
            }

            if (respawnPlayer)
            {
                RespawnPlayer();
            }

        }

        // all damage from projectiles is called here from the porjectile script
        public void ApplyDamage(PlayerRef player)
        {
            if (Runner.TryGetPlayerObject(player, out var attackingPlayer))
            {

                int damageAmount = attackingPlayer.GetComponent<PlayerStatsManager>().AttackDamage;

                //Debug.Log($"Player hit with {damageAmount} damage."); // uncomment for debugging

                if (damageAmount > 0 && BodyArmor > 0 && !HasShield)
                {
                    int damageToArmor = Mathf.Min(damageAmount, BodyArmor);
                    BodyArmor -= damageToArmor;
                    damageAmount -= damageToArmor;
                }

                if (damageAmount > 0 && Hp > 0 && !HasShield)
                {
                    int newHp = Hp - damageAmount;
                    SetHealth(attackingPlayer, newHp);
                }
            }
        }

        // this is where we apply damage from our bots, we have to do it separately because bots don't have a player ref
        public void ApplyBotDamage(int damageAmount, NetworkObject attackingBot)
        {
            // player's body armor takes the damage first if you want to use body armor
            if (BodyArmor > 0 && !HasShield)
            {
                int damageToArmor = Mathf.Min(damageAmount, BodyArmor);
                BodyArmor -= damageToArmor;
                damageAmount -= damageToArmor;
            }

            // set the bots health based on damaged recieved if they are not already dead or no shield currently up
            if (damageAmount > 0 && Hp > 0 && !HasShield)
            {
                int newHp = Hp - damageAmount;
                SetHealth(attackingBot, newHp);
            }
        }

        // this is where we apply damage from all other sources such as exploding barrels or damage zones
        public void ApplyOtherDamage(int damageAmount)
        {
            //Debug.Log($"Player hit with {damageAmount} damage.");

            // player's body armor takes the damage first if you want to use body armor, if you dont just comment this out
            if (BodyArmor > 0 && !HasShield)
            {
                int damageToArmor = Mathf.Min(damageAmount, BodyArmor);
                BodyArmor -= damageToArmor;
                damageAmount -= damageToArmor;
            }

            // set the players health based on damaged recieved if they are not already dead or no shield currently up
            if (damageAmount > 0 && Hp > 0 && !HasShield)
            {
                int newHp = Hp - damageAmount;
                SetHealth(null, newHp);
            }
        }

        // set the players new health after damage is taken
        private void SetHealth(NetworkObject attackingPlayer, int newHealth)
        {

            Hp = Mathf.Max(newHealth, 0); // ensures our players health does not go negative

            if (attackingPlayer != null)
            {
                var attackingPlayerController = attackingPlayer.GetComponent<PlayerManager>();

                if (Hp <= 0)
                {
                    // only host can add kills and deaths
                    if (Object.HasStateAuthority)
                    {
                        _playerManager._playerController.AddDeaths(1);
                        if (attackingPlayer != null && attackingPlayer != _playerManager._networkObject)
                            attackingPlayerController._playerController.AddKills(1);

                        int playerTeam = attackingPlayerController._playerController.TeamInt;
                        string killerName = attackingPlayerController != null ? attackingPlayerController._playerController.PlayerNickName.ToString() : "Unknown";
                        string victimName = _playerManager._playerController.PlayerNickName.ToString();
                        string weaponUsed = attackingPlayerController._playerStats.playerWeapon.weaponName;
                        int weaponInt = attackingPlayerController._playerStats.WeaponID;

                        RpcShowKillPopUp(killerName, victimName, weaponUsed, weaponInt, playerTeam);
                    }

                    HandleDeath();
                    _playerManager._matchManager.CheckCanMatchEnd();
                }
            }
            else if (Hp <= 0)
            {
                _playerManager._playerController.AddDeaths(1);
                HandleDeath();
            }
        }

        // players body armor has been damaged update visuals
        private void PlayerBodyArmorEffected(int oldArmor, int newArmor)
        {

            if (newArmor != oldArmor)
            {
                _playerManager._playerVisuals.bodyArmorFill.fillAmount = newArmor / (float)TotalBodyArmor;

                if (newArmor < oldArmor) // indicates body armor took damage
                {
                    _playerManager._playerVisuals.PlayBodyArmorDamageEffects();
                }
            }
        }

        // set the players weapon
        private void UpdatePlayerWeapon(PlayerRef player, int weaponId)
        {
            // stop reloading if currently reloading
            StopReloading();

            playerWeapon = PlayerGameData.GetWeapon(weaponId); // get the chosen weapon from our list of weapon configs in the static player game data script

            //Debug.Log($"UpdatePlayerWeapon called. Player: {player}, WeaponID: {weaponId}"); // can uncomment these for debugging

            if (playerWeapon != null)
            {
                //Debug.Log($"Player weapon is {playerWeapon.weaponName}.");
                _playerManager._projectileController.SetWeaponInfo();
                _playerManager._playerVisuals.OnSetPlayerModel();

                if (_playerManager._matchUI.localWeaponIcon != null && Object.HasInputAuthority)
                {
                    _playerManager._matchUI.localWeaponIcon.sprite = playerWeapon.weaponIcon;
                }

                // reset stats to ensure stats are updated based on the correct weaponID
                if (Object.HasStateAuthority)
                {
                    UpdateStatsOnWeaponChange();
                }

                // notify the ProjectileController that the weapon has changed
                _playerManager._projectileController.OnWeaponChanged();

            }
            else
            {
                Debug.LogError("Player weapon config is null.");
            }
        }

        private void UpdatePlayerCharacter(PlayerRef player, int characterId)
        {
            playerCharacter = PlayerGameData.GetCharacter(characterId);
            //Debug.Log($"UpdatePlayerCharacter called. Player: {player}, CharacterID: {CharacterID}"); // can uncomment these for debugging

            if (playerCharacter != null)
            {
                //Debug.Log($"Player character is {playerCharacter.characterName}.");
                _playerManager._playerVisuals.OnSetPlayerModel();

                // reset stats to ensure stats are updated based on the correct selected character
                if (Object.HasStateAuthority)
                {
                    InitializeVitals();
                }

            }
            else
            {
                Debug.LogError("Player character config is null.");
            }
        }

        private void UpdatePlayerCosmetic(PlayerRef player, int cosmeticId)
        {
            playerCosmetic = PlayerGameData.GetCosmetic(cosmeticId);

            if (playerCosmetic != null)
            {
                _playerManager._playerVisuals.OnSetPlayerModel();

                // reset stats to ensure stats are updated based on the correct selected cosmetic if you give the cosmetic stats
                if (Object.HasStateAuthority)
                {
                    InitializeVitals();
                }

            }
            else
            {
                Debug.LogError("Player cosmetic config is null.");
            }
        }

        // change the players current weapon to new one
        public void ChangeWeapon(int oldWeapon, int newWeapon)
        {
            if (newWeapon != oldWeapon)
            {
                PlayerIsReady = false;
                UpdatePlayerWeapon(Object.InputAuthority, newWeapon);
            }

        }

        // updates players stats baseed on the new weapon picked up
        private void UpdateStatsOnWeaponChange()
        {
            // store previous max stats
            int previousTotalHp = TotalHp;
            int previousTotalBodyArmor = TotalBodyArmor;

            // store current Hp and Body Armor
            int currentHp = Hp;
            int currentBodyArmor = BodyArmor;

            // recalculate total stats
            reloadTotalPlayerStats = true;

            // get new total stats
            int newTotalHp = GetTotalStat(StatType.Hp);
            int newTotalBodyArmor = GetTotalStat(StatType.BodyArmor);

            // adjust current Hp and Body Armor based on difference in max values
            int hpDifference = newTotalHp - previousTotalHp;
            int bodyArmorDifference = newTotalBodyArmor - previousTotalBodyArmor;

            Hp = currentHp + hpDifference;
            BodyArmor = currentBodyArmor + bodyArmorDifference;

            // ensure Hp and Body Armor are within new max values
            Hp = Mathf.Clamp(Hp, 0, newTotalHp);
            BodyArmor = Mathf.Clamp(BodyArmor, 0, newTotalBodyArmor);

            // update other stats
            ReserveAmmo = GetTotalStat(StatType.ReserveAmmo);
            LoadedAmmo = GetTotalStat(StatType.LoadedAmmo);
            AttackDamage = GetTotalStat(StatType.AttackDamage);
            MoveSpeed = GetTotalStat(StatType.MoveSpeed);

            // set that the player is ready
            PlayerIsReady = true;
        }

        // starts our reloading process
        public void StartReloading()
        {
            if (playerWeapon == null || IsReloading || ReserveAmmo <= 0 || !IsAlive || LoadedAmmo > 0)
                return;

            // cancel any existing reload
            StopReloading();

            // start a new reload
            _reloadCancellationTokenSource = new CancellationTokenSource();
            _ = Reload(_reloadCancellationTokenSource.Token);
        }

        private async Task Reload(CancellationToken token)
        {
            if (playerWeapon == null || !IsAlive)
                return;

            IsReloading = true;
            Debug.Log("Reloading...");

            var reloadFx = playerWeapon.reloadFx;
            var fxPlayer = _playerManager._playerVisuals.fxPlayer;
            float totalReloadTime = playerWeapon.reloadDuration;

            if (fxPlayer != null && reloadFx != null)
            {
                _playerManager._playerVisuals.fxPlayer.PlayOneShot(reloadFx);
            }

            while (LoadedAmmo < playerWeapon.reloadClipSize && ReserveAmmo > 0)
            {
                if (token.IsCancellationRequested)
                {
                    Debug.Log("Reload canceled.");
                    IsReloading = false;
                    return;
                }

                float elapsedTime = 0f;
                while (elapsedTime < totalReloadTime)
                {
                    if (token.IsCancellationRequested)
                    {
                        Debug.Log("Reload canceled during reload duration.");
                        IsReloading = false;
                        return;
                    }

                    elapsedTime += Time.deltaTime;
                    _playerManager._playerVisuals.reloadFill.fillAmount = elapsedTime / totalReloadTime;
                    await Task.Yield();
                }

                // if the weapon config is set to reload one ammo at a time
                if (playerWeapon.reloadOneAmmoAtATime)
                {
                    LoadedAmmo += 1;
                    ReserveAmmo -= 1;
                    Debug.Log("One ammo reloaded.");

                    if (LoadedAmmo >= playerWeapon.reloadClipSize || ReserveAmmo <= 0)
                        break;
                }
                else
                {
                    int ammoToReload = Mathf.Min(playerWeapon.reloadClipSize - LoadedAmmo, ReserveAmmo);
                    LoadedAmmo += ammoToReload;
                    ReserveAmmo -= ammoToReload;
                    break;
                }
            }

            IsReloading = false; // finished reloading
            Debug.Log("Reload complete.");
        }

        // stops the reloading
        private void StopReloading()
        {
            if (IsReloading)
            {
                if (_reloadCancellationTokenSource != null)
                {
                    _reloadCancellationTokenSource.Cancel();
                    _reloadCancellationTokenSource.Dispose();
                    _reloadCancellationTokenSource = null;
                }
                IsReloading = false;
            }
        }

        // updates the players ammo UI on the match UI
        private void UpdateAmmoUI(int oldAmmo, int newAmmo)
        {
            if (newAmmo != oldAmmo)
            {
                if (_playerManager._matchUI.localAmmoAmount != null && Object.HasInputAuthority)
                {
                    _playerManager._matchUI.localAmmoAmount.text = $"{LoadedAmmo}/{ReserveAmmo}";
                }
            }

        }

        // we check for reload if we are completely out of ammo and we pick some up so that we auto reload;
        private void CheckReload(int oldAmmo, int newAmmo)
        {
            if (oldAmmo <= 0 && newAmmo > 0)
            {
                StartReloading();
            }
        }
        
        // handles the player changes on death and initiates the respawning
        private void HandleDeath()
        {
            // stop reloading if currently reloading
            StopReloading();

            if (_playerManager._playerVisuals.deathEffect != null)
                _playerManager._playerVisuals.deathEffect.GetComponent<ParticleSystem>().Play();

            // set the hitboxroot inactive so that we cant take damage anymore because the player is dead
            _hitboxRoot.HitboxRootActive = false;

            // calls the method in render on all clients since its changing to true
            IsRespawning = true;
        }

        // handles respawning the player once they died
        private void HandleRespawn(bool notRespawning, bool isRespawning)
        {
            if (!isRespawning && notRespawning && !_playerManager._playerController.IsBot && Object.HasInputAuthority)
            {
                _playerManager._matchUI.respawnPanel.SetActive(false);
            }
            else if (!notRespawning && isRespawning)
            {
                
                if (Object.HasInputAuthority && !_playerManager._playerController.IsBot)
                    _playerManager._matchUI.respawnPanel.SetActive(true);

                if (!_playerManager._matchManager.GameIsOver)
                    _playerManager._matchManager.CanRespawnPlayer(_playerManager._networkObject, Object.InputAuthority);
            }

            if (!notRespawning && isRespawning)
            {
                _playerManager._playerVisuals.hpHolder.SetActive(false);
                _playerManager._networkCharacterController.enabled = false;
                _playerManager._characterController.enabled = false;
            }
            else if (!isRespawning && notRespawning)
            {
                _playerManager._playerVisuals.hpHolder.SetActive(true);
                _playerManager._networkCharacterController.enabled = true;
                _playerManager._characterController.enabled = true;
            }

        }

        // calls the respawn in render
        public IEnumerator ServerRespawn(float duration)
        {
            yield return new WaitForSeconds(duration);

            respawnPlayer = true;
        }

        // respawn the player in their new position
        private void RespawnPlayer()
        {
            Vector3 spawnPosition = Vector3.zero;

            if (_playerManager._playerController.IsBot)
            {
                spawnPosition = _playerManager._matchManager._spawnManager.GetBotSpawnPosition(_playerManager._playerController.TeamInt);
            }
            else
            {
                spawnPosition = _playerManager._matchManager._spawnManager.GetSpawnPosition(Object.InputAuthority);
            }

            OnRespawn();
            _playerManager._networkCharacterController.enabled = true;
            _playerManager._networkCharacterController.Teleport(spawnPosition);
            
            respawnPlayer = false;
        }

        public void StartRespawnTimer(float respawnDuration)
        {
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDuration);
        }

        // resets the player back to default
        public void OnRespawn()
        {
            // set our respawn bool back to false
            IsRespawning = false;
            PlayerIsReady = false;

            // remove any stat changes
            foreach (var statType in activeBoosts.Keys)
            {
                activeBoosts[statType].Clear();
            }

            // set our weapon back to the weapon we started with
            WeaponID = defaultWeapon;

            // set our stats back to defautl stats at start of match
            reloadTotalPlayerStats = true;
            if (HasStateAuthority)
                InitializeVitals();

            // calls the shield to be set active in render so all clients know our shield is up
            HasShield = true;
            _hitboxRoot.HitboxRootActive = true;
            StartCoroutine(ShieldTimer());
        }

        // gives the player a shield
        private void ApplyShield(bool noShield, bool hasShield)
        {
            // if the HasShield bool was false and it gets set to true then we set it active
            if (!noShield && hasShield)
            {
                _playerManager._playerVisuals.shieldEffect.SetActive(true);
            }
            else if (noShield && !hasShield) // if the HasShield bool was true and is set to false then we turn it off
            {
                _playerManager._playerVisuals.shieldEffect.SetActive(false);
            }

        }

        // the timer for the shield before its turned off
        private IEnumerator ShieldTimer()
        {
            yield return new WaitForSeconds(shieldTimer);
            HasShield = false;
        }

        // rpc used to send player weapon to the Host, we let host know hey this is our selected character and weapon and the host lets all other clients know
        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        private void RpcSetPlayerWeapon(int weaponId) //initial Weapon setting Rpc from clients
        {
            Debug.Log($"RpcSetPlayerWeapon called with WeaponID: {weaponId}");
            WeaponID = weaponId;
            defaultWeapon = weaponId;
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        private void RpcSetPlayerCharacter(int characterId)
        {
            Debug.Log($"RpcSetPlayerCharacter called with CharacterID: {characterId}");
            CharacterID = characterId;
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        private void RpcSetPlayerCosmetic(int cosmeticId)
        {
            Debug.Log($"RpcSetPlayerCosmetic called with CosmeticID: {cosmeticId}");
            CosmeticID = cosmeticId;
        }

        // the Host shows all players the kill feed pop up
        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RpcShowKillPopUp(string attacker, string victim, string weaponName, int weaponId, int playerTeam)
        {
            if (string.IsNullOrEmpty(attacker) || string.IsNullOrEmpty(victim)) return;

            _playerManager._matchUI.ShowKillPopup(attacker, victim, weaponName, weaponId, playerTeam);
        }
    }
}

