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
            Combinables = Normalize(combinables);
        }

        public IEnumerable<int> Extras => Combinables == "999"
            ? Enumerable.Empty<int>()
            : Combinables.Split('-').Select(s => int.TryParse(s, out int id) ? id : 999).Where(id => id != 999);

        // A machine sorts the extras when a product is queued on it, but the manufacturing desk keeps them as typed
        static string Normalize(string combinables)
        {
            if (string.IsNullOrEmpty(combinables) || combinables == "999") return "999";
            var parts = combinables.Split('-');
            var ids = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                if (!int.TryParse(parts[i], out ids[i])) return combinables;
            Array.Sort(ids);
            return string.Join("-", ids);
        }

        public bool Equals(Variant other) => Product == other.Product && Combinables == other.Combinables;
        public override bool Equals(object obj) => obj is Variant other && Equals(other);
        public override int GetHashCode() => Product * 397 ^ Combinables.GetHashCode();
    }

    // Every few seconds on the host: works out which manufactured products are running low and queues them on
    // the manufacturing machines, as if someone pressed the button on the manufacturing desk. The game's own
    // manufacturing employees then fetch the ingredients, start the machine and restock the shelves.
    static class Planner
    {
        // The game's manufacturing desk only hands its queue to machines holding this many products or fewer
        const int DeskQueueLimit = 2;

        // Manufacturing employee job, and the part of it that fetches ingredients for a machine
        const int ManufacturingTask = 7;
        const int FetchingJob = 0;

        static readonly AccessTools.FieldRef<ManufacturingProduction, bool> Producing =
            AccessTools.FieldRefAccess<ManufacturingProduction, bool>("producing");

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

            // Kept at their index in the list, which is how employees refer to them
            var machines = npc.manufacturingProducersList
                .Select((o, index) => (machine: o != null ? o.GetComponent<ManufacturingProduction>() : null, index))
                .Where(m => m.machine != null)
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

            // Queued products' ingredients are spoken for. For the one an employee is fetching,
            // only what they haven't picked up yet is still in storage.
            var ingredients = CountIngredients(npc);
            var fetching = Fetching(npc);
            var queued = new List<Variant>();
            foreach (var (machine, index) in machines)
                for (int j = 0; j < machine.productQueue.Count && j < machine.combinableQueue.Count; j++)
                {
                    var variant = new Variant(machine.productQueue[j], machine.combinableQueue[j]);
                    queued.Add(variant);
                    if (j == 0 && machine.assignedToEmployee && !Producing(machine) && fetching.TryGetValue(index, out var employee))
                        foreach (string item in employee.manufacturedEmployeeProductsList)
                        {
                            if (int.TryParse(item.Split('|')[0], out int id)) ingredients.Use(id);
                        }
                    else
                        TakeIngredients(mbase, variant, ingredients, requireAll: false);
                }
            bool deskWaiting = false;
            foreach (var desk in npc.manufacturingDesksList.Where(o => o != null).Select(o => o.GetComponent<ManufacturingDesk>()))
                if (desk != null)
                    for (int j = 0; j < desk.toSendProductsQueue.Count && j < desk.toSendCombinableQueue.Count; j++)
                    {
                        var variant = new Variant(desk.toSendProductsQueue[j], desk.toSendCombinableQueue[j]);
                        queued.Add(variant);
                        TakeIngredients(mbase, variant, ingredients, requireAll: false);
                        deskWaiting = true;
                    }
            foreach (var variant in queued.Concat(InProgress.Variants))
                if (variant.Product >= 0 && variant.Product < mbase.productsData.Length)
                    Add(stock, variant, mbase.productsData[variant.Product].itemsPerBox);

            var missing = new HashSet<Variant>();
            var candidates = capacity.Keys
                .Where(v => v.Product < mbase.unlockedBaseProducts.Length && mbase.unlockedBaseProducts[v.Product])
                .ToList();
            // Leave room for what players ordered at a manufacturing desk, or the desk never gets to hand it over
            int queueLimit = deskWaiting
                ? Math.Min(AutoManufacturingPlugin.QueuePerMachine.Value, DeskQueueLimit)
                : AutoManufacturingPlugin.QueuePerMachine.Value;
            while (true)
            {
                var machine = machines
                    .Select(m => m.machine)
                    .Where(m => m.productQueue.Count < queueLimit)
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

        // Ingredients where manufacturing employees fetch them from: the store's storage first, then its shelves
        class Ingredients
        {
            public readonly Dictionary<int, int> All = new Dictionary<int, int>();
            public readonly Dictionary<int, int> Stored = new Dictionary<int, int>();

            public void Use(int id)
            {
                if (Get(All, id) > 0) All[id]--;
                if (Get(Stored, id) > 0) Stored[id]--;
            }
        }

        static Ingredients CountIngredients(NPC_Manager npc)
        {
            var counts = new Ingredients();
            foreach (var parent in new[] { npc.storageOBJ, npc.shelvesOBJ })
            {
                if (parent == null) continue;
                foreach (Transform child in parent.transform)
                {
                    var container = child.GetComponent<Data_Container>();
                    if (container == null) continue;
                    var info = container.productInfoArray;
                    for (int row = 0; row < info.Length / 2; row++)
                    {
                        if (info[row * 2] < 0 || info[row * 2 + 1] <= 0) continue;
                        Add(counts.All, info[row * 2], info[row * 2 + 1]);
                        if (parent == npc.storageOBJ) Add(counts.Stored, info[row * 2], info[row * 2 + 1]);
                    }
                }
            }
            return counts;
        }

        // Manufacturing employees fetching ingredients, by the index of the machine they're fetching for
        static Dictionary<int, NPC_Info> Fetching(NPC_Manager npc)
        {
            var fetching = new Dictionary<int, NPC_Info>();
            if (npc.employeeParentOBJ == null) return fetching;
            foreach (Transform employee in npc.employeeParentOBJ.transform)
            {
                var info = employee.GetComponent<NPC_Info>();
                if (info == null || info.taskPriority != ManufacturingTask || info.thiefProductsNumber != FetchingJob
                    || info.packagingAssignedOrderIndex < 0) continue;
                // One that finished at this machine earlier has nothing left to fetch; prefer the one still at it
                if (!fetching.TryGetValue(info.packagingAssignedOrderIndex, out var other) || other.manufacturedEmployeeProductsList.Count == 0)
                    fetching[info.packagingAssignedOrderIndex] = info;
            }
            return fetching;
        }

        // A recipe needs one item from each of its slots plus one of each extra. Like the employees, this takes
        // the first alternative in a slot that's in stock. A missing base item is skipped by the employee, but
        // they wait for a missing extra until it turns up, so extras only count when they're in storage, where
        // customers can't buy them first.
        static bool TakeIngredients(ManufacturingBase mbase, Variant variant, Ingredients available, bool requireAll)
        {
            if (variant.Product < 0) return false;
            var picks = new List<int>();
            foreach (var slot in mbase.RetrieveBaseRecipe(variant.Product).Split('|'))
            {
                var ids = slot.Split('-').Select(s => int.TryParse(s, out int id) ? id : -1).Where(id => id >= 0).ToList();
                if (ids.Count == 0) continue;
                int inStock = ids.FindIndex(id => Get(available.All, id) > 0);
                picks.Add(ids[Math.Max(0, inStock)]);
            }
            var extras = variant.Extras.ToList();

            if (requireAll && (picks.Concat(extras).GroupBy(id => id).Any(g => Get(available.All, g.Key) < g.Count())
                || extras.GroupBy(id => id).Any(g => Get(available.Stored, g.Key) < g.Count())))
                return false;
            foreach (int id in picks.Concat(extras))
                available.Use(id);
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
