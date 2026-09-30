using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace SMTCustomerImprovements
{
    // Now and then a customer who is high on weed shuffles in with the munchies. They walk a bit slower, buy nothing
    // but snacks, mumble about food and, once they get to a register, ask the cashier whether there's any weed behind
    // the checkout. An employee working that register answers them. A hazy green glow hangs around them.
    //
    // Snacks are found with the party animals' snack keywords.
    static class Stoners
    {
        class Stoner
        {
            public NPC_Info npc;
            public float nextMumble;
            // When they got to a register, and what happens there
            public Data_Container register;
            public float askAt, answerAt;
            public bool asked, answered;
        }

        const float RollEvery = 60f;
        const float TickEvery = 0.5f;
        const float WalkSpeed = 0.75f;

        static readonly List<Stoner> stoners = new List<Stoner>();
        static float nextRoll, nextTick;

        static readonly string[] Mumbles =
        {
            "Duuude... snacks.",
            "I'm sooo hungry, man.",
            "Whoa... look at all these colours.",
            "Do chips have feelings?",
            "What was I looking for again?",
            "Nachos. With extra nachos.",
            "This aisle is, like, sooo long.",
            "Have you ever really looked at a cookie?",
            "*giggles*",
            "Is it just me or is the floor moving?",
        };

        static readonly string[] Asks =
        {
            "Psst... you got any weed back there?",
            "So, uh... anything green behind the counter?",
            "Just these. And, like... any weed under the till?",
            "Hey man, you sell the good stuff back there?",
            "Any chance you've got some weed behind the checkout?",
            "Do you, like, have a secret menu? The green kind?",
        };

        static readonly string[] Answers =
        {
            "This is a supermarket.",
            "No. Just the snacks, please.",
            "Sir, that'll be the snacks only.",
            "We have oregano in aisle 3.",
            "I'm going to pretend I didn't hear that.",
            "Nope. Cash or card?",
        };

        public static bool IsStoner(NPC_Info npc)
        {
            foreach (var stoner in stoners) if (stoner.npc == npc) return true;
            return false;
        }

        public static void Update()
        {
            if (!NetworkServer.active) return;
            var manager = NPC_Manager.Instance;
            var game = GameData.Instance;
            if (manager == null || game == null) return;

            if (CustomerImprovementsPlugin.StonerNow.Value.IsDown()) Arrive(manager);
            else if (CustomerImprovementsPlugin.StonersEnabled.Value && Time.time >= nextRoll)
            {
                nextRoll = Time.time + RollEvery;
                if (Spawner.StoreOpen(manager, game) && Random.value < CustomerImprovementsPlugin.StonerChance.Value) Arrive(manager);
            }

            if (Time.time < nextTick) return;
            nextTick = Time.time + TickEvery;
            Tick(manager);
        }

        static void Arrive(NPC_Manager manager)
        {
            var snacks = ProductKeywords.Find("Stoner snacks", CustomerImprovementsPlugin.SnackKeywords.Value);
            if (snacks.Count == 0)
            {
                CustomerImprovementsPlugin.Log.LogInfo("No stoner: the store sells nothing that looks like snacks.");
                return;
            }

            int most = CustomerImprovementsPlugin.StonerSnacks.Value;
            int items = Random.Range(most / 2 + 1, most * 3 / 2 + 1);
            var shopping = new List<int>();
            for (int i = 0; i < items; i++) shopping.Add(snacks[Random.Range(0, snacks.Count)]);

            var npc = Spawner.Spawn(manager, Spawner.SpawnPoint(manager), Spawner.AnyModel(manager), shopping, thief: false);
            npc.GetComponent<NavMeshAgent>().speed *= WalkSpeed;
            stoners.Add(new Stoner { npc = npc, nextMumble = Time.time + Random.Range(5f, 15f) });
            Net.SendStoner(npc);
            Net.Say(npc, "Duuude... I've got the munchies.");
            Net.Announce("Someone with red eyes and the munchies just shuffled in.");
        }

        static void Tick(NPC_Manager manager)
        {
            for (int i = stoners.Count - 1; i >= 0; i--)
            {
                var stoner = stoners[i];
                if (stoner.npc == null)
                {
                    stoners.RemoveAt(i);
                    continue;
                }

                if (stoner.register == null) WatchRegisters(manager, stoner);
                else AtRegister(stoner);

                if (stoner.register == null && CustomerImprovementsPlugin.StonerMumbles.Value && Time.time >= stoner.nextMumble)
                {
                    stoner.nextMumble = Time.time + Random.Range(20f, 40f);
                    Net.Say(stoner.npc, Mumbles[Random.Range(0, Mumbles.Length)]);
                }
            }
        }

        // The register this stoner has got to, once they're the one being served there
        static void WatchRegisters(NPC_Manager manager, Stoner stoner)
        {
            foreach (Transform child in manager.checkoutOBJ.transform)
            {
                var register = child.GetComponent<Data_Container>();
                if (register == null || register.currentNPC != stoner.npc.gameObject) continue;
                stoner.register = register;
                stoner.askAt = Time.time + Random.Range(1f, 3f);
                return;
            }
        }

        static void AtRegister(Stoner stoner)
        {
            if (!stoner.asked && Time.time >= stoner.askAt)
            {
                stoner.asked = true;
                stoner.answerAt = Time.time + Random.Range(2.5f, 4f);
                Net.Say(stoner.npc, Asks[Random.Range(0, Asks.Length)]);
            }
            else if (stoner.asked && !stoner.answered && Time.time >= stoner.answerAt)
            {
                stoner.answered = true;
                // Only an employee answers; a player can answer however they like
                var cashier = Fraud.Cashier(stoner.register);
                if (cashier != null) Net.Say(cashier, Answers[Random.Range(0, Answers.Length)]);
            }
        }

        // Every player: the host says this customer is stoned
        public static void Received(string body)
        {
            if (uint.TryParse(body, out var netId) && CustomerImprovementsPlugin.StonerAura.Value) Aura.Request(netId, AuraStyle.Haze);
        }
    }
}
