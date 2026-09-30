using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SMTAutoManufacturing
{
    // A manufactured product: the base product plus the extra ingredients it was made with.
    // The game writes "999" for no extras; a player-typed queue entry can also have "".
    readonly struct Variant : IEquatable<Variant>
    {
        public readonly int Product;
        public readonly string Combinables;

        public Variant(int product, string combinables)
        {
            Product = product;
            Combinables = string.IsNullOrEmpty(combinables) ? "999" : combinables;
        }

        public IEnumerable<int> Extras => Combinables == "999"
            ? Enumerable.Empty<int>()
            : Combinables.Split('-').Select(int.Parse);

        public bool Equals(Variant other) => Product == other.Product && Combinables == other.Combinables;
        public override bool Equals(object obj) => obj is Variant other && Equals(other);
        public override int GetHashCode() => Product * 397 ^ Combinables.GetHashCode();
    }

    // Every few seconds on the host: works out which manufactured products are running low and queues them on
    // the manufacturing machines, as if someone pressed the button on the manufacturing desk. The game's own
    // manufacturing employees then fetch the ingredients, start the machine and restock the shelves.
    static class Planner
    {
        static readonly Action<ManufacturingProduction, int, string> AddToQueue =
            AccessTools.MethodDelegate<Action<ManufacturingProduction, int, string>>(
                AccessTools.Method(typeof(ManufacturingProduction), "UserCode_CmdAddToProductionQueue__Int32__String"));

        static float nextCheck;
        static HashSet<Variant> lastMissing = new HashSet<Variant>();

        public static void Update()
        {
            if (!AutoManufacturingPlugin.Enabled.Value || !NetworkServer.active || Time.time < nextCheck) return;
            nextCheck = Time.time + AutoManufacturingPlugin.CheckInterval.Value;
            try
            {
                Plan();
            }
            catch (Exception e)
            {
                AutoManufacturingPlugin.Log.LogWarning($"Could not plan manufacturing: {e}");
            }
        }

        static void Plan()
        {
            var npc = NPC_Manager.Instance;
            var mbase = ManufacturingBase.Instance;
            if (npc == null || mbase == null || npc.manufacturingShelvesOBJ == null) return;

            var machines = npc.manufacturingProducersList
                .Where(o => o != null)
                .Select(o => o.GetComponent<ManufacturingProduction>())
                .Where(m => m != null)
                .ToList();
            if (machines.Count == 0) return;

            // What the manufacturing shelves hold when full, per product. A row keeps its product when it sells out.
            var capacity = new Dictionary<Variant, int>();
            var stock = new Dictionary<Variant, int>();
            var shelves = npc.manufacturingShelvesOBJ.transform;
            for (int i = 0; i < shelves.childCount; i++)
            {
                var shelf = shelves.GetChild(i).GetComponent<ManufacturingContainer>();
                if (shelf == null) continue;
                for (int row = 0; row < shelf.productInfoArray.Length / 2; row++)
                {
                    int product = shelf.productInfoArray[row * 2];
                    if (product < 0 || product >= mbase.productsData.Length) continue;
                    var variant = new Variant(product, shelf.combinableInfoArray[row]);
                    Add(capacity, variant, npc.GetMaxManufacturingProductsPerRow(i, product));
                    Add(stock, variant, Math.Max(0, shelf.productInfoArray[row * 2 + 1]));
                }
            }
            if (capacity.Count == 0) return;

            // Everything else that's already there or on its way
            var storage = npc.manufacturingStorageShelvesOBJ.transform;
            for (int i = 0; i < storage.childCount; i++)
            {
                var shelf = storage.GetChild(i).GetComponent<ManufacturingContainer>();
                if (shelf == null) continue;
                for (int row = 0; row < shelf.productInfoArray.Length / 2; row++)
                {
                    int product = shelf.productInfoArray[row * 2];
                    int count = shelf.productInfoArray[row * 2 + 1];
                    if (product >= 0 && count > 0) Add(stock, new Variant(product, shelf.combinableInfoArray[row]), count);
                }
            }
            foreach (Transform box in npc.manufacturingBoxesOBJ.transform)
            {
                var data = box.GetComponent<ManufacturingBoxData>();
                if (data != null && data.numberOfProducts > 0)
                    Add(stock, new Variant(data.manufacturedProductIndex, data.combinablesData), data.numberOfProducts);
            }

            var ingredients = CountIngredients(npc);
            var queued = new List<Variant>();
            foreach (var machine in machines)
                for (int j = 0; j < machine.productQueue.Count && j < machine.combinableQueue.Count; j++)
                    queued.Add(new Variant(machine.productQueue[j], machine.combinableQueue[j]));
            foreach (var desk in npc.manufacturingDesksList.Where(o => o != null).Select(o => o.GetComponent<ManufacturingDesk>()))
                if (desk != null)
                    for (int j = 0; j < desk.toSendProductsQueue.Count && j < desk.toSendCombinableQueue.Count; j++)
                        queued.Add(new Variant(desk.toSendProductsQueue[j], desk.toSendCombinableQueue[j]));
            // Their ingredients are spoken for, unless an employee already took them
            foreach (var variant in queued)
                TakeIngredients(mbase, variant, ingredients, requireAll: false);
            foreach (var variant in queued.Concat(InProgress.Variants))
                if (variant.Product >= 0 && variant.Product < mbase.productsData.Length)
                    Add(stock, variant, mbase.productsData[variant.Product].itemsPerBox);

            var missing = new HashSet<Variant>();
            var candidates = capacity.Keys
                .Where(v => v.Product < mbase.unlockedBaseProducts.Length && mbase.unlockedBaseProducts[v.Product])
                .ToList();
            while (true)
            {
                var machine = machines
                    .Where(m => m.productQueue.Count < AutoManufacturingPlugin.QueuePerMachine.Value)
                    .OrderBy(m => m.productQueue.Count)
                    .FirstOrDefault();
                if (machine == null) break;

                // The emptiest product first
                Variant? next = null;
                float lowestFill = 1f;
                foreach (var variant in candidates)
                {
                    float fill = (float)Get(stock, variant) / Target(mbase, capacity, variant);
                    if (fill < lowestFill && !missing.Contains(variant))
                    {
                        lowestFill = fill;
                        next = variant;
                    }
                }
                if (next == null) break;

                var chosen = next.Value;
                if (!TakeIngredients(mbase, chosen, ingredients, requireAll: true))
                {
                    missing.Add(chosen);
                    continue;
                }
                AddToQueue(machine, chosen.Product, chosen.Combinables);
                Add(stock, chosen, mbase.productsData[chosen.Product].itemsPerBox);
                AutoManufacturingPlugin.Log.LogInfo($"Queued {Describe(chosen)} ({lowestFill:P0} stocked)");
            }

            foreach (var variant in missing.Except(lastMissing))
                AutoManufacturingPlugin.Log.LogInfo($"Can't make {Describe(variant)}: ingredients are out of stock");
            lastMissing = missing;
        }

        static int Target(ManufacturingBase mbase, Dictionary<Variant, int> capacity, Variant variant) =>
            Math.Max(1, capacity[variant] + AutoManufacturingPlugin.ReserveBoxes.Value * mbase.productsData[variant.Product].itemsPerBox);

        // Ingredients sitting in the store's storage and on its shelves, where manufacturing employees fetch them from
        static Dictionary<int, int> CountIngredients(NPC_Manager npc)
        {
            var counts = new Dictionary<int, int>();
            foreach (var parent in new[] { npc.storageOBJ, npc.shelvesOBJ })
            {
                if (parent == null) continue;
                foreach (Transform child in parent.transform)
                {
                    var container = child.GetComponent<Data_Container>();
                    if (container == null) continue;
                    var info = container.productInfoArray;
                    for (int row = 0; row < info.Length / 2; row++)
                        if (info[row * 2] >= 0 && info[row * 2 + 1] > 0) Add(counts, info[row * 2], info[row * 2 + 1]);
                }
            }
            return counts;
        }

        // A recipe needs one item from each of its slots (any of the alternatives in a slot will do) plus one of each extra
        static bool TakeIngredients(ManufacturingBase mbase, Variant variant, Dictionary<int, int> available, bool requireAll)
        {
            if (variant.Product < 0) return false;
            var picks = new List<int>();
            foreach (var slot in mbase.RetrieveBaseRecipe(variant.Product).Split('|'))
            {
                if (slot == "") continue;
                int best = slot.Split('-').Select(int.Parse).OrderByDescending(id => Get(available, id)).First();
                picks.Add(best);
            }
            picks.AddRange(variant.Extras.Where(id => id != 999));

            if (requireAll && picks.Any(id => Get(available, id) <= 0)) return false;
            foreach (int id in picks)
                if (Get(available, id) > 0) available[id]--;
            return true;
        }

        static string Describe(Variant variant)
        {
            string name = LocalizationManager.instance.GetLocalizationString("mfactureproduct" + variant.Product);
            int extras = variant.Extras.Count();
            return extras > 0 ? $"{name} +{extras}" : name;
        }

        static void Add<T>(Dictionary<T, int> counts, T key, int amount) => counts[key] = Get(counts, key) + amount;
        static int Get<T>(Dictionary<T, int> counts, T key) => counts.TryGetValue(key, out int n) ? n : 0;
    }
}
