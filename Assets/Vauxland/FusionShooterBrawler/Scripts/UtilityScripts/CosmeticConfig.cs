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
    [CreateAssetMenu(fileName = "CosmeticConfig", menuName = "FusionBrawler/CosmeticConfig")]
    public class CosmeticConfig : ScriptableObject
    {
        public string cosmeticName; // the cosmetic name displayed in the Loadout UI;

        [Header("Cosmetic Stats")]
        public StatDictionary cosmeticStats; // if you want to give the cosmetic stats you can.
        public GameObject cosmeticModel; // the model of the cosmetic
    }

}
