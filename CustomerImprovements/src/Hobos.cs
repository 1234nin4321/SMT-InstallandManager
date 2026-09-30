using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SMTCustomerImprovements
{
    // Now and then a hobo wanders in. They buy a few things like any customer, but leave a trail of garbage behind
    // them (the game's own trash, so cleaners pick it up) and smell: customers who get close complain, which counts
    // towards the day's complaints about filth. Hobos are more likely to steal, or to pay with fake cash or a stolen
    // card, and a murky green and brown cloud hangs around them.
    static class Hobos
    {
        class Hobo
        {
            public NPC_Info npc;
            public float nextTrash;
            public int trashDropped;
            public float nextMumble;
        }

        const float RollEvery = 60f;
        const float TickEvery = 0.5f;
        const float SmellRadius = 3f;
        // Chance each half second that a customer standing in the smell complains about it
        const float ComplainChance = 0.15f;
        // The game's trash folder under the level props
        const int TrashFolder = 6;

        static readonly List<Hobo> hobos = new List<Hobo>();
        static float nextRoll, nextTick;

        static readonly string[] Mumbles =
        {
            "Spare some change?",
            "Nice store you got here.",
            "*cough* *cough*",
            "Is this food free?",
            "I used to be a banker, you know.",
            "They don't make shopping carts like they used to.",
            "Anyone want a half-eaten sandwich?",
            "I'm not homeless, I'm house-free!",
            "Samples! Where are the free samples?",
            "*scratches*",
        };

        static readonly string[] Complaints =
        {
            "Ugh! What is that smell?!",
            "Did something die in here?",
            "Oh god, my eyes are watering!",
            "Somebody call a cleaner!",
            "I can taste the smell...",
            "Is it the cheese aisle? It's not the cheese aisle.",
            "This store stinks! Literally!",
            "*gags*",
        };

        public static bool IsHobo(NPC_Info npc)
        {
            foreach (var hobo in hobos) if (hobo.npc == npc) return true;
            return false;
        }

        public static void Update()
        {
            if (!NetworkServer.active) return;
            var manager = NPC_Manager.Instance;
            var game = GameData.Instance;
            if (manager == null || game == null) return;

            if (CustomerImprovementsPlugin.HoboNow.Value.IsDown()) Arrive(manager);
            else if (CustomerImprovementsPlugin.HobosEnabled.Value && Time.time >= nextRoll)
            {
                nextRoll = Time.time + RollEvery;
                if (Spawner.StoreOpen(manager, game) && Random.value < CustomerImprovementsPlugin.HoboChance.Value) Arrive(manager);
            }

            if (Time.time < nextTick) return;
            nextTick = Time.time + TickEvery;
            Tick(manager, game);
        }

        static void Arrive(NPC_Manager manager)
        {
            int model = Spawner.AnyModel(manager);
            var shopping = Spawner.UsualShopping(manager, model);
            int keep = Random.Range(2, 6);
            if (shopping.Count > keep) shopping.RemoveRange(keep, shopping.Count - keep);
            if (shopping.Count == 0)
            {
                CustomerImprovementsPlugin.Log.LogInfo("No hobo: the store gave them nothing to buy.");
                return;
            }

            bool thief = Random.value < CustomerImprovementsPlugin.HoboStealChance.Value;
            var npc = Spawner.Spawn(manager, Spawner.SpawnPoint(manager), model, shopping, thief);
            hobos.Add(new Hobo
            {
                npc = npc,
                nextTrash = Time.time + TrashInterval(),
                nextMumble = Time.time + Random.Range(10f, 25f),
            });
            Net.SendHobo(npc);
            CustomerImprovementsPlugin.Log.LogInfo($"A hobo wandered in ({(thief ? "thief" : "shopper")}, {shopping.Count} items).");
            Net.Announce("Something smells... a hobo just wandered in.");
        }

        static float TrashInterval() => CustomerImprovementsPlugin.HoboTrashEvery.Value * Random.Range(0.6f, 1.4f);

        static void Tick(NPC_Manager manager, GameData game)
        {
            for (int i = hobos.Count - 1; i >= 0; i--)
            {
                var hobo = hobos[i];
                if (hobo.npc == null)
                {
                    hobos.RemoveAt(i);
                    continue;
                }
                var position = hobo.npc.transform.position;

                if (Time.time >= hobo.nextTrash && hobo.trashDropped < CustomerImprovementsPlugin.HoboMostTrash.Value
                    && DropTrash(game, position - hobo.npc.transform.forward * 0.5f))
                {
                    hobo.trashDropped++;
                    hobo.nextTrash = Time.time + TrashInterval();
                }

                if (CustomerImprovementsPlugin.HoboSmellComplaints.Value) Smell(manager, game, hobo.npc);

                if (CustomerImprovementsPlugin.HoboMumbles.Value && Time.time >= hobo.nextMumble)
                {
                    hobo.nextMumble = Time.time + Random.Range(25f, 50f);
                    Net.Say(hobo.npc, Mumbles[Random.Range(0, Mumbles.Length)]);
                }
            }
        }

        // The game's own trash, dropped where the game would allow it: only on the store's floor
        static bool DropTrash(GameData game, Vector3 position)
        {
            if (!Physics.Raycast(position + Vector3.up, Vector3.down, out var hit, 3f, game.lMask, QueryTriggerInteraction.Ignore)
                || !hit.transform.gameObject.CompareTag("Buildable"))
                return false;

            var folder = game.GetComponent<NetworkSpawner>().levelPropsOBJ.transform.GetChild(TrashFolder);
            var obj = Object.Instantiate(game.trashSpawnPrefab, folder);
            obj.transform.position = hit.point;
            obj.GetComponent<TrashSpawn>().NetworktrashID = Random.Range(0, 5);
            // The trash's own behaviour, which the game switches on the same way
            if (obj.GetComponent("PlayMakerFSM") is Behaviour fsm) fsm.enabled = true;
            NetworkServer.Spawn(obj);
            return true;
        }

        // Customers near a hobo complain about the smell, once each, like they do about filth
        static void Smell(NPC_Manager manager, GameData game, NPC_Info hobo)
        {
            foreach (Transform other in manager.customersnpcParentOBJ.transform)
            {
                var customer = other.GetComponent<NPC_Info>();
                if (customer == null || customer == hobo || customer.hasComplainedAboutFilth || customer.thiefFleeing || IsHobo(customer)) continue;
                if (Vector3.Distance(other.position, hobo.transform.position) > SmellRadius) continue;
                if (Random.value >= ComplainChance) continue;
                customer.hasComplainedAboutFilth = true;
                game.complainedAboutFilth++;
                Net.Say(customer, Complaints[Random.Range(0, Complaints.Length)]);
            }
        }

        // Every player: the host says this customer is a hobo
        public static void Received(string body)
        {
            if (uint.TryParse(body, out var netId) && CustomerImprovementsPlugin.HoboAura.Value) Aura.Request(netId, AuraStyle.Stink);
        }
    }
}
