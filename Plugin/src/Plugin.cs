using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace SMTModBrowser
{
    [BepInPlugin(Guid, Name, Version)]
    public class ModBrowserPlugin : BaseUnityPlugin
    {
        public const string Guid = "smt.installandmanager.modbrowser";
        public const string Name = "SMT Mod Browser";
        public const string Version = "0.2.0";

        internal ConfigEntry<KeyboardShortcut> ToggleKey;
        internal ConfigEntry<float> UiScale;

        BrowserWindow window;
        bool open;
        CursorLockMode savedLockState;
        bool savedCursorVisible;

        void Awake()
        {
            ToggleKey = Config.Bind("General", "Toggle key", new KeyboardShortcut(KeyCode.F6),
                "Opens and closes the mod browser.");
            UiScale = Config.Bind("General", "UI scale", 0f,
                new ConfigDescription("Size of the mod browser window. 0 picks a size based on your screen resolution.",
                    new AcceptableValueRange<float>(0f, 3f)));

            window = new BrowserWindow(this, Logger, Paths.BepInExRootPath);
            Logger.LogInfo($"{Name} {Version} loaded. Press {ToggleKey.Value} to browse mods.");
        }

        void Update()
        {
            if (ToggleKey.Value.IsDown()) SetOpen(!open);
        }

        void LateUpdate()
        {
            // The game re-locks the cursor every frame while you play, so keep it free while browsing
            if (!open) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnGUI()
        {
            if (open) window.Draw();
        }

        internal void SetOpen(bool value)
        {
            if (open == value) return;
            open = value;
            if (open)
            {
                savedLockState = Cursor.lockState;
                savedCursorVisible = Cursor.visible;
                window.OnOpened();
            }
            else
            {
                Cursor.lockState = savedLockState;
                Cursor.visible = savedCursorVisible;
            }
        }
    }
}
