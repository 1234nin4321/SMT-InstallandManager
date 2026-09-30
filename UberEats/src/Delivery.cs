using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace SMTUberEats
{
    // Replaces ManagerBlackboard.ServerCargoSpawner, which drops one box every 0.5-1 s at a random spot
    static class Delivery
    {
        static readonly AccessTools.FieldRef<ManagerBlackboard, bool> IsSpawning =
            AccessTools.FieldRefAccess<ManagerBlackboard, bool>("isSpawning");
        static readonly Func<ManagerBlackboard, int, float> PricePerBox =
            AccessTools.MethodDelegate<Func<ManagerBlackboard, int, float>>(
                AccessTools.Method(typeof(ManagerBlackboard), "PricePerBoxRetrieve"));
        // The same slot search employees use: labelled slots for the product first, then the nearest empty ones
        static readonly Func<NPC_Manager, int, int> FreeStorageContainer =
            AccessTools.MethodDelegate<Func<NPC_Manager, int, int>>(
                AccessTools.Method(typeof(NPC_Manager), "GetFreeStorageContainer"));
        static readonly Func<NPC_Manager, int, int, int> FreeStorageRow =
            AccessTools.MethodDelegate<Func<NPC_Manager, int, int, int>>(
                AccessTools.Method(typeof(NPC_Manager), "GetFreeStorageRow"));

        // Short pause between floor boxes so each one's collider is in place before the next is stacked on it
        static readonly WaitForSeconds FloorInterval = new WaitForSeconds(0.05f);
        const float OrderSettleTime = 0.3f;

        public static IEnumerator Run(ManagerBlackboard board, bool countdown)
        {
            if (board.idsToSpawn.Count == 0) yield break;
            IsSpawning(board) = true;

            float delay = UberEatsPlugin.DeliveryDelay.Value;
            if (countdown && delay > 0f)
            {
                DeliveryBanner.StartCountdown(board, delay);
                float arrivesAt = Time.time + delay;
                // A purchase reaches the host as one message per box and this starts on the first one,
                // so wait for the whole order to land before telling the other players its size
                int lastCount = -1;
                float countSteadySince = Time.time;
                bool announced = false;
                while (Time.time < arrivesAt)
                {
                    if (!announced)
                    {
                        int count = board.idsToSpawn.Count;
                        if (count != lastCount)
                        {
                            lastCount = count;
                            countSteadySince = Time.time;
                        }
                        else if (Time.time - countSteadySince >= OrderSettleTime)
                        {
                            DeliveryChat.AnnounceCountdown(Mathf.CeilToInt(arrivesAt - Time.time), count);
                            announced = true;
                        }
                    }
                    yield return null;
                }
            }

            bool useStorage = UberEatsPlugin.Mode.Value == DeliveryMode.StorageThenFloor;
            FloorStacker floor = null;
            int stored = 0, stacked = 0;

            // Orders placed while this runs are added to idsToSpawn and delivered in the same run
            while (board.idsToSpawn.Count > 0)
            {
                int productID = board.idsToSpawn[0];
                bool onFloor = false;
                try
                {
                    int count = ProductListing.Instance.productsData[productID].maxItemsPerBox;
                    RecordPurchase(board, productID, count);
                    if (useStorage && TryStore(productID, count))
                    {
                        stored++;
                    }
                    else
                    {
                        floor ??= new FloorStacker(board, UberEatsPlugin.StackHeight.Value);
                        floor.Spawn(productID, count);
                        stacked++;
                        onFloor = true;
                    }
                }
                catch (Exception e)
                {
                    UberEatsPlugin.Log.LogError($"Delivering product {productID} failed, dropping it the normal way: {e}");
                    DropLikeVanilla(board, productID);
                    onFloor = true;
                }
                board.idsToSpawn.RemoveAt(0);
                if (onFloor) yield return FloorInterval;
            }

            if (stored + stacked > 0)
            {
                string summary = DeliveryBanner.Summary(stored, stacked);
                DeliveryBanner.ShowArrived(summary);
                DeliveryChat.AnnounceArrival(summary);
            }
            UberEatsPlugin.Log.LogInfo($"Delivered {stored + stacked} boxes: {stored} into storage, {stacked} by the delivery point.");
            yield return new WaitForSeconds(0.2f);
            IsSpawning(board) = false;
        }

        // Keeps the game's purchase statistics the same as a normal delivery
        static void RecordPurchase(ManagerBlackboard board, int productID, int count)
        {
            var stats = StatisticsManager.Instance;
            if (productID >= stats.productsAcquired.Count) return;
            stats.productsAcquired[productID] += count;
            stats.costPerProductAcquired[productID] += (int)PricePerBox(board, productID);
        }

        static bool TryStore(int productID, int count)
        {
            var npc = NPC_Manager.Instance;
            if (npc == null || npc.storageOBJ == null) return false;
            int container = FreeStorageContainer(npc, productID);
            if (container < 0) return false;
            int row = FreeStorageRow(npc, container, productID);
            if (row < 0) return false;
            // Sets the slot on the host and sends it to every client, like an employee storing a box
            npc.storageOBJ.transform.GetChild(container).GetComponent<Data_Container>()
                .EmployeeUpdateArrayValuesStorage(row * 2, productID, count);
            return true;
        }

        static void DropLikeVanilla(ManagerBlackboard board, int productID)
        {
            try
            {
                var offset = new Vector3(UnityEngine.Random.Range(-2f, 2f), 0f, UnityEngine.Random.Range(-2f, 2f));
                board.SpawnBoxFromEmployee(board.merchandiseSpawnpoint.transform.position + offset, productID,
                    ProductListing.Instance.productsData[productID].maxItemsPerBox);
            }
            catch (Exception e)
            {
                UberEatsPlugin.Log.LogError($"Could not drop product {productID}: {e}");
            }
        }
    }
}
