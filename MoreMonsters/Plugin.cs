using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LethalConfig;
using LethalConfig.ConfigItems;
// using MoreMonsters.GuiMenuComponent;
// using MoreMonsters.PlayerBControllerPatches;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;

namespace MoreMonsters
{
    [BepInPlugin(modGUID, modName, modVersion)]
    [BepInDependency("ainavt.lc.lethalconfig", BepInDependency.DependencyFlags.SoftDependency)]
    public class MoreMonstersBase : BaseUnityPlugin
    {
        private const string modGUID = "Quokka.MoreMonsters";
        private const string modName = "MoreMonsters";
        private const string modVersion = "1.3.0";

        internal const string LethalConfigGUID = "ainavt.lc.lethalconfig";

        private readonly Harmony harmony = new Harmony(modGUID);

        internal static MoreMonstersBase Instance;

        private static ConfigEntry<float> timeBetweenMobSpawns;
        private static ConfigEntry<bool> enableSpawnMobsAsScrapIsFound;

        // private static bool hasGuiSynced = false;

        internal static bool isHost;

        public static ManualLogSource mls;

        private static RoundManager currentRound;

        public static bool firstTier = false;
        public static bool secondTier = false;
        public static bool thirdTier = false;

        public static float timeToSpawn = 120f; // slightly after spawntime
        public static int ventIndex = 0;

        private static float lastCurrentDayTime = 0f;

        // internal static GuiMenu myGUI;

        public static int spawnedMonsterTotal = 0;

        private static readonly Dictionary<string, int> daySpawnCount = new Dictionary<string, int>();

        private void Awake()
        {
            // Major credit to @lawrencea13 for his Lethal Company Game Master code. Made understanding
            // how gui works much easier.

            Instance = this;

            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);

            harmony.PatchAll(typeof(MoreMonstersBase));
            /*
            harmony.PatchAll(typeof(PlayerControllerBPatch));

            var gameObject = new UnityEngine.GameObject("GuiMenu");
            UnityEngine.GameObject.DontDestroyOnLoad(gameObject);
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            gameObject.AddComponent<GuiMenu>();
            myGUI = (GuiMenu)gameObject.GetComponent("GuiMenu");
            */
            SetBindings();
            // setGuiVars();
            if (Chainloader.PluginInfos.ContainsKey(LethalConfigGUID))
            {
                RegisterLethalConfig();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RegisterLethalConfig()
        {
            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(timeBetweenMobSpawns, false));
            LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(enableSpawnMobsAsScrapIsFound, false));
        }

        /*
        private void setGuiVars()
        {
            myGUI.guiTimeBetweenMobSpawns = timeBetweenMobSpawns.Value;
            myGUI.guiEnableSpawnMobsAsScrapIsFound = enableSpawnMobsAsScrapIsFound.Value;
            hasGuiSynced = true;
        }

        internal void updateCFGVarsViaGui()
        {
            if (!hasGuiSynced)
            {
                setGuiVars();
            }

            timeBetweenMobSpawns.Value = myGUI.guiTimeBetweenMobSpawns;
            enableSpawnMobsAsScrapIsFound.Value = myGUI.guiEnableSpawnMobsAsScrapIsFound;
        }

        private void Update()
        {
        }
        */

        private void SetBindings()
        {
            timeBetweenMobSpawns = Config.Bind("Mob Settings", "Time between each Mob Spawn", 1f, new ConfigDescription("Time between each mob spawn in hours", new AcceptableValueRange<float>(0, 8)));
            enableSpawnMobsAsScrapIsFound = Config.Bind("Mob Settings", "Toggle whether more mobs spawn as scrap is found.", false, "If true, an additional mob will spawn at 25%, 50%, and 75% of scrap found in level");
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.LoadNewLevel))]
        [HarmonyPostfix]
        private static void ModifyLevel(ref SelectableLevel newLevel)
        {
            currentRound = RoundManager.Instance;
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ChangeLevel))]
        [HarmonyPostfix]
        private static void ChangeLevel(ref SelectableLevel ___currentLevel, ref SelectableLevel[] ___levels)
        {
            if (___currentLevel == null || ___levels == null || ___levels.Length == 0)
            {
                return;
            }

            EnemyRegistry.DiscoverFromLevels(Instance.Config, ___levels);
            ___currentLevel.Enemies = MergeInsideEnemies(___levels);
            ResetDayState();
        }

        private static void ResetDayState()
        {
            spawnedMonsterTotal = 0;
            timeToSpawn = 120f; // slightly after the game starts spawning enemies (currentDayTime > 85)
            firstTier = false;
            secondTier = false;
            thirdTier = false;
            ventIndex = 0;
            daySpawnCount.Clear();
        }

        private static bool CanSpawnEnemy(EnemyType enemyType)
        {
            int max = EnemyRegistry.TryGetMax(enemyType.name, out int configuredMax) ? configuredMax : int.MaxValue;
            daySpawnCount.TryGetValue(enemyType.name, out int count);
            return count < max;
        }

        private static void CountSpawned(EnemyType enemyType)
        {
            string name = enemyType.name;
            daySpawnCount[name] = daySpawnCount.TryGetValue(name, out int count) ? count + 1 : 1;
        }

        private static List<EnemyType> BuildOutsideCandidates(List<SpawnableEnemyWithRarity> list, bool isDaytime)
        {
            List<EnemyType> result = new List<EnemyType>();
            if (list == null)
            {
                return result;
            }

            float normalizedNow = TimeOfDay.Instance != null ? TimeOfDay.Instance.normalizedTimeOfDay : 1f;
            foreach (SpawnableEnemyWithRarity spawnable in list)
            {
                EnemyType enemyType = spawnable?.enemyType;
                if (enemyType == null)
                {
                    continue;
                }
                if (isDaytime && enemyType.normalizedTimeInDayToLeave < normalizedNow)
                {
                    continue;
                }
                if (CanSpawnEnemy(enemyType))
                {
                    result.Add(enemyType);
                }
            }
            return result;
        }

        private static void SpawnOutsideEnemy(RoundManager round, EnemyType enemyType)
        {
            if (enemyType == null || enemyType.enemyPrefab == null)
            {
                return;
            }

            round.GetOutsideAINodes(true);

            GameObject[] nodes;
            if (enemyType.WaterType == EnemyWaterType.WaterOnly)
            {
                nodes = round.outsideAIWaterNodes;
            }
            else if (enemyType.WaterType == EnemyWaterType.LandOnly)
            {
                nodes = round.outsideAIDryNodes;
            }
            else
            {
                nodes = round.outsideAINodes;
            }

            if (nodes == null || nodes.Length == 0)
            {
                return;
            }

            Vector3 spawnPos = nodes[UnityEngine.Random.Range(0, nodes.Length)].transform.position;
            GameObject go = UnityEngine.Object.Instantiate(enemyType.enemyPrefab, spawnPos, Quaternion.Euler(Vector3.zero));
            go.GetComponentInChildren<NetworkObject>().Spawn(true);
            round.SpawnedEnemies.Add(go.GetComponent<EnemyAI>());
            enemyType.numberSpawned++;
            enemyType.hasSpawnedAtLeastOne = true;
            CountSpawned(enemyType);

            mls.LogInfo("Spawning outside " + enemyType.name + " at " + spawnPos + " spawnedMonsterTotal: " + spawnedMonsterTotal);
        }

        private static List<SpawnableEnemyWithRarity> MergeInsideEnemies(SelectableLevel[] levels)
        {
            List<SpawnableEnemyWithRarity> merged = new List<SpawnableEnemyWithRarity>();
            HashSet<EnemyType> seen = new HashSet<EnemyType>();

            foreach (SelectableLevel level in levels)
            {
                if (level == null || level.Enemies == null)
                {
                    continue;
                }
                foreach (SpawnableEnemyWithRarity spawnable in level.Enemies)
                {
                    if (spawnable == null || spawnable.enemyType == null || !seen.Add(spawnable.enemyType))
                    {
                        continue;
                    }
                    merged.Add(new SpawnableEnemyWithRarity(spawnable.enemyType, spawnable.rarity));
                }
            }

            return merged;
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.StartGame))]
        [HarmonyPostfix]
        private static void modifiedStart()
        {
            ResetDayState();
            StartOfRound startOfRound = StartOfRound.Instance;
            if (startOfRound != null)
            {
                EnemyRegistry.DiscoverFromLevels(Instance.Config, startOfRound.levels);
            }
            // Instance.updateCFGVarsViaGui();
        }

        [HarmonyPatch(typeof(RoundManager), "Start")]
        [HarmonyPostfix]
        private static void setIsHost()
        {
            mls.LogInfo("Host Status: " + RoundManager.Instance.NetworkManager.IsHost.ToString());
            isHost = RoundManager.Instance.NetworkManager.IsHost;
            // MoreMonstersBase.myGUI.guiIsHost = isHost;

            StartOfRound startOfRound = StartOfRound.Instance;
            if (startOfRound != null)
            {
                EnemyRegistry.DiscoverFromLevels(Instance.Config, startOfRound.levels);
            }
            // Instance.updateCFGVarsViaGui();
        }

        [HarmonyPatch(typeof(RoundManager), "SpawnInsideEnemiesFromVentsIfReady")]
        [HarmonyPostfix]
        private static void SpawnInsideEnemiesFromVentsIfReadyPatch()
        {
            if (!isHost)
            {
                return;
            }

            currentRound = RoundManager.Instance;
            if (currentRound == null || currentRound.currentLevel == null)
            {
                return;
            }

            float dayTime = currentRound.timeScript.currentDayTime;
            if (dayTime < lastCurrentDayTime - 1f)
            {
                ResetDayState();
            }
            lastCurrentDayTime = dayTime;

            int monsterSum = EnemyRegistry.TotalConfigured();
            if (monsterSum <= 0)
            {
                return;
            }

            if (spawnedMonsterTotal >= monsterSum || currentRound.timeScript.currentDayTime <= timeToSpawn || currentRound.allEnemyVents.Length == 0)
            {
                return;
            }

            int ventLength = currentRound.allEnemyVents.Length;
            EnemyVent vent = currentRound.allEnemyVents[ventIndex % ventLength];
            Vector3 pos = vent.floorNode.position;
            float y = vent.floorNode.eulerAngles.y;

            List<SpawnableEnemyWithRarity> indoorEnemies = currentRound.currentLevel.Enemies;
            List<int> indoorCandidates = new List<int>();
            if (indoorEnemies != null)
            {
                for (int i = 0; i < indoorEnemies.Count; i++)
                {
                    EnemyType enemyType = indoorEnemies[i].enemyType;
                    if (enemyType != null && CanSpawnEnemy(enemyType))
                    {
                        indoorCandidates.Add(i);
                    }
                }
            }

            List<EnemyType> outdoorCandidates = BuildOutsideCandidates(currentRound.currentLevel.OutsideEnemies, false);
            List<EnemyType> daytimeCandidates = BuildOutsideCandidates(currentRound.currentLevel.DaytimeEnemies, true);

            int indoorCount = indoorCandidates.Count;
            int total = indoorCount + outdoorCandidates.Count + daytimeCandidates.Count;
            if (total == 0)
            {
                return;
            }

            int roll = UnityEngine.Random.Range(0, total);
            if (roll < indoorCount)
            {
                int random = indoorCandidates[roll];
                EnemyType enemyType = indoorEnemies[random].enemyType;
                mls.LogInfo("Spawning indoor " + random + " name: " + enemyType.name + " spawnedMonsterTotal: " + spawnedMonsterTotal);
                currentRound.SpawnEnemyOnServer(pos, y, random);
                enemyType.numberSpawned++;
                CountSpawned(enemyType);
            }
            else if (roll < indoorCount + outdoorCandidates.Count)
            {
                SpawnOutsideEnemy(currentRound, outdoorCandidates[roll - indoorCount]);
            }
            else
            {
                SpawnOutsideEnemy(currentRound, daytimeCandidates[roll - indoorCount - outdoorCandidates.Count]);
            }

            currentRound.currentEnemySpawnIndex++;
            spawnedMonsterTotal++;
            ventIndex++;
            ventIndex %= ventLength;

            timeToSpawn = currentRound.timeScript.currentDayTime + timeBetweenMobSpawns.Value * 100f;

            mls.LogInfo("spawnedMonsterTotal: " + spawnedMonsterTotal);

            if (enableSpawnMobsAsScrapIsFound.Value && indoorEnemies != null && indoorEnemies.Count > 0)
            {
                int random;
                if ((currentRound.valueOfFoundScrapItems > (int)(0.25 * currentRound.totalScrapValueInLevel)) && !firstTier)
                {
                    random = GetRandomSpawnIndex(currentRound, indoorEnemies.Count);
                    currentRound.SpawnEnemyOnServer(pos, y, random);
                    ventIndex++;
                    ventIndex %= ventLength;
                    firstTier = true;
                }
                if ((currentRound.valueOfFoundScrapItems > (int)(0.5 * currentRound.totalScrapValueInLevel)) && !secondTier)
                {
                    random = GetRandomSpawnIndex(currentRound, indoorEnemies.Count);
                    currentRound.SpawnEnemyOnServer(pos, y, random);
                    ventIndex++;
                    ventIndex %= ventLength;
                    secondTier = true;
                }
                if ((currentRound.valueOfFoundScrapItems > (int)(0.75 * currentRound.totalScrapValueInLevel)) && !thirdTier)
                {
                    random = GetRandomSpawnIndex(currentRound, indoorEnemies.Count);
                    currentRound.SpawnEnemyOnServer(pos, y, random);
                    ventIndex++;
                    ventIndex %= ventLength;
                    thirdTier = true;
                }
            }
        }

        private static int GetRandomSpawnIndex(RoundManager round, int enemyListLength)
        {
            int dayTime = (int)round.timeScript.currentDayTime;
            int scrapValue = (int)round.totalScrapValueInLevel;

            int firstTerm = ventIndex * dayTime;
            int secondTerm = 19 * (ventIndex + scrapValue);

            return (firstTerm + secondTerm + 21) % enemyListLength;
        }
    }
}
