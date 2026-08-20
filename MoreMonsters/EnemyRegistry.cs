using BepInEx.Bootstrap;
using BepInEx.Configuration;
using LethalConfig;
using LethalConfig.ConfigItems;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MoreMonsters
{
    internal class EnemyEntry
    {
        public readonly string EnemyTypeName;
        public readonly string DisplayName;
        public readonly string ConfigKey;
        public ConfigEntry<int> MaxEntry;

        public EnemyEntry(string enemyTypeName, string displayName, string configKey)
        {
            EnemyTypeName = enemyTypeName;
            DisplayName = displayName;
            ConfigKey = configKey;
        }
    }

    internal static class EnemyRegistry
    {
        public const string ConfigSection = "Mob Settings";
        public const int MaxEnemyLimit = 50;

        private static readonly List<EnemyEntry> entries = new List<EnemyEntry>();
        private static readonly Dictionary<string, EnemyEntry> byName = new Dictionary<string, EnemyEntry>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<EnemyEntry> Entries => entries;

        public static void DiscoverFromLevels(ConfigFile config, SelectableLevel[] levels)
        {
            if (levels == null)
            {
                return;
            }

            bool changed = false;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (EnemyEntry entry in entries)
            {
                seen.Add(entry.EnemyTypeName);
            }

            foreach (SelectableLevel level in levels)
            {
                if (level == null)
                {
                    continue;
                }
                AddEnemyList(level.Enemies);
                AddEnemyList(level.OutsideEnemies);
                AddEnemyList(level.DaytimeEnemies);
            }

            if (changed)
            {
                config.Save();
            }

            void AddEnemyList(List<SpawnableEnemyWithRarity> list)
            {
                if (list == null)
                {
                    return;
                }
                foreach (SpawnableEnemyWithRarity spawnable in list)
                {
                    EnemyType enemyType = spawnable?.enemyType;
                    if (enemyType == null)
                    {
                        continue;
                    }
                    string name = enemyType.name;
                    if (string.IsNullOrEmpty(name))
                    {
                        name = enemyType.enemyName;
                    }
                    if (string.IsNullOrEmpty(name) || !seen.Add(name))
                    {
                        continue;
                    }
                    if (GetOrCreate(config, name, enemyType.enemyName) != null)
                    {
                        changed = true;
                    }
                }
            }
        }

        public static EnemyEntry GetOrCreate(ConfigFile config, string enemyTypeName, string displayName)
        {
            if (string.IsNullOrEmpty(enemyTypeName))
            {
                return null;
            }

            if (byName.TryGetValue(enemyTypeName, out EnemyEntry existing))
            {
                return existing;
            }

            string configKey = enemyTypeName;
            string label = string.IsNullOrEmpty(displayName) ? enemyTypeName : displayName;

            EnemyEntry entry = new EnemyEntry(enemyTypeName, label, configKey)
            {
                MaxEntry = config.Bind(ConfigSection, "Max " + configKey, 0, new ConfigDescription("Max number of " + label + " the mod is allowed to spawn", new AcceptableValueRange<int>(0, MaxEnemyLimit)))
            };

            if (Chainloader.PluginInfos.ContainsKey(MoreMonstersBase.LethalConfigGUID))
            {
                RegisterEnemyWithLethalConfig(entry);
            }

            entries.Add(entry);
            byName[enemyTypeName] = entry;
            return entry;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RegisterEnemyWithLethalConfig(EnemyEntry entry)
        {
            LethalConfigManager.AddConfigItem(new IntSliderConfigItem(entry.MaxEntry, false));
        }

        public static bool TryGetMax(string enemyTypeName, out int max)
        {
            max = 0;
            if (string.IsNullOrEmpty(enemyTypeName) || !byName.TryGetValue(enemyTypeName, out EnemyEntry entry) || entry.MaxEntry == null)
            {
                return false;
            }
            max = entry.MaxEntry.Value;
            return true;
        }

        public static int TotalConfigured()
        {
            int total = 0;
            foreach (EnemyEntry entry in entries)
            {
                if (entry.MaxEntry != null)
                {
                    total += entry.MaxEntry.Value;
                }
            }
            return total;
        }
    }
}
