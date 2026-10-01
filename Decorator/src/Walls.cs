using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SMTDecorator
{
    // Paints the store's wall panels in the colours the host keeps.
    //
    // The walls are the game's own paintables: groups of panels under the paintables root, which the game's paint
    // tablet gives a material and one of its palette colours. The Decorator only tints the panel's current material
    // with any colour. Painting a panel with the game's tablet again takes the Decorator's colour off it.
    //
    // Walls the players place themselves are decorations, each painted as one piece. Their network ids change with
    // every load, so they're known by where they stand: "d" and their position in centimetres ("d120_0_-340").
    // Moving one loses its Decorator colour.
    static class Walls
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly AccessTools.FieldRef<Builder_Paintables, LayerMask> PaintMask =
            AccessTools.FieldRefAccess<Builder_Paintables, LayerMask>("lMask");

        static int appliedVersion = -1;
        static float nextCheck;
        static int? mask;

        public static Transform Root
        {
            get
            {
                if (GameData.Instance == null) return null;
                var manager = GameData.Instance.GetComponent<PaintablesManager>();
                return manager != null && manager.paintablesRootOBJ != null ? manager.paintablesRootOBJ.transform : null;
            }
        }

        public static void Update()
        {
            // The game paints the walls from the save a few seconds after loading, so the colours are checked now and then too
            if (appliedVersion == Store.Version && Time.unscaledTime < nextCheck) return;
            var root = Root;
            if (root == null) return;
            appliedVersion = Store.Version;
            nextCheck = Time.unscaledTime + 3f;
            var decorations = Decorations();
            foreach (var pair in Store.Paint)
            {
                MeshRenderer renderer;
                if (IsDecoration(pair.Key)) decorations.TryGetValue(pair.Key, out renderer);
                else renderer = Panel(root, pair.Key);
                if (renderer == null) continue;
                var material = renderer.material;
                if (material.HasProperty(BaseColor) && material.GetColor(BaseColor) != (Color)pair.Value)
                    material.SetColor(BaseColor, pair.Value);
            }
        }

        public static bool IsDecoration(string key) => key.StartsWith("d");

        public static string KeyOf(PaintableDecoration decoration)
        {
            var p = decoration.transform.position;
            return "d" + Mathf.RoundToInt(p.x * 100f) + "_" + Mathf.RoundToInt(p.y * 100f) + "_" + Mathf.RoundToInt(p.z * 100f);
        }

        // Every paintable decoration in the store (hedges have materials of their own and are left out), by key
        static Dictionary<string, MeshRenderer> Decorations()
        {
            var found = new Dictionary<string, MeshRenderer>();
            foreach (var decoration in Object.FindObjectsOfType<PaintableDecoration>())
                if (!decoration.isHedge && decoration.mRenderer != null) found[KeyOf(decoration)] = decoration.mRenderer;
            return found;
        }

        static MeshRenderer Panel(Transform root, string key)
        {
            if (IsDecoration(key))
            {
                Decorations().TryGetValue(key, out var renderer);
                return renderer;
            }
            var parts = key.Split('.');
            int group = int.Parse(parts[0]), panel = int.Parse(parts[1]);
            if (group >= root.childCount) return null;
            var parent = root.GetChild(group);
            return panel < parent.childCount ? parent.GetChild(panel).GetComponent<MeshRenderer>() : null;
        }

        // The layers the game's paint tablet aims at
        static int Mask(PlayerNetwork player)
        {
            if (mask.HasValue) return mask.Value;
            var prefabs = player.equippedPrefabs;
            var builder = prefabs != null && prefabs.Length > 5 && prefabs[5] != null ? prefabs[5].GetComponent<Builder_Paintables>() : null;
            mask = builder != null ? PaintMask(builder).value : Physics.DefaultRaycastLayers;
            return mask.Value;
        }

        // The wall panel the player is looking at, as "group.panel", and the panels of its whole group.
        // A placed wall is one panel on its own.
        public static bool Aim(PlayerNetwork player, out string panel, out List<string> group, out RaycastHit hit)
        {
            panel = null;
            group = null;
            var camera = Camera.main;
            var root = Root;
            if (camera == null || root == null ||
                !Physics.Raycast(camera.transform.position, camera.transform.forward, out hit, DecoratorPlugin.Reach.Value, Mask(player), QueryTriggerInteraction.Ignore))
            {
                hit = default;
                return false;
            }
            var decoration = hit.transform.GetComponentInParent<PaintableDecoration>();
            if (decoration != null)
            {
                if (decoration.isHedge || decoration.mRenderer == null) return false;
                panel = KeyOf(decoration);
                group = new List<string> { panel };
                return true;
            }

            var parent = hit.transform.parent;
            if (parent == null || parent.parent != root || parent.GetComponent<Paintable>() == null) return false;

            int groupIndex = parent.GetSiblingIndex();
            panel = groupIndex + "." + hit.transform.GetSiblingIndex();
            group = new List<string>();
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).GetComponent<MeshRenderer>() != null) group.Add(groupIndex + "." + i);
            return true;
        }

        // The colour a panel shows right now
        public static Color32? ColorOf(string key)
        {
            if (Store.Paint.TryGetValue(key, out var color)) return color;
            var root = Root;
            var renderer = root != null ? Panel(root, key) : null;
            if (renderer == null || !renderer.sharedMaterial.HasProperty(BaseColor)) return null;
            return (Color32)renderer.sharedMaterial.GetColor(BaseColor);
        }

        // A material from the walls, to build the pictures' material from
        public static Material AnyWallMaterial()
        {
            var root = Root;
            if (root == null) return null;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(BaseColor)) return renderer.sharedMaterial;
            return null;
        }

        // Every player sees the game's tablet paint a panel, so every player takes the Decorator's colour off it
        [HarmonyPatch(typeof(PaintablesManager), "UserCode_RpcUpdateSingleParentMaterial__String__Int32__Int32__Int32__Int32")]
        static class RepaintPatch
        {
            static void Postfix(int parentIndex, int particularOBJIndex)
            {
                Store.ClearPaint(parentIndex + "." + particularOBJIndex);
            }
        }

        // The same for placed walls
        [HarmonyPatch(typeof(PaintableDecoration), "UserCode_RpcUpdateVisuals__Int32__Int32")]
        static class RepaintDecorationPatch
        {
            static void Postfix(PaintableDecoration __instance)
            {
                Store.ClearPaint(KeyOf(__instance));
            }
        }
    }
}
