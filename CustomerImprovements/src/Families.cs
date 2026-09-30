using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace SMTCustomerImprovements
{
    // Some customers come in with their children. The game has no child models, so a child is one of the customer
    // models at a smaller size. Children are NPCs of their own that no part of the game drives: the host walks them
    // along behind their parent, they leave with the parent, and they now and then say something.
    // Every child adds a few things to the parent's shopping list.
    static class Families
    {
        class Child
        {
            public NPC_Info npc;
            public NavMeshAgent agent;
            public int place;
        }

        class Family
        {
            public NPC_Info parent;
            public NavMeshAgent parentAgent;
            public readonly List<Child> children = new List<Child>();
            public float nextTalk;
            public bool ranOff;
        }

        static readonly List<Family> families = new List<Family>();
        static readonly HashSet<NPC_Info> seen = new HashSet<NPC_Info>();
        static float nextFollow;

        // Every player: children the host told us about whose model isn't there yet
        static readonly List<(uint netId, float size, float until)> pending = new List<(uint, float, float)>();

        static readonly AccessTools.FieldRef<NPC_Info, GameObject> CharacterOBJ =
            AccessTools.FieldRefAccess<NPC_Info, GameObject>("characterOBJ");

        // Where each child walks, relative to the parent: to the side, and behind
        static readonly Vector2[] Places = { new Vector2(-0.55f, 0.8f), new Vector2(0.55f, 0.8f), new Vector2(0f, 1.3f) };

        const float FollowEvery = 0.4f;
        const float SpawnReach = 4f;
        const float CatchUpDistance = 4f;
        const float LostDistance = 12f;

        static readonly string[] Chatter =
        {
            "Can we get candy?",
            "I'm booored!",
            "Are we done yet?",
            "Can I push the cart?",
            "I need the toilet!",
            "Look, cookies!",
            "Why do we need broccoli?",
            "Can I have a toy? Pleeease?",
            "My feet hurt!",
            "I want chocolate cereal!",
            "Are we there yet?",
            "Mom! Mom! Mom!",
            "Dad, I'm hungry!",
            "That one! Get that one!",
            "Can we go home now?",
        };

        static readonly string[] RunningLines =
        {
            "Why are we running?!",
            "Wait for me!",
            "Mom, the man is chasing us!",
            "Is this a game?",
            "Wheee!",
        };

        public static void Update()
        {
            ShowPending();
            if (!NetworkServer.active) return;
            var manager = NPC_Manager.Instance;
            if (manager == null) return;

            if (CustomerImprovementsPlugin.FamiliesEnabled.Value) FindNewCustomers(manager);
            if (Time.time < nextFollow) return;
            nextFollow = Time.time + FollowEvery;
            Follow();
        }

        // Host: a new customer walking in from the street sometimes brings their children
        static void FindNewCustomers(NPC_Manager manager)
        {
            if (manager.customersnpcParentOBJ == null) return;
            foreach (Transform npc in manager.customersnpcParentOBJ.transform)
            {
                var customer = npc.GetComponent<NPC_Info>();
                if (customer == null || !seen.Add(customer)) continue;
                if (customer.isAThief || customer.customerOrderNumber != 0 || customer.state != 0 || Party.IsPartyGoer(customer) || Hobos.IsHobo(customer)) continue;
                if (!NearSpawn(manager, npc.position)) continue;
                if (Random.value >= CustomerImprovementsPlugin.FamilyChance.Value) continue;
                StartFamily(manager, customer);
            }
            if (seen.Count > 200) seen.RemoveWhere(customer => customer == null);
        }

        static bool NearSpawn(NPC_Manager manager, Vector3 position)
        {
            foreach (Transform spawn in manager.spawnPointsOBJ.transform)
                if (Vector3.Distance(spawn.position, position) < SpawnReach) return true;
            return false;
        }

        static void StartFamily(NPC_Manager manager, NPC_Info parent)
        {
            var family = new Family
            {
                parent = parent,
                parentAgent = parent.GetComponent<NavMeshAgent>(),
                nextTalk = Time.time + Random.Range(10f, 30f),
            };
            int count = Random.Range(1, Mathf.Clamp(CustomerImprovementsPlugin.MaxChildren.Value, 1, Places.Length) + 1);
            float size = CustomerImprovementsPlugin.ChildSize.Value;

            for (int i = 0; i < count; i++)
            {
                var position = parent.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f));
                var obj = Object.Instantiate(manager.npcAgentPrefab, position, Quaternion.identity);
                var child = obj.GetComponent<NPC_Info>();
                child.NetworkNPCID = Random.Range(0, manager.NPCsArray.Length - 1);
                NetworkServer.Spawn(obj);

                var agent = obj.GetComponent<NavMeshAgent>();
                agent.enabled = true;
                agent.stoppingDistance = 0.3f;
                agent.radius = family.parentAgent.radius * size;
                agent.speed = family.parentAgent.speed;
                agent.angularSpeed = family.parentAgent.angularSpeed;
                agent.acceleration = family.parentAgent.acceleration;
                // Everyone else walks through children rather than around them
                agent.avoidancePriority = 99;

                family.children.Add(new Child { npc = child, agent = agent, place = i });
                Net.SendChild(child, size);

                // "Can we get this too?"
                for (int k = 0; k < CustomerImprovementsPlugin.ExtraItemsPerChild.Value && parent.productsIDToBuy.Count > 0; k++)
                    parent.productsIDToBuy.Add(parent.productsIDToBuy[Random.Range(0, parent.productsIDToBuy.Count)]);
            }
            families.Add(family);
        }

        // Host: children keep up with their parent, and go when the parent goes
        static void Follow()
        {
            for (int f = families.Count - 1; f >= 0; f--)
            {
                var family = families[f];
                family.children.RemoveAll(child => child.npc == null);
                if (family.parent == null)
                {
                    foreach (var child in family.children) NetworkServer.Destroy(child.npc.gameObject);
                    families.RemoveAt(f);
                    continue;
                }
                if (family.children.Count == 0)
                {
                    families.RemoveAt(f);
                    continue;
                }

                var parent = family.parent.transform;
                foreach (var child in family.children)
                {
                    var place = Places[child.place];
                    var target = parent.position + parent.right * place.x - parent.forward * place.y;
                    float distance = Vector3.Distance(child.npc.transform.position, parent.position);
                    if (!child.agent.isOnNavMesh || distance > LostDistance)
                    {
                        child.agent.Warp(target);
                        continue;
                    }
                    child.agent.speed = family.parentAgent.speed * (distance > CatchUpDistance ? 1.5f : 1.1f);
                    child.agent.destination = target;
                }

                Talk(family);
            }
        }

        static void Talk(Family family)
        {
            if (!CustomerImprovementsPlugin.ChildrenTalk.Value) return;
            var child = family.children[Random.Range(0, family.children.Count)].npc;

            if (!family.ranOff && family.parent.isAThief && family.parent.thiefFleeing)
            {
                family.ranOff = true;
                Net.Say(child, RunningLines[Random.Range(0, RunningLines.Length)]);
                return;
            }
            if (Time.time < family.nextTalk) return;
            family.nextTalk = Time.time + Random.Range(25f, 60f);
            Net.Say(child, Chatter[Random.Range(0, Chatter.Length)]);
        }

        // Every player: the host says this NPC is a child
        public static void Received(string body)
        {
            var parts = body.Split(':');
            if (parts.Length != 2 || !uint.TryParse(parts[0], out var netId)) return;
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var size)) return;
            pending.Add((netId, Mathf.Clamp(size, 0.3f, 1f), Time.time + 10f));
        }

        // The model is made when the NPC shows up on this player's side, which can be a moment after the message
        static void ShowPending()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var (netId, size, until) = pending[i];
                if (NetworkClient.spawned.TryGetValue(netId, out var identity) && identity != null)
                {
                    var npc = identity.GetComponent<NPC_Info>();
                    var model = npc != null ? CharacterOBJ(npc) : null;
                    if (model != null)
                    {
                        model.transform.localScale = Vector3.one * size;
                        pending.RemoveAt(i);
                        continue;
                    }
                }
                if (Time.time > until) pending.RemoveAt(i);
            }
        }
    }
}
