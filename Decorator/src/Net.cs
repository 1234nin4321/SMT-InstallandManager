using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SMTDecorator
{
    // Everything that goes between the host and the other players.
    //
    // It all goes through the game's chat calls, like the other SMT mods. The game's chat drops any message containing
    // "</b>", so players without the mod never see these lines. Players send their requests to the host, the host
    // checks and charges them, keeps the result and passes it on to everyone (itself included), and every player's
    // walls and pictures follow what it sends. Picture files travel in pieces, as text.
    static class Net
    {
        const string Sender = "Decorator";
        const string Prefix = "<b>smt-decorator:";
        const string Suffix = "</b>";

        // Text per piece of a picture file. The game's chat line can hold 65534 bytes.
        const int ChunkSize = 24000;
        // Largest picture file the host takes
        const int MaxFileSize = 6 * 1024 * 1024;
        // Lines are sent a few at a time, so a picture doesn't flood the connection
        const int BytesPerTick = 48000;
        const float TickSeconds = 0.1f;

        static readonly Action<PlayerObjectController, string, string> ReceiveChatMsg =
            AccessTools.MethodDelegate<Action<PlayerObjectController, string, string>>(
                AccessTools.Method(typeof(PlayerObjectController), "RpcReceiveChatMsg"));

        static readonly FieldInfo ChatInputField = AccessTools.Field(typeof(ChatController), "ChatInputField");
        static ChatController chat;

        public static PlayerObjectController LocalPlayer =>
            LobbyController.Instance != null ? LobbyController.Instance.LocalplayerController : null;

        public static bool IsHost => NetworkServer.active;

        // Whether the host has the mod; only then is anything a player does kept and passed on
        public static bool HostHasMod => IsHost || hostAnswered;

        static bool hostAnswered;
        static bool wasConnected;
        static float helloAt = -1f;

        // Lines waiting to go out: (to the host or to everyone, line)
        static readonly Queue<KeyValuePair<bool, string>> outgoing = new Queue<KeyValuePair<bool, string>>();
        static float nextTick;

        // Pieces of picture files on their way in, by picture
        static readonly Dictionary<string, string[]> incoming = new Dictionary<string, string[]>();
        // Pictures this player has asked the host for, and when
        static readonly Dictionary<string, float> requested = new Dictionary<string, float>();
        // Pictures the host has sent everyone, and when, so several players asking at once get it once
        static readonly Dictionary<string, float> sent = new Dictionary<string, float>();

        public static void Update()
        {
            bool connected = NetworkClient.active || NetworkServer.active;
            if (wasConnected && !connected)
            {
                // Left the game: forget that store
                Store.Clear();
                hostAnswered = false;
                helloAt = -1f;
                outgoing.Clear();
                incoming.Clear();
                requested.Clear();
                sent.Clear();
            }
            wasConnected = connected;

            // Players ask the host for the decoration once they're in
            if (connected && !IsHost && !hostAnswered && LocalPlayer != null)
            {
                if (helloAt < 0f) helloAt = Time.unscaledTime + 3f;
                else if (Time.unscaledTime >= helloAt)
                {
                    Request("hello", DecoratorPlugin.Version);
                    helloAt = Time.unscaledTime + 30f;
                }
            }

            if (Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + TickSeconds;
            int bytes = 0;
            while (outgoing.Count > 0 && bytes < BytesPerTick)
            {
                var line = outgoing.Dequeue();
                bytes += line.Value.Length;
                SendNow(line.Key, line.Value);
            }
        }

        public static bool Typing()
        {
            try
            {
                if (chat == null) chat = UnityEngine.Object.FindObjectOfType<ChatController>();
                if (chat == null || ChatInputField == null) return false;
                // TMP_InputField.isFocused, read by reflection so the mod doesn't need TextMeshPro
                var field = ChatInputField.GetValue(chat);
                var focused = field?.GetType().GetProperty("isFocused")?.GetValue(field);
                return focused is bool b && b;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---- What players ask for ----

        public static void RequestPaint(Color32 color, List<string> panels) =>
            Request("paint", Store.FormatPaint(color, panels));

        public static void RequestPicture(Picture picture) => Request("place", Store.FormatPicture(picture));

        public static void RequestRemove(int id) => Request("remove", id.ToString(CultureInfo.InvariantCulture));

        // Sends a picture file to the host, unless it is the host
        public static void Upload(string hash)
        {
            if (IsHost) return;
            var path = Store.ImagePath(hash);
            if (path == null) return;
            foreach (var piece in Pieces(hash, System.IO.File.ReadAllBytes(path)))
                Request("up", piece);
        }

        static void Request(string verb, string body)
        {
            if (IsHost)
            {
                var player = LocalPlayer;
                Host(verb, body, player != null ? player.PlayerName : "");
                return;
            }
            outgoing.Enqueue(new KeyValuePair<bool, string>(true, Prefix + verb + ":" + body + Suffix));
        }

        static void Broadcast(string verb, string body)
        {
            if (!IsHost) return;
            outgoing.Enqueue(new KeyValuePair<bool, string>(false, Prefix + verb + ":" + body + Suffix));
        }

        static void SendNow(bool toHost, string line)
        {
            try
            {
                var player = LocalPlayer;
                if (player == null) return;
                if (toHost) player.SendChatMsg(line);
                else if (player.isServer) ReceiveChatMsg(player, Sender, line);
            }
            catch (Exception e)
            {
                DecoratorPlugin.Log.LogWarning($"Could not send a chat line: {e.Message}");
            }
        }

        static IEnumerable<string> Pieces(string hash, byte[] bytes)
        {
            var text = Convert.ToBase64String(bytes);
            int count = (text.Length + ChunkSize - 1) / ChunkSize;
            for (int i = 0; i < count; i++)
                yield return hash + ":" + i + ":" + count + ":" + text.Substring(i * ChunkSize, Math.Min(ChunkSize, text.Length - i * ChunkSize));
        }

        // Puts a picture file back together from its pieces. Returns the file once the last piece is in and it matches its name.
        static byte[] Piece(string body)
        {
            var parts = body.Split(new[] { ':' }, 4);
            if (parts.Length != 4 || !Store.ValidHash(parts[0]) ||
                !int.TryParse(parts[1], out var index) || !int.TryParse(parts[2], out var count) ||
                count < 1 || count > MaxFileSize * 4 / 3 / ChunkSize + 2 || index < 0 || index >= count) return null;
            var hash = parts[0];
            if (!incoming.TryGetValue(hash, out var pieces) || pieces.Length != count) incoming[hash] = pieces = new string[count];
            pieces[index] = parts[3];
            foreach (var piece in pieces)
                if (piece == null) return null;
            incoming.Remove(hash);

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(string.Concat(pieces));
            }
            catch (FormatException)
            {
                return null;
            }
            if (bytes.Length > MaxFileSize || Store.HashOf(bytes) != hash) return null;
            // Only keep what the game can read as a picture
            var texture = new Texture2D(2, 2);
            bool readable = texture.LoadImage(bytes);
            UnityEngine.Object.Destroy(texture);
            return readable ? bytes : null;
        }

        // Asks the host for a picture file this player hasn't got
        public static void Fetch(string hash)
        {
            if (IsHost || !hostAnswered) return;
            if (requested.TryGetValue(hash, out var at) && Time.unscaledTime - at < 30f) return;
            requested[hash] = Time.unscaledTime;
            Request("get", hash);
        }

        // ---- The host ----

        // Tells everyone the whole decoration; players who already have it just get it again
        public static void SendEverything()
        {
            if (!IsHost) return;
            Broadcast("host", DecoratorPlugin.Version);
            foreach (var line in Store.GroupPaint()) Broadcast("paint", line);
            foreach (var picture in Store.Pictures.Values) Broadcast("place", Store.FormatPicture(picture));
        }

        // A player's request, checked and carried out by the host
        static void Host(string verb, string body, string player)
        {
            switch (verb)
            {
                case "hello":
                    SendEverything();
                    break;

                case "paint":
                {
                    if (!Store.TryParsePaint(body, out var color, out var panels)) return;
                    var changed = panels.FindAll(p => !Store.Paint.TryGetValue(p, out var old) || !Same(old, color));
                    if (changed.Count == 0) return;
                    if (!Pay(changed.Count * DecoratorPlugin.PanelPrice.Value, player)) return;
                    Store.SetPaint(color, changed);
                    Broadcast("paint", Store.FormatPaint(color, changed));
                    break;
                }

                case "place":
                {
                    var picture = Store.ParsePicture(body);
                    if (picture == null) return;
                    if (!Store.HasImage(picture.Hash))
                    {
                        Tell(player, "The picture didn't reach the host. Try again.");
                        return;
                    }
                    if (!Pay(DecoratorPlugin.PicturePrice.Value, player)) return;
                    picture.Id = Store.NextId;
                    Store.SetPicture(picture);
                    Broadcast("place", Store.FormatPicture(picture));
                    break;
                }

                case "remove":
                    if (int.TryParse(body, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && Store.Pictures.ContainsKey(id))
                    {
                        Store.RemovePicture(id);
                        Broadcast("remove", body);
                    }
                    break;

                case "up":
                {
                    var bytes = Piece(body);
                    if (bytes == null) return;
                    var hash = Store.SaveImage(bytes);
                    DecoratorPlugin.Log.LogInfo($"{player} sent a picture ({bytes.Length / 1024} KB), saved as {Store.ImagePath(hash)}");
                    break;
                }

                case "get":
                {
                    if (!Store.ValidHash(body) || !Store.HasImage(body)) return;
                    if (sent.TryGetValue(body, out var at) && Time.unscaledTime - at < 20f) return;
                    sent[body] = Time.unscaledTime;
                    foreach (var piece in Pieces(body, System.IO.File.ReadAllBytes(Store.ImagePath(body))))
                        Broadcast("data", piece);
                    break;
                }
            }
        }

        static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

        static bool Pay(float price, string player)
        {
            if (price <= 0f) return true;
            if (GameData.Instance == null || GameData.Instance.gameFunds < price)
            {
                Tell(player, "Not enough money for that.");
                return false;
            }
            GameData.Instance.CmdAlterFunds(-price);
            return true;
        }

        // A message for one player, shown on their tablet
        static void Tell(string player, string text) => Broadcast("note", player + "\n" + text);

        // ---- Every player, the host included ----

        static void Received(string verb, string body)
        {
            switch (verb)
            {
                case "host":
                    hostAnswered = true;
                    break;

                case "paint":
                    if (!IsHost && Store.TryParsePaint(body, out var color, out var panels)) Store.SetPaint(color, panels);
                    break;

                case "place":
                    if (!IsHost && Store.ParsePicture(body) is Picture picture) Store.SetPicture(picture);
                    break;

                case "remove":
                    if (!IsHost && int.TryParse(body, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) Store.RemovePicture(id);
                    break;

                case "data":
                    if (!IsHost && Piece(body) is byte[] bytes)
                    {
                        Store.SaveImage(bytes);
                        Store.Version++;
                    }
                    break;

                case "note":
                {
                    int newline = body.IndexOf('\n');
                    var player = LocalPlayer;
                    if (newline > 0 && player != null && body.Substring(0, newline) == player.PlayerName)
                        Tablet.Note(body.Substring(newline + 1));
                    break;
                }
            }
        }

        static bool Split(string message, out string verb, out string body)
        {
            verb = body = null;
            if (message == null || !message.StartsWith(Prefix)) return false;
            var rest = message.Substring(Prefix.Length);
            if (rest.EndsWith(Suffix)) rest = rest.Substring(0, rest.Length - Suffix.Length);
            int colon = rest.IndexOf(':');
            if (colon < 0) return false;
            verb = rest.Substring(0, colon);
            body = rest.Substring(colon + 1);
            return true;
        }

        // Runs on the host when a player sends a chat line. A host without the mod passes the line on to everyone,
        // where the chat drops it.
        [HarmonyPatch(typeof(PlayerObjectController), "UserCode_CmdSendMessage__String__NetworkConnectionToClient")]
        static class RequestPatch
        {
            static bool Prefix(string message, NetworkConnectionToClient sender)
            {
                if (!Split(message, out var verb, out var body)) return true;
                string player = "";
                try
                {
                    var controller = sender?.identity != null ? sender.identity.GetComponent<PlayerObjectController>() : null;
                    if (controller != null) player = controller.PlayerName;
                    Host(verb, body, player);
                }
                catch (Exception e)
                {
                    DecoratorPlugin.Log.LogWarning($"Could not handle {player}'s {verb} request: {e.Message}");
                }
                return false;
            }
        }

        // Runs on every player with the mod, the host included, when a chat line arrives
        [HarmonyPatch(typeof(PlayerObjectController), "UserCode_RpcReceiveChatMsg__String__String")]
        static class ReceivePatch
        {
            static bool Prefix(string playerName, string message)
            {
                if (!Split(message, out var verb, out var body)) return true;
                if (playerName == Sender)
                {
                    try
                    {
                        Received(verb, body);
                    }
                    catch (Exception e)
                    {
                        DecoratorPlugin.Log.LogWarning($"Could not handle the host's {verb} line: {e.Message}");
                    }
                }
                return false;
            }
        }
    }
}
