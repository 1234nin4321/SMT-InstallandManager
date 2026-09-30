using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;
using SMTModBrowser.Core;

namespace SMTModBrowser.Preloader
{
    /// <summary>
    /// Runs before any plugin is loaded, so it can replace or delete mod DLLs that were
    /// locked when the player updated or removed them in game.
    /// </summary>
    public static class Preloader
    {
        // BepInEx requires these two members on every patcher; we don't patch any assemblies
        public static IEnumerable<string> TargetDLLs { get; } = new string[0];
        public static void Patch(AssemblyDefinition assembly) { }

        public static void Initialize()
        {
            var log = Logger.CreateLogSource("SMTModBrowser.Preloader");
            PendingOps.Apply(Paths.BepInExRootPath, log.LogInfo);
        }
    }
}
