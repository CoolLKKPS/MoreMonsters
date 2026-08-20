/*
using BepInEx.Configuration;
using GameNetcodeStuff;
using UnityEngine;

// credits to creator of LethalCompanyGameMaster. The following GUI code is based off of his implementation.

namespace MoreMonsters.GuiMenuComponent
{
    internal class GuiMenu : MonoBehaviour
    {
        private KeyboardShortcut toggleMenu;
        private bool b_menu;
        internal bool wasKeyDown;

        private int tabIndex = 0;
        private readonly string[] tabNames = { "Mob Settings" };

        private readonly int MENUWIDTH = 600;
        private readonly int MENUHEIGHT = 800;
        private int MENUX;
        private int MENUY;

        private const int SLIDER_VERTICAL_OFFSET = 2;

        public float guiTimeBetweenMobSpawns;
        public bool guiEnableSpawnMobsAsScrapIsFound;

        private Vector2 scrollPos;

        private GUIStyle menuStyle;
        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;
        private GUIStyle toggleStyle;
        private GUIStyle toggleTextStyle;
        private GUIStyle textStyle;

        public bool guiIsHost;

        private void Awake()
        {
            MoreMonstersBase.mls.LogInfo(" [+] GUI Loaded");
            toggleMenu = new KeyboardShortcut(KeyCode.Insert);
            b_menu = false; // I'll set it here just in case

            MENUX = Screen.width / 2;
            MENUY = Screen.height / 2;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void initMenu()
        {
            if (menuStyle == null)
            {
                menuStyle = new GUIStyle(GUI.skin.box);
                buttonStyle = new GUIStyle(GUI.skin.button);
                labelStyle = new GUIStyle(GUI.skin.label);
                toggleStyle = new GUIStyle(GUI.skin.toggle);
                textStyle = new GUIStyle
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter
                };
                textStyle.normal.textColor = Color.white;

                menuStyle.normal.textColor = Color.white;
                menuStyle.normal.background = MakeTex(2, 2, new Color(0.01f, 0.01f, 0.1f, .9f));
                menuStyle.fontSize = 18;
                menuStyle.normal.background.hideFlags = HideFlags.HideAndDontSave;

                buttonStyle.normal.textColor = Color.white;
                buttonStyle.fontSize = 18;

                labelStyle.normal.textColor = Color.white;
                labelStyle.normal.background = MakeTex(2, 2, new Color(0.01f, 0.01f, 0.1f, .9f));
                labelStyle.fontSize = 18;
                labelStyle.alignment = TextAnchor.MiddleCenter;
                labelStyle.normal.background.hideFlags = HideFlags.HideAndDontSave;

                toggleStyle.normal.textColor = Color.white;
                toggleStyle.fontSize = 18;
                toggleStyle.wordWrap = true;

                toggleTextStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleLeft
                };
                toggleTextStyle.normal.textColor = Color.white;
            }
        }
        public void OnDestroy()
        {
            MoreMonstersBase.mls.LogInfo("[-] The GUILoader was destroyed.");
        }
        public void Update()
        {
            if (toggleMenu.IsDown())
            {
                if (!wasKeyDown)
                {
                    wasKeyDown = true;
                }
            }
            if (toggleMenu.IsUp())
            {
                if (wasKeyDown)
                {
                    wasKeyDown = false;
                    SetMenuOpen(!b_menu);
                }
            }
        } // end of update

        private void SetMenuOpen(bool open)
        {
            b_menu = open;

            PlayerControllerB player = GameNetworkManager.Instance?.localPlayerController;
            if (player != null)
            {
                player.quickMenuManager.isMenuOpen = open;
            }

            Cursor.visible = open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        }

        public void OnGUI()
        {
            if (!guiIsHost)
            {
                return;
            }
            if (menuStyle == null)
            {
                initMenu();
            }

            if (!b_menu)
            {
                return;
            }

            int viewportHeight = Mathf.Min(MENUHEIGHT, Screen.height - MENUY - 60);
            if (viewportHeight < 200)
            {
                viewportHeight = 200;
            }

            tabIndex = GUI.Toolbar(new Rect(MENUX, MENUY - 30, MENUWIDTH, 30), tabIndex, tabNames, buttonStyle);

            const int ROWH = 30;
            const int TOGGLEH = 45;
            const int PAD = 10;
            const int SLIDERH = 16;

            int rows = 0;
            foreach (EnemyEntry entry in EnemyRegistry.Entries)
            {
                if (entry.MaxEntry != null)
                {
                    rows++;
                }
            }
            int contentHeight = PAD + ROWH + ROWH + TOGGLEH + 10 + ROWH + ROWH + rows * ROWH + PAD;

            Rect contentRect = new Rect(MENUX, MENUY, MENUWIDTH, viewportHeight);
            GUI.Box(contentRect, GUIContent.none, menuStyle);

            scrollPos = GUI.BeginScrollView(
                contentRect,
                scrollPos,
                new Rect(0, 0, MENUWIDTH - PAD * 2, contentHeight));

            int y = PAD;

            GUI.Label(new Rect(PAD, y, MENUWIDTH - PAD * 2, ROWH), "Time Between Mob Spawns", labelStyle);
            y += ROWH;
            guiTimeBetweenMobSpawns = GUI.HorizontalSlider(new Rect(PAD, y + (ROWH - SLIDERH) / 2 + SLIDER_VERTICAL_OFFSET, 300, SLIDERH), guiTimeBetweenMobSpawns, 0, 800);
            GUI.Label(new Rect(PAD + 310, y, MENUWIDTH - PAD * 2 - 310, ROWH), (guiTimeBetweenMobSpawns / 100f).ToString("0.00") + " hrs", textStyle);
            y += ROWH;

            const int TOGGLEBOX = 20;
            guiEnableSpawnMobsAsScrapIsFound = GUI.Toggle(
                new Rect(PAD, y + (TOGGLEH - TOGGLEBOX) / 2, TOGGLEBOX, TOGGLEBOX),
                guiEnableSpawnMobsAsScrapIsFound,
                GUIContent.none,
                toggleStyle);
            GUI.Label(
                new Rect(PAD + TOGGLEBOX + 10, y, MENUWIDTH - PAD * 2 - TOGGLEBOX - 10, TOGGLEH),
                "Spawn an extra mob after finding 25%, 50%, and 75% of total scrap.",
                toggleTextStyle);
            y += TOGGLEH + 10;

            GUI.Label(new Rect(PAD, y, MENUWIDTH - PAD * 2, ROWH), "Max Number of Each Monster", labelStyle);
            y += ROWH;
            GUI.Label(new Rect(PAD, y, MENUWIDTH - PAD * 2, ROWH), "Total mobs the mod will spawn: " + EnemyRegistry.TotalConfigured(), textStyle);
            y += ROWH;

            foreach (EnemyEntry entry in EnemyRegistry.Entries)
            {
                if (entry.MaxEntry == null)
                {
                    continue;
                }
                GUI.Label(new Rect(PAD, y, 230, ROWH), entry.DisplayName + ":", labelStyle);
                int value = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(PAD + 235, y + (ROWH - SLIDERH) / 2 + SLIDER_VERTICAL_OFFSET, 260, SLIDERH), entry.MaxEntry.Value, 0, EnemyRegistry.MaxEnemyLimit));
                entry.MaxEntry.Value = value;
                GUI.Label(new Rect(PAD + 500, y, 70, ROWH), "" + value, textStyle);
                y += ROWH;
            }

            GUI.EndScrollView();
        }
    }
}
*/
