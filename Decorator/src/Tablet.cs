using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace SMTDecorator
{
    // The Decorator tablet: the ordering tablet's model, held the same way, with the chosen colour or picture on its
    // screen. Aim at a wall and click to paint it or hang the picture; the menu is where colours and pictures are picked.
    //
    // The game only knows the items in its own list, so the tablet is the player's own: other players don't see them
    // holding it, but they do see the walls and pictures.
    static class Tablet
    {
        enum Mode { Paint, Picture }

        static bool holding;
        static bool menuOpen;
        static Mode mode = Mode.Paint;
        static Color32 color = new Color32(0x4a, 0x90, 0xd9, 255);
        static readonly List<Color32> recent = new List<Color32>();
        static string hexText = Store.Hex(new Color32(0x4a, 0x90, 0xd9, 255));
        static string pictureHash;
        static float pictureWidth = 1f;

        static PlayerNetwork player;
        static GameObject model;
        static Material screen;
        static GameObject preview;
        static Texture2D previewTexture;
        static readonly HashSet<string> uploaded = new HashSet<string>();

        static string note;
        static float noteUntil;

        // The menu's lists, read when it opens and after an import
        static List<string> library = new List<string>();
        static List<string> importFiles = new List<string>();
        static Vector2 libraryScroll;
        static Rect window;
        static bool? savedInput;

        static readonly Color32[] Swatches =
        {
            new Color32(0xff, 0xff, 0xff, 255), new Color32(0xf2, 0xee, 0xe3, 255), new Color32(0xd9, 0xd4, 0xc7, 255), new Color32(0xa0, 0xa0, 0xa0, 255),
            new Color32(0x5a, 0x5a, 0x5a, 255), new Color32(0x22, 0x22, 0x22, 255), new Color32(0xe5, 0x39, 0x35, 255), new Color32(0xff, 0x8a, 0x65, 255),
            new Color32(0xfb, 0x8c, 0x00, 255), new Color32(0xfd, 0xd8, 0x35, 255), new Color32(0xff, 0xf5, 0x9d, 255), new Color32(0x7c, 0xb3, 0x42, 255),
            new Color32(0x2e, 0x7d, 0x32, 255), new Color32(0x26, 0xa6, 0x9a, 255), new Color32(0x4a, 0x90, 0xd9, 255), new Color32(0x1a, 0x23, 0x7e, 255),
            new Color32(0x8e, 0x24, 0xaa, 255), new Color32(0xf4, 0x8f, 0xb1, 255), new Color32(0x79, 0x55, 0x48, 255), new Color32(0xc8, 0xa9, 0x7e, 255),
        };

        public static void Note(string text)
        {
            note = text;
            noteUntil = Time.unscaledTime + 4f;
        }

        static PlayerNetwork LocalPlayerNetwork()
        {
            if (player != null && player.isLocalPlayer) return player;
            player = null;
            foreach (var candidate in UnityEngine.Object.FindObjectsOfType<PlayerNetwork>())
                if (candidate.isLocalPlayer) player = candidate;
            return player;
        }

        public static void Update()
        {
            if (ImageFiles.TakePicked(out var path) && path != null) ImportFile(path);

            if (holding && (player == null || model == null || player.equippedItem != 0 || Walls.Root == null)) PutAway();

            if (!Net.Typing() && DecoratorPlugin.TabletKey.Value.IsDown())
            {
                if (holding) PutAway();
                else TakeOut();
            }
            if (!holding) return;

            if (!Net.Typing() && DecoratorPlugin.MenuKey.Value.IsDown()) SetMenu(!menuOpen);
            if (menuOpen)
            {
                // The game locks the cursor again every frame while you play
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                ShowPreview(false);
                return;
            }

            // A free cursor means one of the game's menus or the chat is open
            bool playing = Cursor.lockState == CursorLockMode.Locked && !Net.Typing();
            if (mode == Mode.Picture) UpdatePictureMode(playing);
            else UpdatePaintMode(playing);
        }

        static void TakeOut()
        {
            var me = LocalPlayerNetwork();
            if (me == null || Walls.Root == null) return;
            if (me.equippedItem != 0)
            {
                Note("Your hands have to be empty.");
                return;
            }
            var permissions = me.GetComponent<PlayerPermissions>();
            if (permissions != null && !permissions.RequestMP())
            {
                Note("The host hasn't given you permission to decorate.");
                return;
            }
            var prefabs = me.equippedPrefabs;
            if (prefabs == null || prefabs.Length <= 6 || prefabs[6] == null || me.equippedParentOBJ == null) return;

            // Placed as the game places the ordering tablet in its owner's hands
            model = UnityEngine.Object.Instantiate(prefabs[6], me.equippedParentOBJ.transform);
            model.name = "SMTDecorator tablet";
            model.transform.localPosition = new Vector3(0.1875f, 0.685f, -0.1185f);
            model.transform.localRotation = Quaternion.Euler(270f, 180f, 0.8f);
            // The game's own scripts on it (the ordering screen) stay off
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour.GetType().Assembly == typeof(PlayerNetwork).Assembly) behaviour.enabled = false;
            MakeScreen();
            holding = true;
            UpdateScreen();
        }

        // The ordering tablet's screen is a canvas; a quad in its place shows the colour or picture instead
        static void MakeScreen()
        {
            screen = null;
            Canvas biggest = null;
            float area = 0f;
            foreach (var canvas in model.GetComponentsInChildren<Canvas>(true))
            {
                var rect = (canvas.transform as RectTransform)?.rect ?? default;
                var scale = canvas.transform.lossyScale;
                float size = Mathf.Abs(rect.width * scale.x * rect.height * scale.y);
                if (size > area)
                {
                    area = size;
                    biggest = canvas;
                }
            }
            foreach (var canvas in model.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            if (biggest == null) return;

            var t = (RectTransform)biggest.transform;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "SMTDecorator screen";
            UnityEngine.Object.Destroy(quad.GetComponent<Collider>());
            quad.layer = model.layer;
            quad.transform.SetParent(t.parent, false);
            quad.transform.localPosition = t.localPosition + t.localRotation * new Vector3(0f, 0f, -0.002f);
            quad.transform.localRotation = t.localRotation;
            quad.transform.localScale = new Vector3(t.rect.width * t.localScale.x * 0.92f, t.rect.height * t.localScale.y * 0.92f, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            screen = Pictures.NewMaterial();
            renderer.material = screen;
        }

        static void UpdateScreen()
        {
            if (screen == null) return;
            var texture = mode == Mode.Picture && pictureHash != null ? Store.Texture(pictureHash) : null;
            if (texture != null)
            {
                Pictures.Show(screen, texture);
                return;
            }
            Pictures.SetTexture(screen, Texture2D.whiteTexture);
            Pictures.SetColor(screen, mode == Mode.Paint ? (Color)color : new Color(0.15f, 0.15f, 0.15f));
        }

        static void PutAway()
        {
            SetMenu(false);
            ShowPreview(false);
            if (model != null) UnityEngine.Object.Destroy(model);
            model = null;
            screen = null;
            holding = false;
        }

        // ---- Painting ----

        static void UpdatePaintMode(bool playing)
        {
            ShowPreview(false);
            if (!playing) return;
            bool click = UnityInput.Current.GetMouseButtonDown(0);
            bool rightClick = UnityInput.Current.GetMouseButtonDown(1);
            if (!click && !rightClick) return;

            var me = LocalPlayerNetwork();
            if (me == null || !Walls.Aim(me, out var panel, out var group, out _)) return;

            if (rightClick)
            {
                // Picks up the colour of the panel
                var current = Walls.ColorOf(panel);
                if (current.HasValue) SetColor(current.Value);
                return;
            }
            if (!CanAct()) return;

            bool wholeWall = UnityInput.Current.GetKey(KeyCode.LeftShift) || UnityInput.Current.GetKey(KeyCode.RightShift);
            var panels = wholeWall ? group : new List<string> { panel };
            panels = panels.FindAll(p => !(Walls.ColorOf(p) is Color32 c && c.r == color.r && c.g == color.g && c.b == color.b));
            if (panels.Count == 0) return;
            if (!Affordable(panels.Count * DecoratorPlugin.PanelPrice.Value)) return;
            Net.RequestPaint(color, panels);
            Remember(color);
        }

        // ---- Pictures ----

        static void UpdatePictureMode(bool playing)
        {
            if (!playing)
            {
                ShowPreview(false);
                return;
            }
            var camera = Camera.main;
            float scroll = UnityInput.Current.mouseScrollDelta.y;
            if (scroll != 0f) pictureWidth = Mathf.Clamp(pictureWidth * (scroll > 0f ? 1.1f : 1f / 1.1f), 0.2f, 6f);

            if (UnityInput.Current.GetMouseButtonDown(1) && camera != null)
            {
                var hit = Pictures.Hit(new Ray(camera.transform.position, camera.transform.forward), DecoratorPlugin.Reach.Value);
                if (hit != null && CanAct()) Net.RequestRemove(hit.Id);
                return;
            }

            var texture = pictureHash != null ? Store.Texture(pictureHash) : null;
            if (texture == null || !Pictures.Spot(out var position, out var rotation))
            {
                ShowPreview(false);
                if (texture == null && UnityInput.Current.GetMouseButtonDown(0)) Note("Pick a picture in the menu first.");
                return;
            }
            float height = pictureWidth * texture.height / texture.width;
            // Nudged a little further out than the real thing, so it shows in front of pictures already there
            ShowPreview(true);
            Pictures.Place(preview.transform, position + rotation * Vector3.back * Pictures.Offset, rotation, pictureWidth, height);
            if (previewTexture != texture)
            {
                Pictures.Show(preview.GetComponent<MeshRenderer>().material, texture);
                previewTexture = texture;
            }

            if (!UnityInput.Current.GetMouseButtonDown(0) || !CanAct() || !Affordable(DecoratorPlugin.PicturePrice.Value)) return;
            if (!Net.IsHost && uploaded.Add(pictureHash)) Net.Upload(pictureHash);
            Net.RequestPicture(new Picture { Hash = pictureHash, Position = position, Rotation = rotation, Width = pictureWidth, Height = height });
        }

        static void ShowPreview(bool show)
        {
            if (show && preview == null) preview = Pictures.Make("SMTDecorator preview");
            if (preview != null && preview.activeSelf != show) preview.SetActive(show);
        }

        static bool CanAct()
        {
            if (Net.HostHasMod) return true;
            Note("The host doesn't have the Decorator, so nobody would see it.");
            return false;
        }

        static bool Affordable(float price)
        {
            if (price <= 0f || GameData.Instance == null || GameData.Instance.gameFunds >= price) return true;
            Note("Not enough money for that.");
            return false;
        }

        static void ImportFile(string path)
        {
            var hash = ImageFiles.Import(path, out var error);
            if (hash == null)
            {
                Note(error);
                return;
            }
            pictureHash = hash;
            mode = Mode.Picture;
            library = Store.Library();
            UpdateScreen();
            Note("Picture ready: " + Path.GetFileName(path));
        }

        static void SetColor(Color32 value)
        {
            color = new Color32(value.r, value.g, value.b, 255);
            hexText = Store.Hex(color);
            UpdateScreen();
        }

        static void Remember(Color32 value)
        {
            recent.RemoveAll(c => c.r == value.r && c.g == value.g && c.b == value.b);
            recent.Insert(0, value);
            if (recent.Count > 10) recent.RemoveAt(recent.Count - 1);
        }

        // ---- The menu ----

        static readonly Type ControllerType = AccessTools.TypeByName("StarterAssets.FirstPersonController");

        static void SetMenu(bool open)
        {
            if (open == menuOpen) return;
            menuOpen = open;
            if (open)
            {
                library = Store.Library();
                importFiles = ImageFiles.ImportFolderFiles();
                window = new Rect((Screen.width - 600) / 2f, (Screen.height - 560) / 2f, 600, 560);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            // Keeps the player from walking and looking around while they use the menu
            try
            {
                var controller = ControllerType != null ? AccessTools.Field(ControllerType, "Instance")?.GetValue(null)
                    ?? AccessTools.Property(ControllerType, "Instance")?.GetValue(null) : null;
                var input = ControllerType != null ? AccessTools.Field(ControllerType, "allowPlayerInput") : null;
                if (controller == null || input == null) return;
                if (open)
                {
                    savedInput = (bool)input.GetValue(controller);
                    input.SetValue(controller, false);
                }
                else if (savedInput.HasValue)
                {
                    input.SetValue(controller, savedInput.Value);
                    savedInput = null;
                }
            }
            catch (Exception e)
            {
                DecoratorPlugin.Log.LogWarning($"Could not hold the player still: {e.Message}");
            }
        }

        public static void OnGUI()
        {
            if (!holding) return;
            if (menuOpen)
            {
                window = GUI.Window(0x5DEC0, window, Menu, "Decorator");
            }
            else
            {
                Hud();
            }
            if (note != null && Time.unscaledTime < noteUntil)
            {
                var rect = new Rect(Screen.width / 2f - 250, Screen.height * 0.62f, 500, 34);
                GUI.Box(rect, note, Styles.Note);
            }
        }

        static void Hud()
        {
            var rect = new Rect(Screen.width - 360, Screen.height - 150, 340, 130);
            GUI.Box(rect, GUIContent.none, Styles.Panel);
            GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 10, rect.width - 24, rect.height - 20));
            if (mode == Mode.Paint)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Paint", Styles.Title);
                GUILayout.FlexibleSpace();
                Swatch(GUILayoutUtility.GetRect(60, 22), color);
                GUILayout.EndHorizontal();
                GUILayout.Label("Click: paint a panel    Shift+click: the whole wall", Styles.Small);
                GUILayout.Label("Right-click: pick up a wall's colour", Styles.Small);
            }
            else
            {
                GUILayout.Label("Pictures   " + pictureWidth.ToString("0.00") + " m wide", Styles.Title);
                GUILayout.Label("Click: put it on a wall or the floor    Scroll: size", Styles.Small);
                GUILayout.Label("Right-click a picture: take it down", Styles.Small);
            }
            GUILayout.Label(DecoratorPlugin.MenuKey.Value + ": menu    " + DecoratorPlugin.TabletKey.Value + ": put the tablet away", Styles.Small);
            GUILayout.EndArea();
        }

        static void Menu(int id)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(mode == Mode.Paint, "Paint walls", Styles.Tab) && mode != Mode.Paint)
            {
                mode = Mode.Paint;
                UpdateScreen();
            }
            if (GUILayout.Toggle(mode == Mode.Picture, "Pictures", Styles.Tab) && mode != Mode.Picture)
            {
                mode = Mode.Picture;
                UpdateScreen();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            if (mode == Mode.Paint) PaintMenu();
            else PictureMenu();

            GUILayout.FlexibleSpace();
            if (!Net.HostHasMod)
                GUILayout.Label("The host doesn't have the Decorator (or hasn't answered yet). Nothing you do would be kept or seen.", Styles.Warning);
            GUILayout.BeginHorizontal();
            GUILayout.Label(DecoratorPlugin.MenuKey.Value + " closes the menu", Styles.Small);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(90))) SetMenu(false);
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        static void PaintMenu()
        {
            GUILayout.BeginHorizontal();
            Swatch(GUILayoutUtility.GetRect(120, 120, GUILayout.Width(120), GUILayout.Height(120)), color);
            GUILayout.Space(12);
            GUILayout.BeginVertical();
            var r = Channel("Red", color.r);
            var g = Channel("Green", color.g);
            var b = Channel("Blue", color.b);
            if (r != color.r || g != color.g || b != color.b) SetColor(new Color32(r, g, b, 255));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Hex  #", GUILayout.Width(52));
            var typed = GUILayout.TextField(hexText, 7, GUILayout.Width(90));
            if (typed != hexText)
            {
                hexText = typed;
                if (Store.TryParseHex(typed, out var parsed))
                {
                    color = parsed;
                    UpdateScreen();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("Colours", Styles.Title);
            SwatchRows(Swatches);
            if (recent.Count > 0)
            {
                GUILayout.Space(6);
                GUILayout.Label("Used lately", Styles.Title);
                SwatchRows(recent.ToArray());
            }
            GUILayout.Space(8);
            GUILayout.Label($"${DecoratorPlugin.PanelPrice.Value:0.##} per wall panel. Shift+click paints every panel of the wall.", Styles.Small);
        }

        static byte Channel(string name, byte value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(52));
            float slid = GUILayout.HorizontalSlider(value, 0f, 255f, GUILayout.Width(250));
            GUILayout.Label(((int)slid).ToString(), GUILayout.Width(36));
            GUILayout.EndHorizontal();
            return (byte)Mathf.RoundToInt(slid);
        }

        static void SwatchRows(Color32[] colors)
        {
            for (int i = 0; i < colors.Length; i += 10)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Math.Min(i + 10, colors.Length); j++)
                {
                    var rect = GUILayoutUtility.GetRect(44, 30, GUILayout.Width(44), GUILayout.Height(30));
                    Swatch(rect, colors[j]);
                    if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) SetColor(colors[j]);
                }
                GUILayout.EndHorizontal();
            }
        }

        static void Swatch(Rect rect, Color32 value)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0f, 0f, 0f, 0.6f), 0f, 4f);
            GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), Texture2D.whiteTexture,
                ScaleMode.StretchToFill, false, 0f, value, 0f, 3f);
        }

        static void PictureMenu()
        {
            GUILayout.BeginHorizontal();
            GUI.enabled = ImageFiles.CanUseDialog && !ImageFiles.DialogOpen;
            if (GUILayout.Button(ImageFiles.DialogOpen ? "Picking a file..." : "Import from PC...", GUILayout.Height(30))) ImageFiles.OpenDialog();
            GUI.enabled = true;
            if (GUILayout.Button("Open Import folder", GUILayout.Height(30)))
                Application.OpenURL("file:///" + Store.ImportFolder.Replace('\\', '/'));
            if (GUILayout.Button("Refresh", GUILayout.Height(30), GUILayout.Width(80)))
            {
                library = Store.Library();
                importFiles = ImageFiles.ImportFolderFiles();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Width " + pictureWidth.ToString("0.00") + " m", GUILayout.Width(110));
            pictureWidth = GUILayout.HorizontalSlider(pictureWidth, 0.2f, 6f);
            GUILayout.EndHorizontal();

            libraryScroll = GUILayout.BeginScrollView(libraryScroll, GUILayout.Height(300));
            if (importFiles.Count > 0)
            {
                GUILayout.Label("In the Import folder (click to import)", Styles.Title);
                foreach (var file in importFiles)
                    if (GUILayout.Button(Path.GetFileName(file), Styles.Left)) ImportFile(file);
                GUILayout.Space(8);
            }
            GUILayout.Label(library.Count > 0 ? "Pictures" : "No pictures yet. Import one from your PC, or put PNG/JPEG files in the Import folder.", Styles.Title);
            const int perRow = 5;
            for (int i = 0; i < library.Count; i += perRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Math.Min(i + perRow, library.Count); j++)
                {
                    var hash = library[j];
                    var texture = Store.Texture(hash);
                    var style = hash == pictureHash ? Styles.Selected : GUI.skin.button;
                    if (GUILayout.Button(texture != null ? new GUIContent(texture) : new GUIContent("?"), style, GUILayout.Width(100), GUILayout.Height(80)))
                    {
                        pictureHash = hash;
                        UpdateScreen();
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.Label($"${DecoratorPlugin.PicturePrice.Value:0.##} per picture. Pictures are saved on the host with the store.", Styles.Small);
        }

        static class Styles
        {
            static GUIStyle panel, note, title, small, warning, tab, selected, left;
            static Texture2D Fill(Color c)
            {
                var t = new Texture2D(1, 1);
                t.SetPixel(0, 0, c);
                t.Apply();
                return t;
            }

            public static GUIStyle Panel => panel ?? (panel = new GUIStyle(GUI.skin.box) { normal = { background = Fill(new Color(0.08f, 0.09f, 0.11f, 0.85f)) } });
            public static GUIStyle Note => note ?? (note = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16, alignment = TextAnchor.MiddleCenter,
                normal = { background = Fill(new Color(0.1f, 0.1f, 0.12f, 0.9f)), textColor = Color.white },
            });
            public static GUIStyle Title => title ?? (title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14, wordWrap = true });
            public static GUIStyle Small => small ?? (small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true });
            public static GUIStyle Warning => warning ?? (warning = new GUIStyle(GUI.skin.label) { wordWrap = true, normal = { textColor = new Color(1f, 0.75f, 0.3f) } });
            public static GUIStyle Tab => tab ?? (tab = new GUIStyle(GUI.skin.button) { fontSize = 14, fixedHeight = 30 });
            public static GUIStyle Left => left ?? (left = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft });
            public static GUIStyle Selected => selected ?? (selected = new GUIStyle(GUI.skin.button)
            {
                normal = { background = Fill(new Color(0.29f, 0.56f, 0.85f, 1f)) },
                hover = { background = Fill(new Color(0.35f, 0.62f, 0.9f, 1f)) },
            });
        }
    }
}
