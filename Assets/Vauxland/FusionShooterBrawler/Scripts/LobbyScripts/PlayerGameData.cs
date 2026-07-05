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
using UnityEngine;
using Random = UnityEngine.Random;

namespace Vauxland.FusionBrawler
{
    [System.Serializable]
    public class PlayerGameData : MonoBehaviour
    {
        public static PlayerGameData PlayerData { get; private set; }

        // local Player info we carry over to Match Scene
        private string playerNickName = null;

        [Header("Match Scene Maps")]
        public MapConfig[] maps; // list of maps to choose from for the match scene

        [Header("Loadout Items")]
        public CharacterConfig[] characters; // list of character configs to choose from
        public WeaponConfig[] weapons; // list of weapon configs to choose from
        public CosmeticConfig[] cosmetics; // list of cosmetics to choose from

        private int selectedWeaponId; // legacy / primary weapon
        private int selectedTopWeaponId;
        private int selectedLeftWeaponId;
        private int selectedRightWeaponId;
        private int selectedCharacterId; // the Id of the selected character
        private int selectedCosmeticId; // the Id of the selected cosmetic
        private int selectedMapId; // the ID of the selected map

        public bool testMobileControls = false; // lets you test the mobile controls in the editor

        public static readonly Dictionary<int, CharacterConfig> Characters = new Dictionary<int, CharacterConfig>();
        public static readonly Dictionary<int, WeaponConfig> Weapons = new Dictionary<int, WeaponConfig>();
        public static readonly Dictionary<int, CosmeticConfig> Cosmetics = new Dictionary<int, CosmeticConfig>();
        public static readonly Dictionary<int, MapConfig> Maps = new Dictionary<int, MapConfig>();

        protected void Awake()
        {
            if (PlayerData != null)
            {
                Destroy(gameObject);
                return;
            }
            PlayerData = this;
            DontDestroyOnLoad(gameObject);

        }

        protected void Start()
        {
            Weapons.Clear();

            for (int i = 0; i < weapons.Length; i++)
            {
                Weapons[i] = weapons[i];
            }

            // load three mounted weapons
            selectedTopWeaponId = ValidateWeaponId(PlayerLocalSave.GetTopWeapon(), WeaponMountType.Top);
            selectedLeftWeaponId = ValidateWeaponId(PlayerLocalSave.GetLeftWeapon(), WeaponMountType.Left);
            selectedRightWeaponId = ValidateWeaponId(PlayerLocalSave.GetRightWeapon(), WeaponMountType.Right);
            selectedWeaponId = selectedTopWeaponId >= 0 ? selectedTopWeaponId : (selectedLeftWeaponId >= 0 ? selectedLeftWeaponId : selectedRightWeaponId);

            Characters.Clear();

            for (int i = 0; i < characters.Length; i++)
            {
                Characters[i] = characters[i];
            }

            // load selected character config
            selectedCharacterId = PlayerLocalSave.GetCharacter();
            if (!Characters.ContainsKey(selectedCharacterId))
                selectedCharacterId = 0; // default to first character in list

            Cosmetics.Clear();

            for (int i = 0;i < cosmetics.Length; i++)
            {
                Cosmetics[i] = cosmetics[i];
            }

            // load selected cosmetic config
            selectedCosmeticId = PlayerLocalSave.GetCosmetic();
            if (!Cosmetics.ContainsKey(selectedCosmeticId))
                selectedCosmeticId = 0; // default to first cosmetic in list

            Maps.Clear();
            for (int i = 0; i < maps.Length; i++)
            {
                Maps[i] = maps[i];
            }

            // load selected map config
            selectedMapId = PlayerLocalSave.GetMap();
            if (!Maps.ContainsKey(selectedMapId))
                selectedMapId = 0; // default to the first map in the list
        }

        // sets the players nickname for the match to load
        public void SetPlayerNickName(string nickName)
        {
            playerNickName = nickName;
        }

         // gets our set player nickname
        public string GetPlayerNickName()
        {
            if (string.IsNullOrWhiteSpace(playerNickName))
            {
                playerNickName = GetRandomPlayerNickName();
            }

            return playerNickName;
        }

        // gets a bot name
        public string GetBotName()
        {
            playerNickName = GetRandomPlayerNickName();

            return playerNickName;
        }

        // randomizes the bot name
        public static string GetRandomPlayerNickName()
        {
            var rngPlayerNumber = Random.Range(0, 9999);
            return $"Player {rngPlayerNumber:0000}";
        }


        private static int ValidateWeaponId(int weaponId, WeaponMountType mountType)
        {
            if (weaponId < 0 || !Weapons.TryGetValue(weaponId, out WeaponConfig config) || config == null)
                return -1;
            return config.requiredMount == mountType ? weaponId : -1;
        }

        public void SetSelectedWeaponForMount(WeaponMountType mountType, int weaponId)
        {
            int validated = ValidateWeaponId(weaponId, mountType);
            switch (mountType)
            {
                case WeaponMountType.Top: selectedTopWeaponId = validated; break;
                case WeaponMountType.Left: selectedLeftWeaponId = validated; break;
                case WeaponMountType.Right: selectedRightWeaponId = validated; break;
            }

            selectedWeaponId = selectedTopWeaponId >= 0 ? selectedTopWeaponId :
                (selectedLeftWeaponId >= 0 ? selectedLeftWeaponId : selectedRightWeaponId);
        }

        public int GetSelectedWeaponForMount(WeaponMountType mountType)
        {
            switch (mountType)
            {
                case WeaponMountType.Top: return selectedTopWeaponId;
                case WeaponMountType.Left: return selectedLeftWeaponId;
                case WeaponMountType.Right: return selectedRightWeaponId;
                default: return -1;
            }
        }

        public int GetSelectedTopWeapon() => selectedTopWeaponId;
        public int GetSelectedLeftWeapon() => selectedLeftWeaponId;
        public int GetSelectedRightWeapon() => selectedRightWeaponId;

        // sets the selected weapon from the main menu
        public void SetSelectedWeapon(int weaponId)
        {
            WeaponConfig config = GetWeapon(weaponId);
            if (config != null)
                SetSelectedWeaponForMount(config.requiredMount, weaponId);
        }

        // sets the selected character from the main menu
        public void SetSelectedCharacter(int characterId)
        {
            selectedCharacterId = characterId;
        }

        // sets the selected cosmetic
        public void SetSelectedCosmetic(int cosmeticId)
        {
            selectedCosmeticId = cosmeticId;
        }

        // called from the player's stat manager to get their chosen weapon in the match
        public int GetSelectedWeapon()
        {
            return selectedWeaponId;
        }

        // called from the player's stat manager to get their chosen character in the match
        public int GetSelectedCharacter()
        {
            return selectedCharacterId;
        }

        // called from player's stat manager to get their chosen cosmetic
        public int GetSelectedCosmetic()
        {
            return selectedCosmeticId;
        }

        // finds the character from the list of character configs
        public static CharacterConfig GetCharacter(int key)
        {
            if (Characters.Count == 0)
                return null;
            Characters.TryGetValue(key, out CharacterConfig result);
            return result;
        }
        
        public int GetCharacterID(CharacterConfig character)
        {
            int index = Array.IndexOf(characters, character);
            return index;
        }

        // finds the weapon from the list of weapon configs
        public static WeaponConfig GetWeapon(int key)
        {
            if (Weapons.Count == 0)
                return null;
            Weapons.TryGetValue(key, out WeaponConfig result);
            return result;
        }

        // changes the weapon from a weapon network pickup
        public int GetWeaponID(WeaponConfig weapon)
        {
            Weapons.Clear();

            for (int i = 0; i < weapons.Length; i++)
            {
                Weapons[i] = weapons[i];
            }
            int index = Array.IndexOf(weapons, weapon);
            return index;
        }

        // finds the cosmetic from the list of cosmetic configs
        public static CosmeticConfig GetCosmetic(int key)
        {
            if (Cosmetics.Count == 0)
                return null;
            Cosmetics.TryGetValue(key, out CosmeticConfig result);
            return result;
        }

        public int GetCosmeticID(CosmeticConfig cosmetic)
        {
            int index = Array.IndexOf(cosmetics, cosmetic);
            return index;
        }

        // finds the map from the list of map configs
        public static MapConfig GetMap(int key)
        {
            if (Maps.Count == 0)
                return null;
            Maps.TryGetValue(key, out MapConfig result);
            return result;
        }

    }
}

