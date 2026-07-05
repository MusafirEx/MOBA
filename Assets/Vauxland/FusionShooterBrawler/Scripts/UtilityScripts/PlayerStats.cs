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
using UnityEngine;

namespace Vauxland.FusionBrawler
{
    // this is the core script of the players stats. Any new stat you want to add you add here
    // and then make appropriate changes in other scripts, wherever these stats are called you will also need to add for your new stat
    public enum StatType
    {
        Hp,
        BodyArmor,
        ReserveAmmo,
        LoadedAmmo,
        AttackDamage,
        MoveSpeed
    }

    [System.Serializable]
    public struct PlayerStats
    {
        public Dictionary<StatType, int> stats;

        public int GetStat(StatType statType)
        {
            return stats.TryGetValue(statType, out int value) ? value : 0;
        }

        public void SetStat(StatType statType, int value)
        {
            stats[statType] = value;
        }

        public void ModifyStat(StatType statType, int value)
        {
            if (stats.ContainsKey(statType))
            {
                stats[statType] += value;
            }
            else
            {
                stats[statType] = value;
            }
        }

        public void AddStats(Dictionary<StatType, int> source)
        {
            foreach (var stat in source)
            {
                ModifyStat(stat.Key, stat.Value);
            }
        }
    }

    [Serializable]
    public class StatEntry
    {
        public StatType statType;
        public int value;
    }

    [Serializable]
    public class StatDictionary
    {
        public List<StatEntry> stats = new List<StatEntry>();

        public int GetStatValue(StatType statType)
        {
            var entry = stats.Find(e => e.statType == statType);
            return entry != null ? entry.value : 0;
        }

        public void SetStatValue(StatType statType, int value)
        {
            var entry = stats.Find(e => e.statType == statType);
            if (entry != null)
            {
                entry.value = value;
            }
            else
            {
                stats.Add(new StatEntry { statType = statType, value = value });
            }
        }

        public Dictionary<StatType, int> ToDictionary()
        {
            Dictionary<StatType, int> dictionary = new Dictionary<StatType, int>();
            foreach (var entry in stats)
            {
                dictionary[entry.statType] = entry.value;
            }
            return dictionary;
        }
    }
}
