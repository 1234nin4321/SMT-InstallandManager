using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace SMTAutoManufacturing
{
    // A machine takes its product off the queue when it starts making it, so for the 4 minutes it takes the
    // product is in neither the queue nor a box. This keeps track of it so it isn't queued a second time.
    static class InProgress
    {
        class Batch
        {
            public Variant Variant;
            public float FinishedAt = float.MaxValue;
        }

        // The finished box is spawned through a command that the host handles a frame later, so the batch
        // is still counted for a few seconds after it's done
        const float BoxSpawnGrace = 5f;

        static readonly Dictionary<ManufacturingProduction, Batch> batches = new Dictionary<ManufacturingProduction, Batch>();

        public static IEnumerable<Variant> Variants
        {
            get
            {
                foreach (var machine in batches.Keys.ToList())
                {
                    if (machine == null || Time.time > batches[machine].FinishedAt + BoxSpawnGrace)
                        batches.Remove(machine);
                }
                return batches.Values.Select(b => b.Variant).ToList();
            }
        }

        // Only called on the host, from the machine's Produce coroutine, just before the queue is shortened
        [HarmonyPatch(typeof(ManufacturingProduction), "RpcProduceStart")]
        static class StartPatch
        {
            static void Prefix(ManufacturingProduction __instance)
            {
                if (!__instance.isServer || __instance.productQueue.Count == 0 || __instance.combinableQueue.Count == 0) return;
                batches[__instance] = new Batch { Variant = new Variant(__instance.productQueue[0], __instance.combinableQueue[0]) };
            }
        }

        [HarmonyPatch(typeof(ManufacturingProduction), "RpcProduceFinish")]
        static class FinishPatch
        {
            static void Prefix(ManufacturingProduction __instance)
            {
                if (batches.TryGetValue(__instance, out var batch)) batch.FinishedAt = Time.time;
            }
        }
    }
}
