using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
// using MoreMonsters.GuiMenuComponent;
// using MoreMonsters.PlayerBControllerPatches;
using System.Collections.Generic;
using UnityEngine;

namespace MoreMonsters
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class MoreMonstersBase : BaseUnityPlugin
    {
        private const string modGUID = "Quokka.MoreMonsters";
        private const string modName = "MoreMonsters";
        private const string modVersion = "1.3.0";

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
            timeBetweenMobSpawns = Config.Bind("Mob Settings", "Time between each Mob Spawn", 100f, new ConfigDescription("Time between each mob spawn where 0.1 = 1.5 hours", new AcceptableValueRange<float>(0, 800)));
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

            Vector3 pos = currentRound.allEnemyVents[ventIndex].floorNode.position;
            float y = currentRound.allEnemyVents[ventIndex].floorNode.eulerAngles.y;

            List<SpawnableEnemyWithRarity> enemies = currentRound.currentLevel.Enemies;
            if (enemies == null || enemies.Count == 0)
            {
                return;
            }

            List<int> candidates = new List<int>(enemies.Count);
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyType enemyType = enemies[i].enemyType;
                if (enemyType == null)
                {
                    continue;
                }
                int max = EnemyRegistry.TryGetMax(enemyType.name, out int configuredMax) ? configuredMax : int.MaxValue;
                if (enemyType.numberSpawned < max)
                {
                    candidates.Add(i);
                }
            }
            if (candidates.Count == 0)
            {
                return;
            }

            int random = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            string currEnemyName = enemies[random].enemyType.name;

            mls.LogInfo("Spawning " + random + " name: " + currEnemyName + " spawnedMonsterTotal: " + spawnedMonsterTotal);
            currentRound.SpawnEnemyOnServer(pos, y, random);

            enemies[random].enemyType.numberSpawned++;
            currentRound.currentEnemySpawnIndex++;
            spawnedMonsterTotal++;
            ventIndex++;
            ventIndex %= currentRound.allEnemyVents.Length;

            timeToSpawn = currentRound.timeScript.currentDayTime + timeBetweenMobSpawns.Value;

            mls.LogInfo("spawnedMonsterTotal: " + spawnedMonsterTotal);

            if (enableSpawnMobsAsScrapIsFound.Value)
            {
                if ((currentRound.valueOfFoundScrapItems > (int)(0.25 * currentRound.totalScrapValueInLevel)) && !firstTier)
                {
                    random = GetRandomSpawnIndex(currentRound, enemies.Count);
                    currentRound.SpawnEnemyOnServer(pos, y, random);
                    ventIndex++;
                    ventIndex %= currentRound.allEnemyVents.Length;
                    firstTier = true;
                }
                if ((currentRound.valueOfFoundScrapItems > (int)(0.5 * currentRound.totalScrapValueInLevel)) && !secondTier)
                {
                    random = GetRandomSpawnIndex(currentRound, enemies.Count);
                    currentRound.SpawnEnemyOnServer(pos, y, random);
                    ventIndex++;
                    ventIndex %= currentRound.allEnemyVents.Length;
                    secondTier = true;
                }
                if ((currentRound.valueOfFoundScrapItems > (int)(0.75 * currentRound.totalScrapValueInLevel)) && !thirdTier)
                {
                    random = GetRandomSpawnIndex(currentRound, enemies.Count);
                    currentRound.SpawnEnemyOnServer(pos, y, random);
                    ventIndex++;
                    ventIndex %= currentRound.allEnemyVents.Length;
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
