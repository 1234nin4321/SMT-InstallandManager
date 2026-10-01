using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SMTDecorator
{
    [BepInPlugin(Guid, Name, Version)]
    public class DecoratorPlugin : BaseUnityPlugin
    {
        public const string Guid = "smt.installandmanager.decorator";
        public const string Name = "SMT Decorator";
        public const string Version = "0.1.2";

        internal static BepInEx.Logging.ManualLogSource Log;
        internal static ConfigEntry<KeyboardShortcut> TabletKey;
        internal static ConfigEntry<KeyboardShortcut> MenuKey;
        internal static ConfigEntry<float> PanelPrice;
        internal static ConfigEntry<float> PicturePrice;
        internal static ConfigEntry<int> MaxImageSize;
        internal static ConfigEntry<float> Reach;

        void Awake()
        {
            Log = Logger;
            TabletKey = Config.Bind("Decorator", "Tablet", new KeyboardShortcut(KeyCode.F7),
                "Takes out the Decorator tablet, or puts it away. Your hands have to be empty");
            MenuKey = Config.Bind("Decorator", "Tablet menu", new KeyboardShortcut(KeyCode.Tab),
                "Opens the tablet's menu, where you pick a colour or a picture");
            PanelPrice = Config.Bind("Decorator", "Price per wall panel", 2f,
                new ConfigDescription("What painting one wall panel costs (host only)", new AcceptableValueRange<float>(0f, 100f)));
            PicturePrice = Config.Bind("Decorator", "Price per picture", 10f,
                new ConfigDescription("What hanging a picture costs (host only)", new AcceptableValueRange<float>(0f, 1000f)));
            MaxImageSize = Config.Bind("Decorator", "Largest picture size", 1024,
                new ConfigDescription("Imported pictures bigger than this many pixels across are scaled down before they're sent to the others",
                    new AcceptableValueRange<int>(256, 2048)));
            Reach = Config.Bind("Decorator", "Reach", 8f,
                new ConfigDescription("How far away you can paint walls and hang pictures, in metres", new AcceptableValueRange<float>(2f, 30f)));

            Store.Init();
            new Harmony(Guid).PatchAll(typeof(DecoratorPlugin).Assembly);
            Logger.LogInfo($"{Name} {Version} loaded.");
        }

        void Update()
        {
            Run("Net", Net.Update);
            Run("Walls", Walls.Update);
            Run("Pictures", Pictures.Sync);
            Run("Tablet", Tablet.Update);
        }

        void OnGUI()
        {
            Run("Menu", Tablet.OnGUI);
        }

        // One part failing mustn't stop the others. Each error is logged once, so the log doesn't fill up every frame.
        static readonly System.Collections.Generic.HashSet<string> failed = new System.Collections.Generic.HashSet<string>();

        static void Run(string part, System.Action update)
        {
            try
            {
                update();
            }
            catch (System.Exception e)
            {
                if (failed.Add(part)) Log.LogError($"{part} failed: {e}");
            }
        }
    }
}
