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
    [CreateAssetMenu(fileName = "MapConfig", menuName = "FusionBrawler/MapConfig")]
    public class MapConfig : ScriptableObject
    {
        public string mapName;  // displays name of the map
        public string sceneName;  // the name of the scene to load
        public Texture2D mapImage; // screenshot image for the map
    }
}

