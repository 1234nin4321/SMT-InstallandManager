using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace SMTCustomerImprovements
{
    // Host: brings in a customer the way the game does, with a chosen model and shopping list. From then on the game
    // runs them like any other customer: they shop, queue, pay (or steal) and leave.
    static class Spawner
    {
        // The game treats the first 53 customer models as men when it names the customers who send in orders
        public const int MaleModels = 53;

        static readonly MethodInfo SetAgentData = AccessTools.Method(typeof(NPC_Manager), "SetAgentData");
        static readonly MethodInfo GenerateCompensatedList = AccessTools.Method(typeof(NPC_Manager), "GenerateCompensatedList");

        public static bool StoreOpen(NPC_Manager manager, GameData game) =>
            game.isSupermarketOpen && game.timeOfDay > 8f && game.timeOfDay < 21f
            && manager.shelvesOBJ.transform.childCount > 0 && manager.checkoutOBJ.transform.childCount > 0;

        public static Vector3 SpawnPoint(NPC_Manager manager) =>
            manager.spawnPointsOBJ.transform.GetChild(Random.Range(0, manager.spawnPointsOBJ.transform.childCount - 1)).position;

        public static int AnyModel(NPC_Manager manager) => Random.Range(0, manager.NPCsArray.Length - 1);

        public static int MaleModel(NPC_Manager manager) => Random.Range(0, Mathf.Min(MaleModels, manager.NPCsArray.Length - 1));

        // What the game would give a new customer with this model to buy
        public static List<int> UsualShopping(NPC_Manager manager, int model) =>
            (List<int>)GenerateCompensatedList.Invoke(manager, new object[] { model });

        public static NPC_Info Spawn(NPC_Manager manager, Vector3 position, int model, List<int> shopping, bool thief)
        {
            var obj = Object.Instantiate(manager.npcAgentPrefab, position, Quaternion.identity);
            obj.transform.SetParent(manager.customersnpcParentOBJ.transform);
            var customer = obj.GetComponent<NPC_Info>();
            customer.NetworkNPCID = model;
            customer.NetworkisCustomer = true;
            customer.isAThief = thief;
            customer.productItemPlaceWait = Mathf.Clamp(0.5f - GameData.Instance.gameDay * 0.003f, 0.1f, 0.5f);
            NetworkServer.Spawn(obj);

            customer.productsIDToBuy = shopping;
            customer.productsIDToBuy.Sort();
            if (Random.value < 0.5f) customer.productsIDToBuy.Reverse();

            var agent = obj.GetComponent<NavMeshAgent>();
            SetAgentData.Invoke(manager, new object[] { agent });
            var shelf = manager.shelvesOBJ.transform.GetChild(Random.Range(0, manager.shelvesOBJ.transform.childCount));
            agent.destination = shelf.GetComponent<Data_Container>().standSpot.position;
            return customer;
        }
    }
}
