using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace SMTCustomerImprovements
{
    enum FraudKind { Cash, Card }

    // Host side. When a customer at a register is about to pay, they sometimes pay with fake cash or a stolen card.
    // A player who checks the payment in time catches them: the customer grabs everything that was scanned and runs
    // off like any other thief. A payment nobody checks goes through, and the money is lost again at the end of the day.
    static class Fraud
    {
        // Customers paying with fake cash or a stolen card, until they pay or get caught
        static readonly Dictionary<GameObject, FraudKind> fraudsters = new Dictionary<GameObject, FraudKind>();

        // What each customer at a register had scanned, so a caught fraudster can run off with it
        static readonly Dictionary<GameObject, List<(int id, float price)>> scanned = new Dictionary<GameObject, List<(int, float)>>();

        // Waiting at a register for the player to take the payment
        const int PayingState = 5;
        // The game's own states: a thief roaming the store, and a customer heading out without paying
        const int ThiefRoamState = 11;
        const int LeaveState = 98;

        // Called on the host when a player asks to check the payment at a register
        public static void Check(Data_Container register)
        {
            var npc = register.currentNPC;
            if (npc == null) return;
            var customer = npc.GetComponent<NPC_Info>();
            if (customer == null || customer.state != PayingState || !customer.alreadyGaveMoney) return;

            if (fraudsters.TryGetValue(npc, out var kind))
            {
                fraudsters.Remove(npc);
                Catch(register, customer, kind, byEmployee: false);
            }
            else
            {
                Insult(register, customer);
            }
        }

        static void Catch(Data_Container register, NPC_Info customer, FraudKind kind, bool byEmployee)
        {
            float value = register.checkoutProductValue;
            ClearRegister(register);

            if (scanned.TryGetValue(customer.gameObject, out var items))
            {
                scanned.Remove(customer.gameObject);
                foreach (var (id, price) in items)
                {
                    customer.productsIDCarrying.Add(id);
                    customer.productsCarryingPrice.Add(price);
                }
            }
            // Scanned before the mod was watching: take what was put on the belt, at the average price
            if (customer.productsIDCarrying.Count == 0 && customer.productsIDInCheckout.Count > 0)
            {
                float each = value / customer.productsIDInCheckout.Count;
                foreach (int id in customer.productsIDInCheckout)
                {
                    customer.productsIDCarrying.Add(id);
                    customer.productsCarryingPrice.Add(each);
                }
            }

            RunLikeAThief(customer);

            string what = kind == FraudKind.Cash ? "Fake cash" : "Stolen credit card";
            string who = byEmployee ? "The cashier spotted it" : "Caught";
            Net.Announce($"{who}: {what}! The customer grabbed their shopping and is making a run for it. Stop the thief!");
        }

        // The same thing the game does when a thief heads for the exit
        static void RunLikeAThief(NPC_Info customer)
        {
            var manager = NPC_Manager.Instance;
            var agent = customer.GetComponent<NavMeshAgent>();
            agent.destination = manager.exitPoints.GetChild(Random.Range(0, manager.exitPoints.childCount)).position;
            agent.speed *= 1.25f;
            if (manager.offensiveNPCs) customer.RPCNotificationAboveHead("NPCmessage4", "");
            customer.isAThief = true;
            customer.RpcShowThief();
            customer.thiefFleeing = true;
            customer.thiefProductsNumber = customer.productsIDCarrying.Count;
            customer.StartWaitState(2f, ThiefRoamState);
            customer.state = -1;
        }

        // An honest customer who gets accused storms out without paying
        static void Insult(Data_Container register, NPC_Info customer)
        {
            bool card = Showing(register, "Payments/Payment_Card") || Showing(register, "CreditCardCanvas/Container");
            ClearRegister(register);
            scanned.Remove(customer.gameObject);

            Net.Say(customer, InsultedLines.Pick(card));
            customer.state = LeaveState;

            Net.Announce("That customer paid honestly! They stormed out insulted, and the sale is lost.");
        }

        static bool Showing(Data_Container register, string path)
        {
            var part = register.transform.Find(path);
            return part != null && part.gameObject.activeSelf;
        }

        // What the game does after a payment, without taking any money
        static void ClearRegister(Data_Container register)
        {
            register.checkoutQueue[0] = false;
            register.checkoutProductValue = 0f;
            register.NetworkproductsLeft = 0;
            register.internalProductListForEmployees.Clear();
            register.currentNPC = null;
            register.RpcClearCheckoutData();
        }

        static void ForgetGone<T>(Dictionary<GameObject, T> map)
        {
            List<GameObject> gone = null;
            foreach (var npc in map.Keys)
                if (npc == null) (gone ??= new List<GameObject>()).Add(npc);
            if (gone != null) foreach (var npc in gone) map.Remove(npc);
        }

        // Runs on the host for every product scanned at a register, by a player or an employee
        [HarmonyPatch(typeof(ProductCheckoutSpawn), "AddProductShared")]
        static class ScanPatch
        {
            static void Prefix(ProductCheckoutSpawn __instance)
            {
                var npc = __instance.NetworkNPCOBJ;
                if (npc == null) return;
                if (!scanned.TryGetValue(npc, out var items))
                {
                    ForgetGone(scanned);
                    scanned[npc] = items = new List<(int, float)>();
                }
                items.Add((__instance.productID, __instance.productCarryingPrice));
            }
        }

        // Runs on the host when a customer shows how they'll pay (0 = cash, 1 = card)
        [HarmonyPatch(typeof(Data_Container), nameof(Data_Container.RpcShowPaymentMethod))]
        static class PayPatch
        {
            static void Prefix(Data_Container __instance, int index)
            {
                if (!NetworkServer.active || !CustomerImprovementsPlugin.Enabled.Value) return;
                var npc = __instance.currentNPC;
                if (npc == null) return;
                var customer = npc.GetComponent<NPC_Info>();
                if (customer == null || customer.isAThief) return;
                if (Random.value >= CustomerImprovementsPlugin.FraudChance.Value) return;

                var kind = index == 0 ? FraudKind.Cash : FraudKind.Card;
                ForgetGone(fraudsters);
                fraudsters[npc] = kind;
                // Sent before the payment shows up, so it arrives first
                Net.SendHint(__instance, kind);
            }
        }

        // Runs on the host when a player or an employee takes the payment
        [HarmonyPatch(typeof(Data_Container), nameof(Data_Container.AuxReceivePayment))]
        static class ReceivePatch
        {
            static bool Prefix(Data_Container __instance, ref float returnDifference, bool applyEmployeeRate)
            {
                var npc = __instance.currentNPC;
                // A player finishing a payment for a customer who has already run off or stormed out
                if (npc == null) return __instance.checkoutProductValue != 0f;

                if (!fraudsters.TryGetValue(npc, out var kind))
                {
                    scanned.Remove(npc);
                    return true;
                }
                fraudsters.Remove(npc);

                if (applyEmployeeRate && Random.value < CustomerImprovementsPlugin.EmployeeCatchChance.Value)
                {
                    Catch(__instance, npc.GetComponent<NPC_Info>(), kind, byEmployee: true);
                    return false;
                }

                // Paid like any other purchase; the money goes again at the end of the day
                scanned.Remove(npc);
                float rate = applyEmployeeRate ? NPC_Manager.Instance.extraCheckoutMoney : 1f;
                DayLosses.Add(kind, Mathf.Round(__instance.checkoutProductValue * rate * 100f) / 100f - Mathf.Round(returnDifference * 100f) / 100f);
                return true;
            }
        }
    }
}
