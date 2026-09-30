using BepInEx;
using HarmonyLib;

namespace SMTAnyoneContinues
{
    [BepInPlugin(Guid, Name, Version)]
    public class AnyoneContinuesPlugin : BaseUnityPlugin
    {
        public const string Guid = "smt.installandmanager.anyonecontinues";
        public const string Name = "SMT Anyone Continues";
        public const string Version = "0.1.0";

        internal static BepInEx.Logging.ManualLogSource Log;

        void Awake()
        {
            Log = Logger;
            new Harmony(Guid).PatchAll(typeof(AnyoneContinuesPlugin).Assembly);
            Logger.LogInfo($"{Name} {Version} loaded.");
        }

        void Update() => Continue.Update();
    }
}
