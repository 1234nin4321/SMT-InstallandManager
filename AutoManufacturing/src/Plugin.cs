using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace SMTAutoManufacturing
{
    [BepInPlugin(Guid, Name, Version)]
    public class AutoManufacturingPlugin : BaseUnityPlugin
    {
        public const string Guid = "smt.installandmanager.automanufacturing";
        public const string Name = "SMT Auto Manufacturing";
        public const string Version = "0.1.1";

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> ReserveBoxes;
        internal static ConfigEntry<int> QueuePerMachine;
        internal static ConfigEntry<float> CheckInterval;
        internal static BepInEx.Logging.ManualLogSource Log;

        void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true,
                "Queue manufactured products automatically when the manufacturing shelves run low. Only the host's setting counts.");
            ReserveBoxes = Config.Bind("General", "Boxes in reserve", 1,
                new ConfigDescription("How many boxes of each product to keep on top of what fits on the manufacturing " +
                    "shelves. A new box is queued as soon as shelves, storage, boxes and the queue together hold less than that.",
                    new AcceptableValueRange<int>(0, 10)));
            QueuePerMachine = Config.Bind("General", "Queue per machine", 2,
                new ConfigDescription("How many products each manufacturing machine gets queued at most, not counting the " +
                    "one it's making. While a manufacturing desk has orders waiting, machines are only filled up to 2, " +
                    "so the desk can hand them over.", new AcceptableValueRange<int>(1, 5)));
            CheckInterval = Config.Bind("General", "Check interval", 10f,
                new ConfigDescription("Seconds between checks of what's needed.", new AcceptableValueRange<float>(5f, 120f)));

            new Harmony(Guid).PatchAll(typeof(AutoManufacturingPlugin).Assembly);
            Logger.LogInfo($"{Name} {Version} loaded.");
        }

        void Update() => Planner.Update();
    }
}
