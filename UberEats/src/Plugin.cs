using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace SMTUberEats
{
    public enum DeliveryMode
    {
        StorageThenFloor,
        FloorOnly,
        Vanilla
    }

    [BepInPlugin(Guid, Name, Version)]
    public class UberEatsPlugin : BaseUnityPlugin
    {
        public const string Guid = "smt.installandmanager.ubereats";
        public const string Name = "SMT UberEats";
        public const string Version = "0.3.1";

        internal static ConfigEntry<DeliveryMode> Mode;
        internal static ConfigEntry<int> StackHeight;
        internal static ConfigEntry<float> DeliveryDelay;
        internal static ConfigEntry<bool> AnnounceInChat;
        internal static BepInEx.Logging.ManualLogSource Log;

        void Awake()
        {
            Log = Logger;
            Mode = Config.Bind("General", "Mode", DeliveryMode.StorageThenFloor,
                "StorageThenFloor puts ordered boxes straight into free storage slots (the ones employees would use) " +
                "and stacks the rest by the delivery point. FloorOnly only stacks them. Vanilla drops them from the sky " +
                "like the unmodded game. Only the host's setting counts.");
            StackHeight = Config.Bind("General", "Stack height", 3,
                new ConfigDescription("How many boxes high the delivery stacks get.", new AcceptableValueRange<int>(1, 6)));
            DeliveryDelay = Config.Bind("General", "Delivery time", 10f,
                new ConfigDescription("Seconds between buying and the delivery arriving, counted down at the top of the " +
                    "screen. Anything ordered during the countdown comes with the same delivery. 0 delivers instantly.",
                    new AcceptableValueRange<float>(0f, 60f)));

            AnnounceInChat = Config.Bind("General", "Announce in chat", true,
                "The host also posts the countdown and arrival in the game chat, so other players see them. " +
                "Players who have this mod see them as the banner instead of chat lines.");

            new Harmony(Guid).PatchAll(typeof(UberEatsPlugin).Assembly);
            Logger.LogInfo($"{Name} {Version} loaded.");
        }

        void OnGUI() => DeliveryBanner.Draw();
    }

    [HarmonyPatch(typeof(ManagerBlackboard), "ServerCargoSpawner")]
    static class CargoSpawnerPatch
    {
        internal static bool LoadingSave;

        // The game only runs this coroutine on the host, so clients without the mod are unaffected
        static bool Prefix(ManagerBlackboard __instance, ref System.Collections.IEnumerator __result)
        {
            if (UberEatsPlugin.Mode.Value == DeliveryMode.Vanilla) return true;
            __result = Delivery.Run(__instance, countdown: !LoadingSave);
            return false;
        }
    }

    // Loading a save starts a delivery for whatever hadn't arrived when it was saved, even if that's nothing.
    // Those boxes were already waited for, so they skip the countdown.
    [HarmonyPatch(typeof(ManagerBlackboard), nameof(ManagerBlackboard.SpawnRemainingCargoFromAutosave))]
    static class AutosaveCargoPatch
    {
        static void Prefix() => CargoSpawnerPatch.LoadingSave = true;
        static void Finalizer() => CargoSpawnerPatch.LoadingSave = false;
    }
}
