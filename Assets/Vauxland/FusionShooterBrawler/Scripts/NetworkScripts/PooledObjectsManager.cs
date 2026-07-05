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
    public static class PooledObjectsManager
    {
        private static Transform pooledObjectsParent;

        public static Transform GetPooledObjectsParent()
        {
            if (pooledObjectsParent == null)
            {
                // try to find the "Pooled Objects" gameObject in the active scene
                var parentObject = GameObject.Find("Pooled Objects");

                if (parentObject != null)
                {
                    pooledObjectsParent = parentObject.transform;
                }
                else
                {
                    // create the "Pooled Objects" gameObject if it doesn't exist
                    parentObject = new GameObject("Pooled Objects");
                    pooledObjectsParent = parentObject.transform;
                }
            }
            return pooledObjectsParent;
        }
    }
}

