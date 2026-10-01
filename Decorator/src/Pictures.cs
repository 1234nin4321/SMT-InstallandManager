using System.Collections.Generic;
using UnityEngine;

namespace SMTDecorator
{
    // Hangs the pictures the host keeps, as flat quads just in front of the walls. A picture whose file this player
    // hasn't got yet shows grey until the host has sent it.
    static class Pictures
    {
        // Pictures stand this far off the wall, so the wall doesn't show through
        public const float Offset = 0.006f;

        static readonly Dictionary<int, GameObject> shown = new Dictionary<int, GameObject>();
        static readonly Dictionary<int, string> shownTexture = new Dictionary<int, string>();
        static int syncedVersion = -1;
        static GameObject root;
        static Material baseMaterial;

        public static void Sync()
        {
            if (syncedVersion == Store.Version) return;
            if (Walls.Root == null)
            {
                // Not in a store (any more)
                foreach (var go in shown.Values) if (go != null) Object.Destroy(go);
                shown.Clear();
                shownTexture.Clear();
                syncedVersion = -1;
                return;
            }
            syncedVersion = Store.Version;

            var gone = new List<int>();
            foreach (var pair in shown)
                if (!Store.Pictures.ContainsKey(pair.Key) || pair.Value == null) gone.Add(pair.Key);
            foreach (var id in gone)
            {
                if (shown[id] != null) Object.Destroy(shown[id]);
                shown.Remove(id);
                shownTexture.Remove(id);
            }

            foreach (var picture in Store.Pictures.Values)
            {
                if (!shown.TryGetValue(picture.Id, out var go))
                {
                    go = Make("SMTDecorator picture " + picture.Id);
                    shown[picture.Id] = go;
                }
                Place(go.transform, picture.Position, picture.Rotation, picture.Width, picture.Height);

                var texture = Store.Texture(picture.Hash);
                if (texture == null)
                {
                    Net.Fetch(picture.Hash);
                    // Checked again when the file arrives, which changes the version
                }
                else if (!shownTexture.TryGetValue(picture.Id, out var hash) || hash != picture.Hash)
                {
                    Show(go.GetComponent<MeshRenderer>().material, texture);
                    shownTexture[picture.Id] = picture.Hash;
                }
            }
        }

        public static void Place(Transform t, Vector3 position, Quaternion rotation, float width, float height)
        {
            t.SetPositionAndRotation(position, rotation);
            t.localScale = new Vector3(width, height, 1f);
        }

        // A quad facing the way its rotation's back points, with no collider, so it never gets in the way
        public static GameObject Make(string name)
        {
            if (root == null)
            {
                root = new GameObject("SMTDecorator pictures");
                Object.DontDestroyOnLoad(root);
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.material = NewMaterial();
            return go;
        }

        // A copy of a wall's material, so pictures are lit like the walls, with its own textures taken off
        public static Material NewMaterial()
        {
            if (baseMaterial == null)
            {
                var wall = Walls.AnyWallMaterial();
                var shader = wall != null ? wall.shader : Shader.Find("Universal Render Pipeline/Lit");
                baseMaterial = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
                foreach (var keyword in new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_EMISSION", "_OCCLUSIONMAP", "_PARALLAXMAP" })
                    baseMaterial.DisableKeyword(keyword);
                Set(baseMaterial, "_Smoothness", 0.15f);
                Set(baseMaterial, "_Metallic", 0f);
            }
            var material = new Material(baseMaterial);
            material.color = new Color(0.55f, 0.55f, 0.55f);
            return material;
        }

        static void Set(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        public static void Show(Material material, Texture2D texture)
        {
            material.color = Color.white;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", Vector2.one);
                material.SetTextureOffset("_BaseMap", Vector2.zero);
            }
            // Transparent parts of a PNG are left out
            bool alpha = ImageFiles.HasTransparency(texture);
            Set(material, "_AlphaClip", alpha ? 1f : 0f);
            Set(material, "_Cutoff", 0.5f);
            if (alpha) material.EnableKeyword("_ALPHATEST_ON");
            else material.DisableKeyword("_ALPHATEST_ON");
        }

        // The picture the ray hits first, if it's closer than the wall
        public static Picture Hit(Ray ray, float maxDistance)
        {
            Picture best = null;
            float bestDistance = maxDistance;
            foreach (var picture in Store.Pictures.Values)
            {
                var forward = picture.Rotation * Vector3.forward;
                var plane = new Plane(-forward, picture.Position);
                if (!plane.Raycast(ray, out var distance) || distance > bestDistance) continue;
                var local = Quaternion.Inverse(picture.Rotation) * (ray.GetPoint(distance) - picture.Position);
                if (Mathf.Abs(local.x) <= picture.Width / 2f && Mathf.Abs(local.y) <= picture.Height / 2f)
                {
                    best = picture;
                    bestDistance = distance;
                }
            }
            return best;
        }

        // Where a picture goes on the wall the player is looking at, upright, facing out of the wall.
        // Only fairly upright surfaces count as walls.
        public static bool Spot(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            var camera = Camera.main;
            if (camera == null) return false;
            var mask = Physics.DefaultRaycastLayers & ~(1 << 2);
            if (!Physics.Raycast(camera.transform.position, camera.transform.forward, out var hit, DecoratorPlugin.Reach.Value, mask, QueryTriggerInteraction.Ignore))
                return false;
            if (Mathf.Abs(hit.normal.y) > 0.5f) return false;
            var normal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
            position = hit.point + normal * Offset;
            // The quad shows its front to whoever looks along its forward, so forward points into the wall
            rotation = Quaternion.LookRotation(-normal, Vector3.up);
            return true;
        }
    }
}
