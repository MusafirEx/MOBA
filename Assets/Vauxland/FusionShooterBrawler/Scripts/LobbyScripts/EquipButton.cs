/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vauxland.FusionBrawler
{
    public class EquipButton : MonoBehaviour
    {
        public MainMenuManager mainMenu; // the main menu in the scene to tell what weapon or character we've picked
        public Button buttonEquip; // the button that equips the weapon or selects the character
        public WeaponConfig weaponEquip; // the weapon to equip
        public CharacterConfig characterEquip; // the character to select
        public CosmeticConfig cosmeticEquip; // the cosmetic to equip
        public TMP_Text selectionNameText; // name of the weapon or character
        public TMP_Text healthStatText; // the hp stat of the weapon or character
        public TMP_Text attackStatText; // the attack stat of the weapon or character
        public TMP_Text speedStatText; // the move speed stat of the weapon or character
        public TMP_Text bodyArmorStatText; // the body armor stat of the weapon or character
        public TMP_Text loadedAmmoStatText; // the laoded ammo stat of the weapon or character
        public TMP_Text reserveAmmoStatText; // the reserve ammo stat of the weapon or character     

        private bool isWeapon = false;
        private bool isCharacter = false;
        private bool isCosmetic = false;

        void Start()
        {
            if (mainMenu == null)
            {
                mainMenu = FindObjectOfType<MainMenuManager>();
            }

            if (weaponEquip != null)
            {
                selectionNameText.text = weaponEquip.weaponName;

                // retrieve stats from weaponEquip.weaponStats
                int healthValue = weaponEquip.weaponStats.GetStatValue(StatType.Hp);
                int attackValue = weaponEquip.weaponStats.GetStatValue(StatType.AttackDamage);
                int speedValue = weaponEquip.weaponStats.GetStatValue(StatType.MoveSpeed);
                int bodyArmorValue = weaponEquip.weaponStats.GetStatValue(StatType.BodyArmor);
                int loadedAmmoValue = weaponEquip.weaponStats.GetStatValue(StatType.LoadedAmmo);
                int reserveAmmoValue = weaponEquip.weaponStats.GetStatValue(StatType.ReserveAmmo);

                // set the stat values to the text fields
                if (healthStatText != null)
                    healthStatText.text = healthValue.ToString();

                if (attackStatText != null)
                    attackStatText.text = attackValue.ToString();

                if (speedStatText != null)
                    speedStatText.text = speedValue.ToString();

                if (bodyArmorStatText != null)
                    bodyArmorStatText.text = bodyArmorValue.ToString();

                if (loadedAmmoStatText.text != null)
                    loadedAmmoStatText.text = loadedAmmoValue.ToString();

                if (reserveAmmoStatText.text != null)
                    reserveAmmoStatText.text = reserveAmmoValue.ToString();


                isWeapon = true;
            }
            else if (characterEquip != null)
            {
                selectionNameText.text = characterEquip.characterName;

                // retrieve stats from weaponEquip.weaponStats
                int healthValue = characterEquip.characterStats.GetStatValue(StatType.Hp);
                int attackValue = characterEquip.characterStats.GetStatValue(StatType.AttackDamage);
                int speedValue = characterEquip.characterStats.GetStatValue(StatType.MoveSpeed);
                int bodyArmorValue = characterEquip.characterStats.GetStatValue(StatType.BodyArmor);

                // set the stat values to the text fields
                if (healthStatText != null)
                    healthStatText.text = healthValue.ToString();

                if (attackStatText != null)
                    attackStatText.text = attackValue.ToString();

                if (speedStatText != null)
                    speedStatText.text = speedValue.ToString();

                if (bodyArmorStatText != null)
                    bodyArmorStatText.text = bodyArmorValue.ToString();

                isCharacter = true;
            }
            else if (cosmeticEquip != null)
            {
                selectionNameText.text = cosmeticEquip.cosmeticName;

                // if you want to give the cosmetic stats, copy just like character and weapon equips do above and get the stat value here
                // then you assign the text objects to their respective fields in the equip button inspector

                isCosmetic = true;
            }
        }

        private void Update()
        {
            if (buttonEquip != null)
                buttonEquip.interactable = !IsEquipped();
        }

        // if the weapon or character is already equipped then it shows in the loadout UI.
        private bool IsEquipped()
        {
            if (isWeapon)
            {
                for (var i = 0; i < PlayerGameData.Weapons.Count; ++i)
                {
                    var config = PlayerGameData.Weapons[i];

                    if (config == weaponEquip)
                        return i == mainMenu.GetSelectedWeaponForMount(weaponEquip.requiredMount);
                }
            }
            else if (isCharacter)
            {
                for (var i = 0; i < PlayerGameData.Characters.Count; ++i)
                {
                    var config = PlayerGameData.Characters[i];

                    if (config == characterEquip)
                        return i == mainMenu.SelectedCharacterConfig;
                }
            }
            else if (isCosmetic)
            {
                for (var i = 0; i < PlayerGameData.Cosmetics.Count; ++i)
                {
                    var config = PlayerGameData.Cosmetics[i];

                    if (config == cosmeticEquip)
                        return i == mainMenu.SelectedCosmeticConfig;
                }
            }

            return false;
        }

        // this method is used to equip the weapon or select the character
        public void OnClickEquip()
        {
            if (isWeapon)
            {
                for (var i = 0; i < PlayerGameData.Weapons.Count; ++i)
                {
                    var config = PlayerGameData.Weapons[i];

                    if (config == weaponEquip)
                        mainMenu.SetSelectedWeaponForMount(i); // equips into Top / Left / Right based on WeaponConfig                 
                }
            }
            else if (isCharacter)
            {
                for (var i = 0; i < PlayerGameData.Characters.Count; ++i)
                {
                    var config = PlayerGameData.Characters[i];

                    if (config == characterEquip)
                        mainMenu.SelectedCharacterConfig = i; // set the selected character int which updates the character model
                }
            }
            else if (isCosmetic)
            {
                for (var i = 0; i < PlayerGameData.Cosmetics.Count; ++i)
                {
                    var config = PlayerGameData.Cosmetics[i];

                    if (config == cosmeticEquip)
                        mainMenu.SelectedCosmeticConfig = i; // set the selected cosmetic int which updates the cosmetic model
                }
            }
        }
    }
}

