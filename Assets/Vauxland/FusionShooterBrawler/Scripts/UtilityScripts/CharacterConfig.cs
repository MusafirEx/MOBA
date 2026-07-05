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
    [CreateAssetMenu(fileName = "CharacterConfig", menuName = "FusionBrawler/CharacterConfig")]
    public class CharacterConfig : ScriptableObject
    {
        public string characterName; // the character name displayed in the Loadout UI;

        [Header("Character Stats")]
        public StatDictionary characterStats; // set the character stats here how much damage it does, other stats it gives to player like Hp, Attack, etc.
        public CharacterModelConfig characterModel; // the character model config prefab
    }

}
