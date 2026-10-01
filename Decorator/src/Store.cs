using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SMTDecorator
{
    // A picture hanging on a wall
    class Picture
    {
        public int Id;
        public string Hash;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Width;
        public float Height;
    }

    // What every player knows about the store's decoration: the colour of each painted wall panel and the pictures.
    // The host's copy is the real one; everyone else's comes from the host. The walls and pictures follow it.
    //
    // The host keeps it in a file next to the game's save, written whenever the game saves. Picture files are kept
    // apart, named after their contents, so the same picture is stored once however often it hangs.
    static class Store
    {
        // Wall panel ("group.panel", as the game's paint tablet counts them) => colour
        public static readonly Dictionary<string, Color32> Paint = new Dictionary<string, Color32>();
        public static readonly Dictionary<int, Picture> Pictures = new Dictionary<int, Picture>();
        public static int NextId = 1;

        // Goes up with every change, so the walls and pictures know when to catch up
        public static int Version;

        public static string ImageFolder;
        public static string ImportFolder;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Init()
        {
            ImageFolder = Path.Combine(Paths.ConfigPath, "SMTDecorator", "images");
            ImportFolder = Path.Combine(Paths.ConfigPath, "SMTDecorator", "Import");
            Directory.CreateDirectory(ImageFolder);
            Directory.CreateDirectory(ImportFolder);
        }

        public static void Clear()
        {
            Paint.Clear();
            Pictures.Clear();
            NextId = 1;
            Version++;
        }

        // ---- Picture files ----

        public static string HashOf(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var text = new StringBuilder();
                for (int i = 0; i < 16; i++) text.Append(hash[i].ToString("x2"));
                return text.ToString();
            }
        }

        public static bool ValidHash(string hash)
        {
            if (hash == null || hash.Length != 32) return false;
            foreach (var c in hash)
                if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false;
            return true;
        }

        public static string ImagePath(string hash)
        {
            if (!ValidHash(hash)) return null;
            foreach (var extension in new[] { ".png", ".jpg" })
            {
                var path = Path.Combine(ImageFolder, hash + extension);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        public static bool HasImage(string hash) => ImagePath(hash) != null;

        // Keeps a picture file and returns its name. PNG files start with 0x89 'P', anything else the game can read is a JPEG.
        public static string SaveImage(byte[] bytes)
        {
            var hash = HashOf(bytes);
            if (!HasImage(hash))
            {
                var extension = bytes.Length > 1 && bytes[0] == 0x89 && bytes[1] == (byte)'P' ? ".png" : ".jpg";
                File.WriteAllBytes(Path.Combine(ImageFolder, hash + extension), bytes);
            }
            return hash;
        }

        // Every picture file there is, newest first
        public static List<string> Library()
        {
            var files = new List<FileInfo>();
            foreach (var file in new DirectoryInfo(ImageFolder).GetFiles())
                if (ValidHash(Path.GetFileNameWithoutExtension(file.Name))) files.Add(file);
            files.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            return files.ConvertAll(f => Path.GetFileNameWithoutExtension(f.Name));
        }

        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        // The picture as a texture, or null while its file isn't here
        public static Texture2D Texture(string hash)
        {
            if (textures.TryGetValue(hash, out var texture) && texture != null) return texture;
            var path = ImagePath(hash);
            if (path == null) return null;
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.anisoLevel = 4;
            texture.name = "SMTDecorator " + hash;
            textures[hash] = texture;
            return texture;
        }

        // ---- Text form, used both in the save file and between players ----

        public static string Hex(Color32 c) => c.r.ToString("x2") + c.g.ToString("x2") + c.b.ToString("x2");

        public static bool TryParseHex(string text, out Color32 color)
        {
            color = new Color32(255, 255, 255, 255);
            if (text == null) return false;
            text = text.Trim().TrimStart('#');
            if (text.Length != 6 || !int.TryParse(text, NumberStyles.HexNumber, Inv, out var value)) return false;
            color = new Color32((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);
            return true;
        }

        public static bool ValidPanel(string key)
        {
            var parts = key.Split('.');
            return parts.Length == 2 && int.TryParse(parts[0], out var a) && int.TryParse(parts[1], out var b) && a >= 0 && b >= 0;
        }

        // "colour=panel,panel,..."
        public static string FormatPaint(Color32 color, IEnumerable<string> panels) => Hex(color) + "=" + string.Join(",", panels);

        public static bool TryParsePaint(string text, out Color32 color, out List<string> panels)
        {
            panels = new List<string>();
            int equals = text.IndexOf('=');
            if (equals < 0 || !TryParseHex(text.Substring(0, equals), out color))
            {
                color = default;
                return false;
            }
            foreach (var panel in text.Substring(equals + 1).Split(','))
                if (ValidPanel(panel)) panels.Add(panel);
            return panels.Count > 0;
        }

        // "id;hash;x,y,z;x,y,z,w;width;height"
        public static string FormatPicture(Picture p) => string.Join(";",
            p.Id.ToString(Inv), p.Hash,
            F(p.Position.x) + "," + F(p.Position.y) + "," + F(p.Position.z),
            F(p.Rotation.x) + "," + F(p.Rotation.y) + "," + F(p.Rotation.z) + "," + F(p.Rotation.w),
            F(p.Width), F(p.Height));

        static string F(float value) => value.ToString("R", Inv);

        public static Picture ParsePicture(string text)
        {
            var parts = text.Split(';');
            if (parts.Length != 6 || !int.TryParse(parts[0], NumberStyles.Integer, Inv, out var id) || !ValidHash(parts[1])) return null;
            var position = Floats(parts[2], 3);
            var rotation = Floats(parts[3], 4);
            if (position == null || rotation == null) return null;
            if (!float.TryParse(parts[4], NumberStyles.Float, Inv, out var width) ||
                !float.TryParse(parts[5], NumberStyles.Float, Inv, out var height)) return null;
            if (!(width > 0.05f && width <= 20f && height > 0.05f && height <= 20f)) return null;
            return new Picture
            {
                Id = id,
                Hash = parts[1],
                Position = new Vector3(position[0], position[1], position[2]),
                Rotation = new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]).normalized,
                Width = width,
                Height = height,
            };
        }

        static float[] Floats(string text, int count)
        {
            var parts = text.Split(',');
            if (parts.Length != count) return null;
            var values = new float[count];
            for (int i = 0; i < count; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, Inv, out values[i]) || float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                    return null;
            return values;
        }

        // ---- Changes, made on every player as the host announces them ----

        public static void SetPaint(Color32 color, List<string> panels)
        {
            foreach (var panel in panels) Paint[panel] = color;
            Version++;
        }

        public static void ClearPaint(string panel)
        {
            if (Paint.Remove(panel)) Version++;
        }

        public static void SetPicture(Picture picture)
        {
            Pictures[picture.Id] = picture;
            if (picture.Id >= NextId) NextId = picture.Id + 1;
            Version++;
        }

        public static void RemovePicture(int id)
        {
            if (Pictures.Remove(id)) Version++;
        }

        // ---- The save file ----

        static string SaveFile()
        {
            try
            {
                var type = AccessTools.TypeByName("HutongGames.PlayMaker.FsmVariables");
                var globals = AccessTools.Property(type, "GlobalVariables").GetValue(null);
                var fsmString = AccessTools.Method(type, "GetFsmString", new[] { typeof(string) }).Invoke(globals, new object[] { "CurrentFilename" });
                var name = AccessTools.Property(fsmString.GetType(), "Value").GetValue(fsmString) as string;
                if (string.IsNullOrEmpty(name)) return null;
                return Path.Combine(Application.persistentDataPath, name + ".decorator.txt");
            }
            catch (Exception e)
            {
                DecoratorPlugin.Log.LogWarning($"Could not tell which save is loaded: {e.Message}");
                return null;
            }
        }

        static void Save()
        {
            var path = SaveFile();
            if (path == null) return;
            var lines = new List<string> { "day " + GameData.Instance.gameDay.ToString(Inv) };
            foreach (var group in GroupPaint()) lines.Add("paint " + group);
            foreach (var picture in Pictures.Values) lines.Add("picture " + FormatPicture(picture));
            File.WriteAllLines(path, lines);
        }

        static void Load()
        {
            Clear();
            var path = SaveFile();
            if (path == null || !File.Exists(path)) return;

            var lines = File.ReadAllLines(path);
            // A new game started in a slot that was used before would otherwise get the old store's decoration
            if (lines.Length > 0 && lines[0].StartsWith("day ") && int.TryParse(lines[0].Substring(4), out var day) &&
                day > GameData.Instance.gameDay)
            {
                DecoratorPlugin.Log.LogInfo("The decoration file belongs to a later day than this save, so it's left out (new game?)");
                return;
            }
            foreach (var line in lines)
            {
                if (line.StartsWith("paint ") && TryParsePaint(line.Substring(6), out var color, out var panels))
                    SetPaint(color, panels);
                else if (line.StartsWith("picture ") && ParsePicture(line.Substring(8)) is Picture picture)
                    SetPicture(picture);
            }
            DecoratorPlugin.Log.LogInfo($"Loaded {Paint.Count} painted wall panels and {Pictures.Count} pictures.");
        }

        // The painted panels as "colour=panel,..." lines, one per colour, each short enough for one chat line
        public static List<string> GroupPaint()
        {
            var byColor = new Dictionary<string, List<string>>();
            foreach (var pair in Paint)
            {
                var hex = Hex(pair.Value);
                if (!byColor.TryGetValue(hex, out var list)) byColor[hex] = list = new List<string>();
                list.Add(pair.Key);
            }
            var lines = new List<string>();
            foreach (var pair in byColor)
                for (int i = 0; i < pair.Value.Count; i += 2000)
                    lines.Add(pair.Key + "=" + string.Join(",", pair.Value.GetRange(i, Math.Min(2000, pair.Value.Count - i))));
            return lines;
        }

        [HarmonyPatch(typeof(SaveBehaviour), nameof(SaveBehaviour.SavePersistentValues))]
        static class SavePatch
        {
            static void Postfix()
            {
                if (!NetworkServer.active) return;
                try
                {
                    Save();
                }
                catch (Exception e)
                {
                    DecoratorPlugin.Log.LogError($"Could not save the decoration: {e}");
                }
            }
        }

        [HarmonyPatch(typeof(SaveBehaviour), nameof(SaveBehaviour.LoadPersistentValues))]
        static class LoadPatch
        {
            static void Postfix()
            {
                // Only the host loads a save; a player in someone else's store keeps what the host sent
                if (NetworkClient.active && !NetworkServer.active) return;
                try
                {
                    Load();
                    Net.SendEverything();
                }
                catch (Exception e)
                {
                    DecoratorPlugin.Log.LogError($"Could not load the decoration: {e}");
                }
            }
        }
    }
}
