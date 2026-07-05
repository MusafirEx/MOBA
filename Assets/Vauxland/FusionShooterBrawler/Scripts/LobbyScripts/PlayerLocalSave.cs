/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using UnityEngine;

namespace Vauxland.FusionBrawler
{
    // our local player save handler to save our selected data on the lobby scene
    public class PlayerLocalSave : MonoBehaviour
    {
        public const string SaveKeyPlayerName = "SavePlayerName";
        public const string SaveKeyCharacter = "SaveSelectCharacter";
        public const string SaveKeyWeapon = "SaveSelectWeapon"; // legacy / primary weapon
        public const string SaveKeyTopWeapon = "SaveSelectTopWeapon";
        public const string SaveKeyLeftWeapon = "SaveSelectLeftWeapon";
        public const string SaveKeyRightWeapon = "SaveSelectRightWeapon";
        public const string SaveKeyCosmetic = "SaveSelectCosmetic";
        public const string SaveKeyMatchType = "SaveSelectMatchType";
        private const string SelectedRegionKey = "SelectedRegion";
        private const string SelectedMapKey = "SelectedMap";

        // gets our players saved name
        public static string GetPlayerName()
        {
            // creates a generic one if no name is currently saved
            if (!PlayerPrefs.HasKey(SaveKeyPlayerName))
                SetPlayerName("Player-" + string.Format("{0:0000}", Random.Range(1, 9999)));
            return PlayerPrefs.GetString(SaveKeyPlayerName);
        }

        // saves our players current name
        public static void SetPlayerName(string value)
        {
            PlayerPrefs.SetString(SaveKeyPlayerName, value);
            PlayerPrefs.Save();
        }

        // gets our saved selected character config
        public static int GetCharacter()
        {
            return PlayerPrefs.GetInt(SaveKeyCharacter, 0);
        }

        // save our selected character config
        public static void SetCharacter(int value)
        {
            PlayerPrefs.SetInt(SaveKeyCharacter, value);
            PlayerPrefs.Save();
        }

        // get our saved selected weapon config
        public static int GetWeapon()
        {
            return PlayerPrefs.GetInt(SaveKeyWeapon, 0);
        }

        // saves our selected weapon config
        public static void SetWeapon(int value)
        {
            PlayerPrefs.SetInt(SaveKeyWeapon, value);
            PlayerPrefs.Save();
        }


        public static int GetTopWeapon()
        {
            return PlayerPrefs.GetInt(SaveKeyTopWeapon, GetWeapon());
        }

        public static void SetTopWeapon(int value)
        {
            PlayerPrefs.SetInt(SaveKeyTopWeapon, value);
            SetWeapon(value); // keep old single-weapon systems using the top weapon
        }

        public static int GetLeftWeapon()
        {
            return PlayerPrefs.GetInt(SaveKeyLeftWeapon, -1);
        }

        public static void SetLeftWeapon(int value)
        {
            PlayerPrefs.SetInt(SaveKeyLeftWeapon, value);
            PlayerPrefs.Save();
        }

        public static int GetRightWeapon()
        {
            return PlayerPrefs.GetInt(SaveKeyRightWeapon, -1);
        }

        public static void SetRightWeapon(int value)
        {
            PlayerPrefs.SetInt(SaveKeyRightWeapon, value);
            PlayerPrefs.Save();
        }

        // gets our saved selected cosmetic config
        public static int GetCosmetic()
        {
            return PlayerPrefs.GetInt(SaveKeyCosmetic, 0);
        }

        // save our selected cosmetic config
        public static void SetCosmetic(int value)
        {
            PlayerPrefs.SetInt(SaveKeyCosmetic, value);
            PlayerPrefs.Save();
        }

        // gets the current saved match type (gamemode)
        public static int GetMatchType()
        {
            return PlayerPrefs.GetInt(SaveKeyMatchType, 0);
        }

        // saves the currently selected match type (gamemode)
        public static void SetMatchType(MatchType matchType)
        {
            if (matchType == MatchType.DeathMatch)
                PlayerPrefs.SetInt(SaveKeyMatchType, 0);
            else
                PlayerPrefs.SetInt(SaveKeyMatchType, 1);
        }
        // saves the current selected region
        public static void SetSelectedRegion(string region)
        {
            PlayerPrefs.SetString(SelectedRegionKey, region);
            PlayerPrefs.Save();
        }
        // gets the saved selected region
        public static string GetSelectedRegion()
        {
            return PlayerPrefs.GetString(SelectedRegionKey, ""); // default to empty string
        }

        // saves the selected map
        public static void SetMap(int mapIndex)
        {
            PlayerPrefs.SetInt(SelectedMapKey, mapIndex);
        }

        // gets the last used map
        public static int GetMap()
        {
            return PlayerPrefs.GetInt(SelectedMapKey, 0);
        }
    }
}

