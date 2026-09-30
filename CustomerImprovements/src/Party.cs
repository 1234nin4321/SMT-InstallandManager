using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SMTCustomerImprovements
{
    // Now and then a group of at least five men comes in for a party. They are ordinary customers who walk in
    // together, glow with a colour-cycling aura and buy nothing but alcohol and snacks, lots of it.
    static class Party
    {
        class Group
        {
            public readonly List<NPC_Info> members = new List<NPC_Info>();
            public float nextShout;
        }

        const float RollEvery = 60f;

        static readonly List<Group> groups = new List<Group>();
        static readonly HashSet<NPC_Info> partyGoers = new HashSet<NPC_Info>();
        static float nextRoll;

        static readonly string[] Shouts =
        {
            "PARTY TIME!",
            "WOOOOO!",
            "Grab the beer, boys!",
            "Who's got the chips?",
            "Best. Night. Ever.",
            "Did someone say nachos?",
            "More ice! We need more ice!",
            "Is this enough beer? No. Never.",
            "Let's gooooo!",
            "Dave, put the kale back!",
            "Party at Steve's place!",
            "Snacks! SNACKS!",
            "Cheers, mate!",
            "Tonight's gonna be legendary!",
        };

        public static bool IsPartyGoer(NPC_Info npc) => partyGoers.Contains(npc);

        public static void Update()
        {
            if (!NetworkServer.active) return;

            var manager = NPC_Manager.Instance;
            var game = GameData.Instance;
            if (manager == null || game == null) return;

            if (CustomerImprovementsPlugin.PartyNow.Value.IsDown()) StartParty(manager);
            else if (CustomerImprovementsPlugin.PartiesEnabled.Value && Time.time >= nextRoll)
            {
                nextRoll = Time.time + RollEvery;
                if (Spawner.StoreOpen(manager, game) && Random.value < CustomerImprovementsPlugin.PartyChance.Value) StartParty(manager);
            }
            Shout();
        }

        // Host: five or more party-goers walk in together from the same spot
        static void StartParty(NPC_Manager manager)
        {
            var products = PartyProducts();
            if (products.Count == 0)
            {
                CustomerImprovementsPlugin.Log.LogInfo("No party: the store sells nothing that looks like alcohol or snacks.");
                return;
            }

            int size = Random.Range(CustomerImprovementsPlugin.PartyMinSize.Value,
                Mathf.Max(CustomerImprovementsPlugin.PartyMinSize.Value, CustomerImprovementsPlugin.PartyMaxSize.Value) + 1);
            var spawn = Spawner.SpawnPoint(manager);
            var group = new Group { nextShout = Time.time + Random.Range(15f, 30f) };

            for (int i = 0; i < size; i++)
            {
                var member = SpawnMember(manager, spawn + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f)), products);
                group.members.Add(member);
                partyGoers.Add(member);
                Net.SendParty(member);
            }
            groups.Add(group);
            Net.Say(group.members[0], "WOOOOO! PARTY!");
            Net.Announce($"A party of {size} just walked in. Hope you stocked up on beer and snacks!");
        }

        static NPC_Info SpawnMember(NPC_Manager manager, Vector3 position, List<int> products)
        {
            int items = Random.Range(CustomerImprovementsPlugin.PartyItemsEach.Value / 2 + 1, CustomerImprovementsPlugin.PartyItemsEach.Value * 3 / 2 + 1);
            var shopping = new List<int>();
            for (int i = 0; i < items; i++) shopping.Add(products[Random.Range(0, products.Count)]);
            return Spawner.Spawn(manager, position, Spawner.MaleModel(manager), shopping, thief: false);
        }

        // Products the store sells whose name or model says alcohol or snack
        static List<int> PartyProducts() => ProductKeywords.Find("Party products",
            CustomerImprovementsPlugin.AlcoholKeywords.Value, CustomerImprovementsPlugin.SnackKeywords.Value);

        // Host: now and then someone in the group shouts something
        static void Shout()
        {
            for (int g = groups.Count - 1; g >= 0; g--)
            {
                var group = groups[g];
                group.members.RemoveAll(member => member == null);
                if (group.members.Count == 0)
                {
                    groups.RemoveAt(g);
                    partyGoers.RemoveWhere(member => member == null);
                    continue;
                }
                if (Time.time < group.nextShout || !CustomerImprovementsPlugin.PartyShouts.Value) continue;
                group.nextShout = Time.time + Random.Range(15f, 35f);
                Net.Say(group.members[Random.Range(0, group.members.Count)], Shouts[Random.Range(0, Shouts.Length)]);
            }
        }

        // Every player: the host says this customer is a party-goer
        public static void Received(string body)
        {
            if (uint.TryParse(body, out var netId) && CustomerImprovementsPlugin.PartyAura.Value) Aura.Request(netId, AuraStyle.Party);
        }
    }
}
