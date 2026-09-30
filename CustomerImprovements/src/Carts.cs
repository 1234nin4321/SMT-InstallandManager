using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SMTCustomerImprovements
{
    // Every customer pushes a shopping cart, with what they've picked up so far lying in it.
    //
    // Carts are only for show and each player with the mod makes their own: the cart is a model hanging in front of
    // the customer, and after the animation has run the customer's arms are bent to put their hands on the handle.
    // Only the host knows what a customer has picked up, so the host sends what's in each cart whenever it changes;
    // each player then puts copies of those products' own models into the cart.
    //
    // The model (ShoppingCart.obj and ShoppingCart.png, by mechano-file, MIT License) is loaded from the mod's folder.
    static class Carts
    {
        class Arm
        {
            public Transform upper, fore, hand;
            public float side;
        }

        class Cart
        {
            public NPC_Info npc;
            public GameObject obj;
            public Transform items;
            public readonly List<int> shown = new List<int>();
            public List<int> wanted;
            public Arm right, left;
        }

        // The model is in centimetres. Scaled so the handle is about 95 cm off the floor
        const float Scale = 0.0125f;
        // Where the handle bar is in the model, in centimetres: its height, how far back it sits, and where the hands go
        const float HandleHeight = 76.5f;
        const float HandleBack = 42f;
        const float HandsApart = 15f;
        // How far in front of the customer the handle is, in metres
        const float Ahead = 0.35f;

        // Where products lie in the basket, in metres from the handle: the floor, and a grid of spots on it
        // (the basket runs from 0.27 to 0.98 m, clear of the folding back gate from about 0.35 m, under a rim at 0.73 m)
        const float FloorHeight = 0.42f;
        const float LayerHeight = 0.15f;
        const float ItemSize = 0.15f;
        static readonly float[] Columns = { -0.15f, 0f, 0.15f };
        static readonly float[] Rows = { 0.37f, 0.53f, 0.69f, 0.85f };
        const int MostItems = 24;

        const float ScanEvery = 0.5f;
        const float SendEvery = 1f;

        public static string Folder;

        static Mesh mesh;
        static Material material;
        static bool loadFailed;

        static readonly Dictionary<NPC_Info, Cart> carts = new Dictionary<NPC_Info, Cart>();
        // Every player: what the host says is in each customer's cart, until that cart is there to fill
        static readonly Dictionary<uint, List<int>> received = new Dictionary<uint, List<int>>();
        // Host: what was last sent for each customer
        static readonly Dictionary<NPC_Info, string> sent = new Dictionary<NPC_Info, string>();
        static float nextScan, nextSend;

        static readonly AccessTools.FieldRef<NPC_Info, GameObject> CharacterOBJ =
            AccessTools.FieldRefAccess<NPC_Info, GameObject>("characterOBJ");

        public static void Update()
        {
            if (!CustomerImprovementsPlugin.CartsEnabled.Value || loadFailed)
            {
                if (carts.Count > 0) RemoveAll();
                return;
            }
            if (!NetworkClient.active) return;

            if (Time.time >= nextScan)
            {
                nextScan = Time.time + ScanEvery;
                Scan();
            }
            if (NetworkServer.active && Time.time >= nextSend)
            {
                nextSend = Time.time + SendEvery;
                SendContents();
            }
        }

        // Every player: gives new customers a cart, forgets those who have gone, and fills carts
        static void Scan()
        {
            foreach (var identity in NetworkClient.spawned.Values)
            {
                if (identity == null) continue;
                var npc = identity.GetComponent<NPC_Info>();
                if (npc == null || !npc.isCustomer || npc.isEmployee || carts.ContainsKey(npc) || CharacterOBJ(npc) == null) continue;
                if (!Load()) return;
                carts[npc] = Make(npc);
            }

            List<NPC_Info> gone = null;
            float size = CustomerImprovementsPlugin.CartSize.Value;
            foreach (var pair in carts)
            {
                var cart = pair.Value;
                if (pair.Key == null || cart.obj == null)
                {
                    (gone ??= new List<NPC_Info>()).Add(pair.Key);
                    continue;
                }
                cart.obj.transform.localScale = Vector3.one * size;
                cart.obj.transform.localPosition = new Vector3(0f, 0f, Ahead * size);
                if (received.TryGetValue(cart.npc.netId, out var list))
                {
                    received.Remove(cart.npc.netId);
                    cart.wanted = list;
                }
                Fill(cart);
            }
            if (gone != null)
                foreach (var npc in gone)
                {
                    if (carts[npc].obj != null) Object.Destroy(carts[npc].obj);
                    carts.Remove(npc);
                }

            if (received.Count > 50) received.Clear();
        }

        static bool Load()
        {
            if (mesh != null) return true;
            if (loadFailed) return false;
            try
            {
                var model = Path.Combine(Folder, "ShoppingCart.obj");
                var texture = Path.Combine(Folder, "ShoppingCart.png");
                mesh = ObjModel.Load(model, Matrix4x4.Scale(Vector3.one * Scale) * Matrix4x4.Translate(new Vector3(0f, 0f, HandleBack)));
                material = MakeMaterial(texture);
                CustomerImprovementsPlugin.Log.LogInfo($"Shopping cart loaded: {mesh.vertexCount} vertices, shader {material.shader.name}.");
                return true;
            }
            catch (System.Exception e)
            {
                loadFailed = true;
                mesh = null;
                CustomerImprovementsPlugin.Log.LogError($"Could not load the shopping cart, so customers won't have carts: {e.Message}");
                return false;
            }
        }

        // Lit like the game's products, with the cart's texture
        static Material MakeMaterial(string texturePath)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(texturePath))) throw new IOException("unreadable texture " + texturePath);
            texture.name = "ShoppingCart";

            var shader = ProductShader() ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new IOException("no shader to draw the cart with");
            var result = new Material(shader) { name = "ShoppingCart", mainTexture = texture, color = Color.white };
            return result;
        }

        static Shader ProductShader()
        {
            var listing = ProductListing.Instance;
            if (listing == null) return null;
            foreach (var data in listing.productsData)
            {
                var renderer = data?.productPrefab != null ? data.productPrefab.GetComponentInChildren<MeshRenderer>() : null;
                var shader = renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.shader : null;
                if (shader != null && (shader.name.Contains("Lit") || shader.name == "Standard")) return shader;
            }
            return null;
        }

        static Cart Make(NPC_Info npc)
        {
            var obj = new GameObject("SMTShoppingCart");
            obj.transform.SetParent(npc.transform, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            var items = new GameObject("Items").transform;
            items.SetParent(obj.transform, false);

            var character = CharacterOBJ(npc).transform;
            return new Cart
            {
                npc = npc,
                obj = obj,
                items = items,
                right = FindArm(character, "R", 1f),
                left = FindArm(character, "L", -1f),
            };
        }

        static void RemoveAll()
        {
            foreach (var cart in carts.Values) if (cart.obj != null) Object.Destroy(cart.obj);
            carts.Clear();
        }

        // Puts copies of the products into the cart, laid out on the basket floor in layers
        static void Fill(Cart cart)
        {
            var wanted = CustomerImprovementsPlugin.CartProducts.Value && cart.wanted != null ? cart.wanted : new List<int>();
            if (wanted.SequenceEqual(cart.shown)) return;
            // Picking something up adds to the end; anything else (paying, running off) is laid out again
            bool adding = wanted.Count > cart.shown.Count && wanted.Take(cart.shown.Count).SequenceEqual(cart.shown);
            if (!adding)
            {
                foreach (Transform item in cart.items) Object.Destroy(item.gameObject);
                cart.shown.Clear();
            }
            for (int i = cart.shown.Count; i < wanted.Count && i < MostItems; i++) AddItem(cart, wanted[i], i);
            cart.shown.Clear();
            cart.shown.AddRange(wanted);
        }

        static void AddItem(Cart cart, int productID, int place)
        {
            var listing = ProductListing.Instance;
            if (listing == null || productID < 0 || productID >= listing.productsData.Length) return;
            var data = listing.productsData[productID];
            if (data?.productPrefab == null) return;

            // The same copy of the product the game puts on its shelves, without anything customers could bump into
            var item = Object.Instantiate(data.productPrefab, cart.items);
            foreach (var collider in item.GetComponentsInChildren<Collider>(true)) Object.Destroy(collider);
            foreach (var body in item.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(body);

            int perLayer = Columns.Length * Rows.Length;
            int layer = place / perLayer, spot = place % perLayer;
            var size = data.colliderSize;
            float biggest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float scale = biggest > ItemSize ? ItemSize / biggest : 1f;
            item.transform.localScale = Vector3.one * scale;
            item.transform.localPosition = new Vector3(
                Columns[spot % Columns.Length] + Random.Range(-0.02f, 0.02f),
                FloorHeight + layer * LayerHeight,
                Rows[spot / Columns.Length] + Random.Range(-0.02f, 0.02f));
            item.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        // Host: sends what's in the carts that changed since last time, all in one line
        static void SendContents()
        {
            var manager = NPC_Manager.Instance;
            if (manager == null || manager.customersnpcParentOBJ == null) return;
            var line = new StringBuilder();
            foreach (Transform child in manager.customersnpcParentOBJ.transform)
            {
                var npc = child.GetComponent<NPC_Info>();
                if (npc == null || !npc.isCustomer) continue;
                string ids = string.Join(",", npc.productsIDCarrying.Take(MostItems));
                bool known = sent.TryGetValue(npc, out var last);
                if (known ? last == ids : ids.Length == 0)
                {
                    sent[npc] = ids;
                    continue;
                }
                sent[npc] = ids;
                if (line.Length > 0) line.Append(';');
                line.Append(npc.netId).Append('=').Append(ids);
            }
            if (sent.Count > 200)
                foreach (var npc in sent.Keys.Where(npc => npc == null).ToList()) sent.Remove(npc);
            if (line.Length > 0) Net.SendCarts(line.ToString());
        }

        // Every player: "netId=product,product;netId=" from the host
        public static void Received(string body)
        {
            foreach (var entry in body.Split(';'))
            {
                int equals = entry.IndexOf('=');
                if (equals < 0 || !uint.TryParse(entry.Substring(0, equals), out var netId)) continue;
                var list = new List<int>();
                foreach (var id in entry.Substring(equals + 1).Split(','))
                    if (int.TryParse(id, out var productID)) list.Add(productID);
                received[netId] = list;
            }
        }

        // The customer models use 3ds Max's biped skeleton: "Bip01 R UpperArm", "Bip01 R Forearm", "Bip01 R Hand"
        static Arm FindArm(Transform character, string side, float sign)
        {
            Transform upper = null, fore = null, hand = null;
            foreach (var bone in character.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name.EndsWith(" " + side + " UpperArm")) upper = bone;
                else if (bone.name.EndsWith(" " + side + " Forearm")) fore = bone;
                else if (bone.name.EndsWith(" " + side + " Hand")) hand = bone;
            }
            if (upper == null || fore == null || hand == null) return null;
            return new Arm { upper = upper, fore = fore, hand = hand, side = sign };
        }

        // Every player, after the animation: hands on the handle
        public static void LateUpdate()
        {
            foreach (var cart in carts.Values)
            {
                if (cart.obj == null || cart.npc == null) continue;
                Reach(cart, cart.right);
                Reach(cart, cart.left);
            }
        }

        static void Reach(Cart cart, Arm arm)
        {
            if (arm == null || arm.upper == null) return;
            var body = cart.npc.transform;
            var target = cart.obj.transform.TransformPoint(new Vector3(arm.side * HandsApart * Scale, HandleHeight * Scale, 0f));
            // Elbows bend down, back and out, the way people hold a cart
            var pole = arm.upper.position - body.up * 0.4f - body.forward * 0.3f + body.right * (arm.side * 0.3f);
            TwoBoneIK(arm.upper, arm.fore, arm.hand, target, pole);
        }

        // Bends the elbow so the hand is as far from the shoulder as the target, points the arm at the target,
        // then turns the arm about that line so the elbow points towards the pole
        internal static void TwoBoneIK(Transform upper, Transform fore, Transform hand, Vector3 target, Vector3 pole)
        {
            Vector3 a = upper.position, b = fore.position, c = hand.position;
            float upperLength = (b - a).magnitude, foreLength = (c - b).magnitude;
            if (upperLength < 1e-4f || foreLength < 1e-4f) return;
            float reach = Mathf.Clamp((target - a).magnitude, 0.01f, (upperLength + foreLength) * 0.999f);

            float elbowNow = Vector3.Angle(a - b, c - b);
            float elbowWanted = Mathf.Acos(Mathf.Clamp(
                (upperLength * upperLength + foreLength * foreLength - reach * reach) / (2f * upperLength * foreLength), -1f, 1f)) * Mathf.Rad2Deg;
            var bend = Vector3.Cross(c - a, b - a);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.Cross(target - a, pole - a);
            if (bend.sqrMagnitude < 1e-8f) return;
            fore.rotation = Quaternion.AngleAxis(elbowWanted - elbowNow, bend.normalized) * fore.rotation;

            upper.rotation = Quaternion.FromToRotation(hand.position - a, target - a) * upper.rotation;

            var line = (target - a).normalized;
            var elbow = Vector3.ProjectOnPlane(fore.position - a, line);
            var towards = Vector3.ProjectOnPlane(pole - a, line);
            if (elbow.sqrMagnitude > 1e-8f && towards.sqrMagnitude > 1e-8f)
                upper.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(elbow, towards, line), line) * upper.rotation;
        }
    }
}
